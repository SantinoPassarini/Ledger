# Backups programados

Objetivo: que exista una copia reciente de la base, fuera del servidor de la API, y que hayas probado que se puede restaurar.

Los scripts ya existen en `scripts/` y su referencia completa está en [`scripts/README.md`](../../scripts/README.md). Esta guía cubre la parte de producción: programación, copia externa, monitoreo y simulacro de restore.

## Punto de partida

| Script | Plataforma |
|---|---|
| `scripts/backup-ledger.ps1` | Windows PowerShell 7+ |
| `scripts/backup-ledger.sh` | Linux/macOS Bash |

Ambos ejecutan `pg_dump --format=custom --no-owner --no-privileges`, verifican con `pg_restore --list` y aplican retención local. La API no participa: no hay endpoint de backup.

Antes de programar nada, ejecuta uno a mano y confirma que el archivo existe y pesa más que cero.

## Definir frecuencia y retención

Para un uso privado, un punto de partida razonable:

- **RPO** (cuánto dato puedes perder): 24 horas.
- **RTO** (cuánto tardas en volver): menos de 1 hora.
- **Local**: un backup diario, retención 30 días.
- **Externo**: uno por semana (o diario si el volumen es pequeño), retención 90 días.
- **Regla 3-2-1**: tres copias, en dos medios, una fuera del servidor.

Ajusta los números a lo que realmente puedas mantener. Un backup diario que nunca se copia fuera del servidor no cumple el objetivo.

## Requisitos en el servidor

- `pg_dump` y `pg_restore` en `PATH`, con versión igual o mayor que la del servidor PostgreSQL.
- La cadena de conexión en un archivo protegido o en el entorno de la cuenta que ejecuta el job.
- Una carpeta de salida con espacio suficiente y permisos solo para esa cuenta.
- La carpeta `backups/` no se versiona y no se sirve por HTTP.

No pongas la contraseña en la línea de comandos. Usa `LEDGER_BACKUP_CONNECTION`, `PGPASSWORD` o `PGPASSFILE`.

## Programar en Linux

### Opción 1: cron

`/etc/ledger/backup.env` (permisos `600`, dueño de la cuenta de backup):

```bash
LEDGER_BACKUP_CONNECTION='postgresql://user:password@host/db?sslmode=require'
LEDGER_BACKUP_DIR=/srv/backups/ledger
LEDGER_BACKUP_RETENTION_DAYS=30
```

Crontab de esa cuenta:

```cron
15 3 * * * . /etc/ledger/backup.env && /opt/ledger/scripts/backup-ledger.sh >> /var/log/ledger-backup.log 2>&1
```

### Opción 2: systemd timer (mejor para diagnosticar)

`/etc/systemd/system/ledger-backup.service`:

```ini
[Unit]
Description=Ledger database backup
After=network-online.target

[Service]
Type=oneshot
User=ledger-backup
EnvironmentFile=/etc/ledger/backup.env
ExecStart=/opt/ledger/scripts/backup-ledger.sh --output /srv/backups/ledger
```

`/etc/systemd/system/ledger-backup.timer`:

```ini
[Unit]
Description=Daily Ledger backup

[Timer]
OnCalendar=*-*-* 03:15:00
RandomizedDelaySec=300
Persistent=true

[Install]
WantedBy=timers.target
```

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now ledger-backup.timer
systemctl list-timers ledger-backup.timer
journalctl -u ledger-backup.service
```

`Persistent=true` ejecuta el backup perdido si el servidor estuvo apagado a la hora programada.

## Programar en Windows

Tarea del Programador de tareas:

```text
Programa:   pwsh.exe
Argumentos: -NoProfile -File C:\Ledger\scripts\backup-ledger.ps1 -OutputDirectory D:\Backups\Ledger -RetentionDays 30
```

- "Ejecutar tanto si el usuario inició sesión como si no".
- Cuenta dedicada, con permiso solo sobre la carpeta de backups.
- Define `LEDGER_BACKUP_CONNECTION` para esa cuenta, o pasa `-ConnectionString` desde un archivo protegido.
- Configura reintento si falla y revisa el historial de la tarea.

También puedes crear un wrapper `.ps1` que cargue el secreto y llame al script, para que la tarea no contenga la cadena.

## Monitoreo

Un backup que falla en silencio es peor que no tener backup, porque da falsa tranquilidad.

- Haz que el job termine con código distinto de cero ante cualquier error (los scripts ya lo hacen).
- Revisa el resultado: `systemctl status`, `journalctl`, historial de la tarea o el log de cron.
- Agrega una alerta externa (por ejemplo un ping a Healthchecks.io al final del job) para detectar que dejó de correr.
- Comprueba semanalmente que el archivo más reciente tiene tamaño razonable.
- No bases la alerta solo en el espacio en disco.

## Copia fuera del servidor

El backup local no protege contra perder el servidor. Cópialo a otro lugar:

```powershell
rclone copy D:\Backups\Ledger remote:ledger-backups\
```

```bash
scp /srv/backups/ledger/ledger-*.dump backup@backup-host:/srv/ledger-backups/
```

Si el destino no es de plena confianza, cifra antes de copiar:

```bash
age -r age1examplepublickey -o ledger-20261108.dump.age ledger-20261108.dump
```

Guarda la clave privada de `age` en un lugar distinto del servidor y de la copia cifrada. Si pierdes la clave, pierdes los backups.

Automatiza también la copia externa, no solo el dump. Un job diario que hace dump + `rclone copy` + verificación remota es más útil que dos tareas manuales.

## Simulacro de restore

Hazlo al menos una vez al mes y antes de cada cambio grande.

1. Toma un backup nuevo.
2. Crea una base **separada** (otra base local o una rama de Neon). Nunca la de producción.
3. Restaura:

   ```bash
   pg_restore --clean --if-exists --no-owner --no-privileges \
     --dbname "postgresql://user:password@host/ledger_restore_check?sslmode=require" \
     backups/ledger-20261108-030000.dump
   ```

4. Apunta una instancia desechable de la API a esa base y verifica `/health`.
5. Inicia sesión con una cuenta real y revisa dashboard, cuentas, movimientos y presupuestos.
6. Borra la base de prueba.

No ejecutes `--clean` contra producción. `--clean` elimina objetos existentes antes de restaurar.

## Verificación

- [ ] El script corre a mano y produce un `.dump` no vacío.
- [ ] El job programado corre solo a la hora prevista.
- [ ] El job falla con código distinto de cero si `pg_dump` falla.
- [ ] El archivo se copia fuera del servidor.
- [ ] La copia externa está cifrada si el destino no es de confianza.
- [ ] La retención borra archivos viejos y conserva los recientes.
- [ ] Hay una alerta si el job deja de ejecutarse.
- [ ] Se completó al menos un restore de prueba.
- [ ] La clave de cifrado está guardada fuera del servidor.
- [ ] La carpeta de backups no está en Git ni expuesta por HTTP.

## Lo que voy a revisar

1. Que la frecuencia y la retención coincidan con el RPO/RTO que elegiste.
2. Que el secreto no esté en la línea de comandos ni en el repositorio.
3. Que exista una copia fuera del servidor.
4. Que el fallo del job sea visible.
5. Que hayas restaurado un backup en una base separada y verificado la aplicación.
