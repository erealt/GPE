<#
    Crea la base de datos GPE desde cero en un SQL Server: tablas, datos iniciales y el primer usuario.
    Solo para una instalación nueva: si la base de datos ya tiene tablas, no toca nada.
    Quien lo ejecuta tiene que poder crear bases de datos en ese SQL Server. Pasos en INSTALACION_WEB.md.
#>
param(
    [Parameter(Mandatory)] [string] $Servidor,   # SERVIDOR o SERVIDOR\INSTANCIA
    [Parameter(Mandatory)] [string] $Nombre,     # primer usuario: recibe los cuatro roles
    [Parameter(Mandatory)] [string] $Correo
)

$ErrorActionPreference = 'Stop'
$base = 'GPE'   # los scripts llevan USE GPE

# Las bases nuevas ya nacen con las columnas de rutina_revisiones, deca_normativa y agua_varias_por_dia.
# secciones_calidad y subseccion_calidad se ejecutan solo si están en esta rama.
$scripts = 'crear_tablas', 'no_conformidades', 'rutina_calidad', 'control_agua',
           'secciones_calidad', 'subseccion_calidad'

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

foreach ($nombreScript in $scripts) {
    $ruta = Join-Path $PSScriptRoot "$nombreScript.sql"
    if (-not (Test-Path $ruta)) {
        Write-Host "$nombreScript.sql no está en esta rama: se salta."
        continue
    }
    Write-Host "Ejecutando $nombreScript.sql..."
    # -I: los índices filtrados lo necesitan. -f 65001: por las tildes. -C: acepta el certificado del servidor.
    $salida = sqlcmd -S $Servidor -E -C -b -I -f 65001 -i $ruta
    if ($LASTEXITCODE -ne 0) {
        $salida | ForEach-Object { Write-Host $_ }
        throw "Ha fallado $nombreScript.sql. La instalación se ha parado en ese punto."
    }
}

Write-Host "Creando el primer usuario..."
$conexion = Conectar $base
try {
    $usuario = Consultar $conexion @"
INSERT INTO USUARIO (nombre, email) OUTPUT INSERTED.id VALUES (@nombre, @correo);
"@ @{ '@nombre' = $Nombre; '@correo' = $Correo }

    [void](Consultar $conexion @"
INSERT INTO USUARIO_ROL (usuario_id, rol_id, fabrica_id)
SELECT @usuario, r.id, f.id FROM ROL r CROSS JOIN FABRICA f;
"@ @{ '@usuario' = $usuario })
}
finally { $conexion.Close() }

Write-Host ""
Write-Host "Instalación terminada. Primer usuario: $Nombre (id $usuario), con los cuatro roles."
