<#
    Pasa la hoja INCIDENCIAS del Excel "INCIDENCIAS Y NO CONFORMIDADES - CALIDAD" (MT-RG09)
    a las tablas NO_CONFORMIDAD y NC_ACCION. Se ejecuta una vez, después de no_conformidades.sql y causa_raiz.sql.
    Para volver a cargarlo cuando calidad añade filas: -Reemplazar (borra lo que hay en la misma transacción).
    En el servidor, desde el ordenador de desarrollo: -Servidor 192.168.2.249 (entra con gpe_app).
    En el propio servidor (que no tiene Excel): en el ordenador de desarrollo -GuardarDatos incidencias.xml
    lee el Excel y guarda lo leído; en el servidor -Datos incidencias.xml lo carga en la base.
    Es para el Excel con el formato de noviembre de 2026 (títulos agrupados en la fila 2, cabeceras en la 3,
    columna CAUSA RAIZ). ESTADO, Año, Mes, Sem y Día son fórmulas y no se pasan: la aplicación los calcula.
    La columna DESCRIPCIÓN DEL PROBLEMA es la antigua "Descripción causa" (mismo texto): va a descripcion_causa.
#>
param(
    [int] $UsuarioId,     # usuario que figura como creador (y en el historial); no hace falta con -GuardarDatos
    [string] $Excel = (Join-Path $PSScriptRoot '..\..\INCIDENCIAS Y NO CONFORMIDADES - CALIDAD.xlsx'),
    [string] $Conexion = $(if ($env:GPE_CONEXION) { $env:GPE_CONEXION }
                           else { 'Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;' }),
    [switch] $Probar,      # deshace todo al terminar
    [switch] $Forzar,      # importa aunque NO_CONFORMIDAD ya tenga registros
    # Para volver a cargar el Excel: borra antes todos los registros (con sus acciones y fotos) en la
    # misma transacción. Enseña los que se hicieron a mano y pide confirmación.
    [switch] $Reemplazar,
    # Base GPE del servidor con el usuario gpe_app (su contraseña, la de %LOCALAPPDATA%\GPE\gpe_app.txt)
    [string] $Servidor,
    # Solo lee el Excel y guarda lo leído en este archivo (para cargarlo en un ordenador sin Excel)
    [string] $GuardarDatos,
    # Carga lo guardado con -GuardarDatos en vez de leer el Excel
    [string] $Datos
)
if (-not $GuardarDatos -and -not $UsuarioId) { throw 'Falta -UsuarioId (el usuario que figura como creador).' }
if ($GuardarDatos -and $Datos) { throw '-GuardarDatos y -Datos no van juntos.' }
if ($Servidor) {
    $archivoClave = Join-Path $env:LOCALAPPDATA 'GPE\gpe_app.txt'
    if (-not (Test-Path $archivoClave)) { throw "No está $archivoClave (la contraseña de gpe_app del ordenador de desarrollo)." }
    $Conexion = "Server=$Servidor,1433;Database=GPE;User Id=gpe_app;Password=$((Get-Content $archivoClave -Raw).Trim());TrustServerCertificate=True;"
}

$ErrorActionPreference = 'Stop'
$avisos = New-Object System.Collections.Generic.List[string]
function Aviso($fila, $texto) { $avisos.Add("Fila ${fila}: $texto") }

# Columnas de la hoja INCIDENCIAS (fila 3 = cabeceras, datos desde la fila 4)
$COL = @{
    FechaDetectado = 1; Numero = 2; NcI = 3; Origen = 4; Seccion = 5; Tipo = 6; Problema = 7
    DescripcionCausa = 8; AccionInmediata = 9; Responsable = 10; AnalisisCausaRaiz = 11; CausaRaiz = 12
    AccionCorrectora = 13; ResponsableCorrectora = 14; Seguimiento = 15; FechaPrevista = 16; FechaFin = 17
    Cliente = 19; Proveedor = 20; McPccBpm = 21; Parametro = 22; Operario = 23; CapituloNorma = 24
    Coste = 25; Horas = 26
}
$PRIMERA_FILA = 4
$ULTIMA_COLUMNA = 26

# CAUSA RAIZ: uno de los grupos de la hoja CAUSA RAIZ, igual que en la aplicación (GPE.Datos/CausasRaiz.cs)
$CAUSAS_RAIZ = 'Personal', 'Equipos', 'Economía', 'Procesos', 'Alimentos'

# Conversión de celdas

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

# Un grupo de la lista (sin mirar mayúsculas ni tildes); si no es ninguno, vacío y aviso
function CausaRaiz($v, $fila) {
    $t = Texto $v
    if (-not $t) { return $null }
    function SinTildes($s) { -join ($s.Normalize([Text.NormalizationForm]::FormD).ToCharArray() |
        Where-Object { [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne 'NonSpacingMark' }) }
    # Calidad escribe "GRUPO - causa" (una o varias líneas): cuenta el grupo de delante del guion
    $nombres = @($t -split "`n" | ForEach-Object { ($_ -split ' - ', 2)[0].Trim() } | Where-Object { $_ })
    $grupos = @($nombres | ForEach-Object {
        $n = $_; $CAUSAS_RAIZ | Where-Object { (SinTildes $_) -eq (SinTildes $n) } | Select-Object -First 1
    } | Select-Object -Unique)
    if ($grupos.Count -ne 1 -or $grupos.Count -ne @($nombres | Select-Object -Unique).Count) {
        Aviso $fila "Causa raíz '$t' no es uno solo de los grupos ($($CAUSAS_RAIZ -join ', ')); se deja vacía."
        return $null
    }
    return $grupos[0]
}

# Si el análisis está escrito como "¿Por qué...? Porque... ¿Por qué...? Porque...", se reparte en los 5 porqués
# de la aplicación. Si es texto normal (o tiene más de 5 preguntas) devuelve $null y el texto se deja tal cual.
function PorQues($texto, $fila) {
    if (-not $texto -or $texto -notmatch '^¿\s*Por\s+qu[eé]') { return $null }
    $pares = [regex]::Matches($texto, '(?s)(¿\s*Por\s+qu[eé][^?]*\?)\s*(.*?)\s*(?=¿\s*Por\s+qu[eé]|\z)')
    if ($pares.Count -gt 5) {
        Aviso $fila "el análisis de la causa raíz tiene $($pares.Count) porqués (máximo 5); se deja como texto."
        return $null
    }
    return @($pares | ForEach-Object {
        [pscustomobject]@{ Pregunta = $_.Groups[1].Value.Trim(); Respuesta = (Texto $_.Groups[2].Value) }
    })
}

# Lectura del Excel (o de lo guardado con -GuardarDatos)

if ($Datos) {
    Write-Host "Leyendo $Datos ..."
    $guardado = Import-Clixml $Datos
    $registros = @($guardado.Registros)
    foreach ($a in $guardado.Avisos) { $avisos.Add($a) }
}
else {
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
            CausaRaiz = CausaRaiz $c.CausaRaiz $fila
            Coste = Numero $c.Coste $fila 'Coste'
            Horas = Numero $c.Horas $fila 'Horas'
            AccionInmediata = Texto $c.AccionInmediata
            AccionCorrectora = Texto $c.AccionCorrectora
            ResponsableCorrectora = Texto $c.ResponsableCorrectora
            Seguimiento = Texto $c.Seguimiento
        }
        # Los porqués van a sus casillas y el texto ya no se repite en "Análisis de la causa raíz"
        $r | Add-Member PorQues (PorQues $r.AnalisisCausaRaiz $fila)
        if ($r.PorQues) { $r.AnalisisCausaRaiz = $null }
        if ($r.FechaFin -and $r.FechaFin -lt $r.FechaDetectado) {
            Aviso $fila "la fecha fin ($($r.FechaFin.ToString('dd/MM/yy'))) es anterior a la de detección; se importa igual."
        }
        $registros.Add($r)
    }

    # Las incidencias no tienen número en el Excel: se numeran por fecha, como hace la aplicación.
    $n = 0
    foreach ($r in ($registros | Where-Object { $_.TipoRegistro -eq 'INCIDENCIA' -and -not $_.Numero } |
                    Sort-Object FechaDetectado, Fila)) {
        $n++
        $r.Numero = [string]$n
    }
}

Write-Host ("Filas leídas: {0} ({1} NC, {2} incidencias, {3} acciones de mejora)" -f $registros.Count,
    @($registros | Where-Object TipoRegistro -eq 'NC').Count,
    @($registros | Where-Object TipoRegistro -eq 'INCIDENCIA').Count,
    @($registros | Where-Object TipoRegistro -eq 'MEJORA').Count)

if ($GuardarDatos) {
    $guardar = New-Object PSObject
    $guardar | Add-Member Registros ([object[]]$registros.ToArray())
    $guardar | Add-Member Avisos ([string[]]$avisos.ToArray())
    Export-Clixml -InputObject $guardar -Path $GuardarDatos -Depth 5
    Write-Host "Guardado en $GuardarDatos (no se ha tocado ninguna base de datos)." -ForegroundColor Green
    return
}

# Escritura en la base de datos

function Valor($v) { if ($null -eq $v) { [DBNull]::Value } else { $v } }

$sqlRegistro = @'
INSERT INTO NO_CONFORMIDAD
    (tipo_registro, numero, fecha_detectado, origen, seccion, tipo,
     problema, descripcion_causa,
     fecha_prevista, fecha_fin, cliente, proveedor,
     mc_pcc_bpm, parametro, operario,
     capitulo_norma, analisis_causa_raiz, causa_raiz,
     porque_pregunta_1, porque_respuesta_1, porque_pregunta_2, porque_respuesta_2,
     porque_pregunta_3, porque_respuesta_3, porque_pregunta_4, porque_respuesta_4,
     porque_pregunta_5, porque_respuesta_5,
     coste_economico, horas_mano_obra, creado_por)
VALUES
    (@tipo_registro, @numero, @fecha_detectado, @origen, @seccion, @tipo,
     @problema, @descripcion_causa,
     @fecha_prevista, @fecha_fin, @cliente, @proveedor,
     @mc_pcc_bpm, @parametro, @operario,
     @capitulo_norma, @analisis_causa_raiz, @causa_raiz,
     @porque_pregunta_1, @porque_respuesta_1, @porque_pregunta_2, @porque_respuesta_2,
     @porque_pregunta_3, @porque_respuesta_3, @porque_pregunta_4, @porque_respuesta_4,
     @porque_pregunta_5, @porque_respuesta_5,
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
    if ($existentes -gt 0 -and -not $Forzar -and -not $Reemplazar) {
        throw "NO_CONFORMIDAD ya tiene $existentes registros. Para no duplicarlos no se importa nada (-Reemplazar para borrarlos y cargar el Excel otra vez)."
    }

    if ($Reemplazar -and $existentes -gt 0) {
        # Los de la carga anterior se crearon todos a la vez; los de después se hicieron a mano
        $cmd = $bd.CreateCommand()
        $cmd.CommandText = @'
DECLARE @carga DATETIME2 = (SELECT MIN(creado_en) FROM NO_CONFORMIDAD);
SELECT tipo_registro, numero, CONVERT(varchar(10), fecha_detectado, 103) AS fecha,
       LEFT(REPLACE(problema, CHAR(10), ' '), 70) AS problema, FORMAT(creado_en, 'dd/MM/yyyy HH:mm') AS creado
FROM NO_CONFORMIDAD WHERE creado_en > DATEADD(MINUTE, 30, @carga) ORDER BY creado_en;
SELECT (SELECT COUNT(*) FROM NC_FOTO) AS fotos,
       (SELECT COUNT(*) FROM RUTINA_RESULTADO WHERE no_conformidad_id IS NOT NULL) AS casillas;
'@
        $ds = New-Object System.Data.DataSet
        [void](New-Object System.Data.SqlClient.SqlDataAdapter $cmd).Fill($ds)
        $aMano = $ds.Tables[0]
        Write-Host "`nSe van a borrar los $existentes registros que hay, con sus acciones, $($ds.Tables[1].Rows[0].fotos) fotos" -ForegroundColor Yellow
        Write-Host "y el enlace de $($ds.Tables[1].Rows[0].casillas) casillas de la rutina (la casilla se queda, sin su incidencia)." -ForegroundColor Yellow
        if ($aMano.Rows.Count -gt 0) {
            Write-Host "Hechos a mano después de la carga anterior ($($aMano.Rows.Count)); si no están en el Excel, se pierden:" -ForegroundColor Yellow
            $aMano | Format-Table -AutoSize | Out-String -Width 200 | Write-Host
        } else {
            Write-Host 'No hay ninguno hecho a mano después de la carga anterior.'
        }
        if (-not $Probar -and (Read-Host '¿Borrarlos y cargar el Excel? (S/N)') -notmatch '^[sS]') {
            Write-Host 'No se ha cambiado nada.'
            return
        }
    }

    # Los triggers de historial necesitan saber quién hace el cambio
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = "EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;"
    [void]$cmd.Parameters.AddWithValue('@usuario', $UsuarioId)
    [void]$cmd.ExecuteNonQuery()

    $transaccion = $bd.BeginTransaction()
    $acciones = 0
    try {
        if ($Reemplazar) {
            # Acciones, fotos, avisos y bloqueos se borran con su registro; las casillas se quedan sin enlace
            $cmd = New-Object System.Data.SqlClient.SqlCommand('DELETE FROM NO_CONFORMIDAD;', $bd, $transaccion)
            $cmd.CommandTimeout = 0
            [void]$cmd.ExecuteNonQuery()
        }

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
            [void]$p.Add('@fecha_prevista', [Data.SqlDbType]::Date); $p['@fecha_prevista'].Value = Valor $r.FechaPrevista
            [void]$p.Add('@fecha_fin', [Data.SqlDbType]::Date); $p['@fecha_fin'].Value = Valor $r.FechaFin
            [void]$p.AddWithValue('@cliente', (Valor $r.Cliente))
            [void]$p.AddWithValue('@proveedor', (Valor $r.Proveedor))
            [void]$p.AddWithValue('@mc_pcc_bpm', (Valor $r.McPccBpm))
            [void]$p.AddWithValue('@parametro', (Valor $r.Parametro))
            [void]$p.AddWithValue('@operario', (Valor $r.Operario))
            [void]$p.AddWithValue('@capitulo_norma', (Valor $r.CapituloNorma))
            [void]$p.AddWithValue('@analisis_causa_raiz', (Valor $r.AnalisisCausaRaiz))
            for ($k = 0; $k -lt 5; $k++) {
                $pq = if ($r.PorQues) { $r.PorQues | Select-Object -Skip $k -First 1 }
                [void]$p.AddWithValue("@porque_pregunta_$($k + 1)", (Valor $pq.Pregunta))
                [void]$p.AddWithValue("@porque_respuesta_$($k + 1)", (Valor $pq.Respuesta))
            }
            [void]$p.AddWithValue('@causa_raiz', (Valor $r.CausaRaiz))
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

        # Las acciones del Excel se dan por avisadas, como hace acciones_nc_avisos.sql: si no, cuando
        # haya correo a cada responsable le llegaría un aviso por cada acción antigua
        $cmd = New-Object System.Data.SqlClient.SqlCommand(@'
IF OBJECT_ID('NC_ACCION_AVISO') IS NOT NULL
    INSERT INTO NC_ACCION_AVISO (accion_id, responsable)
    SELECT a.id, a.responsable FROM NC_ACCION a
    WHERE a.responsable IS NOT NULL AND NOT EXISTS (SELECT 1 FROM NC_ACCION_AVISO v WHERE v.accion_id = a.id);
'@, $bd, $transaccion)
        [void]$cmd.ExecuteNonQuery()

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
