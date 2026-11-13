# Instala (o actualiza) la web del DeCA en un servidor Windows con IIS, y la tarea diaria que
# borra los PDF de más de un año. Se ejecuta en el servidor, desde la carpeta del paquete,
# en PowerShell como administrador:
#   powershell -ExecutionPolicy Bypass -File .\instalar-servidor.ps1
# Se puede volver a ejecutar: lo que ya está hecho no se repite y la web se actualiza.
#
# Opcionales:
#   -Conexion "Server=...;Database=GPE;User Id=...;Password=...;TrustServerCertificate=True;"
#       solo si la base de datos no está en este servidor (por defecto, GPE en este servidor)
#   -UsuarioWeb 1      id del usuario a cuyo nombre se guardan los DeCA hechos desde la web
#   -Puerto 80         puerto de la web
#   -Carpeta C:\GPE    dónde se instala todo

param(
    [string]$Conexion = "",
    [int]$UsuarioWeb = 1,
    [int]$Puerto = 80,
    [string]$Carpeta = "C:\GPE"
)

$ErrorActionPreference = "Stop"
$sitio = "GPE DeCA"
$grupo = "GPE"                         # grupo de aplicaciones de IIS
$cuentaWeb = "IIS APPPOOL\$grupo"      # cuenta con la que se ejecuta la web
$tarea = "GPE - Borrado DeCA"

function Paso($texto) { Write-Host ""; Write-Host "== $texto" -ForegroundColor Cyan }
function Aviso($texto) { Write-Host $texto -ForegroundColor Yellow }

# ---------- 0. Comprobaciones ----------

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw "Hay que abrir PowerShell como administrador (clic derecho, Ejecutar como administrador)." }

foreach ($necesaria in "Web", "Avisos") {
    if (-not (Test-Path (Join-Path $PSScriptRoot $necesaria))) {
        throw "No encuentro la carpeta '$necesaria' junto a este script. Ejecútalo desde la carpeta del paquete."
    }
}

# ---------- 1. IIS ----------

Paso "1. IIS"
# Con su consola y sus comandos de PowerShell (los usa este script). Si ya está, no hace nada.
Write-Host "Comprobando IIS (si no está, tarda unos minutos)..."
if (Get-Command Install-WindowsFeature -ErrorAction SilentlyContinue) {
    Install-WindowsFeature Web-Server, Web-Mgmt-Console, Web-Scripting-Tools | Out-Null       # Windows Server
} else {
    Enable-WindowsOptionalFeature -Online -All -NoRestart -FeatureName IIS-WebServerRole, IIS-WebServer, `
        IIS-ManagementConsole, IIS-ManagementScriptingTools | Out-Null                         # Windows 10/11
}
Write-Host "Instalado."

# ---------- 2. .NET 10 (Hosting Bundle) ----------
# Tiene que instalarse después de IIS, si no IIS no sabe arrancar la web

Paso "2. .NET 10 (Hosting Bundle)"
$modulo = Join-Path $env:ProgramFiles "IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
$runtime = (& { try { dotnet --list-runtimes } catch { "" } }) -match "Microsoft.AspNetCore.App 10\."
if (-not (Test-Path $modulo) -or -not $runtime) {
    Aviso "Falta el Hosting Bundle de .NET 10."
    Aviso "Instala 'dotnet-hosting-10...-win.exe' (está en la guía LEEME.txt) y vuelve a ejecutar este script."
    exit 1
}
Write-Host "Instalado."

# ---------- 3. Copiar la web y GPE.Avisos ----------

Paso "3. Copiar archivos a $Carpeta"
Import-Module WebAdministration

# Si la web ya estaba, se para mientras se copian los archivos (si no, están en uso)
if ((Test-Path "IIS:\AppPools\$grupo") -and (Get-WebAppPoolState $grupo).Value -eq "Started") {
    Stop-WebAppPool $grupo
    Start-Sleep -Seconds 3
}

foreach ($parte in "Web", "Avisos") {
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
Write-Host "Copiado. Copias de los PDF en $carpetaDeca. Usuario de la web: $UsuarioWeb."

# ---------- 4. Variables para todo el servidor ----------
# Las leen la web y la tarea diaria. La contraseña se escribe aquí, no queda en ningún archivo del paquete.

Paso "4. Variables"
function Poner($nombre, $valor) { [Environment]::SetEnvironmentVariable($nombre, $valor, "Machine") }
function Actual($nombre) { [Environment]::GetEnvironmentVariable($nombre, "Machine") }

Poner "GPE_DECA_CARPETA" $carpetaDeca
if ($Conexion) { Poner "GPE_CONEXION" $Conexion; Write-Host "GPE_CONEXION puesta." }
elseif (Actual "GPE_CONEXION") { Write-Host "GPE_CONEXION se queda como estaba." }
else { Write-Host "Base de datos: GPE en este servidor." }

$usuarioWp = Actual "GPE_WP_USUARIO"
$texto = if ($usuarioWp) { "Usuario de WordPress (Intro = $usuarioWp)" } else { "Usuario de WordPress" }
$nuevo = Read-Host $texto
if ($nuevo) { $usuarioWp = $nuevo.Trim() }
if (-not $usuarioWp) { throw "Hace falta el usuario de WordPress para subir los PDF." }
Poner "GPE_WP_USUARIO" $usuarioWp

$claveGuardada = [bool](Actual "GPE_WP_CLAVE")
$texto = if ($claveGuardada) { "Contraseña de aplicación de WordPress (Intro = dejar la que hay)" } else { "Contraseña de aplicación de WordPress" }
$segura = Read-Host $texto -AsSecureString
$clave = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura))
if ($clave) { Poner "GPE_WP_CLAVE" $clave.Trim() }
elseif (-not $claveGuardada) { throw "Hace falta la contraseña de aplicación de WordPress para subir los PDF." }
Write-Host "Variables puestas."

# ---------- 5. Sitio en IIS ----------

Paso "5. Sitio en IIS"
if (-not (Test-Path "IIS:\AppPools\$grupo")) { New-WebAppPool $grupo | Out-Null }
Set-ItemProperty "IIS:\AppPools\$grupo" managedRuntimeVersion ""   # .NET 10 no usa el .NET antiguo de IIS

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
        $respuesta = Read-Host "La página de ejemplo de IIS (Default Web Site) usa el puerto $Puerto. ¿Pararla para poner la del DeCA? (S/N)"
        if ($respuesta -notmatch "^[sS]") { throw "Vuelve a ejecutar con otro puerto, por ejemplo: -Puerto 8080" }
        Stop-Website "Default Web Site"
        Set-ItemProperty "IIS:\Sites\Default Web Site" serverAutoStart $false
    }
    New-Website -Name $sitio -Port $Puerto -PhysicalPath (Join-Path $Carpeta "Web") -ApplicationPool $grupo | Out-Null
    Write-Host "Creado en el puerto $Puerto."
}

# La web guarda la copia de los PDF en la carpeta del DeCA
icacls $carpetaDeca /grant "$($cuentaWeb):(OI)(CI)M" /Q | Out-Null

# Entrar desde otros ordenadores de la red
$regla = "GPE DeCA (web, puerto $Puerto)"
if (-not (Get-NetFirewallRule -DisplayName $regla -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $regla -Direction Inbound -Protocol TCP -LocalPort $Puerto `
        -Action Allow -Profile Domain, Private | Out-Null
}
Write-Host "Permisos y cortafuegos listos."

# ---------- 6. Permiso en la base de datos ----------
# Solo si la base de datos está en este servidor y se entra con la cuenta de Windows (sin -Conexion)

Paso "6. Base de datos"
$sql = @"
IF SUSER_ID(N'$cuentaWeb') IS NULL CREATE LOGIN [$cuentaWeb] FROM WINDOWS;
USE GPE;
IF USER_ID(N'$cuentaWeb') IS NULL CREATE USER [$cuentaWeb] FOR LOGIN [$cuentaWeb];
ALTER ROLE db_datareader ADD MEMBER [$cuentaWeb];
ALTER ROLE db_datawriter ADD MEMBER [$cuentaWeb];
"@
if ($Conexion -or (Actual "GPE_CONEXION")) {
    Write-Host "La conexión lleva su propio usuario de SQL: no hace falta dar permiso a la web."
} elseif (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
    sqlcmd -S localhost -E -C -b -Q $sql | Out-Null
    if ($LASTEXITCODE -eq 0) { Write-Host "Permiso dado a $cuentaWeb en la base GPE." }
    else { Aviso "No se pudo dar el permiso. Ejecuta esto en SQL Server Management Studio:"; Write-Host $sql }
} else {
    Aviso "No está sqlcmd. Ejecuta esto en SQL Server Management Studio:"
    Write-Host $sql
}

# ---------- 7. Tarea diaria de borrado ----------

Paso "7. Tarea diaria"
$accion = New-ScheduledTaskAction -Execute (Join-Path $Carpeta "Avisos\GPE.Avisos.exe") -Argument "--solo-deca" `
    -WorkingDirectory (Join-Path $Carpeta "Avisos")
$cuando = New-ScheduledTaskTrigger -Daily -At "03:00"
$quien = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$opciones = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)
Register-ScheduledTask -TaskName $tarea -Action $accion -Trigger $cuando -Principal $quien -Settings $opciones -Force | Out-Null
Write-Host "Tarea '$tarea': cada día a las 3:00 borra los PDF del DeCA de más de un año."

# ---------- 8. Arrancar y comprobar ----------
# iisreset para que la web lea las variables nuevas

Paso "8. Arrancar"
iisreset /restart | Out-Null
Start-WebAppPool $grupo -ErrorAction SilentlyContinue

$direccion = if ($Puerto -eq 80) { "http://$env:COMPUTERNAME/" } else { "http://$($env:COMPUTERNAME):$Puerto/" }
try {
    $respuesta = Invoke-WebRequest "http://localhost:$Puerto/" -UseBasicParsing -TimeoutSec 60
    Write-Host "La web responde ($($respuesta.StatusCode))." -ForegroundColor Green
} catch {
    Aviso "La web no responde todavía: $($_.Exception.Message)"
    Aviso "Mira el apartado 'Si algo falla' de LEEME.txt."
}

Write-Host ""
Write-Host "Listo. Desde los ordenadores de la fábrica: $direccion" -ForegroundColor Green
Write-Host "Registro de la tarea diaria: C:\Windows\System32\config\systemprofile\AppData\Local\GPE\avisos.log"
