# Despliegue de Ledger

Estas guías son material de trabajo para la parte de despliegue. La idea es que **tú implementes** cada paso en tu servidor y que yo revise el resultado antes de exponer la API a Internet.

Ledger es privado y lo usan 2 o 3 personas conocidas, pero eso no lo hace menos sensible: la API mueve datos financieros, contraseñas y tokens. Los mismos cuidados aplican.

## Orden recomendado

1. [Secretos de producción](01-secretos-produccion.md): conexión a la base, clave JWT, usuarios y código de invitación.
2. [HTTPS](02-https.md): TLS, proxy inverso, certificados y redirección.
3. [Empaquetado](03-empaquetado.md): publicar la API y el cliente de escritorio.
4. [Backups](04-backups-programados.md): programar, copiar fuera del servidor y probar restores.
5. [Render Free](05-render.md): despliegue exacto en Render, límites del plan Free y verificación.
6. Verificación final con la checklist de este documento.

> Si desplegás en Render, `02-https.md` queda cubierto por el proxy de Render. Seguí
> `05-render.md` para la configuración real y usá las otras guías como referencia de
> secretos, empaquetado y backups.

## Principios que no se negocian

- **Nada de secretos en Git.** Ni contraseñas, ni cadenas de conexión, ni la clave JWT, ni el código de invitación. `appsettings.json` se queda con valores vacíos.
- **Producción y desarrollo no comparten base de datos.** Usa una rama o base separada en Neon. Los datos financieros reales no se usan para probar.
- **HTTPS en todo el camino público.** Las contraseñas y los tokens viajan en cada petición; sin TLS cualquiera en la red los puede leer.
- **Migraciones explícitas.** La API no aplica migraciones al arrancar. Primero migras, después arrancas la versión nueva.
- **Al menos una copia del backup fuera del servidor.** Un backup en el mismo disco no protege contra el fallo más común: perder ese disco.
- **La API no se expone directamente.** Escucha en `127.0.0.1` detrás de un proxy que termina TLS, salvo que uses TLS en Kestrel.

## Checklist global

- [ ] La base de producción es distinta de la de desarrollo.
- [ ] Todos los secretos viven en variables de entorno o en el gestor de secretos del host.
- [ ] `dotnet user-secrets` no se usa en producción.
- [ ] La API corre con `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] Swagger no está disponible en producción.
- [ ] El puerto interno de la API no está abierto en el firewall.
- [ ] HTTPS responde, el certificado es válido y se renueva solo.
- [ ] El cliente de escritorio apunta a `https://...`.
- [ ] Las migraciones se aplicaron antes de arrancar la API.
- [ ] `/health` responde `Healthy`.
- [ ] El backup corre desde una máquina externa (Render Free no ejecuta cron) y se copia fuera del servidor.
- [ ] Se probó un restore en una base separada.
- [ ] Se decidió explícitamente si se cambian las contraseñas débiles existentes antes de exponer el servicio.

## Antes de exponer la API

Haz una prueba completa en el servidor con el firewall cerrado:

1. Arranca la API publicada `Production` y confirma `/health`.
2. Inicia sesión con cada cuenta y revisa dashboard, cuentas, movimientos y presupuestos.
3. Cambia una contraseña de prueba y verifica que el cliente vuelve al login.
4. Ejecuta un backup y restáuralo en una base separada.
5. Recién entonces abre el puerto 443 en el firewall.

Si algo falla, no lo resuelvas "temporalmente" abriendo el puerto 5100. Vuelve atrás y corrige.
