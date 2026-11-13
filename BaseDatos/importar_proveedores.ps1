<#
    Pasa la hoja PRODUCTOS del Excel "CP-RG05 - EVALUACIÓN PROVEEDORES Y FRAUDE" a PROVEEDOR y PROVEEDOR_PRODUCTO.
    Se ejecuta una vez, después de proveedores.sql.

    En la hoja cada fila es un proveedor con uno de sus productos, y los datos del proveedor (columnas A-N,
    HISTORIAL y P. SUBCONTRATADO) se repiten en cada fila. Cuando no son iguales en todas sus filas:
    - se guarda el de las filas con la fecha de revisión más reciente (las otras parecen no actualizadas);
      entre ellas, el que más se repite (si empatan, el de la fila más arriba), sin contar las celdas vacías;
    - y se apunta en PROVEEDOR.revisar qué valores había, para que calidad decida en la app cuál es el bueno.
    Para BAJA sí cuentan las celdas vacías (vacío = no está de baja).

    Las columnas calculadas (EVALUACIÓN, MUESTREO, O*D, RIESGO, PUNTUACIÓN, RIESGO FRAUDE, ACTUACIÓN) no se pasan:
    la app las calcula igual que el Excel.
    IMPACTO DEL PRODUCTO y RELACIÓN copian con fórmula el impacto y el estatus del proveedor, pero en algunas filas
    se escribió otro valor a mano: ese valor se guarda (PROVEEDOR_PRODUCTO.impacto y PROVEEDOR.relacion) para que
    las notas salgan como en el Excel. Si la celda tiene la fórmula o está vacía, se deja en NULL (= el del proveedor).
    Lo que no cabe en su columna (una nota con texto, una fecha mal escrita) se guarda en observaciones del producto.
#>
param(
    [Parameter(Mandatory)] [int] $UsuarioId,     # usuario que figura como creador (y en el historial)
    [string] $Excel,     # por defecto, el Excel que está al lado de la carpeta GPE
    [string] $Conexion = $(if ($env:GPE_CONEXION) { $env:GPE_CONEXION }
                           else { 'Server=localhost;Database=GPE;Integrated Security=True;TrustServerCertificate=True;' }),
    [switch] $Probar     # deshace todo al terminar
)

$ErrorActionPreference = 'Stop'
$avisos = New-Object System.Collections.Generic.List[string]
function Aviso($fila, $texto) { $avisos.Add("Fila ${fila}: $texto") }

# Columnas de la hoja PRODUCTOS (fila 2 = cabeceras, datos desde la fila 3)
$COL = @{
    Numero = 1; Nombre = 2; FechaAlta = 3; Baja = 4; Tipo = 5; FechaRevision = 6
    Impacto = 7; Estatus = 8; Reclamaciones = 9
    Certificados = 11; Ensayo = 12; Auditoria = 13; Rfa = 14
    Codigo = 15; Descripcion = 16; TipoProducto = 17; ProdAlta = 18; ProdBaja = 19; Alergenos = 20; PosiblesAlergenos = 21
    ProdRevision = 22; FichaTecnica = 23; Quimicos = 24; Micro = 25; AnalisisAlergenos = 26
    EurochocFecha = 27; EurochocAlcance = 28; EurochocResultado = 29
    ImpactoProducto = 30; ControlRecepcion = 32; ProdReclamaciones = 33; Parametro = 36
    PHistorial = 37; PMotivacion = 38; PFacilidad = 39; DOrigen = 40; DCadena = 41; DMedidas = 42
    Relacion = 45; Historial = 46; Subcontratado = 47; Medidas = 51
}
$PRIMERA_FILA = 3
$ULTIMA_COLUMNA = 51

# Conversión de celdas

function Texto($v) {
    if ($null -eq $v) { return $null }
    $t = ([string]$v).Trim()
    if ($t -eq '') { return $null }
    return $t
}

# En las columnas de texto hay fechas que Excel guarda como número: se pasan como "dd/MM/yyyy"; los años, tal cual
function TextoOFecha($v) {
    if ($v -is [double]) {
        if ($v -ge 20000 -and $v -lt 80000) { return [DateTime]::FromOADate($v).ToString('dd/MM/yyyy') }
        return ([string]$v)
    }
    return Texto $v
}

# Devuelve @(fecha, textoQueNoEsFecha)
function Fecha($v) {
    if ($v -is [double]) { return @([DateTime]::FromOADate($v).Date, $null) }
    $t = Texto $v
    if (-not $t) { return @($null, $null) }
    $f = [DateTime]::MinValue
    if ([DateTime]::TryParseExact($t, [string[]]@('dd/MM/yyyy', 'd/M/yyyy', 'dd/MM/yy', 'd/M/yy'),
            [Globalization.CultureInfo]'es-ES', [Globalization.DateTimeStyles]::None, [ref]$f)) { return @($f, $null) }
    return @($null, $t)
}

# Una nota de 0-3. Devuelve @(nota, textoQueSobra): "1 UE Y CHINA" -> 1 y el texto entero para observaciones
function Nota($v, $min, $max) {
    if ($v -is [double]) {
        if ($v -eq [math]::Floor($v) -and $v -ge $min -and $v -le $max) { return @([int]$v, $null) }
        return @($null, [string]$v)
    }
    $t = Texto $v
    if (-not $t) { return @($null, $null) }
    if ($t -match '^\s*(\d+)') {
        $n = [int]$Matches[1]
        if ($n -ge $min -and $n -le $max) { return @($n, $t) }
    }
    return @($null, $t)
}

# Lectura del Excel

if (-not $Excel) { $Excel = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\..\CP-RG05 - EVALUACIÓN PROVEEDORES Y FRAUDE.xlsx' }
$Excel = (Resolve-Path $Excel).Path
Write-Host "Leyendo $Excel ..."
$xl = New-Object -ComObject Excel.Application
$xl.Visible = $false
$xl.DisplayAlerts = $false
try {
    $libro = $xl.Workbooks.Open($Excel, 0, $true)   # solo lectura
    $hoja = $libro.Worksheets.Item('PRODUCTOS')
    $ultimaFila = $hoja.Cells.Item($hoja.Rows.Count, $COL.Nombre).End(-4162).Row   # xlUp
    $rango = $hoja.Range($hoja.Cells.Item($PRIMERA_FILA, 1), $hoja.Cells.Item($ultimaFila, $ULTIMA_COLUMNA))
    $celdas = $rango.Value2
    $formulas = $rango.Formula   # para saber si IMPACTO DEL PRODUCTO y RELACIÓN se escribieron a mano
    $libro.Close($false)
}
finally {
    $xl.Quit()
    [void][Runtime.InteropServices.Marshal]::ReleaseComObject($xl)
}

# Cada fila, con lo del proveedor y lo del producto por separado
$filas = New-Object System.Collections.Generic.List[object]
for ($i = 1; $i -le $celdas.GetLength(0); $i++) {
    $fila = $i + $PRIMERA_FILA - 1
    function Celda($nombre) { $celdas[$i, $COL[$nombre]] }
    # El valor escrito a mano en una columna que normalmente lleva fórmula (null si tiene la fórmula)
    function AMano($nombre) { if (([string]$formulas[$i, $COL[$nombre]]).StartsWith('=')) { $null } else { Celda $nombre } }

    $nombre = Texto (Celda 'Nombre')
    if (-not $nombre) {
        if (Texto (Celda 'Codigo')) { Aviso $fila 'producto sin proveedor; no se importa.' }
        continue
    }
    $nombre = $nombre -replace '\s+', ' '
    $numero = $null
    $n = Celda 'Numero'
    if ($n -is [double]) { $numero = [int]$n } elseif (Texto $n) { Aviso $fila "Nº '$n' no es un número; se deja vacío." }

    $observaciones = New-Object System.Collections.Generic.List[string]

    # Datos del proveedor: como texto, para comparar las filas de un mismo proveedor
    $prov = [ordered]@{}
    foreach ($campo in 'FechaAlta', 'FechaRevision') {
        $f = Fecha (Celda $campo)
        if ($f[1]) { Aviso $fila "$campo '$($f[1])' no es una fecha; se deja vacía." }
        $prov[$campo] = if ($f[0]) { $f[0].ToString('yyyy-MM-dd') } else { $null }
    }
    $prov.Baja = if ((Texto (Celda 'Baja')) -eq 'BAJA') { 'BAJA' } else { $null }
    $prov.Tipo = Texto (Celda 'Tipo')
    foreach ($par in @(@('Impacto', 1, 3), @('Estatus', 0, 3), @('Reclamaciones', 0, 2), @('Historial', 0, 3), @('Subcontratado', 0, 3))) {
        $r = Nota (Celda $par[0]) $par[1] $par[2]
        if ($null -eq $r[0] -and $r[1]) { Aviso $fila "$($par[0]) '$($r[1])' no es una nota de $($par[1]) a $($par[2]); se deja vacía." }
        elseif ($r[1]) { Aviso $fila "$($par[0]) '$($r[1])': se guarda $($r[0])." }
        $prov[$par[0]] = if ($null -ne $r[0]) { [string]$r[0] } else { $null }
    }
    foreach ($campo in 'Certificados', 'Ensayo', 'Auditoria', 'Rfa') { $prov[$campo] = TextoOFecha (Celda $campo) }

    # RELACIÓN escrita a mano y distinta del estatus de la fila ("MEDIA (2)" se guarda como 2)
    $r = Nota (AMano 'Relacion') 0 3
    if ($null -eq $r[0] -and $r[1]) { Aviso $fila "Relación '$($r[1])' no es una nota de 0 a 3; se deja como el estatus." }
    elseif ($r[1]) { Aviso $fila "Relación '$($r[1])': se guarda $($r[0])." }
    $prov.Relacion = if ($null -ne $r[0] -and [string]$r[0] -ne $prov.Estatus) { [string]$r[0] } else { $null }

    # Datos del producto
    $prod = [ordered]@{
        Codigo = Texto (Celda 'Codigo')
        Descripcion = Texto (Celda 'Descripcion')
        TipoProducto = Texto (Celda 'TipoProducto')
        Baja = (Texto (Celda 'ProdBaja')) -eq 'BAJA'
        Alergenos = Texto (Celda 'Alergenos')
        PosiblesAlergenos = Texto (Celda 'PosiblesAlergenos')
        FichaTecnica = TextoOFecha (Celda 'FichaTecnica')
        Quimicos = TextoOFecha (Celda 'Quimicos')
        Micro = TextoOFecha (Celda 'Micro')
        AnalisisAlergenos = TextoOFecha (Celda 'AnalisisAlergenos')
        EurochocAlcance = Texto (Celda 'EurochocAlcance')
        EurochocResultado = Texto (Celda 'EurochocResultado')
        Parametro = Texto (Celda 'Parametro')
        Medidas = Texto (Celda 'Medidas')
    }
    foreach ($par in @(@('ProdAlta', 'FECHA ALTA'), @('ProdRevision', 'REVISIÓN'), @('EurochocFecha', 'FECHA EUROCHOC'))) {
        $f = Fecha (Celda $par[0])
        $prod[$par[0]] = $f[0]
        if ($f[1]) { $observaciones.Add("$($par[1]): $($f[1])") }
    }
    foreach ($par in @(@('ControlRecepcion', 1, 3, 'CONTROL RECEPCIÓN'), @('ProdReclamaciones', 0, 2, 'RECLAMACIONES'),
                       @('PHistorial', 0, 3, 'P. HISTORIAL'), @('PMotivacion', 0, 3, 'P. MOTIVACIÓN ECONÓMICA'),
                       @('PFacilidad', 0, 3, 'P. FACILIDAD'), @('DOrigen', 0, 3, 'D. ORIGEN'),
                       @('DCadena', 0, 3, 'D. CADENA SUMINISTRO'), @('DMedidas', 0, 3, 'D. MEDIDAS CONTROL'))) {
        $r = Nota (Celda $par[0]) $par[1] $par[2]
        $prod[$par[0]] = $r[0]
        if ($r[1]) { $observaciones.Add("$($par[3]): $($r[1])") }
    }
    # IMPACTO DEL PRODUCTO escrito a mano y distinto del impacto del proveedor en esa fila
    $r = Nota (AMano 'ImpactoProducto') 1 3
    if ($r[1]) { $observaciones.Add("IMPACTO DEL PRODUCTO: $($r[1])") }
    $prod.Impacto = if ($null -ne $r[0] -and [string]$r[0] -ne $prov.Impacto) { $r[0] } else { $null }

    $prod.Observaciones = if ($observaciones.Count -gt 0) { $observaciones -join "`n" } else { $null }

    $filas.Add([pscustomobject]@{ Fila = $fila; Numero = $numero; Nombre = $nombre; Proveedor = $prov; Producto = $prod })
}
if ($filas.Count -eq 0) { throw 'La hoja PRODUCTOS no tiene ninguna fila.' }

# Un proveedor por Nº (o por nombre si no tiene Nº), con el valor más repetido de cada dato

$NOMBRES_CAMPO = [ordered]@{
    Nombre = 'Nombre'; FechaAlta = 'Fecha alta'; Baja = 'Baja'; Tipo = 'Tipo'; FechaRevision = 'Fecha revisión'
    Impacto = 'Impacto'; Estatus = 'Estatus'; Reclamaciones = 'Reclamaciones'
    Certificados = 'Certificados'; Ensayo = 'Ensayo trazabilidad'; Auditoria = 'Auditoría'; Rfa = 'RFA'
    Relacion = 'Relación'; Historial = 'Historial'; Subcontratado = 'P. subcontratado'
}

# Valores de un dato en esas filas: sin las celdas vacías, salvo en BAJA (vacío = no está de baja)
function Cuentan($lineasCampo, $campo) {
    $valores = @($lineasCampo | ForEach-Object { if ($campo -eq 'Nombre') { $_.Nombre } else { $_.Proveedor[$campo] } })
    if ($campo -eq 'Baja') { return @($valores | ForEach-Object { if ($_) { $_ } else { '' } }) }
    return @($valores | Where-Object { $_ })
}

# Cada valor con las veces que sale, el más repetido primero; si empatan, el que sale antes en la hoja
function Contar($valores) {
    $k = 0
    return @($valores | Group-Object -CaseSensitive | ForEach-Object {
        [pscustomobject]@{ Valor = $_.Name; Veces = $_.Count; Orden = $k++ } } | Sort-Object @{ e = 'Veces'; d = $true }, Orden)
}

function Mostrar($valor, $campo) {
    if ($valor -eq '') { return '(vacío)' }
    if ($campo -like 'Fecha*') { return ([DateTime]$valor).ToString('dd/MM/yyyy') }
    return $valor
}

$proveedores = New-Object System.Collections.Generic.List[object]
foreach ($grupo in ($filas | Group-Object { if ($null -ne $_.Numero) { "N$($_.Numero)" } else { "T$($_.Nombre.ToUpperInvariant())" } })) {
    $lineas = @($grupo.Group)
    $datos = [ordered]@{}
    $revisar = New-Object System.Collections.Generic.List[string]

    # Las filas que no se actualizaron se quedaron con una fecha de revisión antigua: mandan las de la más reciente
    $ultima = $lineas | ForEach-Object { $_.Proveedor.FechaRevision } | Where-Object { $_ } | Sort-Object -Descending | Select-Object -First 1
    $recientes = if ($ultima) { @($lineas | Where-Object { $_.Proveedor.FechaRevision -eq $ultima }) } else { $lineas }

    foreach ($campo in $NOMBRES_CAMPO.Keys) {
        $todos = Cuentan $lineas $campo
        if ($todos.Count -eq 0) { $datos[$campo] = $null; continue }

        $deRecientes = Cuentan $recientes $campo
        $elegido = (Contar $(if ($deRecientes.Count -gt 0) { $deRecientes } else { $todos }))[0].Valor
        $datos[$campo] = if ($elegido -eq '') { $null } else { $elegido }

        # La fecha de revisión no se apunta: es la que decide qué filas mandan
        $grupos = Contar $todos
        if ($grupos.Count -gt 1 -and $campo -ne 'FechaRevision') {
            $detalle = ($grupos | ForEach-Object { "$(Mostrar $_.Valor $campo) ($($_.Veces) $(if ($_.Veces -eq 1) { 'fila' } else { 'filas' }))" }) -join ', '
            $revisar.Add("$($NOMBRES_CAMPO[$campo]): $detalle. Se ha guardado: $(Mostrar $elegido $campo)")
        }
    }

    $proveedores.Add([pscustomobject]@{
        Numero = $lineas[0].Numero
        Datos = $datos
        Revisar = if ($revisar.Count -gt 0) { $revisar -join "`n" } else { $null }
        Filas = $lineas
    })
}

Write-Host ("Filas leídas: {0}. Proveedores: {1} ({2} con datos distintos según la fila)." -f $filas.Count,
    $proveedores.Count, @($proveedores | Where-Object Revisar).Count)

# Escritura en la base de datos

function Valor($p, $nombre, $valor, $tipo) {
    $par = $p.Add($nombre, $tipo)
    $par.Value = if ($null -eq $valor) { [DBNull]::Value } else { $valor }
}

$bd = New-Object System.Data.SqlClient.SqlConnection $Conexion
$bd.Open()
try {
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM USUARIO WHERE id = @id'
    [void]$cmd.Parameters.AddWithValue('@id', $UsuarioId)
    if ([int]$cmd.ExecuteScalar() -eq 0) { throw "No existe el usuario con id $UsuarioId." }

    $cmd = $bd.CreateCommand()
    $cmd.CommandText = 'SELECT COUNT(*) FROM PROVEEDOR'
    $existentes = [int]$cmd.ExecuteScalar()
    if ($existentes -gt 0) { throw "PROVEEDOR ya tiene $existentes proveedores. Para no duplicarlos no se importa nada." }

    # Los triggers de historial necesitan saber quién hace el cambio
    $cmd = $bd.CreateCommand()
    $cmd.CommandText = "EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;"
    [void]$cmd.Parameters.AddWithValue('@usuario', $UsuarioId)
    [void]$cmd.ExecuteNonQuery()

    $S = [Data.SqlDbType]
    $productos = 0
    $transaccion = $bd.BeginTransaction()
    try {
        foreach ($pr in $proveedores) {
            $d = $pr.Datos
            $cmd = New-Object System.Data.SqlClient.SqlCommand(@'
INSERT INTO PROVEEDOR (numero, nombre, fecha_alta, baja, tipo, fecha_revision, impacto, estatus, reclamaciones,
    certificados, ensayo_trazabilidad, auditoria, rfa, relacion, historial, subcontratado, revisar, creado_por)
VALUES (@numero, @nombre, @alta, @baja, @tipo, @revision, @impacto, @estatus, @reclamaciones,
    @certificados, @ensayo, @auditoria, @rfa, @relacion, @historial, @subcontratado, @revisar, @usuario);
SELECT CAST(SCOPE_IDENTITY() AS INT);   -- OUTPUT no se puede usar: la tabla tiene trigger
'@, $bd, $transaccion)
            $p = $cmd.Parameters
            Valor $p '@numero' $pr.Numero $S::Int
            Valor $p '@nombre' $d.Nombre $S::NVarChar
            Valor $p '@alta' $(if ($d.FechaAlta) { [DateTime]$d.FechaAlta }) $S::Date
            Valor $p '@baja' ([bool]$d.Baja) $S::Bit
            Valor $p '@tipo' $d.Tipo $S::NVarChar
            Valor $p '@revision' $(if ($d.FechaRevision) { [DateTime]$d.FechaRevision }) $S::Date
            foreach ($par in @(@('@impacto', 'Impacto'), @('@estatus', 'Estatus'), @('@reclamaciones', 'Reclamaciones'),
                               @('@relacion', 'Relacion'), @('@historial', 'Historial'), @('@subcontratado', 'Subcontratado'))) {
                Valor $p $par[0] $(if ($null -ne $d[$par[1]]) { [byte]$d[$par[1]] }) $S::TinyInt
            }
            Valor $p '@certificados' $d.Certificados $S::NVarChar
            Valor $p '@ensayo' $d.Ensayo $S::NVarChar
            Valor $p '@auditoria' $d.Auditoria $S::NVarChar
            Valor $p '@rfa' $d.Rfa $S::NVarChar
            Valor $p '@revisar' $pr.Revisar $S::NVarChar
            Valor $p '@usuario' $UsuarioId $S::Int
            try { $id = [int]$cmd.ExecuteScalar() }
            catch { throw "Proveedor $($pr.Numero) $($d.Nombre): $($_.Exception.Message)" }

            foreach ($linea in $pr.Filas) {
                $x = $linea.Producto
                if (-not $x.Codigo -and -not $x.Descripcion) { continue }   # fila solo con datos del proveedor
                $cmd = New-Object System.Data.SqlClient.SqlCommand(@'
INSERT INTO PROVEEDOR_PRODUCTO (proveedor_id, codigo, descripcion, tipo_producto, fecha_alta, baja, alergenos, posibles_alergenos,
    fecha_revision, ficha_tecnica, analisis_quimicos, analisis_micro, analisis_alergenos,
    eurochoc_fecha, eurochoc_alcance, eurochoc_resultado, impacto, control_recepcion, reclamaciones, parametro,
    p_historial, p_motivacion, p_facilidad, d_origen, d_cadena, d_medidas, medidas_preventivas, observaciones, creado_por)
VALUES (@proveedor, @codigo, @descripcion, @tipo, @alta, @baja, @alergenos, @posibles,
    @revision, @ft, @quimicos, @micro, @analergenos,
    @efecha, @ealcance, @eresultado, @impacto, @control, @reclamaciones, @parametro,
    @ph, @pm, @pf, @do, @dc, @dm, @medidas, @observaciones, @usuario);
'@, $bd, $transaccion)
                $p = $cmd.Parameters
                Valor $p '@proveedor' $id $S::Int
                Valor $p '@codigo' $x.Codigo $S::NVarChar
                Valor $p '@descripcion' $x.Descripcion $S::NVarChar
                Valor $p '@tipo' $x.TipoProducto $S::NVarChar
                Valor $p '@alta' $x.ProdAlta $S::Date
                Valor $p '@baja' $x.Baja $S::Bit
                Valor $p '@alergenos' $x.Alergenos $S::NVarChar
                Valor $p '@posibles' $x.PosiblesAlergenos $S::NVarChar
                Valor $p '@revision' $x.ProdRevision $S::Date
                Valor $p '@ft' $x.FichaTecnica $S::NVarChar
                Valor $p '@quimicos' $x.Quimicos $S::NVarChar
                Valor $p '@micro' $x.Micro $S::NVarChar
                Valor $p '@analergenos' $x.AnalisisAlergenos $S::NVarChar
                Valor $p '@efecha' $x.EurochocFecha $S::Date
                Valor $p '@ealcance' $x.EurochocAlcance $S::NVarChar
                Valor $p '@eresultado' $x.EurochocResultado $S::NVarChar
                foreach ($par in @(@('@impacto', 'Impacto'), @('@control', 'ControlRecepcion'), @('@reclamaciones', 'ProdReclamaciones'),
                                   @('@ph', 'PHistorial'), @('@pm', 'PMotivacion'), @('@pf', 'PFacilidad'),
                                   @('@do', 'DOrigen'), @('@dc', 'DCadena'), @('@dm', 'DMedidas'))) {
                    Valor $p $par[0] $(if ($null -ne $x[$par[1]]) { [byte]$x[$par[1]] }) $S::TinyInt
                }
                Valor $p '@parametro' $x.Parametro $S::NVarChar
                Valor $p '@medidas' $x.Medidas $S::NVarChar
                Valor $p '@observaciones' $x.Observaciones $S::NVarChar
                Valor $p '@usuario' $UsuarioId $S::Int
                try { [void]$cmd.ExecuteNonQuery() }
                catch { throw "Fila $($linea.Fila): $($_.Exception.Message)" }
                $productos++
            }
        }

        if ($Probar) {
            $transaccion.Rollback()
            Write-Host "PRUEBA: se habrían importado $($proveedores.Count) proveedores y $productos productos. No se ha guardado nada." -ForegroundColor Yellow
        }
        else {
            $transaccion.Commit()
            Write-Host "Importados $($proveedores.Count) proveedores y $productos productos." -ForegroundColor Green
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

$distintos = @($proveedores | Where-Object Revisar)
if ($distintos.Count -gt 0) {
    Write-Host "`nProveedores con datos distintos según la fila ($($distintos.Count)); en la app salen como 'Revisar':" -ForegroundColor Yellow
    foreach ($pr in $distintos) {
        Write-Host "  $($pr.Numero) $($pr.Datos.Nombre)"
        $pr.Revisar -split "`n" | ForEach-Object { Write-Host "      $_" }
    }
}

if ($avisos.Count -gt 0) {
    Write-Host "`nAvisos ($($avisos.Count)):" -ForegroundColor Yellow
    $avisos | ForEach-Object { Write-Host "  $_" }
}
