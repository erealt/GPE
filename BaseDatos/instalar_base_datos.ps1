<#
    Crea la base de datos GPE en un SQL Server solo con lo que usa la web del DeCA, y el primer usuario.
    Solo para una instalación nueva: si la base de datos ya tiene tablas, no toca nada.
    Quien lo ejecuta tiene que poder crear bases de datos en ese SQL Server. Pasos en INSTALACION_WEB.md.
#>
param(
    [Parameter(Mandatory)] [string] $Servidor,   # SERVIDOR o SERVIDOR\INSTANCIA
    [Parameter(Mandatory)] [string] $Nombre,     # primer usuario: a su nombre quedan los DeCA de la web
    [Parameter(Mandatory)] [string] $Correo
)

$ErrorActionPreference = 'Stop'
$base = 'GPE'   # deca_web.sql lleva USE GPE

if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'No se encuentra sqlcmd. Hay que instalarlo o ejecutar este script desde un equipo que lo tenga.'
}

function Conectar($baseDatos) {
    $conexion = New-Object System.Data.SqlClient.SqlConnection(
        "Server=$Servidor;Database=$baseDatos;Integrated Security=True;TrustServerCertificate=True;")
    $conexion.Open()
    return $conexion
}

function Consultar($conexion, $sql, $parametros = @{}) {
    $comando = $conexion.CreateCommand()
    $comando.CommandText = $sql
    foreach ($clave in $parametros.Keys) { [void]$comando.Parameters.AddWithValue($clave, $parametros[$clave]) }
    return $comando.ExecuteScalar()
}

$conexion = Conectar 'master'
try {
    if ((Consultar $conexion "SELECT DB_ID('$base')") -is [DBNull]) {
        Write-Host "Creando la base de datos $base..."
        [void](Consultar $conexion "CREATE DATABASE [$base]")
    }
    elseif ((Consultar $conexion "SELECT COUNT(*) FROM [$base].sys.tables") -gt 0) {
        throw "La base de datos $base ya tiene tablas en $Servidor. No se ha cambiado nada."
    }
}
finally { $conexion.Close() }

Write-Host "Creando las tablas del DeCA..."
# -I: lo necesita la columna calculada de DECA. -f 65001: por las tildes. -C: acepta el certificado del servidor.
$salida = sqlcmd -S $Servidor -E -C -b -I -f 65001 -i (Join-Path $PSScriptRoot 'deca_web.sql')
if ($LASTEXITCODE -ne 0) {
    $salida | ForEach-Object { Write-Host $_ }
    throw 'Ha fallado deca_web.sql. La instalación se ha parado en ese punto.'
}

Write-Host "Creando el primer usuario..."
$conexion = Conectar $base
try {
    $usuario = Consultar $conexion @"
INSERT INTO USUARIO (nombre, email) OUTPUT INSERTED.id VALUES (@nombre, @correo);
"@ @{ '@nombre' = $Nombre; '@correo' = $Correo }
}
finally { $conexion.Close() }

Write-Host ""
Write-Host "Instalación terminada. Primer usuario: $Nombre (id $usuario)."
