# Instala (o actualiza) GPE en el servidor: base de datos, web en IIS, aplicación de escritorio
# (ClickOnce, carpeta compartida \\SERVIDOR\GPE) y la tarea diaria que borra los PDF del DeCA.
# Se ejecuta en el servidor, desde la carpeta del paquete, en PowerShell como administrador:
#   powershell -ExecutionPolicy Bypass -File .\instalar-servidor.ps1
# Se puede volver a ejecutar para actualizar: lo que ya está hecho no se repite
# y la base de datos que ya existe NO se toca.
#
# Opcionales:
#   -ReemplazarBaseDatos       la base GPE que ya hay se guarda en una copia y se cambia por la del paquete
#   -InstanciaSql SQLEXPRESS   solo si en el servidor hay más de una instancia de SQL Server
#   -UsuarioWeb 1              id del usuario a cuyo nombre se guardan los DeCA hechos desde la web
#   -Puerto 80                 puerto de la web
#   -Carpeta C:\GPE            dónde se instala todo

param(
    [switch]$ReemplazarBaseDatos,
    [string]$InstanciaSql = "",
    [int]$UsuarioWeb = 1,
    [int]$Puerto = 80,
    [string]$Carpeta = "C:\GPE"
)

$ErrorActionPreference = "Stop"
$sitio = "GPE Web"
$grupo = "GPE"                         # grupo de aplicaciones de IIS
$cuentaWeb = "IIS APPPOOL\$grupo"      # cuenta con la que se ejecuta la web
$tarea = "GPE - Borrado DeCA"
$base = "GPE"
$recurso = "GPE"                       # carpeta compartida \\SERVIDOR\GPE (instalación del escritorio)
$sql = "localhost,1433"                # la web, la tarea y este script entran por el puerto fijo

function Paso($texto) { Write-Host ""; Write-Host "== $texto" -ForegroundColor Cyan }
function Aviso($texto) { Write-Host $texto -ForegroundColor Yellow }
function Poner($nombre, $valor) { [Environment]::SetEnvironmentVariable($nombre, $valor, "Machine") }
function Actual($nombre) { [Environment]::GetEnvironmentVariable($nombre, "Machine") }

function Sql($servidor, $texto, $baseDatos = "master") {
    $conexion = New-Object System.Data.SqlClient.SqlConnection(
        "Server=$servidor;Database=$baseDatos;Integrated Security=True;TrustServerCertificate=True;")
    $conexion.Open()
    try {
        $comando = $conexion.CreateCommand()
        $comando.CommandTimeout = 0
        $comando.CommandText = $texto
        return $comando.ExecuteScalar()
    } finally { $conexion.Close() }
}

# ---------- 0. Comprobaciones ----------

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw "Hay que abrir PowerShell como administrador (clic derecho, Ejecutar como administrador)." }

foreach ($necesaria in "Web", "Avisos", "Escritorio", "BaseDatos") {
    if (-not (Test-Path (Join-Path $PSScriptRoot $necesaria))) {
        throw "No encuentro la carpeta '$necesaria' junto a este script. Ejecútalo desde la carpeta del paquete."
    }
}

# ---------- 1. SQL Server ----------

Paso "1. SQL Server"
$todas = @(Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL" -ErrorAction SilentlyContinue |
    ForEach-Object { $_.PSObject.Properties } | Where-Object { $_.Name -notlike "PS*" })
if ($todas.Count -eq 0) {
    throw "No hay SQL Server en este servidor. Instala SQL Server Express (está en LEEME.txt) y vuelve a ejecutar el script."
}
$instancias = if ($InstanciaSql) { @($todas | Where-Object { $_.Name -eq $InstanciaSql }) } else { $todas }
if ($instancias.Count -ne 1) {
    $lista = ($todas | ForEach-Object { $_.Name }) -join ", "
    throw "Hay varias instancias de SQL Server o no existe la indicada. Vuelve a ejecutar con -InstanciaSql y una de estas: $lista"
}
$nombreInstancia = $instancias[0].Name
$idInstancia = $instancias[0].Value
$servicio = if ($nombreInstancia -eq "MSSQLSERVER") { "MSSQLSERVER" } else { "MSSQL`$$nombreInstancia" }
$local = if ($nombreInstancia -eq "MSSQLSERVER") { "localhost" } else { "localhost\$nombreInstancia" }
Write-Host "Instancia: $nombreInstancia"

$reiniciar = $false

# Entrar desde la red por TCP en el puerto fijo 1433 (así no hace falta el servicio SQL Browser)
$tcp = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$idInstancia\MSSQLServer\SuperSocketNetLib\Tcp"
if ((Get-ItemProperty $tcp).Enabled -ne 1) { Set-ItemProperty $tcp Enabled 1; $reiniciar = $true }
$ipAll = Join-Path $tcp "IPAll"
if ((Get-ItemProperty $ipAll).TcpPort -ne "1433" -or (Get-ItemProperty $ipAll).TcpDynamicPorts -ne "") {
    Set-ItemProperty $ipAll TcpPort "1433"
    Set-ItemProperty $ipAll TcpDynamicPorts ""
    $reiniciar = $true
}

# Entrar con usuario y contraseña de SQL (el escritorio usa gpe_app), además de con Windows
$servidorReg = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$idInstancia\MSSQLServer"
if ((Get-ItemProperty $servidorReg).LoginMode -ne 2) { Set-ItemProperty $servidorReg LoginMode 2; $reiniciar = $true }

if ($reiniciar) {
    Write-Host "Activando TCP 1433 y el inicio de sesión con usuario de SQL (se reinicia SQL Server)..."
    Restart-Service $servicio -Force
    Start-Sleep -Seconds 5
}

try { [void](Sql $sql "SELECT 1") }
catch { throw "No se puede entrar en SQL Server por localhost,1433 con este usuario de Windows: $($_.Exception.Message)" }

$esAdmin = Sql $sql "SELECT IS_SRVROLEMEMBER('sysadmin')"
if ($esAdmin -ne 1) { throw "Tu usuario de Windows no es administrador (sysadmin) de SQL Server. Hay que ejecutar el script con el usuario que instaló SQL Server." }

if (-not (Get-NetFirewallRule -DisplayName "GPE SQL Server (1433)" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "GPE SQL Server (1433)" -Direction Inbound -Protocol TCP -LocalPort 1433 `
        -Action Allow -Profile Domain, Private | Out-Null
}
Write-Host "SQL Server listo (puerto 1433 abierto en el cortafuegos)."

# ---------- 2. Base de datos ----------

Paso "2. Base de datos $base"
$existe = -not ((Sql $sql "SELECT DB_ID('$base')") -is [DBNull])
if ($existe -and $ReemplazarBaseDatos) {
    # Se comprueba antes de quitar nada que este SQL Server puede abrir la copia
    if ((Sql $sql "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)") -lt 17) {
        throw "Este SQL Server es más antiguo que SQL Server 2025 y no puede abrir la copia. No se ha cambiado nada. Mira 'SI ALGO FALLA' en LEEME.txt."
    }
    # Antes de cambiarla se guarda la que había, por si hace falta recuperar algo
    $carpetaCopias = Sql $sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400))"
    $anterior = Join-Path $carpetaCopias ("GPE-antes-" + (Get-Date -Format "yyyyMMdd-HHmm") + ".bak")
    Write-Host "Guardando la base $base que había en $anterior ..."
    [void](Sql $sql "BACKUP DATABASE [$base] TO DISK = N'$anterior' WITH COPY_ONLY, INIT, CHECKSUM")
    [void](Sql $sql "ALTER DATABASE [$base] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$base];")
    $existe = $false
}
if ($existe) {
    Write-Host "Ya existe: no se toca (se conservan sus datos). Para cambiarla por la del paquete: -ReemplazarBaseDatos"
} else {
    $version = Sql $sql "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)"
    if ($version -lt 17) {
        throw "Este SQL Server es más antiguo (versión $version) que el del ordenador de desarrollo (17 = SQL Server 2025) y no puede abrir la copia. Mira 'SI ALGO FALLA' en LEEME.txt."
    }
    # SQL Server lee la copia con su propia cuenta: se deja en su carpeta de copias
    $carpetaCopias = Sql $sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400))"
    $copia = Join-Path $carpetaCopias "GPE-instalacion.bak"
    Copy-Item (Join-Path $PSScriptRoot "BaseDatos\GPE.bak") $copia -Force
    $datos = Sql $sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))"
    $registro = Sql $sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(400))"

    # Los nombres internos de los archivos vienen dentro de la copia
    $conexion = New-Object System.Data.SqlClient.SqlConnection("Server=$sql;Database=master;Integrated Security=True;TrustServerCertificate=True;")
    $conexion.Open()
    $comando = $conexion.CreateCommand()
    $comando.CommandText = "RESTORE FILELISTONLY FROM DISK = N'$copia'"
    $lector = $comando.ExecuteReader()
    $mover = @()
    while ($lector.Read()) {
        $carpetaDestino = if ($lector["Type"] -eq "L") { $registro } else { $datos }
        $extension = if ($lector["Type"] -eq "L") { "_log.ldf" } else { ".mdf" }
        $mover += "MOVE N'$($lector["LogicalName"])' TO N'$(Join-Path $carpetaDestino ($base + $extension))'"
    }
    $lector.Close(); $conexion.Close()

    Write-Host "Restaurando la copia de la base de datos..."
    [void](Sql $sql "RESTORE DATABASE [$base] FROM DISK = N'$copia' WITH $($mover -join ', '), RECOVERY")
    Remove-Item $copia -Force
    Write-Host "Base de datos $base creada con la copia del ordenador de desarrollo."
}

# Usuario de SQL para la aplicación de escritorio (su contraseña va dentro de la aplicación publicada)
$archivoClave = Join-Path $PSScriptRoot "gpe_app.txt"
if (Test-Path $archivoClave) {
    $clave = (Get-Content $archivoClave -Raw).Trim()
    [void](Sql $sql @"
IF SUSER_ID(N'gpe_app') IS NULL
    CREATE LOGIN gpe_app WITH PASSWORD = N'$clave', DEFAULT_DATABASE = [$base], CHECK_POLICY = OFF;
ELSE
    ALTER LOGIN gpe_app WITH PASSWORD = N'$clave';
"@)
    Remove-Item $archivoClave -Force
    Write-Host "Usuario de SQL gpe_app listo."
} elseif ((Sql $sql "SELECT SUSER_ID(N'gpe_app')") -is [DBNull]) {
    throw "Falta gpe_app.txt en el paquete. Vuelve a preparar el paquete en el ordenador de desarrollo."
} else {
    Write-Host "Usuario de SQL gpe_app: se queda como estaba."
}

# Permisos: el escritorio (gpe_app) y la web (cuenta de IIS) leen y escriben en GPE
[void](Sql $sql @"
IF SUSER_ID(N'$cuentaWeb') IS NULL CREATE LOGIN [$cuentaWeb] FROM WINDOWS;
IF USER_ID(N'gpe_app') IS NULL CREATE USER gpe_app FOR LOGIN gpe_app;
ELSE ALTER USER gpe_app WITH LOGIN = gpe_app;
IF USER_ID(N'$cuentaWeb') IS NULL CREATE USER [$cuentaWeb] FOR LOGIN [$cuentaWeb];
ALTER ROLE db_datareader ADD MEMBER gpe_app;
ALTER ROLE db_datawriter ADD MEMBER gpe_app;
ALTER ROLE db_datareader ADD MEMBER [$cuentaWeb];
ALTER ROLE db_datawriter ADD MEMBER [$cuentaWeb];
"@ $base)
Write-Host "Permisos dados a gpe_app y a la web."

# ---------- 3. IIS ----------

Paso "3. IIS"
Write-Host "Comprobando IIS (si no está, tarda unos minutos)..."
if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
    Install-WindowsFeature Web-Server, Web-Mgmt-Console, Web-Scripting-Tools | Out-Null       # Windows Server
} else {
    Enable-WindowsOptionalFeature -Online -All -NoRestart -FeatureName IIS-WebServerRole, IIS-WebServer, `
        IIS-ManagementConsole, IIS-ManagementScriptingTools | Out-Null                         # Windows 10/11
}
Write-Host "Instalado."

# ---------- 4. .NET 10 (Hosting Bundle) ----------
# Tiene que instalarse después de IIS, si no IIS no sabe arrancar la web

Paso "4. .NET 10 (Hosting Bundle)"
$modulo = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
$runtime = (& { try { dotnet --list-runtimes } catch { "" } }) -match "Microsoft.AspNetCore.App 10\."
if (-not (Test-Path $modulo) -or -not $runtime) {
    Aviso "Falta el Hosting Bundle de .NET 10."
    Aviso "Instálalo (está en LEEME.txt) y vuelve a ejecutar este script. Lo de SQL Server ya hecho no se repite."
    exit 1
}
Write-Host "Instalado."

# ---------- 5. Copiar los programas ----------

Paso "5. Copiar archivos a $Carpeta"
Import-Module WebAdministration

# Si la web ya estaba, se para mientras se copian los archivos (si no, están en uso)
if ((Test-Path "IIS:\AppPools\$grupo") -and (Get-WebAppPoolState $grupo).Value -eq "Started") {
    Stop-WebAppPool $grupo
    Start-Sleep -Seconds 3
}

foreach ($parte in "Web", "Avisos", "Escritorio") {
    robocopy (Join-Path $PSScriptRoot $parte) (Join-Path $Carpeta $parte) /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "No se pudo copiar la carpeta $parte (robocopy $LASTEXITCODE)." }
}

$carpetaDeca = Join-Path $Carpeta "DeCA"
New-Item -ItemType Directory $carpetaDeca -Force | Out-Null

# Usuario a cuyo nombre se guardan los DeCA de la web
$rutaAjustes = Join-Path $Carpeta "Web\appsettings.json"
$ajustes = Get-Content $rutaAjustes -Raw | ConvertFrom-Json
$ajustes.UsuarioWeb = $UsuarioWeb
$ajustes | ConvertTo-Json -Depth 10 | Set-Content $rutaAjustes -Encoding UTF8
Write-Host "Copiado."

# ---------- 6. Carpeta compartida para instalar el escritorio ----------

Paso "6. Carpeta compartida \\$env:COMPUTERNAME\$recurso"
$carpetaEscritorio = Join-Path $Carpeta "Escritorio"
# "Todos" en Windows en español, "Everyone" en inglés
$todos = ([Security.Principal.SecurityIdentifier]"S-1-1-0").Translate([Security.Principal.NTAccount]).Value
if (-not (Get-SmbShare -Name $recurso -ErrorAction SilentlyContinue)) {
    New-SmbShare -Name $recurso -Path $carpetaEscritorio -ReadAccess $todos | Out-Null
}
icacls $carpetaEscritorio /grant "*S-1-1-0:(OI)(CI)RX" /Q | Out-Null
Enable-NetFirewallRule -DisplayGroup "Compartir archivos e impresoras" -ErrorAction SilentlyContinue
Enable-NetFirewallRule -DisplayGroup "File and Printer Sharing" -ErrorAction SilentlyContinue
Write-Host "Los ordenadores instalan la aplicación desde \\$env:COMPUTERNAME\$recurso\setup.exe"

# ---------- 7. Variables para todo el servidor ----------
# Las leen la web y la tarea diaria. La contraseña de WordPress se escribe aquí, no queda en ningún archivo.

Paso "7. Variables"
Poner "GPE_CONEXION" "Server=$sql;Database=$base;Integrated Security=True;TrustServerCertificate=True;"
Poner "GPE_DECA_CARPETA" $carpetaDeca

$usuarioWp = Actual "GPE_WP_USUARIO"
$texto = if ($usuarioWp) { "Usuario de WordPress (Intro = $usuarioWp)" } else { "Usuario de WordPress" }
$nuevo = Read-Host $texto
if ($nuevo) { $usuarioWp = $nuevo.Trim() }
if (-not $usuarioWp) { throw "Hace falta el usuario de WordPress para subir los PDF del DeCA." }
Poner "GPE_WP_USUARIO" $usuarioWp

$claveGuardada = [bool](Actual "GPE_WP_CLAVE")
$texto = if ($claveGuardada) { "Contraseña de aplicación de WordPress (Intro = dejar la que hay)" } else { "Contraseña de aplicación de WordPress" }
$segura = Read-Host $texto -AsSecureString
$claveWp = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura))
if ($claveWp) { Poner "GPE_WP_CLAVE" $claveWp.Trim() }
elseif (-not $claveGuardada) { throw "Hace falta la contraseña de aplicación de WordPress para subir los PDF del DeCA." }
Write-Host "Variables puestas."

# ---------- 8. Sitio en IIS ----------

Paso "8. Sitio en IIS"
if (-not (Test-Path "IIS:\AppPools\$grupo")) { New-WebAppPool $grupo | Out-Null }
Set-ItemProperty "IIS:\AppPools\$grupo" managedRuntimeVersion ""   # .NET 10 no usa el .NET antiguo de IIS

# La primera instalación (solo DeCA) lo llamó "GPE DeCA"
if ((Test-Path "IIS:\Sites\GPE DeCA") -and -not (Test-Path "IIS:\Sites\$sitio")) {
    Rename-Item "IIS:\Sites\GPE DeCA" $sitio
    Write-Host "El sitio 'GPE DeCA' pasa a llamarse '$sitio'."
}

if (Test-Path "IIS:\Sites\$sitio") {
    Write-Host "Ya existe."
} else {
    # Otro sitio en el mismo puerto impediría arrancar este
    $ocupados = Get-Website | Where-Object {
        $_.State -eq "Started" -and ($_.bindings.Collection | Where-Object { $_.bindingInformation -like "*:$($Puerto):*" })
    }
    foreach ($otro in $ocupados) {
        if ($otro.Name -ne "Default Web Site") {
            throw "El sitio '$($otro.Name)' ya usa el puerto $Puerto. Vuelve a ejecutar con otro puerto, por ejemplo: -Puerto 8080"
        }
        $respuesta = Read-Host "La página de ejemplo de IIS (Default Web Site) usa el puerto $Puerto. ¿Pararla para poner la de GPE? (S/N)"
        if ($respuesta -notmatch "^[sS]") { throw "Vuelve a ejecutar con otro puerto, por ejemplo: -Puerto 8080" }
        Stop-Website "Default Web Site"
        Set-ItemProperty "IIS:\Sites\Default Web Site" serverAutoStart $false
    }
    New-Website -Name $sitio -Port $Puerto -PhysicalPath (Join-Path $Carpeta "Web") -ApplicationPool $grupo | Out-Null
    Write-Host "Creado en el puerto $Puerto."
}

# La web guarda la copia de los PDF del DeCA en su carpeta
icacls $carpetaDeca /grant "$($cuentaWeb):(OI)(CI)M" /Q | Out-Null

# Entrar desde otros ordenadores de la red
$regla = "GPE Web (puerto $Puerto)"
if (-not (Get-NetFirewallRule -DisplayName $regla -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $regla -Direction Inbound -Protocol TCP -LocalPort $Puerto `
        -Action Allow -Profile Domain, Private | Out-Null
}
Write-Host "Permisos y cortafuegos listos."

# ---------- 9. Tarea diaria de borrado ----------

Paso "9. Tarea diaria"
$accion = New-ScheduledTaskAction -Execute (Join-Path $Carpeta "Avisos\GPE.Avisos.exe") -Argument "--solo-deca" `
    -WorkingDirectory (Join-Path $Carpeta "Avisos")
$cuando = New-ScheduledTaskTrigger -Daily -At "03:00"
$quien = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$opciones = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)
Register-ScheduledTask -TaskName $tarea -Action $accion -Trigger $cuando -Principal $quien -Settings $opciones -Force | Out-Null
Write-Host "Tarea '$tarea': cada día a las 3:00 borra los PDF del DeCA de más de un año."

# ---------- 10. Arrancar y comprobar ----------
# iisreset para que la web lea las variables nuevas

Paso "10. Arrancar"
iisreset /restart | Out-Null
Start-WebAppPool $grupo -ErrorAction SilentlyContinue

$direccion = if ($Puerto -eq 80) { "http://$env:COMPUTERNAME/" } else { "http://$($env:COMPUTERNAME):$Puerto/" }
try {
    $respuesta = Invoke-WebRequest "http://localhost:$Puerto/" -UseBasicParsing -TimeoutSec 60
    Write-Host "La web responde ($($respuesta.StatusCode))." -ForegroundColor Green
} catch {
    Aviso "La web no responde todavía: $($_.Exception.Message)"
    Aviso "Mira el apartado 'SI ALGO FALLA' de LEEME.txt."
}

Write-Host ""
Write-Host "Listo." -ForegroundColor Green
Write-Host "  Web:                 $direccion"
Write-Host "  Aplicación:          \\$env:COMPUTERNAME\$recurso\setup.exe"
Write-Host "  Registro de la tarea: C:\Windows\System32\config\systemprofile\AppData\Local\GPE\avisos.log"
