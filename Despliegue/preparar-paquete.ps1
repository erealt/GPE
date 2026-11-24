# Prepara en Despliegue\paquete todo lo que hay que llevar al servidor:
#   Web\         la web (acciones, control diario, agua, DeCA)
#   Avisos\      GPE.Avisos (borrado diario de los PDF del DeCA)
#   Escritorio\  la aplicación de escritorio (ClickOnce), solo si no se usa -Instalacion
#   BaseDatos\   GPE.bak (copia de la base local) y los scripts
#   gpe_app.txt  contraseña del usuario de SQL que usa el escritorio (el instalador la usa y la borra)
# Se ejecuta en el ordenador de desarrollo, en la rama main, sin permisos de administrador:
#   powershell -ExecutionPolicy Bypass -File Despliegue\preparar-paquete.ps1 -Servidor servereurochoc -Instalacion \\servereurochoc\DATOS\GPE
#
# Opcionales:
#   -Instalacion \\servereurochoc\DATOS\GPE   carpeta compartida que ya existe desde la que los
#        ordenadores instalan la aplicación. La aplicación se copia ahí directamente (no va en el
#        paquete). Sin esto, va en el paquete y el instalador crea \\SERVIDOR\GPE.
#   -SoloEscritorio      solo publica la aplicación en -Instalacion (para actualizarla sin ir al servidor)
#   -SinEscritorio       solo la web y la tarea, para actualizarlas en el servidor sin tocar la base de datos:
#                        no publica una versión nueva de la aplicación (McAfee la volvería a bloquear),
#                        no hace la copia de la base ni pone gpe_app.txt. Basta con -Servidor.
#   -BaseLocal miGPE     base de datos local que se copia al servidor
#   -CopiarEn            carpeta donde se copian los archivos, si desde este ordenador no se entra
#                        por la ruta de -Instalacion (p. ej. -Instalacion por IP y -CopiarEn por nombre)
#   -Incidencias "..\INCIDENCIAS Y NO CONFORMIDADES - CALIDAD.xlsx"   lee el Excel (aquí, que hay Excel)
#                        y lo deja en BaseDatos\incidencias.xml para cargarlo en el servidor con
#                        importar_no_conformidades.ps1 -Datos

param(
    [Parameter(Mandatory)] [string]$Servidor,
    [string]$Instalacion = "",
    [switch]$SoloEscritorio,
    [switch]$SinEscritorio,
    [string]$BaseLocal = "miGPE",
    [string]$CopiarEn = "",
    [string]$Incidencias = ""
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$paquete = Join-Path $PSScriptRoot "paquete"
$proyectoEscritorio = Join-Path $raiz "GPE.Escritorio"
$archivoConexion = Join-Path $proyectoEscritorio "conexion.txt"
$Servidor = $Servidor.Trim().TrimStart('\')
$Instalacion = $Instalacion.Trim().TrimEnd('\')

function Paso($texto) { Write-Host ""; Write-Host "== $texto" -ForegroundColor Cyan }

if ($SoloEscritorio -and $SinEscritorio) { throw "-SoloEscritorio y -SinEscritorio no van juntos." }
if ($SoloEscritorio -and -not $Instalacion) { throw "-SoloEscritorio necesita -Instalacion (la carpeta compartida de la aplicación)." }
if ($Instalacion -and -not $Instalacion.StartsWith("\\")) {
    throw "-Instalacion tiene que ser la ruta de red (\\servidor\carpeta), no una letra de unidad: la letra puede ser otra en cada ordenador."
}
$urlInstalacion = if ($Instalacion) { $Instalacion } else { "\\$Servidor\GPE" }

$rama = git -C $raiz branch --show-current
if ($rama -ne "main") {
    Write-Host "Estás en la rama '$rama', no en 'main'. Se publica lo que haya en esta rama." -ForegroundColor Yellow
    if ((Read-Host "¿Seguir? (S/N)") -notmatch "^[sS]") { exit 1 }
}

# La contraseña de gpe_app se crea una vez y se guarda en este ordenador (fuera del proyecto),
# para que las versiones nuevas de la aplicación sigan entrando sin volver a instalar en el servidor
$carpetaLocal = Join-Path $env:LOCALAPPDATA "GPE"
$archivoClave = Join-Path $carpetaLocal "gpe_app.txt"
if (-not (Test-Path $archivoClave)) {
    New-Item -ItemType Directory $carpetaLocal -Force | Out-Null
    $letras = [char[]]"ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"   # sin ; ni comillas
    Set-Content $archivoClave (-join (1..24 | ForEach-Object { $letras | Get-Random })) -NoNewline
}
$clave = (Get-Content $archivoClave -Raw).Trim()

if (-not $SoloEscritorio) {
    if (Test-Path $paquete) { Remove-Item $paquete -Recurse -Force }
    New-Item -ItemType Directory $paquete | Out-Null

    # ---------- 1. Web y GPE.Avisos ----------

    Paso "1. Web y GPE.Avisos"
    dotnet publish (Join-Path $raiz "GPE.Web") -c Release -o (Join-Path $paquete "Web") --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar GPE.Web." }
    dotnet publish (Join-Path $raiz "GPE.Avisos") -c Release -o (Join-Path $paquete "Avisos") --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar GPE.Avisos." }
    Write-Host "Publicadas."

    # ---------- 2. Copia de la base de datos ----------

    Copy-Item (Join-Path $raiz "BaseDatos") (Join-Path $paquete "BaseDatos") -Recurse

    if ($Incidencias) {
        Paso "Incidencias del Excel"
        & (Join-Path $raiz "BaseDatos\importar_no_conformidades.ps1") -Excel $Incidencias `
            -GuardarDatos (Join-Path $paquete "BaseDatos\incidencias.xml")
    }
}

if (-not $SoloEscritorio -and -not $SinEscritorio) {
    Paso "2. Copia de la base de datos $BaseLocal"

    function Sql($texto) {
        $conexion = New-Object System.Data.SqlClient.SqlConnection(
            "Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True;")
        $conexion.Open()
        try {
            $comando = $conexion.CreateCommand()
            $comando.CommandTimeout = 0
            $comando.CommandText = $texto
            return $comando.ExecuteScalar()
        } finally { $conexion.Close() }
    }

    # SQL Server escribe la copia con su propia cuenta: se le deja una carpeta en la que pueda escribir
    $carpetaCopia = "C:\GPE-Copia"
    New-Item -ItemType Directory $carpetaCopia -Force | Out-Null
    $cuentaSql = Sql "SELECT TOP 1 service_account FROM sys.dm_server_services WHERE servicename LIKE 'SQL Server (%'"
    icacls $carpetaCopia /grant "$($cuentaSql):(OI)(CI)M" /Q | Out-Null
    $copia = Join-Path $carpetaCopia "GPE.bak"
    if (Test-Path $copia) { Remove-Item $copia -Force }

    Sql "BACKUP DATABASE [$BaseLocal] TO DISK = N'$copia' WITH COPY_ONLY, INIT, FORMAT, COMPRESSION, CHECKSUM" | Out-Null
    Move-Item $copia (Join-Path $paquete "BaseDatos\GPE.bak")
    Remove-Item $carpetaCopia -Recurse -Force
    $version = Sql "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)"
    $nombreVersion = @{ 15 = "2019"; 16 = "2022"; 17 = "2025" }[[int]$version]
    Write-Host "Copia hecha (BaseDatos\GPE.bak). Para restaurarla, el servidor necesita SQL Server $nombreVersion o más nuevo."

    # Además, la base en formato .bacpac (estructura y datos): vale para un SQL Server más antiguo
    # que el de este ordenador, que no puede abrir el .bak. La importa SqlPackage, que va en el paquete.
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    $vs = & $vswhere -latest -prerelease -property installationPath | Select-Object -First 1
    $carpetaDac = Join-Path $vs "Common7\IDE\Extensions\Microsoft\SQLDB\DAC"
    if (-not (Test-Path (Join-Path $carpetaDac "SqlPackage.exe"))) { throw "No encuentro SqlPackage en $carpetaDac (viene con Visual Studio)." }
    Write-Host "Exportando $BaseLocal a GPE.bacpac..."
    # El nivel de compatibilidad viaja en el .bacpac: el de SQL Server 2025 (170) no lo aceptan versiones
    # anteriores. Mientras se exporta se pone 130 (SQL Server 2016 o más nuevo; la aplicación usa
    # STRING_SPLIT, que lo necesita) y después se deja como estaba.
    $compatibilidad = Sql "SELECT compatibility_level FROM sys.databases WHERE name = N'$BaseLocal'"
    Sql "ALTER DATABASE [$BaseLocal] SET COMPATIBILITY_LEVEL = 130" | Out-Null
    try {
        & (Join-Path $carpetaDac "SqlPackage.exe") /Action:Export /SourceServerName:localhost /SourceDatabaseName:$BaseLocal `
            /SourceTrustServerCertificate:True "/TargetFile:$(Join-Path $paquete 'BaseDatos\GPE.bacpac')" /Quiet:True
        if ($LASTEXITCODE -ne 0) { throw "No se pudo exportar $BaseLocal a GPE.bacpac." }
    } finally {
        Sql "ALTER DATABASE [$BaseLocal] SET COMPATIBILITY_LEVEL = $compatibilidad" | Out-Null
    }
    Copy-Item $carpetaDac (Join-Path $paquete "SqlPackage") -Recurse
    Write-Host "Copia hecha también en BaseDatos\GPE.bacpac (para cualquier versión de SQL Server)."

    # El instalador crea el usuario gpe_app con esta contraseña
    Set-Content (Join-Path $paquete "gpe_app.txt") $clave -NoNewline
}

# ---------- 3. Aplicación de escritorio (ClickOnce) ----------

if ($SinEscritorio) {
    Paso "3. Aplicación de escritorio: no se publica (-SinEscritorio)"
} else {
Paso "3. Aplicación de escritorio"
# Certificado con el que se firma ClickOnce. Tiene que ser siempre el mismo: si cambia,
# los ordenadores no se actualizan y hay que desinstalar e instalar otra vez.
$certificado = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert |
    Where-Object { $_.Subject -eq "CN=GPE Eurochoc" -and $_.NotAfter -gt (Get-Date).AddDays(30) } |
    Sort-Object NotAfter -Descending | Select-Object -First 1
if (-not $certificado) {
    $certificado = New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=GPE Eurochoc" `
        -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(10)
    Write-Host "Certificado nuevo 'GPE Eurochoc', válido hasta $($certificado.NotAfter.ToString('dd/MM/yyyy'))."
}

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "No encuentro el MSBuild de Visual Studio (hace falta para ClickOnce)." }

# Cada publicación lleva un número mayor que la anterior para que los ordenadores se actualicen
$ahora = Get-Date
$versionApp = "1.{0}.{1}.{2}" -f $ahora.ToString("yy"), [int]$ahora.ToString("MMdd"), [int]$ahora.ToString("HHmm")
$temporal = Join-Path $env:TEMP "GPE-ClickOnce\"
if (Test-Path $temporal) { Remove-Item $temporal -Recurse -Force }

Set-Content $archivoConexion "Server=$Servidor,1433;Database=GPE;User Id=gpe_app;Password=$clave;TrustServerCertificate=True;" -NoNewline
try {
    & $msbuild (Join-Path $proyectoEscritorio "GPE.Escritorio.csproj") -restore -t:Publish -nologo -v:m `
        -p:PublishProfile=ClickOnceProfile -p:Configuration=Release `
        "-p:PublishDir=$temporal" "-p:InstallUrl=$urlInstalacion\" `
        "-p:ApplicationVersion=$versionApp" "-p:ManifestCertificateThumbprint=$($certificado.Thumbprint)"
    if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar la aplicación de escritorio." }

    # Solo va lo de ClickOnce: el instalador, Application Files y el manifiesto. El manifiesto
    # se copia el último: mientras no cambia, los ordenadores siguen con la versión anterior.
    $destino = if ($CopiarEn) { $CopiarEn.Trim().TrimEnd('\') } elseif ($Instalacion) { $Instalacion } else { Join-Path $paquete "Escritorio" }
    New-Item -ItemType Directory $destino -Force | Out-Null
    foreach ($parte in "Application Files", "setup.exe", "GPE.Escritorio.application") {
        Copy-Item (Join-Path $temporal $parte) $destino -Recurse -Force
    }
} finally {
    # La conexión del servidor no se queda en el proyecto: al depurar se sigue usando la base local
    Remove-Item $archivoConexion -Force -ErrorAction SilentlyContinue
    Remove-Item $temporal -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $proyectoEscritorio "bin") -Recurse -Filter conexion.txt -ErrorAction SilentlyContinue |
        Remove-Item -Force
}
Write-Host "Publicada la versión $versionApp en $destino. Se instala desde $urlInstalacion\setup.exe"
}

if ($SoloEscritorio) {
    Write-Host ""
    Write-Host "Listo. Los ordenadores se actualizan al abrir GPE." -ForegroundColor Green
    exit 0
}

# ---------- 4. Instalador y guía ----------

Copy-Item (Join-Path $PSScriptRoot "instalar-servidor.ps1") $paquete
Copy-Item (Join-Path $PSScriptRoot "LEEME.txt") $paquete

Write-Host ""
Write-Host "Paquete listo en $paquete" -ForegroundColor Green
Write-Host "Copia esa carpeta entera al servidor y sigue LEEME.txt."
if (-not $SinEscritorio) {
    Write-Host "Ojo: gpe_app.txt lleva una contraseña. No subas la carpeta a ningún sitio compartido con más gente." -ForegroundColor Yellow
}
