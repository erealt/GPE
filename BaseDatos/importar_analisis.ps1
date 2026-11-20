<#
    Pasa la hoja MICRO & FYQ del Excel "PLAN DE ANALISIS 2026" a ANALISIS (apartado Análisis).
    Se ejecuta una vez, después de analisis.sql.

    - Cada fila de la hoja es un análisis; se pasan todas, aunque falte el parámetro o el resultado.
    - Los tipos son los del Excel: PRODUCTO, MMPP y ENVUELTA. La fila 236 (PASTA DE AVELLANAS) no tiene tipo
      y se pasa como MMPP; se puede cambiar luego en la aplicación.
    - Caducidad: si solo pone el mes ("abr-26", " ago-27", "oct-2026"), el día 1; si pone el día, ese día.
      La que no se entiende ("11/230") se deja vacía y sale en los avisos.
    - Los resultados que son números se pasan tal como se ven en el Excel ("1,44").
#>
param(
    [Parameter(Mandatory)] [int] $UsuarioId,     # usuario que figura como creador (y en el historial)
    [string] $Excel,     # por defecto, el Excel que está al lado de la carpeta GPE
    [string] $Conexion = $(if ($env:GPE_CONEXION) { $env:GPE_CONEXION }
                           else { 'Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;' }),
    [switch] $Probar,      # deshace todo al terminar
    [switch] $Forzar       # importa aunque ANALISIS ya tenga análisis
)

$ErrorActionPreference = 'Stop'
$avisos = New-Object System.Collections.Generic.List[string]
function Aviso($fila, $texto) { $avisos.Add("Fila ${fila}: $texto") }

# Columnas de la hoja MICRO & FYQ (fila 1 = cabeceras, datos desde la fila 2)
$COL = @{ Tipo = 1; Descripcion = 2; Lote = 3; Caducidad = 4; Totales = 5; Parametro = 6; Detalle = 7; Fecha = 8; Resultado = 9 }
$PRIMERA_FILA = 2
$ULTIMA_COLUMNA = 9

$TIPOS = @('PRODUCTO', 'MMPP', 'ENVUELTA')
$TIPO_FILA = @{ 236 = 'MMPP' }   # filas sin tipo en el Excel

# Largos de las columnas de ANALISIS
$LARGOS = @{ Descripcion = 300; Lote = 300; Totales = 500; Parametro = 30; Detalle = 100; Resultado = 500 }

$MESES = @{ ene = 1; feb = 2; mar = 3; abr = 4; may = 5; jun = 6; jul = 7; ago = 8; sep = 9; oct = 10; nov = 11; dic = 12 }

# Conversión de celdas

# Texto de la celda tal como se ve en el Excel (los números, con su formato: "1,44")
function Texto($i, $columna) {
    $v = $celdas[$i, $columna]
    if ($null -eq $v) { return $null }
    if ($v -is [double]) { $t = [string]$hoja.Cells.Item($i + $PRIMERA_FILA - 1, $columna).Text }
    else { $t = [string]$v }
    $t = $t.Trim()
    if ($t -eq '') { return $null }
    return $t
}

function Fecha($v) {
    if ($v -is [double]) { return [DateTime]::FromOADate($v).Date }
    if ($null -eq $v) { return $null }
    $t = ([string]$v).Trim()
    $f = [DateTime]::MinValue
    if ($t -and [DateTime]::TryParseExact($t, [string[]]@('dd/MM/yyyy', 'd/M/yyyy', 'dd-MM-yyyy', 'd-M-yyyy'),
            [Globalization.CultureInfo]'es-ES', [Globalization.DateTimeStyles]::None, [ref]$f)) { return $f }
    return $null
}

# "abr-26", "oct-2026" o "12/2026": el día 1 de ese mes
function Mes($v) {
    $t = ([string]$v).Trim().ToLowerInvariant()
    if ($t -match '^([a-z]{3})[a-z]*[-/ .]+(\d{2}|\d{4})$' -and $MESES.ContainsKey($Matches[1])) {
        $anio = [int]$Matches[2]; if ($anio -lt 100) { $anio += 2000 }
        return New-Object DateTime $anio, $MESES[$Matches[1]], 1
    }
    if ($t -match '^(\d{1,2})[-/](\d{4})$' -and [int]$Matches[1] -ge 1 -and [int]$Matches[1] -le 12) {
        return New-Object DateTime ([int]$Matches[2]), ([int]$Matches[1]), 1
    }
    return $null
}

function Largo($fila, $texto, $campo) {
    if ($null -ne $texto -and $texto.Length -gt $LARGOS[$campo]) {
        Aviso $fila "$campo con más de $($LARGOS[$campo]) caracteres; se corta."
        return $texto.Substring(0, $LARGOS[$campo])
    }
    return $texto
}

# Lectura del Excel

if (-not $Excel) { $Excel = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\..\PLAN DE ANALISIS 2026.xlsx' }
$Excel = (Resolve-Path $Excel).Path
Write-Host "Leyendo $Excel ..."
$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false
$xl.DisplayAlerts = $false
$analisis = New-Object System.Collections.Generic.List[object]
try {
    $libro = $xl.Workbooks.Open($Excel, 0, $true)   # solo lectura
    $hoja = $libro.Worksheets.Item('MICRO & FYQ')
    $ultimaFila = $hoja.Cells.Item($hoja.Rows.Count, $COL.Descripcion).End(-4162).Row   # xlUp
    $celdas = $hoja.Range($hoja.Cells.Item($PRIMERA_FILA, 1), $hoja.Cells.Item($ultimaFila, $ULTIMA_COLUMNA)).Value2

    for ($i = 1; $i -le $celdas.GetLength(0); $i++) {
        $fila = $i + $PRIMERA_FILA - 1
        $descripcion = Texto $i $COL.Descripcion
        if (-not $descripcion) {
            if ((1..$ULTIMA_COLUMNA | Where-Object { $null -ne $celdas[$i, $_] }).Count -gt 0) { Aviso $fila 'sin descripción; no se importa.' }
            continue
        }

        $tipo = Texto $i $COL.Tipo
        if ($tipo) { $tipo = $tipo.ToUpperInvariant() }
        if (-not $tipo -and $TIPO_FILA.ContainsKey($fila)) { $tipo = $TIPO_FILA[$fila]; Aviso $fila "sin tipo; se pasa como $tipo." }
        if ($TIPOS -notcontains $tipo) { Aviso $fila "tipo '$tipo' no es $($TIPOS -join ', '); no se importa."; continue }

        $vCad = $celdas[$i, $COL.Caducidad]
        $caducidad = $null
        if ($vCad -is [double]) { $caducidad = [DateTime]::FromOADate($vCad).Date }
        elseif ($null -ne $vCad -and ([string]$vCad).Trim() -ne '') {
            $caducidad = Mes $vCad
            if (-not $caducidad) { $caducidad = Fecha $vCad }
            if (-not $caducidad) { Aviso $fila "caducidad '$(([string]$vCad).Trim())' no se entiende; se deja vacía." }
        }

        $vFecha = $celdas[$i, $COL.Fecha]
        $fecha = Fecha $vFecha
        if (-not $fecha -and $null -ne $vFecha -and ([string]$vFecha).Trim() -ne '') { Aviso $fila "fecha de análisis '$vFecha' no se entiende; se deja vacía." }
        if ($fecha -and $fecha -gt [DateTime]::Today) { Aviso $fila "fecha de análisis $($fecha.ToString('dd/MM/yyyy')) todavía no ha llegado; se deja vacía."; $fecha = $null }

        $analisis.Add([pscustomobject]@{
            Fila = $fila
            Tipo = $tipo
            Descripcion = Largo $fila $descripcion 'Descripcion'
            Lote = Largo $fila (Texto $i $COL.Lote) 'Lote'
            Caducidad = $caducidad
            Totales = Largo $fila (Texto $i $COL.Totales) 'Totales'
            Parametro = Largo $fila (Texto $i $COL.Parametro) 'Parametro'
            Detalle = Largo $fila (Texto $i $COL.Detalle) 'Detalle'
            Fecha = $fecha
            Resultado = Largo $fila (Texto $i $COL.Resultado) 'Resultado'
        })
    }
    $libro.Close($false)
}
finally {
    $xl.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
}
if ($analisis.Count -eq 0) { throw 'La hoja MICRO & FYQ no tiene ningún análisis.' }
Write-Host "Filas leídas: $($analisis.Count)"

# Escritura en la base de datos

function Valor($v) { if ($null -eq $v) { [DBNull]::Value } else { $v } }

$bd = New-Object System.Data.SqlClient.SqlConnection $Conexion
$bd.Open()
try {
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM USUARIO WHERE id = @id'
    [void]$cmd.Parameters.AddWithValue('@id', $UsuarioId)
    if ([int]$cmd.ExecuteScalar() -eq 0) { throw "No existe el usuario con id $UsuarioId." }

    $cmd = $bd.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM ANALISIS'
    $existentes = [int]$cmd.ExecuteScalar()
    if ($existentes -gt 0 -and -not $Forzar) {
        throw "ANALISIS ya tiene $existentes análisis. Para no duplicarlos no se importa nada (usa -Forzar si es lo que quieres)."
    }

    # Los triggers de historial necesitan saber quién hace el cambio
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = "EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;"
    [void]$cmd.Parameters.AddWithValue('@usuario', $UsuarioId)
    [void]$cmd.ExecuteNonQuery()

    $transaccion = $bd.BeginTransaction()
    try {
        foreach ($a in $analisis) {
            $cmd = New-Object System.Data.SqlClient.SqlCommand(@'
INSERT INTO ANALISIS (tipo, descripcion, lote, caducidad, analisis_totales, parametro, parametro_detalle,
    fecha_analisis, resultado, creado_por)
VALUES (@tipo, @descripcion, @lote, @caducidad, @totales, @parametro, @detalle, @fecha, @resultado, @usuario);
'@, $bd, $transaccion)
            $p = $cmd.Parameters
            [void]$p.AddWithValue('@tipo', $a.Tipo)
            [void]$p.AddWithValue('@descripcion', $a.Descripcion)
            [void]$p.AddWithValue('@lote', (Valor $a.Lote))
            [void]$p.Add('@caducidad', [Data.SqlDbType]::Date); $p['@caducidad'].Value = Valor $a.Caducidad
            [void]$p.AddWithValue('@totales', (Valor $a.Totales))
            [void]$p.AddWithValue('@parametro', (Valor $a.Parametro))
            [void]$p.AddWithValue('@detalle', (Valor $a.Detalle))
            [void]$p.Add('@fecha', [Data.SqlDbType]::Date); $p['@fecha'].Value = Valor $a.Fecha
            [void]$p.AddWithValue('@resultado', (Valor $a.Resultado))
            [void]$p.AddWithValue('@usuario', $UsuarioId)
            try { [void]$cmd.ExecuteNonQuery() }
            catch { throw "Fila $($a.Fila): $($_.Exception.Message)" }
        }

        if ($Probar) {
            $transaccion.Rollback()
            Write-Host "PRUEBA: se habrían importado $($analisis.Count) análisis. No se ha guardado nada." -ForegroundColor Yellow
        }
        else {
            $transaccion.Commit()
            Write-Host "Importados $($analisis.Count) análisis." -ForegroundColor Green
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

Write-Host "`nPor tipo:"
$analisis | Group-Object Tipo | Sort-Object Count -Descending | ForEach-Object { Write-Host ("  {0,4}  {1}" -f $_.Count, $_.Name) }
Write-Host "`nPor parámetro:"
$analisis | Group-Object { if ($_.Parametro) { $_.Parametro } else { '(sin parámetro)' } } | Sort-Object Count -Descending |
    ForEach-Object { Write-Host ("  {0,4}  {1}" -f $_.Count, $_.Name) }

if ($avisos.Count -gt 0) {
    Write-Host "`nAvisos ($($avisos.Count)):" -ForegroundColor Yellow
    $avisos | ForEach-Object { Write-Host "  $_" }
}
