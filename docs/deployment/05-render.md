# Render (plan Free)

Guía para publicar `Ledger.Api` en Render usando exclusivamente el plan **Free** y
Neon como base de datos. No crea recursos por vos ni modifica Neon: son pasos para
ejecutar en el panel de Render cuando decidas desplegar.

## Qué incluye el repositorio

| Archivo | Para qué sirve |
|---|---|
| `Dockerfile` | Compila y publica la API en una imagen .NET 8, escucha en `$PORT` (10000 por defecto). |
| `.dockerignore` | Deja fuera tests, docs, binarios y secretos del contexto de build. |
| `render.yaml` | Blueprint con el servicio web Free, health check y variables de entorno. |
| `src/Ledger.Api/Configuration/ProductionConfigurationValidator.cs` | Falla al arrancar si falta configuración crítica. |

## Límites del plan Free que debés conocer

- **0.1 CPU y 512 MB de RAM**, una sola instancia.
- **750 horas de instancia por mes** por workspace. Un servicio dormido no consume horas.
- El servicio **se duerme a los 15 minutos sin tráfico**. La primera petición después
  de eso tarda **alrededor de un minuto** en despertarlo.
- El sistema de archivos es **efímero**: se pierde en cada redeploy o reinicio. Ledger
  no escribe archivos locales, así que no lo necesita; los backups van fuera.
- **No hay shell, jobs de una sola vez, cron ni pre-deploy commands.** Las migraciones
  y los backups se ejecutan desde tu máquina.
- El health check HTTP debe responder **2xx o 3xx en menos de 5 segundos**.
- Si se agota la cuota de horas o de bandwidth, Render puede suspender el servicio
  hasta el mes siguiente. Con 3 o 4 usuarios el uso normal es bajo.
- La región **no se puede cambiar** después de crear el servicio.

## 1. Preparar los valores

### Cadena de conexión de Neon

En el panel de Neon, abrí tu proyecto → **Connect** y copiá la cadena para .NET/Npgsql.
Debe incluir `SSL Mode=Require` (Neon lo exige). No la escribas en el repositorio.

Formato esperado:

```text
Host=ep-xxxx-xxxx.us-east-2.aws.neon.tech;Database=neondb;Username=...;Password=...;SSL Mode=Require
```

Si tu cadena usa `postgresql://...`, mantenela tal cual si Neon la entrega así; la API
la acepta a través de Npgsql.

### Clave JWT

Generá una clave nueva, distinta de la de desarrollo. Debe tener al menos 32
caracteres; se recomienda base64 de 48 bytes:

```powershell
[Convert]::ToBase64String(
  [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
```

Guardala en un gestor de contraseñas. **No la reutilices** de otro entorno y no la
commitees.

### Código de invitación

Mientras el registro esté habilitado:

```powershell
[Convert]::ToHexString(
  [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24)).ToLowerInvariant()
```

Entregalo a cada usuario por un canal separado del ZIP.

## 2. Crear el servicio con el Blueprint

1. Subí el repositorio a GitHub (o al proveedor Git que uses) en la rama que vas a
   desplegar. Render construye desde ese repositorio.
2. Entrá a <https://dashboard.render.com> → **New** → **Blueprint**.
3. Conectá el repositorio y elegí la rama.
4. Render detecta `render.yaml` y muestra el servicio `ledger-api`.
5. Completá los valores marcados como secretos (`sync: false`):
   - `ConnectionStrings__Ledger`
   - `Jwt__SigningKey`
   - `Ledger__Registration__InviteCode`
6. Revisá la región. En `render.yaml` está comentada; si no la definís, Render usa
   **oregon**. Elegí la más cercana a tu proyecto de Neon y no la cambies después.
   Valores válidos: `oregon`, `ohio`, `virginia`, `frankfurt`, `singapore`.
7. Confirmá la creación. El plan debe quedar en **Free**.

El Blueprint configura:

| Variable | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Jwt__Issuer` | `Ledger.Api` |
| `Jwt__Audience` | `Ledger.Desktop` |
| `Jwt__AccessTokenMinutes` | `480` |
| `Ledger__Registration__Enabled` | `true` (temporal) |
| `ConnectionStrings__Ledger` | secreto, se carga en Render |
| `Jwt__SigningKey` | secreto, se carga en Render |
| `Ledger__Registration__InviteCode` | secreto, se carga en Render |

No configures `Ledger__Owner__*` ni `Ledger__Users__*`. En `Production` el
aprovisionamiento es solo de creación y las cuentas existentes de Neon no se tocan.

### Alternativa manual

Si preferís no usar el Blueprint:

1. **New** → **Web Service** → repositorio.
2. **Language**: Docker.
3. **Dockerfile Path**: `./Dockerfile`; **Docker Context**: `.`.
4. **Region**: la más cercana a Neon.
5. **Instance Type**: Free.
6. **Health Check Path**: `/health`.
7. **Auto-Deploy**: Off (despliegues manuales).
8. Cargá las variables de la tabla anterior en **Environment**.

## 3. Primer despliegue

1. Render construye la imagen y arranca el contenedor. La API escucha en el puerto
   que Render inyecta en `PORT` (10000 por defecto) sobre `0.0.0.0`.
2. Si falta `Jwt__SigningKey`, la cadena de Neon o el código de invitación con el
   registro habilitado, el proceso **falla al arrancar** y Render muestra el error en
   los logs. Corregí la variable y redesplegá.
3. Cuando el health check devuelve 200, abrí:

```text
https://TU-SERVICIO.onrender.com/health
```

Respuesta esperada:

```json
{"status":"Healthy","database":"Reachable"}
```

4. Verificá que Swagger **no** esté disponible en Producción:

```text
https://TU-SERVICIO.onrender.com/swagger
```

Debe devolver 401 o 404, nunca la interfaz.

5. Probá un login con una cuenta existente usando el cliente o `curl`:

```powershell
$body = @{ identifier = 'TU_USUARIO'; password = 'TU_PASSWORD' } | ConvertTo-Json
Invoke-RestMethod -Method Post `
  -Uri https://TU-SERVICIO.onrender.com/api/v1/auth/login `
  -ContentType application/json `
  -Body $body
```

No guardes la respuesta ni la contraseña en el repositorio.

## 4. Migraciones

La API **no** aplica migraciones al arrancar. Neon ya tiene las migraciones actuales
aplicadas. Para cambios futuros, ejecutá desde tu máquina antes de desplegar:

```powershell
$env:ConnectionStrings__Ledger = '<cadena de Neon>'
dotnet ef database update `
  --project src/Ledger.Api `
  --startup-project src/Ledger.Api
Remove-Item Env:ConnectionStrings__Ledger
```

Revisá el SQL generado antes de aplicarlo:

```powershell
dotnet ef migrations script --idempotent `
  --project src/Ledger.Api `
  --startup-project src/Ledger.Api
```

No agregues `Database.Migrate()` al arranque ni un pre-deploy command: el plan Free no
los soporta y las migraciones deben ser una decisión explícita.

## 5. Backups con Render Free

Render Free no ejecuta tareas programadas. Los backups se ejecutan desde tu máquina
(o desde otra máquina siempre encendida) con Docker, que ya trae `pg_dump` y
`pg_restore`:

```powershell
$env:LEDGER_BACKUP_CONNECTION = '<cadena de Neon>'
.\scripts\backup-ledger-docker.ps1
```

En Linux/macOS:

```bash
export LEDGER_BACKUP_CONNECTION='<cadena de Neon>'
./scripts/backup-ledger-docker.sh
```

Programá esa tarea con el Programador de tareas de Windows o `cron`. Copiá los
`.dump` fuera de la máquina que los genera. Ver `scripts/README.md` y
`docs/deployment/04-backups-programados.md`.

## 6. Actualizar el cliente

La URL pública queda fija en el `appsettings.json` del ZIP. Si cambia:

1. Editá `appsettings.json` junto al ejecutable, o
2. definí `LEDGER_API_BASE_URL` antes de abrir el cliente, o
3. regenerá el paquete:

```powershell
.\scripts\release-desktop.ps1 -Version 1.0.0 `
  -ApiBaseUrl https://TU-SERVICIO.onrender.com/
```

No hace falta recompilar el cliente para un cambio de URL.

## 7. Después del onboarding

Cuando todos los usuarios tengan cuenta:

1. En Render, cambiá `Ledger__Registration__Enabled` a `false`.
2. Redesplegá.
3. Verificá que `POST /api/v1/auth/register` responde 404 o 400.

Si más adelante necesitás invitar a alguien, volvé a habilitarlo temporalmente y
cambiá el código de invitación.

## 8. Checklist de verificación

- [ ] El servicio figura en plan **Free**.
- [ ] La región es la elegida y no se modificará.
- [ ] `/health` responde 200 con `database: Reachable`.
- [ ] `/swagger` no es accesible en Producción.
- [ ] El login de una cuenta existente funciona.
- [ ] `Ledger__Owner` y `Ledger__Users` **no** están configurados.
- [ ] `Jwt__SigningKey` es nueva, tiene 32+ caracteres y vive en Render.
- [ ] El código de invitación está cargado y se entregó por un canal separado.
- [ ] Existe al menos un backup fuera de Neon y se probó un restore.
- [ ] El ZIP del cliente apunta a la URL de Render y su SHA256 fue verificado.

## 9. Problemas frecuentes

| Síntoma | Causa probable | Solución |
|---|---|---|
| El deploy falla al arrancar | Falta una variable o la clave JWT es corta | Leer los logs; completar `Jwt__SigningKey`, `ConnectionStrings__Ledger` o el código de invitación. |
| `/health` devuelve 503 | Neon no responde o la cadena es incorrecta | Probar la cadena desde Neon; revisar `SSL Mode=Require`; mirar los logs. |
| Redirección infinita a HTTPS | El proxy no envía `X-Forwarded-Proto` | La API ya usa `UseForwardedHeaders`; verificar que el tráfico llegue por Render y no por un proxy propio. |
| Error 429 al iniciar sesión | Rate limit por IP (20/minuto) | Esperar un minuto; con varios usuarios detrás de la misma IP puede ocurrir. |
| El cliente tarda ~1 minuto la primera vez | El servicio estaba dormido | Es esperado en Free; el cliente espera hasta 100 segundos. |
| Cambios en disco desaparecen | Filesystem efímero | No usar archivos locales; los datos viven en Neon. |
