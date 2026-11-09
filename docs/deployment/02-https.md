# HTTPS

Objetivo: que todo el tráfico entre el cliente de escritorio y la API viaje cifrado, con un certificado válido y renovado automáticamente.

## Por qué no alcanza con "es privado"

Cada inicio de sesión envía usuario y contraseña, y cada petición envía un token Bearer. Sin TLS, cualquiera que comparta la red (Wi-Fi, proveedor, host) puede leer ambos y suplantar a un usuario. El autorregistro además transporta el código de invitación.

Un certificado autofirmado sirve para pruebas locales, no para producción: el cliente rechazará la conexión o alguien tendrá que desactivar la validación, que es peor.

## Dos topologías válidas

### A. Proxy inverso con TLS (recomendada)

```
Escritorio --HTTPS--> Caddy/nginx --HTTP local--> API en 127.0.0.1:5100
```

- El proxy escucha en 443, presenta el certificado y reenvía a la API por loopback.
- La API no se publica en el firewall.
- El proxy administra la renovación.

### B. TLS en Kestrel

La API presenta el certificado directamente. Sirve si no quieres un proxy, pero implica administrar el certificado, el puerto 443 y la renovación dentro del proceso o del servicio. Para este proyecto, la opción A es más simple y más segura.

## Proxy con Caddy

Caddy obtiene y renueva certificados de Let's Encrypt automáticamente. Necesitas un dominio que resuelva al servidor y los puertos 80 y 443 abiertos.

`/etc/caddy/Caddyfile`:

```caddyfile
ledger.example.com {
    reverse_proxy 127.0.0.1:5100
}
```

```bash
sudo systemctl reload caddy
```

Caddy agrega `X-Forwarded-For` y `X-Forwarded-Proto` automáticamente.

## Proxy con nginx + Certbot

```nginx
server {
    listen 80;
    server_name ledger.example.com;
    return 301 https://$host$request_uri;
}

server {
    # nginx 1.25+: "http2 on;". En versiones anteriores: "listen 443 ssl http2;"
    listen 443 ssl;
    http2 on;
    server_name ledger.example.com;

    ssl_certificate     /etc/letsencrypt/live/ledger.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/ledger.example.com/privkey.pem;

    location / {
        proxy_pass         http://127.0.0.1:5100;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

```bash
sudo certbot --nginx -d ledger.example.com
sudo certbot renew --dry-run
```

`certbot renew --dry-run` debe pasar antes de confiar en la renovación automática.

## Cuidado con `UseHttpsRedirection` detrás del proxy

`Program.cs` llama a `UseHttpsRedirection()` fuera del entorno `Testing`. Detrás de un proxy, la API recibe HTTP desde loopback y no sabe que el cliente original usó HTTPS. Sin `ForwardedHeaders`, puede responder una redirección a HTTPS, el proxy la reenvía, la API vuelve a redirigir y se produce un bucle.

Elige una de estas salidas y hazla explícita:

1. **Procesar encabezados reenviados** (recomendado): agregar `UseForwardedHeaders` antes de `UseHttpsRedirection`, limitando los proxies confiables a loopback. Es un cambio de código; lo revisamos juntos.
2. **Quitar `UseHttpsRedirection` en producción** y confiar en que el proxy es el único punto de entrada. La API debe escuchar solo en `127.0.0.1`.
3. **Terminar TLS en Kestrel**, sin proxy, con `ASPNETCORE_URLS=https://...` y el certificado configurado.

No dejes la redirección activa "por si acaso" sin forwarded headers: es la causa más común de bucles en este montaje.

## Aislar la API

- `ASPNETCORE_URLS=http://127.0.0.1:5100`, no `0.0.0.0`.
- Firewall: permite 80 y 443; bloquea 5100 desde fuera.
- Si usas Docker, publica el puerto solo en loopback: `-p 127.0.0.1:5100:5100`.

## Endurecimiento adicional

- **HSTS**: agrega `Strict-Transport-Security: max-age=31536000` en el proxy **solo después** de confirmar que HTTPS funciona. Si lo activas antes, los clientes quedan atrapados en un dominio roto.
- **TLS moderno**: deja que Caddy o Certbot usen su configuración por defecto; evita habilitar TLS 1.0/1.1.
- **Redirección de HTTP a HTTPS**: en el proxy, con `301`.
- **Ocultar la versión**: no publiques Swagger en producción; el entorno `Production` ya lo desactiva.

## Cliente de escritorio

El cliente lee `ApiBaseUrl` desde `appsettings.json` junto al ejecutable o desde `LEDGER_API_BASE_URL`. En producción debe ser:

```json
{
  "ApiBaseUrl": "https://ledger.example.com/"
}
```

No desactives la validación de certificados en `HttpClient`. Si el certificado es inválido, el problema se corrige en el servidor.

## Verificación

Desde una máquina externa:

```bash
curl -i https://ledger.example.com/health
curl -i http://ledger.example.com/health      # debe redirigir a https
openssl s_client -connect ledger.example.com:443 -servername ledger.example.com </dev/null \
  | openssl x509 -noout -subject -issuer -dates
```

- [ ] El certificado es válido y coincide con el dominio.
- [ ] La fecha de expiración es futura y la renovación automática está configurada.
- [ ] HTTP redirige a HTTPS.
- [ ] `http://<ip>:5100` no responde desde fuera del servidor.
- [ ] El cliente de escritorio inicia sesión contra `https://...` sin errores de certificado.
- [ ] No hay bucles de redirección.
- [ ] Swagger devuelve 404 en producción.

## Lo que voy a revisar

1. Qué topología elegiste y por qué.
2. Que el puerto interno no esté expuesto.
3. Que `ForwardedHeaders` o la ausencia de `UseHttpsRedirection` sean coherentes con el proxy.
4. Que la renovación del certificado esté probada, no solo configurada.
5. Que el cliente use HTTPS y valide el certificado.
