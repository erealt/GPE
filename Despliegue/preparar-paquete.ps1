# Prepara en Despliegue\paquete todo lo que hay que llevar al servidor para la web del DeCA:
# la web publicada, GPE.Avisos publicado (para el borrado diario), los scripts de la base de datos,
# el script de instalación y la guía. Se ejecuta en el ordenador de desarrollo, sin permisos de administrador:
#   powershell -ExecutionPolicy Bypass -File Despliegue\preparar-paquete.ps1

$ErrorActionPreference = "Stop"
$raiz = Split-Path $PSScriptRoot -Parent
$paquete = Join-Path $PSScriptRoot "paquete"

if (Test-Path $paquete) { Remove-Item $paquete -Recurse -Force }
New-Item -ItemType Directory $paquete | Out-Null

Write-Host "Publicando la web..."
dotnet publish (Join-Path $raiz "GPE.Web") -c Release -o (Join-Path $paquete "Web") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar GPE.Web." }

Write-Host "Publicando GPE.Avisos..."
dotnet publish (Join-Path $raiz "GPE.Avisos") -c Release -o (Join-Path $paquete "Avisos") --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar GPE.Avisos." }

Copy-Item (Join-Path $raiz "BaseDatos") (Join-Path $paquete "BaseDatos") -Recurse
Copy-Item (Join-Path $PSScriptRoot "instalar-servidor.ps1") $paquete
Copy-Item (Join-Path $PSScriptRoot "LEEME.txt") $paquete

Write-Host ""
Write-Host "Paquete listo en $paquete" -ForegroundColor Green
Write-Host "Copia esa carpeta entera al servidor y sigue LEEME.txt."
