# Secretos de producción

Objetivo: que la API reciba todos sus secretos desde el entorno del servidor, sin que ninguno esté en el repositorio ni en una imagen publicada.

## Qué secretos existen

| Configuración | Variable de entorno | Notas |
|---|---|---|
| Conexión a PostgreSQL | `ConnectionStrings__Ledger` | Cadena de Neon con SSL |
| Clave de firma JWT | `Jwt__SigningKey` | Mínimo 32 caracteres; ideal 48 bytes aleatorios |
| Emisor y audiencia | `Jwt__Issuer`, `Jwt__Audience` | No son secretos, pero deben ser estables |
| Cuenta dueña | `Ledger__Owner__Username`, `Ledger__Owner__Email`, `Ledger__Owner__Password`, `Ledger__Owner__DisplayName` | La contraseña sí es secreta |
| Cuentas adicionales | `Ledger__Users__<usuario>__Email`, `...__Password`, `...__DisplayName` | Una entrada por persona |
| Registro | `Ledger__Registration__Enabled`, `Ledger__Registration__InviteCode` | El código de invitación es secreto |

En variables de entorno, `:` se reemplaza por `__` (doble guion bajo). En Linux las mayúsculas importan; usa exactamente estos nombres.

## Cómo generar valores fuertes

PowerShell:

```powershell
# Clave JWT: 48 bytes aleatorios en Base64
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))

# Contraseña o código de invitación: 24 bytes aleatorios en Base64
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
```

Linux/macOS:

```bash
openssl rand -base64 48   # clave JWT
openssl rand -base64 24   # contraseña o código de invitación
```

No reutilices la clave JWT entre entornos. Si alguien obtiene la clave, puede fabricar tokens válidos para cualquier usuario.

## Dónde guardarlos

### Linux con systemd

Archivo `/etc/ledger/ledger.env`, solo lectura para root y para el usuario del servicio:

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5100
ConnectionStrings__Ledger=Host=...;Database=...;Username=...;Password=...;SSL Mode=Require
Jwt__SigningKey=...
Ledger__Owner__Username=...
Ledger__Owner__Email=...
Ledger__Owner__Password=...
Ledger__Owner__DisplayName=...
Ledger__Users__zamira__Email=...
Ledger__Users__zamira__Password=...
Ledger__Users__zamira__DisplayName=...
Ledger__Registration__Enabled=true
Ledger__Registration__InviteCode=...
```

```bash
sudo chown root:ledger /etc/ledger/ledger.env
sudo chmod 640 /etc/ledger/ledger.env
```

Referéncialo desde la unidad de systemd:

```ini
[Service]
User=ledger
EnvironmentFile=/etc/ledger/ledger.env
ExecStart=/usr/bin/dotnet /opt/ledger/api/Ledger.Api.dll
```

`EnvironmentFile` no hace expansión de variables ni interpreta `$()`; escribe los valores literales.

### Windows Server

- Crea una cuenta de servicio dedicada para la API.
- Configura las variables como variables de entorno de esa cuenta o del servicio.
- Si usas IIS, revisa que el Application Pool no tenga identidad interactiva.
- No dejes el archivo de variables en una carpeta compartida ni en el escritorio.

### Nunca

- `appsettings.Production.json` con secretos dentro del artefacto.
- Secretos en la línea de comandos (quedan en el historial y en la lista de procesos).
- Secretos en capturas, issues, commits o mensajes de chat.

## Advertencia sobre usuarios configurados

`UserProvisioningService` crea o actualiza las cuentas de `Ledger:Owner` y `Ledger:Users` **en cada arranque** usando la contraseña configurada. Si cambias la contraseña desde el cliente, también debes actualizar el secreto correspondiente; si no, el próximo arranque la revierte.

Esto no aplica a usuarios creados por autorregistro: no están en la configuración y su contraseña vive solo en la base de datos.

## Ajustes de producción

- `ASPNETCORE_ENVIRONMENT=Production`: desactiva Swagger y evita detalles de desarrollo.
- `ConnectionStrings__Ledger`: usa `SSL Mode=Require`. Neon entrega certificados válidos; no desactives la validación.
- `Ledger__Registration__Enabled`: déjalo en `false` salvo que necesites invitar a alguien en ese momento. Si lo activas, el código de invitación es obligatorio; sin él la API falla cerrado y rechaza todo registro.
- Cambia las tres contraseñas débiles actuales antes de abrir el puerto 443.

## Verificación

- [ ] `git grep -n` no encuentra contraseñas, cadenas de conexión ni claves.
- [ ] Gitleaks pasa en CI.
- [ ] El artefacto publicado no contiene secretos.
- [ ] La API arranca sin `dotnet user-secrets`.
- [ ] Los logs no imprimen la cadena de conexión ni contraseñas.
- [ ] El archivo de entorno tiene permisos restringidos.
- [ ] Cambiar una contraseña de usuario configurado actualiza también el secreto.

## Lo que voy a revisar

1. Que ningún secreto esté en el repositorio ni en `appsettings*.json`.
2. Que la clave JWT sea aleatoria, larga y distinta de la de desarrollo.
3. Que la conexión use SSL y que la base sea de producción, no la de pruebas.
4. Que los permisos del archivo de entorno impidan lectura por otros usuarios.
5. Que el arranque no dependa de user secrets.
