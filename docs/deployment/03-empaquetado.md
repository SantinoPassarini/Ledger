# Empaquetado

Objetivo: producir dos artefactos reproducibles, sin secretos y con la configuración correcta:

- la API publicada para el servidor,
- el cliente de escritorio publicado para las personas que lo usan.

## Antes de publicar

- [ ] `dotnet build Ledger.sln -c Release` sin advertencias.
- [ ] Tests de API en verde.
- [ ] Smoke de escritorio en verde.
- [ ] No hay secretos en `appsettings*.json`.
- [ ] Elegiste una versión, por ejemplo `1.0.0`.

## Publicar la API

```powershell
dotnet publish src/Ledger.Api/Ledger.Api.csproj `
  -c Release `
  -o artifacts/publish/api
```

Por defecto es *framework-dependent*: el servidor necesita el runtime de .NET 8 (ASP.NET Core Runtime). Si no quieres instalarlo, publica self-contained:

```powershell
dotnet publish src/Ledger.Api/Ledger.Api.csproj `
  -c Release -r linux-x64 --self-contained true `
  -o artifacts/publish/api
```

El resultado incluye `Ledger.Api.dll`, dependencias y `appsettings.json` (con valores vacíos). No incluye secretos: se inyectan por entorno.

### Migraciones antes de arrancar

La API no migra sola. Genera un bundle de migraciones y aplícalo como paso separado:

```powershell
dotnet ef migrations bundle `
  --project src/Ledger.Api `
  --startup-project src/Ledger.Api `
  -c Release `
  -r linux-x64 `
  -o artifacts/publish/ledger-migrations
```

En el servidor, con la cadena de producción:

```bash
./ledger-migrations --connection "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
```

Alternativa: `dotnet ef database update` desde una máquina con el proyecto y la cadena de producción. Nunca uses la cadena de desarrollo contra la base real.

Orden de despliegue:

1. Backup reciente.
2. Aplicar migraciones.
3. Reemplazar los binarios.
4. Reiniciar el servicio.
5. Verificar `/health` y un inicio de sesión.

### Ejecutar como servicio

Linux con systemd (`/etc/systemd/system/ledger-api.service`):

```ini
[Unit]
Description=Ledger API
After=network.target

[Service]
User=ledger
WorkingDirectory=/opt/ledger/api
EnvironmentFile=/etc/ledger/ledger.env
ExecStart=/usr/bin/dotnet /opt/ledger/api/Ledger.Api.dll
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now ledger-api
sudo systemctl status ledger-api
```

Windows: crea un servicio con la cuenta dedicada, por ejemplo con `sc.exe` o NSSM, apuntando a `dotnet.exe` y `Ledger.Api.dll`. No lo dejes corriendo en una consola interactiva.

### Configuración de producción

- `ASPNETCORE_ENVIRONMENT=Production`.
- `ASPNETCORE_URLS=http://127.0.0.1:5100` si hay proxy.
- Secretos por entorno, nunca en el artefacto.
- Swagger solo existe en `Development`; no lo fuerces en producción.

## Publicar el escritorio

Framework-dependent (requiere .NET 8 Desktop Runtime en cada PC):

```powershell
dotnet publish src/Ledger.Desktop/Ledger.Desktop.csproj `
  -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true `
  -o artifacts/publish/desktop
```

Self-contained (no requiere runtime, archivo más grande):

```powershell
dotnet publish src/Ledger.Desktop/Ledger.Desktop.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o artifacts/publish/desktop
```

Luego edita el `appsettings.json` que queda junto al ejecutable:

```json
{
  "ApiBaseUrl": "https://ledger.example.com/"
}
```

También puedes omitir el archivo y definir `LEDGER_API_BASE_URL` en la máquina. La variable de entorno tiene prioridad.

### Distribución

- Comprime la carpeta en un `.zip` versionado: `Ledger-1.0.0-win-x64.zip`.
- No incluyas `appsettings.Development.json`, logs, `artifacts/` ni secretos.
- Publica el zip en un almacenamiento privado; no lo subas al repositorio.
- Opcional: firma el ejecutable para reducir advertencias de SmartScreen.
- Documenta el requisito de runtime si publicas framework-dependent.

## Versionado

Fija la versión en el comando de publicación o en el `.csproj`:

```powershell
dotnet publish src/Ledger.Api/Ledger.Api.csproj -c Release -p:Version=1.0.0 -o artifacts/publish/api
```

Guarda junto a cada artefacto: versión, fecha, commit y resultado de las pruebas. Si algo falla, necesitas saber exactamente qué reemplazar.

## Verificación

- [ ] `artifacts/publish/api` no contiene secretos.
- [ ] El bundle de migraciones se generó para la arquitectura del servidor.
- [ ] La API arranca como servicio y sobrevive un reinicio del servidor.
- [ ] `/health` responde `Healthy` en producción.
- [ ] El login funciona con una cuenta real.
- [ ] El zip del escritorio apunta a la URL HTTPS correcta.
- [ ] El cliente funciona en una PC limpia (o con el runtime documentado).
- [ ] `artifacts/` sigue ignorado por Git.

## Lo que voy a revisar

1. Que el artefacto no tenga secretos ni configuración de desarrollo.
2. Que las migraciones se apliquen como paso separado y con backup previo.
3. Que el servicio tenga usuario dedicado, reinicio automático y variables por entorno.
4. Que la versión publicada sea identificable.
5. Que el cliente use la URL de producción y no dependa de una configuración manual frágil.
