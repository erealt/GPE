# Cambia la contraseña de un usuario de GPE (escritorio y web). Pide la nueva dos veces sin mostrarla
# y la guarda cifrada igual que la aplicación (GPE.Logica/Clave.cs).
#
#   En la base local (miGPE):
#     powershell -ExecutionPolicy Bypass -File BaseDatos\cambiar_clave.ps1 -Usuario alba
#   En el servidor (con gpe_app, la contraseña de %LOCALAPPDATA%\GPE\gpe_app.txt):
#     powershell -ExecutionPolicy Bypass -File BaseDatos\cambiar_clave.ps1 -Usuario alba -Servidor 192.168.2.249
#
# Ojo: volver a ejecutar usuarios.sql pone otra vez las contraseñas que lleva escritas.
param(
    [Parameter(Mandatory)] [string] $Usuario,
    [string] $Servidor
)
$ErrorActionPreference = 'Stop'

if ($Servidor) {
    $archivoClave = Join-Path $env:LOCALAPPDATA 'GPE\gpe_app.txt'
    if (-not (Test-Path $archivoClave)) { throw "No está $archivoClave (la contraseña de gpe_app)." }
    $conexion = "Server=$Servidor,1433;Database=GPE;User Id=gpe_app;Password=$((Get-Content $archivoClave -Raw).Trim());TrustServerCertificate=True;"
    $donde = "el servidor $Servidor"
} else {
    $conexion = if ($env:GPE_CONEXION) { $env:GPE_CONEXION }
                else { 'Server=localhost;Database=miGPE;Integrated Security=True;TrustServerCertificate=True;' }
    $donde = 'la base local'
}

function Leer-Clave($texto) {
    $segura = Read-Host $texto -AsSecureString
    $puntero = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($segura)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($puntero) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($puntero) }
}

# "PBKDF2-SHA256$100000$sal$resultado", como Clave.Cifrar
function Cifrar-Clave([string] $clave, [byte[]] $sal = $null) {
    if (-not $sal) {
        $sal = New-Object byte[] 16
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($sal)
    }
    $pbkdf2 = New-Object Security.Cryptography.Rfc2898DeriveBytes(
        [Text.Encoding]::UTF8.GetBytes($clave), $sal, 100000, [Security.Cryptography.HashAlgorithmName]::SHA256)
    try { $resultado = $pbkdf2.GetBytes(32) } finally { $pbkdf2.Dispose() }
    'PBKDF2-SHA256$100000$' + [Convert]::ToBase64String($sal) + '$' + [Convert]::ToBase64String($resultado)
}

$cn = New-Object System.Data.SqlClient.SqlConnection $conexion
$cn.Open()
try {
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = 'SELECT nombre FROM USUARIO WHERE usuario = @usuario'
    [void]$cmd.Parameters.AddWithValue('@usuario', $Usuario)
    $nombre = $cmd.ExecuteScalar()
    if ($null -eq $nombre) { throw "No hay ningún usuario '$Usuario' en $donde." }

    Write-Host "Usuario '$Usuario' ($nombre) en $donde."
    $clave = Leer-Clave 'Contraseña nueva'
    if ($clave.Length -eq 0) { throw 'La contraseña está vacía. No se ha cambiado nada.' }
    if ((Leer-Clave 'Repítela') -cne $clave) { throw 'No coinciden. No se ha cambiado nada.' }

    $cmd = $cn.CreateCommand()
    $cmd.CommandText = "EXEC sp_set_session_context @key = N'usuario_id', @value = 1;
                        UPDATE USUARIO SET clave = @clave WHERE usuario = @usuario;"
    [void]$cmd.Parameters.AddWithValue('@clave', (Cifrar-Clave $clave))
    [void]$cmd.Parameters.AddWithValue('@usuario', $Usuario)
    [void]$cmd.ExecuteNonQuery()
    Write-Host "Contraseña de '$Usuario' cambiada en $donde." -ForegroundColor Green
} finally {
    $cn.Close()
}
