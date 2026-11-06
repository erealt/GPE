# Instalar GPE en el servidor

Esta guía instala la base de datos con las tablas del DeCA, la web del DeCA (`GPE.Web`) y la tarea diaria que borra los PDF del DeCA de más de un año (`GPE.Avisos`). La web no envía avisos por correo.

Los pasos se hacen en orden. Los comandos son de PowerShell y, en el servidor, se ejecutan **como administrador**.

---

## 1. Antes de subir


### Publicar en tu ordenador

**Qué es publicar.** En el servidor no se instala el código del proyecto ni Visual Studio. Publicar convierte el código en los programas ya preparados para funcionar, y los deja en una carpeta. Esa carpeta es lo único que se lleva al servidor. Se hace en tu ordenador, antes de subir, y no cambia nada del proyecto.

**1. Abre PowerShell en la carpeta del proyecto**

```powershell
cd C:\Users\TERMI\Desktop\Estela\GPE
```

**2. Comprueba que estás en `main`**

```powershell
git branch --show-current
```

Tiene que salir `main`. Lo que se publica es el código de la rama en la que estés. `main` tiene que tener ya esta guía y `BaseDatos\instalar_base_datos.ps1`.

**3. Publica la web**

```powershell
dotnet publish GPE.Web -c Release -o C:\GPE-Publicar\Web
```

- `GPE.Web` es el proyecto que se publica.
- `-c Release` lo prepara para usarlo de verdad, no para hacer pruebas.
- `-o C:\GPE-Publicar\Web` es la carpeta donde se deja. Si no existe, se crea sola.

Tarda un rato. Termina sin errores y, al final, sale la carpeta donde lo ha dejado.

**4. Publica la tarea diaria**

Es la que borra los PDF de más de un año. El programa se llama `GPE.Avisos` porque también envía los correos de las devoluciones, pero aquí no se usa para eso.

```powershell
dotnet publish GPE.Avisos -c Release -o C:\GPE-Publicar\Avisos
```

**5. Comprueba lo que ha salido**

Abre `C:\GPE-Publicar` en el Explorador de archivos. Tienen que estar estos archivos:

- En la carpeta `Web`: `GPE.Web.exe` y `web.config`.
- En la carpeta `Avisos`: `GPE.Avisos.exe`.

Abre `C:\GPE-Publicar\Web\appsettings.json` con el Bloc de notas y comprueba que `"UsuarioWeb"` es `1`. Los DeCA que se guarden desde la web quedarán a nombre de ese usuario, que será el primero que crees en el paso 3.

**6. Lleva las carpetas al servidor**

Copia estas dos carpetas al disco `C:\` del servidor, con un USB o una carpeta compartida:

- `C:\GPE-Publicar`
- `C:\Users\TERMI\Desktop\Estela\GPE\BaseDatos`

La carpeta `BaseDatos` solo hace falta si vas a crear la base de datos desde el servidor (paso 3).

---

## 2. Preparar el servidor

Instala IIS:

```powershell
Install-WindowsFeature Web-Server,Web-WebSockets -IncludeManagementTools
```

Después descarga e instala el **ASP.NET Core Hosting Bundle** de .NET 10 desde <https://dotnet.microsoft.com/download/dotnet/10.0>. Tiene que instalarse **después** de IIS. También instala lo necesario para que funcione `GPE.Avisos`.

Copia los programas a su sitio:

```powershell
New-Item -ItemType Directory -Force C:\GPE\Web, C:\GPE\Avisos, C:\GPE\DeCA
```

```powershell
Copy-Item C:\GPE-Publicar\Web\* C:\GPE\Web -Recurse -Force
```

```powershell
Copy-Item C:\GPE-Publicar\Avisos\* C:\GPE\Avisos -Recurse -Force
```

Si copiaste `GPE-Publicar` a otro sitio del servidor, cambia la ruta.

---

## 3. Crear la base de datos

Se puede hacer en el servidor o desde tu ordenador, donde ya tienes `sqlcmd`. Hay que ejecutarlo con un usuario que pueda crear bases de datos.

Cambia los tres valores antes de ejecutarlo, y entra en la carpeta `BaseDatos`:

```powershell
powershell -ExecutionPolicy Bypass -File .\instalar_base_datos.ps1 -Servidor "PONER-SERVIDOR" -Nombre "PONER-NOMBRE" -Correo "PONER-CORREO"
```

Debe terminar con `Instalación terminada`. Si la base de datos ya tiene tablas, el script se niega y no cambia nada.

Solo se crean las tres tablas que usa la web: `USUARIO`, `DECA` y `DECA_PALET`. El resto se instalará cuando se junte con lo demás de la aplicación. Entonces no se puede ejecutar `crear_tablas.sql` tal cual, porque estas tres ya existirán.

El primer usuario es a cuyo nombre quedarán los DeCA que se guarden desde la web.

---

## 4. Variables del servidor

Cambia los valores y ejecuta cada línea en el servidor. `/M` las deja para todo el servidor, así las ven tanto la web como la tarea.

```powershell
setx GPE_CONEXION "Server=PONER-SERVIDOR;Database=GPE;Integrated Security=True;TrustServerCertificate=True;" /M
```

```powershell
setx GPE_DECA_CARPETA "C:\GPE\DeCA" /M
```

```powershell
setx GPE_WP_USUARIO "PONER-USUARIO-WORDPRESS" /M
```

```powershell
setx GPE_WP_CLAVE "PONER-CLAVE-WORDPRESS" /M
```

Sin las de WordPress, la web no puede subir los PDF y la tarea diaria no puede borrarlos.

---

## 5. Crear la web en IIS

Cambia el puerto si no es el 80. Si el 80 lo usa el sitio *Default Web Site*, usa el puerto libre que te haya dicho Carlos.

```powershell
Import-Module WebAdministration
New-WebAppPool -Name "GPE.Web"
Set-ItemProperty "IIS:\AppPools\GPE.Web" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\GPE.Web" -Name processModel.loadUserProfile -Value $true
New-Website -Name "GPE.Web" -PhysicalPath "C:\GPE\Web" -ApplicationPool "GPE.Web" -Port 80
```

Da permiso a la web para escribir en la carpeta de los PDF:

```powershell
icacls C:\GPE\DeCA /grant "IIS APPPOOL\GPE.Web:(OI)(CI)M"
```

Reinicia IIS para que lea las variables del paso 4:

```powershell
net stop was /y
```

```powershell
net start w3svc
```

---

## 6. Permisos en SQL Server

La web entra con la cuenta `IIS APPPOOL\GPE.Web`. Este comando le da permiso para leer y escribir en `GPE`, y vale cuando SQL Server está en el mismo servidor que IIS. La tarea diaria no necesita permiso, porque sin correo no entra en la base de datos.

```powershell
sqlcmd -S "PONER-SERVIDOR" -E -C -d GPE -Q "CREATE LOGIN [IIS APPPOOL\GPE.Web] FROM WINDOWS; CREATE USER [IIS APPPOOL\GPE.Web] FOR LOGIN [IIS APPPOOL\GPE.Web]; ALTER ROLE db_datareader ADD MEMBER [IIS APPPOOL\GPE.Web]; ALTER ROLE db_datawriter ADD MEMBER [IIS APPPOOL\GPE.Web];"
```

Si SQL Server está en otro servidor, esta cuenta no sirve. Lo más sencillo es crear un usuario de SQL y ponerlo en `GPE_CONEXION`.

---

## 7. Tarea diaria

Borra los PDF del DeCA de más de un año, en la web y en `C:\GPE\DeCA`. Se ejecuta cada día a las 7:00; cambia la hora si hace falta.

```powershell
schtasks /Create /TN "GPE Avisos" /TR "C:\GPE\Avisos\GPE.Avisos.exe" /SC DAILY /ST 07:00 /RU SYSTEM
```

---

## 8. Comprobar que funciona

1. **Web**: desde otro ordenador de la red, abre `http://PONER-SERVIDOR/` (con `:8080` al final si usas otro puerto). Si no carga desde fuera pero sí en el propio servidor, pide a Carlos que abra el puerto en el firewall.
2. **Guardar**: crea un DeCA de prueba y guárdalo.
3. **PDF**: pulsa *Generar PDF y QR*. Comprueba que aparece en `C:\GPE\DeCA` y que el QR abre el PDF desde un móvil.
4. **Tarea diaria**: lánzala a mano.

   ```powershell
   schtasks /Run /TN "GPE Avisos"
   ```

   Después mira el registro:

   ```powershell
   Get-Content C:\Windows\System32\config\systemprofile\AppData\Local\GPE\avisos.log -Tail 10
   ```

   Tiene que aparecer `DeCA: ningún PDF de más de un año.`

   También saldrá `ERROR: Falta configurar GPE_SMTP_USUARIO y GPE_SMTP_CLAVE.` Es normal: no hay correo configurado, y el borrado se hace igual. Por eso la tarea aparece con resultado `0x1` en el Programador de tareas. Cualquier otro `ERROR` sí hay que mirarlo.

---

## 9. Si algo falla

| Qué pasa | Qué suele ser | Qué hacer |
|---|---|---|
| Error 500.19 en la web | Falta el Hosting Bundle, o se instaló antes que IIS | Repara el Hosting Bundle desde *Aplicaciones instaladas* y repite los dos comandos de reinicio del paso 5 |
| Error 500.30 o 500.31 | No arranca la aplicación o no encuentra .NET 10 | Mira el *Visor de eventos* → *Registros de Windows* → *Aplicación* |
| `Login failed for user 'IIS APPPOOL\GPE.Web'` | Falta el permiso del paso 6 | Repite el paso 6 |
| La web sigue sin ver las variables | IIS no las ha leído | Reinicia el servidor |
| `El usuario 1 de "UsuarioWeb"... no existe` | No se creó el primer usuario, o tiene otro id | Comprueba la tabla `USUARIO` y `appsettings.json` |
| Error al generar el PDF por permisos | La web no puede escribir en `C:\GPE\DeCA` | Repite el `icacls` del paso 5 |
| `ERROR al borrar los DeCA antiguos: Falta configurar...` en `avisos.log` | Falta una variable de WordPress, o la tarea no la ha leído | Revisa el paso 4 y reinicia el servidor |

---

## 10. Actualizar la web más adelante

Publica de nuevo en tu ordenador (paso 1) y, en el servidor:

```powershell
Stop-WebAppPool -Name "GPE.Web"
```

```powershell
Copy-Item C:\GPE-Publicar\Web\* C:\GPE\Web -Recurse -Force
```

```powershell
Start-WebAppPool -Name "GPE.Web"
```

Si cambias `"UsuarioWeb"` en el servidor, recuerda que esta copia sobrescribe `appsettings.json`.

Si la actualización trae algún script nuevo en `BaseDatos`, ejecútalo antes de arrancar la web con:

```powershell
sqlcmd -S "PONER-SERVIDOR" -E -C -I -f 65001 -i .\NOMBRE-DEL-SCRIPT.sql
```
