<#
    Pasa la hoja AGUA del Excel "INCIDENCIAS Y NO CONFORMIDADES - CALIDAD" (MT-RG09) a AGUA_CONTROL.
    Se ejecuta una vez, después de control_agua.sql y agua_varias_por_dia.sql.

    Decisiones de calidad (12/11/2026):
    - Las filas 154 a 262 pone 2026 pero son de 2025 (van tras el 08/08/2025): se pasan con 2025.
    - Las filas sin cloro se pasan con el cloro vacío.
    - "LAVAMANOS LROCAS" es LAVABO LROCAS; "LAVAMANOS LINEA 3" y "LAVAMANOS L3" son un punto nuevo.
    - Las filas del mismo día y punto se pasan todas (puede haber varias tomas en un día).
#>
param(
    [Parameter(Mandatory)] [int] $UsuarioId,     # usuario que figura como creador (y en el historial)
    [string] $Excel,     # por defecto, el Excel que está al lado de la carpeta GPE
    [string] $Conexion = $(if ($env:GPE_CONEXION) { $env:GPE_CONEXION }
                           else { 'Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;' }),
    [switch] $Probar,      # deshace todo al terminar
    [switch] $Forzar       # importa aunque ya haya controles en las fechas de la hoja
)

$ErrorActionPreference = 'Stop'
$avisos = New-Object System.Collections.Generic.List[string]
function Aviso($fila, $texto) { $avisos.Add("Fila ${fila}: $texto") }

# Columnas de la hoja AGUA (fila 2 = cabeceras, datos desde la fila 3)
$COL = @{ Fecha = 1; Toma = 2; Cloro = 3; Sabor = 4; Olor = 5; Color = 6; Turbidez = 7 }
$PRIMERA_FILA = 3
$ULTIMA_COLUMNA = 7

# Filas con el año mal puesto (2026 en vez de 2025)
$FILAS_2025 = 154..262

# Nombres de la hoja que no coinciden con AGUA_TOMA
$NOMBRES = @{
    'LAVAMANOS LROCAS'  = 'LAVABO LROCAS'
    'LAVAMANOS LINEA 3' = 'LAVAMANOS LINEA 3'
    'LAVAMANOS L3'      = 'LAVAMANOS LINEA 3'
}
$TOMAS_NUEVAS = @('LAVAMANOS LINEA 3')   # se crean si no existen

$CLORO_MAXIMO = 9.99   # lo que cabe en AGUA_CONTROL.cloro

# Conversión de celdas

function Texto($v) {
    if ($null -eq $v) { return $null }
    $t = ([string]$v).Trim()
    if ($t -eq '') { return $null }
    return $t
}

function Fecha($v, $fila) {
    if ($v -is [double]) { return [DateTime]::FromOADate($v).Date }
    $t = Texto $v
    $f = [DateTime]::MinValue
    if ($t -and [DateTime]::TryParseExact($t, [string[]]@('dd/MM/yyyy', 'd/M/yyyy', 'dd/MM/yy', 'd/M/yy'),
            [Globalization.CultureInfo]'es-ES', [Globalization.DateTimeStyles]::None, [ref]$f)) { return $f }
    return $null
}

function Cloro($v, $fila) {
    if ($null -eq $v -or ([string]$v).Trim() -eq '') { return $null }
    $n = 0.0
    if ($v -is [double]) { $n = $v }
    elseif (-not [double]::TryParse(([string]$v).Trim().Replace(',', '.'), [Globalization.NumberStyles]::Float,
                [Globalization.CultureInfo]::InvariantCulture, [ref]$n)) {
        Aviso $fila "cloro '$v' no es un número; se deja vacío."
        return $null
    }
    if ($n -lt 0 -or $n -gt $CLORO_MAXIMO) { Aviso $fila "cloro $n fuera de 0-$CLORO_MAXIMO; se deja vacío."; return $null }
    return [decimal][math]::Round($n, 2)
}

# Ok = correcto. Cualquier otra cosa se guarda como no correcto y se avisa.
function EsOk($v, $fila, $columna) {
    $t = Texto $v
    if ($null -eq $t -or $t -eq 'Ok') { return $true }
    Aviso $fila "$columna '$t'; se guarda como no correcto."
    return $false
}

# Lectura del Excel

if (-not $Excel) { $Excel = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\..\INCIDENCIAS Y NO CONFORMIDADES - CALIDAD.xlsx' }
$Excel = (Resolve-Path $Excel).Path
Write-Host "Leyendo $Excel ..."
$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false
$xl.DisplayAlerts = $false
try {
    $libro = $xl.Workbooks.Open($Excel, 0, $true)   # solo lectura
    $hoja = $libro.Worksheets.Item('AGUA')
    $ultimaFila = $hoja.Cells.Item($hoja.Rows.Count, $COL.Toma).End(-4162).Row   # xlUp
    $celdas = $hoja.Range($hoja.Cells.Item($PRIMERA_FILA, 1), $hoja.Cells.Item($ultimaFila, $ULTIMA_COLUMNA)).Value2
    $libro.Close($false)
}
finally {
    $xl.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
}

$controles = New-Object System.Collections.Generic.List[object]
for ($i = 1; $i -le $celdas.GetLength(0); $i++) {
    $fila = $i + $PRIMERA_FILA - 1
    $toma = Texto $celdas[$i, $COL.Toma]
    $fecha = Fecha ($celdas[$i, $COL.Fecha]) $fila
    if (-not $toma -and -not $fecha) { continue }   # fila vacía
    if (-not $toma) { Aviso $fila 'sin punto de agua; no se importa.'; continue }
    if (-not $fecha) { Aviso $fila "fecha '$($celdas[$i, $COL.Fecha])' no válida; no se importa."; continue }

    if ($FILAS_2025 -contains $fila) {
        if ($fecha.Year -ne 2026) { throw "Fila ${fila}: se esperaba el año 2026 para pasarlo a 2025 y es $($fecha.Year). ¿Ha cambiado la hoja?" }
        $fecha = $fecha.AddYears(-1)
    }
    if ($fecha -gt [DateTime]::Today) { Aviso $fila "fecha $($fecha.ToString('dd/MM/yyyy')) todavía no ha llegado; no se importa."; continue }

    $nombre = $toma.ToUpperInvariant() -replace '\s+', ' '
    if ($NOMBRES.ContainsKey($nombre)) { $nombre = $NOMBRES[$nombre] }

    $controles.Add([pscustomobject]@{
        Fila = $fila
        Fecha = $fecha
        Toma = $nombre
        Cloro = Cloro ($celdas[$i, $COL.Cloro]) $fila
        SaborOk = EsOk ($celdas[$i, $COL.Sabor]) $fila 'Sabor'
        OlorOk = EsOk ($celdas[$i, $COL.Olor]) $fila 'Olor'
        ColorOk = EsOk ($celdas[$i, $COL.Color]) $fila 'Color'
        TurbidezOk = EsOk ($celdas[$i, $COL.Turbidez]) $fila 'Turbidez'
    })
}
if ($controles.Count -eq 0) { throw 'La hoja AGUA no tiene ningún control.' }

$desde = ($controles | Measure-Object -Property Fecha -Minimum).Minimum
$hasta = ($controles | Measure-Object -Property Fecha -Maximum).Maximum
Write-Host ("Filas leídas: {0} ({1} con cloro), del {2:dd/MM/yyyy} al {3:dd/MM/yyyy}" -f $controles.Count,
    @($controles | Where-Object { $null -ne $_.Cloro }).Count, $desde, $hasta)

# Escritura en la base de datos

$bd = New-Object System.Data.SqlClient.SqlConnection $Conexion
$bd.Open()
try {
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM USUARIO WHERE id = @id'
    [void]$cmd.Parameters.AddWithValue('@id', $UsuarioId)
    if ([int]$cmd.ExecuteScalar() -eq 0) { throw "No existe el usuario con id $UsuarioId." }

    $cmd = $bd.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM AGUA_CONTROL WHERE fecha BETWEEN @desde AND @hasta'
    [void]$cmd.Parameters.AddWithValue('@desde', $desde)
    [void]$cmd.Parameters.AddWithValue('@hasta', $hasta)
    $existentes = [int]$cmd.ExecuteScalar()
    if ($existentes -gt 0 -and -not $Forzar) {
        throw "AGUA_CONTROL ya tiene $existentes controles entre el $($desde.ToString('dd/MM/yyyy')) y el $($hasta.ToString('dd/MM/yyyy')). Para no duplicarlos no se importa nada (usa -Forzar si es lo que quieres)."
    }

    # Los triggers de historial necesitan saber quién hace el cambio
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = "EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;"
    [void]$cmd.Parameters.AddWithValue('@usuario', $UsuarioId)
    [void]$cmd.ExecuteNonQuery()

    $transaccion = $bd.BeginTransaction()
    try {
        # Puntos nuevos, al final de la lista
        foreach ($nueva in $TOMAS_NUEVAS) {
            $cmd = New-Object System.Data.SqlClient.SqlCommand(@'
IF NOT EXISTS (SELECT 1 FROM AGUA_TOMA WHERE nombre = @nombre)
    INSERT INTO AGUA_TOMA (nombre, orden) SELECT @nombre, ISNULL(MAX(orden), 0) + 1 FROM AGUA_TOMA;
'@, $bd, $transaccion)
            [void]$cmd.Parameters.AddWithValue('@nombre', $nueva)
            if ($cmd.ExecuteNonQuery() -gt 0) { Write-Host "Punto de agua nuevo: $nueva" }
        }

        $tomas = @{}
        $cmd = New-Object System.Data.SqlClient.SqlCommand('SELECT id, nombre FROM AGUA_TOMA', $bd, $transaccion)
        $lector = $cmd.ExecuteReader()
        while ($lector.Read()) { $tomas[$lector.GetString(1).ToUpperInvariant()] = $lector.GetInt32(0) }
        $lector.Close()

        $desconocidas = @($controles | Where-Object { -not $tomas.ContainsKey($_.Toma) } | ForEach-Object Toma | Sort-Object -Unique)
        if ($desconocidas.Count -gt 0) { throw "Puntos de agua que no existen en AGUA_TOMA: $($desconocidas -join ', ')" }

        foreach ($c in $controles) {
            $cmd = New-Object System.Data.SqlClient.SqlCommand(@'
INSERT INTO AGUA_CONTROL (fecha, toma_id, cloro, sabor_ok, olor_ok, color_ok, turbidez_ok, comentario, creado_por)
VALUES (@fecha, @toma, @cloro, @sabor, @olor, @color, @turbidez, NULL, @usuario);
'@, $bd, $transaccion)
            $p = $cmd.Parameters
            [void]$p.Add('@fecha', [Data.SqlDbType]::Date); $p['@fecha'].Value = $c.Fecha
            [void]$p.AddWithValue('@toma', $tomas[$c.Toma])
            $pc = $p.Add('@cloro', [Data.SqlDbType]::Decimal); $pc.Precision = 4; $pc.Scale = 2
            $pc.Value = if ($null -eq $c.Cloro) { [DBNull]::Value } else { $c.Cloro }
            [void]$p.AddWithValue('@sabor', $c.SaborOk)
            [void]$p.AddWithValue('@olor', $c.OlorOk)
            [void]$p.AddWithValue('@color', $c.ColorOk)
            [void]$p.AddWithValue('@turbidez', $c.TurbidezOk)
            [void]$p.AddWithValue('@usuario', $UsuarioId)
            try { [void]$cmd.ExecuteNonQuery() }
            catch { throw "Fila $($c.Fila): $($_.Exception.Message)" }
        }

        if ($Probar) {
            $transaccion.Rollback()
            Write-Host "PRUEBA: se habrían importado $($controles.Count) controles. No se ha guardado nada." -ForegroundColor Yellow
        }
        else {
            $transaccion.Commit()
            Write-Host "Importados $($controles.Count) controles." -ForegroundColor Green
        }
    }
    catch {
        $transaccion.Rollback()
        throw
    }
}
finally {
    $bd.Close()
}

Write-Host "`nPor punto de agua:"
$controles | Group-Object Toma | Sort-Object Count -Descending | ForEach-Object { Write-Host ("  {0,4}  {1}" -f $_.Count, $_.Name) }

if ($avisos.Count -gt 0) {
    Write-Host "`nAvisos ($($avisos.Count)):" -ForegroundColor Yellow
    $avisos | ForEach-Object { Write-Host "  $_" }
}
