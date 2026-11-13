# Onboarding de usuarios

Guía para el responsable del servicio. Cubre cómo entregar Ledger a 3 o 4 personas
conocidas sin publicar credenciales ni datos.

## Antes de empezar

- La API está desplegada en Render y responde en una URL `https://...onrender.com/`.
- La base de datos de Neon contiene las cuentas existentes y no se modifica desde
  este procedimiento.
- Tenés el código de invitación configurado en Render (`Ledger__Registration__InviteCode`).
- Tenés el ZIP del cliente generado con `scripts/release-desktop.ps1`.

## 1. Generar el paquete del cliente

Desde la raíz del repositorio, en PowerShell 7:

```powershell
.\scripts\release-desktop.ps1 `
  -Version 1.0.0 `
  -ApiBaseUrl https://TU-SERVICIO.onrender.com/
```

El script:

1. publica `Ledger.Desktop` como `win-x64`, autocontenido y de archivo único,
2. quita los PDB,
3. escribe un `appsettings.json` externo con la URL indicada,
4. agrega un `README.txt` para el usuario final,
5. crea `artifacts/release/Ledger.Desktop-1.0.0-win-x64.zip`,
6. escribe `Ledger.Desktop-1.0.0-win-x64.sha256.txt`.

No es necesario recompilar para cambiar la URL: el usuario puede editar
`appsettings.json` o definir `LEDGER_API_BASE_URL`.

Verificá el contenido antes de distribuirlo:

```powershell
Get-ChildItem artifacts\release\Ledger.Desktop-1.0.0-win-x64 | Select-Object Name, Length
Get-Content artifacts\release\Ledger.Desktop-1.0.0-win-x64.sha256.txt
```

## 2. Definir quién entra

Decidí con anticipación:

- qué personas van a usar Ledger,
- si van a ingresar con una cuenta existente o con una cuenta nueva,
- quién recibe el código de invitación.

No envíes contraseñas por el mismo canal que el código de invitación. Preferí
entregar el código en persona o por un canal distinto.

## 3. Entregar el cliente

Enviá el ZIP y el archivo de checksums. Incluí este mensaje base:

```text
Adjunto Ledger 1.0.0 para Windows.

1. Descomprimí el ZIP en una carpeta propia (no lo ejecutes dentro del ZIP).
2. Abrí Ledger.Desktop.exe.
3. Ingresá con tu usuario o email y tu contraseña.
4. Si todavía no tenés cuenta, usá "Crear cuenta" con el código de invitación
   que te paso aparte.

La primera vez puede tardar hasta un minuto si el servidor estaba inactivo.
Si Windows muestra SmartScreen, elegí "Más información" y "Ejecutar de todas formas".
```

El `README.txt` incluido en el ZIP repite estas instrucciones y explica cómo
cambiar la dirección de la API.

## 4. Alta de una cuenta nueva

Si el usuario no tiene cuenta:

1. Abrí Ledger y elegí **Crear cuenta**.
2. Completá usuario, email, nombre visible, contraseña y código de invitación.
3. La cuenta se crea con sus categorías por defecto.
4. Pedile que cambie la contraseña si la entregaste vos.

El registro se puede desactivar cuando todos los usuarios ya tengan cuenta:
en Render, cambiá `Ledger__Registration__Enabled` a `false` y redesplegá.

## 5. Cuentas existentes

Las cuentas que ya están en la base no se modifican al arrancar la API. En
`Production` el aprovisionamiento es **solo de creación**:

- si la cuenta existe, no se toca su contraseña, email ni nombre,
- si la cuenta no existe y no hay `Ledger__Owner` ni `Ledger__Users`, no se crea nada.

Por eso no configures `Ledger__Owner__*` ni `Ledger__Users__*` en Render.

## 6. Contraseñas

No hay recuperación automática de contraseña. Si un usuario la olvida, el
responsable del servicio debe restablecerla con acceso controlado a la base o
con el procedimiento que se defina. Documentá cada restablecimiento.

Recomendaciones:

- contraseñas de al menos 12 caracteres,
- gestor de contraseñas,
- no reutilizar contraseñas de otros servicios.

## 7. Verificación con cada usuario

Pedile que confirme:

- que puede iniciar sesión,
- que ve sus cuentas y movimientos,
- que puede crear y editar un movimiento de prueba,
- que el dashboard muestra datos,
- que puede cerrar sesión y volver a entrar.

Si algo falla, pedile el texto exacto del error y revisá los logs de Render.
Nunca pidas la contraseña.

## 8. Baja o pérdida de acceso

- Si alguien deja de usar Ledger, desactivalo desde la administración de la base
  (`IsActive = false`) y avisá al resto.
- Si sospechás que una contraseña se filtró, restablecela de inmediato.
- Si se filtró el código de invitación, cambialo en Render y redesplegá.

## 9. Checklist de entrega

- [ ] El ZIP se generó con la URL correcta de Render.
- [ ] El checksum SHA256 coincide con el archivo entregado.
- [ ] El usuario recibió el `README.txt`.
- [ ] El código de invitación se entregó por un canal separado.
- [ ] La cuenta del usuario existe y puede iniciar sesión.
- [ ] El usuario completó la verificación del paso 7.
- [ ] El registro se desactivó si ya no se necesitan cuentas nuevas.
