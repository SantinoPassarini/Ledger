# Ledger 1.0.0

Primera versión pública de Ledger: una aplicación de finanzas personales de escritorio construida con C# y .NET 8.

## Qué incluye

- Dashboard mensual con saldo total, ingresos, gastos, tendencia de 6 meses y movimientos recientes.
- Gestión de cuentas: crear, editar y archivar.
- Categorías de ingresos y gastos, con categorías iniciales para cada usuario nuevo.
- Movimientos: crear, editar, buscar, filtrar y archivar.
- Transferencias entre cuentas con validación de saldo.
- Presupuestos mensuales por categoría con seguimiento de gasto.
- Autenticación JWT con cuentas individuales y aislamiento de datos por usuario.

## Descarga y uso

1. Descargar `Ledger.Desktop-1.0.0-win-x64.zip`.
2. Verificar el hash con `Ledger.Desktop-1.0.0-win-x64.sha256.txt` si se desea.
3. Extraer el ZIP en una carpeta local.
4. Ejecutar `Ledger.Desktop.exe`.
5. Elegir **Create account** y usar el código de invitación indicado en el README del repositorio.
6. Elegir un username y una contraseña de al menos 12 caracteres.

La cuenta creada es propia y privada: arranca limpia, con las categorías por defecto y sin datos financieros precargados. Cada visitante puede cargar sus propios datos de prueba sin afectar a otros usuarios.

## Requisitos

- Windows x64.
- No requiere instalar .NET: el ejecutable es autocontenido.
- Conexión a Internet para comunicarse con la API.

## Notas

- El primer inicio puede tardar alrededor de un minuto por el arranque en frío del servicio.
- Windows SmartScreen puede mostrar una advertencia al ejecutar un archivo descargado de Internet.
- No hay recuperación de contraseña: guardar las credenciales elegidas.
- El código de invitación puede rotarse o desactivarse en cualquier momento. Las cuentas ya creadas no se ven afectadas.
