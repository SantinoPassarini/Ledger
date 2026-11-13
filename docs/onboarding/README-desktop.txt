Ledger {{VERSION}} - Cliente de escritorio para Windows
Generado: {{GENERATED_AT}}

Ledger es una aplicacion privada de finanzas personales. Este paquete contiene el
cliente de escritorio y la configuracion necesaria para conectarse a la API.

CONTENIDO
---------
  Ledger.Desktop.exe   Aplicacion principal.
  appsettings.json     Direccion de la API. Se puede editar con el Bloc de notas.
  README.txt           Este archivo.

REQUISITOS
----------
  - Windows 10 u 11 de 64 bits.
  - Conexion a Internet.
  - No hace falta instalar .NET: el ejecutable es autocontenido.

INSTALACION
-----------
  1. Descomprimi el ZIP en una carpeta propia, por ejemplo:
       C:\Users\<tu usuario>\Ledger
  2. No ejecutes el programa desde dentro del ZIP. Windows puede bloquear archivos
     comprimidos; extrae todo primero.
  3. Si Windows SmartScreen muestra una advertencia, elegi "Mas informacion" y
     despues "Ejecutar de todas formas". El ejecutable no esta firmado digitalmente.

PRIMER INGRESO
--------------
  1. Abri Ledger.Desktop.exe.
  2. Ingresa tu nombre de usuario o email y tu contrasena.
  3. Si recibiste un codigo de invitacion, usa la opcion "Crear cuenta" del login.
     El codigo lo entrega el responsable del servicio.

  La API puede tardar hasta un minuto en responder la primera vez del dia si el
  servicio gratuito estuvo inactivo. La ventana de login espera ese tiempo; no
  cierres la aplicacion durante la espera.

CUENTAS Y CONTRASENAS
---------------------
  - Las cuentas son privadas. Cada usuario ve unicamente sus propias cuentas,
    categorias, movimientos y presupuestos.
  - No existe recuperacion automatica de contrasena. Si olvidas la contrasena,
    pedile al responsable del servicio que restablezca la cuenta.
  - Cambia tu contrasena desde el menu de la aplicacion.
  - No compartas tu contrasena ni el codigo de invitacion.

DATOS Y RESPALDOS
-----------------
  - Los datos se guardan en la base de datos del servidor, no en esta carpeta.
  - Los respaldos los administra el responsable del servicio, fuera de la API.
  - No copies la carpeta del programa esperando conservar tus datos: el cliente
    no guarda la informacion financiera de forma local.

PROBLEMAS FRECUENTES
--------------------
  "No se puede conectar con el servidor"
    - Verifica tu conexion a Internet.
    - Revisa que appsettings.json tenga la URL correcta y que termine en /.
    - Espera un minuto: el servicio gratuito puede estar despertando.

  "Usuario o contrasena incorrectos"
    - Revisa mayusculas y minusculas.
    - Podes ingresar con el nombre de usuario o con el email.
    - Si olvidaste la contrasena, pedi un restablecimiento.

  La aplicacion se cierra al abrirla
    - Extrae el ZIP en una carpeta con permisos de escritura.
    - Verifica que appsettings.json sea un JSON valido.
