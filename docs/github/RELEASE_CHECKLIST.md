# Checklist para publicar el Release v1.0.0 en GitHub

La subida del Release la hace el owner del repositorio. Esta checklist deja todo listo para ese paso.

## Antes de publicar

- [ ] Hacer commit y push a `main` de `README.md`, `docs/github/` y `docs/images/`. El Release debe apuntar a un commit que ya incluya el código de invitación y las capturas.
- [ ] Confirmar que el README ya contiene el código de invitación y las instrucciones de **Create account**.
- [ ] Confirmar que las capturas de `docs/images/` muestran datos falsos y ningún usuario real.
- [ ] Verificar que el ZIP y el `.sha256.txt` estén actualizados:

```powershell
cd artifacts\release
Get-FileHash .\Ledger.Desktop-1.0.0-win-x64.zip -Algorithm SHA256
Get-Content .\Ledger.Desktop-1.0.0-win-x64.sha256.txt
```

- [ ] Confirmar que el ZIP solo contiene `Ledger.Desktop.exe`, `appsettings.json` y `README.txt`.
- [ ] Confirmar que `appsettings.json` dentro del ZIP apunta a la API pública y que el `appsettings.json` versionado sigue apuntando a `http://localhost:5100/`.

## Publicar en GitHub

1. Entrar a `https://github.com/SantinoPassarini/Ledger/releases`.
2. Hacer clic en **Draft a new release**.
3. Elegir o crear el tag `v1.0.0` apuntando a `main`.
4. Título: `Ledger 1.0.0`.
5. Descripción: copiar el contenido de `docs/github/RELEASE_NOTES.md`.
6. Adjuntar ambos archivos:
   - `artifacts/release/Ledger.Desktop-1.0.0-win-x64.zip`
   - `artifacts/release/Ledger.Desktop-1.0.0-win-x64.sha256.txt`
7. Revisar que **Set as a pre-release** esté desactivado.
8. Hacer clic en **Publish release**.

## Después de publicar

- [ ] Abrir el Release en el navegador y descargar el ZIP desde el link público.
- [ ] Verificar que el hash descargado coincida con el `.sha256.txt`.
- [ ] Probar **Create account** una vez con el código publicado.
- [ ] Si el código se comparte fuera del README o empieza a haber cuentas extrañas, rotar `Ledger__Registration__InviteCode` en Render.
