<#
    Pasa la hoja INCIDENCIAS del Excel "INCIDENCIAS Y NO CONFORMIDADES - CALIDAD" (MT-RG09)
    a las tablas NO_CONFORMIDAD y NC_ACCION. Se ejecuta una vez, después de no_conformidades.sql.
#>
param(
    [Parameter(Mandatory)] [int] $UsuarioId,     # usuario que figura como creador (y en el historial)
    [string] $Excel = (Join-Path $PSScriptRoot '..\..\INCIDENCIAS Y NO CONFORMIDADES - CALIDAD.xlsx'),
    [string] $Conexion = $(if ($env:GPE_CONEXION) { $env:GPE_CONEXION }
                           else { 'Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;' }),
    [switch] $Probar,      # deshace todo al terminar
    [switch] $Forzar       # importa aunque NO_CONFORMIDAD ya tenga registros
)

$ErrorActionPreference = 'Stop'
$avisos = New-Object System.Collections.Generic.List[string]
function Aviso($fila, $texto) { $avisos.Add("Fila ${fila}: $texto") }

# Columnas de la hoja INCIDENCIAS (fila 2 = cabeceras, datos desde la fila 3)
$COL = @{
    FechaDetectado = 1; Numero = 2; NcI = 3; Origen = 4; Seccion = 5; Tipo = 6; Problema = 7
    DescripcionCausa = 8; AccionInmediata = 9; Responsable = 10; FechaPrevista = 11; FechaFin = 12
    Cliente = 13; Proveedor = 14; McPccBpm = 15; Parametro = 16; Operario = 17; CapituloNorma = 18
    AnalisisCausaRaiz = 19; AccionCorrectora = 20; ResponsableCorrectora = 21; Coste = 22
    Horas = 23; Seguimiento = 24
}
$PRIMERA_FILA = 3
$ULTIMA_COLUMNA = 24

# ---------- Conversión de celdas ----------

function Texto($v) {
    if ($null -eq $v) { return $null }
    if ($v -is [double]) { $v = [string][decimal]$v }   # 312 y no 312.0
    $t = ([string]$v).Trim()
    if ($t -eq '') { return $null }
    return $t
}

$formatosFecha = 'dd/MM/yyyy', 'd/M/yyyy', 'dd/MM/yy', 'd/M/yy', 'dd-MM-yyyy', 'dd-MM-yy', 'd-M-yy'
function Fecha($v, $fila, $columna) {
    if ($null -eq $v -or ([string]$v).Trim() -eq '') { return $null }
    if ($v -is [double]) { return [DateTime]::FromOADate($v).Date }
    $t = ([string]$v).Trim()
    $f = [DateTime]::MinValue
    if ([DateTime]::TryParseExact($t, [string[]]$formatosFecha, [Globalization.CultureInfo]'es-ES',
            [Globalization.DateTimeStyles]::None, [ref]$f)) { return $f }
    Aviso $fila "$columna '$t' no es una fecha; se deja vacía."
    return $null
}

# Coste y horas: a veces llevan texto ("877,68 + iva", "45 minutos: ..."). Se toma el primer número.
function Numero($v, $fila, $columna) {
    if ($null -eq $v -or ([string]$v).Trim() -eq '') { return $null }
    if ($v -is [double]) { return [decimal]$v }
    $t = ([string]$v).Trim()
    $m = [regex]::Match($t, '\d+(?:[.,]\d+)?')
    if (-not $m.Success) { Aviso $fila "$columna '$t' no tiene ningún número; se deja vacío."; return $null }
    $n = [decimal]::Parse($m.Value.Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture)
    if ($columna -eq 'Horas' -and $t -match 'minuto') { $n = [math]::Round($n / 60, 2) }
    Aviso $fila "$columna '$t' se guarda como $n."
    return $n
}

function TipoRegistro($v, $fila) {
    switch -Regex (Texto $v) {
        '^NC$'          { return 'NC' }
        '^(I|INCIDENCIA)$' { return 'INCIDENCIA' }
        'MEJORA'        { return 'MEJORA' }
        default         { Aviso $fila "NC/I '$v' desconocido; se toma como INCIDENCIA."; return 'INCIDENCIA' }
    }
}

# ---------- Lectura del Excel ----------

$Excel = (Resolve-Path $Excel).Path
Write-Host "Leyendo $Excel ..."
$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false
$xl.DisplayAlerts = $false
try {
    $libro = $xl.Workbooks.Open($Excel, 0, $true)   # solo lectura
    $hoja = $libro.Worksheets.Item('INCIDENCIAS')
    $ultimaFila = $hoja.Cells.Item($hoja.Rows.Count, $COL.Problema).End(-4162).Row   # xlUp
    $celdas = $hoja.Range($hoja.Cells.Item($PRIMERA_FILA, 1), $hoja.Cells.Item($ultimaFila, $ULTIMA_COLUMNA)).Value2
    $libro.Close($false)
}
finally {
    $xl.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
}

$registros = New-Object System.Collections.Generic.List[object]
for ($i = 1; $i -le $celdas.GetLength(0); $i++) {
    $fila = $i + $PRIMERA_FILA - 1
    $c = @{}
    foreach ($k in $COL.Keys) { $c[$k] = $celdas[$i, $COL[$k]] }

    $problema = Texto $c.Problema
    $fecha = Fecha $c.FechaDetectado $fila 'Fecha detectado'
    if (-not $problema -and -not $fecha) { continue }   # fila vacía
    if (-not $problema) { Aviso $fila 'sin problema; no se importa.'; continue }
    if (-not $fecha) { Aviso $fila 'sin fecha detectado; no se importa.'; continue }

    $r = [pscustomobject]@{
        Fila = $fila
        TipoRegistro = TipoRegistro $c.NcI $fila
        Numero = Texto $c.Numero
        FechaDetectado = $fecha
        Origen = Texto $c.Origen
        Seccion = Texto $c.Seccion
        Tipo = Texto $c.Tipo
        Problema = $problema
        DescripcionCausa = Texto $c.DescripcionCausa
        Responsable = Texto $c.Responsable
        FechaPrevista = Fecha $c.FechaPrevista $fila 'Fecha prevista'
        FechaFin = Fecha $c.FechaFin $fila 'Fecha fin'
        Cliente = Texto $c.Cliente
        Proveedor = Texto $c.Proveedor
        McPccBpm = Texto $c.McPccBpm
        Parametro = Texto $c.Parametro
        Operario = Texto $c.Operario
        CapituloNorma = Texto $c.CapituloNorma
        AnalisisCausaRaiz = Texto $c.AnalisisCausaRaiz
        Coste = Numero $c.Coste $fila 'Coste'
        Horas = Numero $c.Horas $fila 'Horas'
        AccionInmediata = Texto $c.AccionInmediata
        AccionCorrectora = Texto $c.AccionCorrectora
        ResponsableCorrectora = Texto $c.ResponsableCorrectora
        Seguimiento = Texto $c.Seguimiento
    }
    if ($r.FechaFin -and $r.FechaFin -lt $r.FechaDetectado) {
        Aviso $fila "la fecha fin ($($r.FechaFin.ToString('dd/MM/yy'))) es anterior a la de detección; se importa igual."
    }
    $registros.Add($r)
}

# Las incidencias no tienen número en el Excel. Se numeran 1, 2, 3... por fecha (como hará la
# aplicación con las nuevas); si no, la aplicación les daría número al editarlas.
$n = 0
foreach ($r in ($registros | Where-Object { $_.TipoRegistro -eq 'INCIDENCIA' -and -not $_.Numero } |
                Sort-Object FechaDetectado, Fila)) {
    $n++
    $r.Numero = [string]$n
}

Write-Host ("Filas leídas: {0} ({1} NC, {2} incidencias, {3} acciones de mejora)" -f $registros.Count,
    @($registros | Where-Object TipoRegistro -eq 'NC').Count,
    @($registros | Where-Object TipoRegistro -eq 'INCIDENCIA').Count,
    @($registros | Where-Object TipoRegistro -eq 'MEJORA').Count)

# ---------- Escritura en la base de datos ----------

function Valor($v) { if ($null -eq $v) { [DBNull]::Value } else { $v } }

$sqlRegistro = @'
INSERT INTO NO_CONFORMIDAD
    (tipo_registro, numero, fecha_detectado, origen, seccion, tipo,
     problema, descripcion_causa, responsable,
     fecha_prevista, fecha_fin, cliente, proveedor,
     mc_pcc_bpm, parametro, operario,
     capitulo_norma, analisis_causa_raiz,
     coste_economico, horas_mano_obra, creado_por)
VALUES
    (@tipo_registro, @numero, @fecha_detectado, @origen, @seccion, @tipo,
     @problema, @descripcion_causa, @responsable,
     @fecha_prevista, @fecha_fin, @cliente, @proveedor,
     @mc_pcc_bpm, @parametro, @operario,
     @capitulo_norma, @analisis_causa_raiz,
     @coste_economico, @horas_mano_obra, @usuario);
SELECT CAST(SCOPE_IDENTITY() AS INT);
'@
$sqlAccion = @'
INSERT INTO NC_ACCION (no_conformidad_id, clase, orden, accion, fecha, responsable, observaciones, hecha)
VALUES (@nc, @clase, 1, @accion, NULL, @responsable, NULL, @hecha);
'@

$bd = New-Object System.Data.SqlClient.SqlConnection $Conexion
$bd.Open()
try {
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM USUARIO WHERE id = @id'
    [void]$cmd.Parameters.AddWithValue('@id', $UsuarioId)
    if ([int]$cmd.ExecuteScalar() -eq 0) { throw "No existe el usuario con id $UsuarioId." }

    $cmd = $bd.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM NO_CONFORMIDAD'
    $existentes = [int]$cmd.ExecuteScalar()
    if ($existentes -gt 0 -and -not $Forzar) {
        throw "NO_CONFORMIDAD ya tiene $existentes registros. Para no duplicarlos no se importa nada (usa -Forzar si es lo que quieres)."
    }

    # Los triggers de historial necesitan saber quién hace el cambio
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = "EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;"
    [void]$cmd.Parameters.AddWithValue('@usuario', $UsuarioId)
    [void]$cmd.ExecuteNonQuery()

    $transaccion = $bd.BeginTransaction()
    $acciones = 0
    try {
        foreach ($r in $registros) {
            $cmd = New-Object System.Data.SqlClient.SqlCommand($sqlRegistro, $bd, $transaccion)
            $p = $cmd.Parameters
            [void]$p.AddWithValue('@tipo_registro', $r.TipoRegistro)
            [void]$p.AddWithValue('@numero', (Valor $r.Numero))
            [void]$p.Add('@fecha_detectado', [Data.SqlDbType]::Date); $p['@fecha_detectado'].Value = $r.FechaDetectado
            [void]$p.AddWithValue('@origen', (Valor $r.Origen))
            [void]$p.AddWithValue('@seccion', (Valor $r.Seccion))
            [void]$p.AddWithValue('@tipo', (Valor $r.Tipo))
            [void]$p.AddWithValue('@problema', $r.Problema)
            [void]$p.AddWithValue('@descripcion_causa', (Valor $r.DescripcionCausa))
            [void]$p.AddWithValue('@responsable', (Valor $r.Responsable))
            [void]$p.Add('@fecha_prevista', [Data.SqlDbType]::Date); $p['@fecha_prevista'].Value = Valor $r.FechaPrevista
            [void]$p.Add('@fecha_fin', [Data.SqlDbType]::Date); $p['@fecha_fin'].Value = Valor $r.FechaFin
            [void]$p.AddWithValue('@cliente', (Valor $r.Cliente))
            [void]$p.AddWithValue('@proveedor', (Valor $r.Proveedor))
            [void]$p.AddWithValue('@mc_pcc_bpm', (Valor $r.McPccBpm))
            [void]$p.AddWithValue('@parametro', (Valor $r.Parametro))
            [void]$p.AddWithValue('@operario', (Valor $r.Operario))
            [void]$p.AddWithValue('@capitulo_norma', (Valor $r.CapituloNorma))
            [void]$p.AddWithValue('@analisis_causa_raiz', (Valor $r.AnalisisCausaRaiz))
            [void]$p.Add('@coste_economico', [Data.SqlDbType]::Decimal); $p['@coste_economico'].Value = Valor $r.Coste
            [void]$p.Add('@horas_mano_obra', [Data.SqlDbType]::Decimal); $p['@horas_mano_obra'].Value = Valor $r.Horas
            [void]$p.AddWithValue('@usuario', $UsuarioId)
            try { $id = [int]$cmd.ExecuteScalar() }
            catch { throw "Fila $($r.Fila): $($_.Exception.Message)" }

            # El Excel no dice si cada acción está hecha: se dan por hechas las de los registros cerrados.
            $hecha = $null -ne $r.FechaFin
            $lista = @(
                @{ Clase = 'INMEDIATA';   Texto = $r.AccionInmediata;  Responsable = $r.Responsable;           Hecha = $hecha },
                @{ Clase = 'CORRECTORA';  Texto = $r.AccionCorrectora; Responsable = $r.ResponsableCorrectora; Hecha = $hecha },
                @{ Clase = 'SEGUIMIENTO'; Texto = $r.Seguimiento;      Responsable = $null;                    Hecha = $false }
            )
            foreach ($a in $lista | Where-Object { $_.Texto }) {
                $cmd = New-Object System.Data.SqlClient.SqlCommand($sqlAccion, $bd, $transaccion)
                [void]$cmd.Parameters.AddWithValue('@nc', $id)
                [void]$cmd.Parameters.AddWithValue('@clase', $a.Clase)
                [void]$cmd.Parameters.AddWithValue('@accion', $a.Texto)
                [void]$cmd.Parameters.AddWithValue('@responsable', (Valor $a.Responsable))
                [void]$cmd.Parameters.AddWithValue('@hecha', [bool]$a.Hecha)
                [void]$cmd.ExecuteNonQuery()
                $acciones++
            }
        }

        if ($Probar) {
            $transaccion.Rollback()
            Write-Host "PRUEBA: se habrían importado $($registros.Count) registros y $acciones acciones. No se ha guardado nada." -ForegroundColor Yellow
        }
        else {
            $transaccion.Commit()
            Write-Host "Importados $($registros.Count) registros y $acciones acciones." -ForegroundColor Green
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

if ($avisos.Count -gt 0) {
    Write-Host "`nAvisos ($($avisos.Count)):" -ForegroundColor Yellow
    $avisos | ForEach-Object { Write-Host "  $_" }
}
