using System.Data.Common;
using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Ficha de un producto de un proveedor, nuevo o ya guardado. El muestreo se calcula con las notas del producto y
// el estatus del proveedor; el riesgo de fraude, solo con las del producto (como en cada fila del Excel).
public class FormProductoProveedor : Form
{
    private readonly ProveedorServicio servicio = new();
    private readonly CamposFormulario campos = new();
    private readonly Proveedor proveedor;
    private readonly ProductoProveedor original;
    private bool hayCambios;

    private readonly TextBox txtCodigo = new();
    private readonly TextBox txtDescripcion = new();
    private readonly ComboBox cmbEstado = CamposProveedor.ComboEstado();
    private readonly ComboBox cmbTipo;
    private readonly DateTimePicker dtpAlta = CamposProveedor.Fecha();
    private readonly DateTimePicker dtpRevision = CamposProveedor.Fecha();
    private readonly TextBox txtAlergenos = new();
    private readonly TextBox txtPosibles = new();

    private readonly TextBox txtFicha = new();
    private readonly TextBox txtQuimicos = new();
    private readonly TextBox txtMicro = new();
    private readonly TextBox txtAnalisisAlergenos = new();
    private readonly DateTimePicker dtpEurochoc = CamposProveedor.Fecha();
    private readonly TextBox txtEurochocAlcance = new();
    private readonly TextBox txtEurochocResultado = CamposProveedor.Multilinea();

    private readonly ComboBox cmbImpacto;
    private readonly ComboBox cmbControl = CamposProveedor.ComboNota(1, 3);
    private readonly ComboBox cmbReclamaciones = CamposProveedor.ComboNota(0, 2);
    private readonly TextBox txtParametro = CamposProveedor.Multilinea();
    private readonly Label lblEstatusProducto = new();
    private readonly Label lblMuestreo = new();
    private readonly Label lblFaltaMuestreo = new();
    private readonly Label lblAlcance = new();

    private readonly ComboBox cmbPHistorial = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbPMotivacion = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbPFacilidad = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbDOrigen = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbDCadena = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbDMedidas = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbRelacion = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbHistorial = CamposProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbSubcontratado = CamposProveedor.ComboNota(0, 3);
    private readonly TextBox txtMedidas = CamposProveedor.Multilinea();
    private readonly Label lblOxD = new();
    private readonly Label lblVulnerabilidad = new();
    private readonly Label lblPuntuacion = new();
    private readonly Label lblRiesgo = new();
    private readonly Label lblFaltaRiesgo = new();
    private readonly Label lblActuacion = new();

    private readonly TextBox txtObservaciones = CamposProveedor.Multilinea();

    public int IdGuardado { get; private set; }

    // producto == null: producto nuevo de este proveedor
    public FormProductoProveedor(Proveedor proveedor, ProductoProveedor? producto)
    {
        this.proveedor = proveedor;
        bool nuevo = producto == null;
        original = producto ?? new ProductoProveedor(0, proveedor.Id, null, null, null, DateTime.Today, false, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null)
            // Nuevo: empieza con el impacto del proveedor y, de relación, su estatus (como la fórmula del Excel)
            with { Impacto = proveedor.Impacto, Relacion = proveedor.Estatus };
        cmbImpacto = CamposProveedor.ComboNota(1, 3);
        cmbTipo = CamposProveedor.ComboTipo(TipoProductoProveedor.Todos, original.TipoProducto);

        Text = nuevo ? $"Nuevo producto - {proveedor.Nombre}" : $"Producto {original.Codigo} - {original.Descripcion}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 650);
        Componentes.AjustarAPantalla(this, 0.8);
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var (scroll, pila) = Componentes.Pagina();
        string ruta = $"PROVEEDORES  ·  Nº {proveedor.Numero?.ToString() ?? CamposProveedor.SinNota}  ·  {proveedor.Nombre}";
        pila.Controls.Add(nuevo
            ? Componentes.Cabecera(ruta, "Nuevo producto", "Rellena sus datos y guárdalo.")
            : Componentes.Cabecera(ruta, original.Descripcion ?? original.Codigo ?? "Producto",
                string.Join("  ·  ", new[] { original.Codigo, original.TipoProducto, original.Baja ? CamposProveedor.DeBaja : CamposProveedor.Activo }.OfType<string>())));

        pila.Controls.Add(CamposProveedor.Marcada(TarjetaDatos(), EstiloProveedores.RayaDatos));
        pila.Controls.Add(CamposProveedor.Marcada(TarjetaEurochoc(), EstiloProveedores.RayaProductos));
        pila.Controls.Add(CamposProveedor.Marcada(TarjetaMuestreo(), EstiloProveedores.RayaEvaluacion));
        pila.Controls.Add(CamposProveedor.Marcada(TarjetaFraude(), EstiloProveedores.RayaRiesgo));
        pila.Controls.Add(CamposProveedor.Marcada(TarjetaRiesgoProveedor(), EstiloProveedores.RayaRiesgo));
        pila.Controls.Add(CamposProveedor.Marcada(TarjetaMitigacion(), EstiloProveedores.RayaRiesgo));
        // No es un apartado del Excel: guarda lo que al pasar el Excel no cabía en su columna
        var observaciones = Componentes.Tarjeta("Observaciones");
        txtObservaciones.Dock = DockStyle.Top;
        txtObservaciones.Height = 80;
        observaciones.Controls.Add(txtObservaciones);
        pila.Controls.Add(observaciones);

        var btnGuardar = Tema.BotonPrincipal(nuevo ? "Guardar producto" : "Guardar cambios", 190);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 140);
        btnCancelar.Click += (s, e) => Close();
        pila.Controls.Add(Componentes.Acciones(btnGuardar, btnCancelar));

        Controls.Add(scroll);

        Poner(original);
        ActualizarNotas();

        CamposProveedor.AlCambiar(this, () =>
        {
            hayCambios = true;
            ActualizarNotas();
        });
        FormClosing += AvisarCambiosSinGuardar;

        Shown += (s, e) =>
        {
            scroll.AutoScrollPosition = Point.Empty;
            (nuevo ? txtCodigo : txtDescripcion).Focus();
        };
    }

    // Tarjetas

    // Cada tarjeta es un apartado de la hoja PRODUCTOS del Excel (fila 1), con sus columnas y en su orden

    // DATOS SOBRE PRODUCTOS (columnas O-Z)
    private Control TarjetaDatos()
    {
        var tarjeta = Componentes.Tarjeta("Datos sobre productos");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Código", txtCodigo));
        CamposProveedor.Ancho(rejilla, campos.Campo("Descripción", txtDescripcion), 2);
        rejilla.Controls.Add(campos.Campo("Tipo producto", cmbTipo));
        rejilla.Controls.Add(campos.Campo("Alta", dtpAlta));
        rejilla.Controls.Add(campos.Campo("Estado producto", cmbEstado));
        CamposProveedor.Ancho(rejilla, campos.Campo("Alérgenos", txtAlergenos), 2);
        CamposProveedor.Ancho(rejilla, campos.Campo("Posibles alérgenos", txtPosibles), 2);
        rejilla.Controls.Add(campos.Campo("Revisión", dtpRevision));
        rejilla.Controls.Add(CamposProveedor.Hueco());
        rejilla.Controls.Add(campos.Campo("FT (año)", txtFicha));
        rejilla.Controls.Add(campos.Campo("A. químicos / Declaración conformidad", txtQuimicos));
        rejilla.Controls.Add(campos.Campo("A. micro. / Ensayo migración", txtMicro));
        rejilla.Controls.Add(campos.Campo("A. alérgenos / Formaldehído", txtAnalisisAlergenos));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // ANALISIS DE EUROCHOC (columnas AA-AC)
    private Control TarjetaEurochoc()
    {
        var tarjeta = Componentes.Tarjeta("Análisis de Eurochoc");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Fecha", dtpEurochoc));
        CamposProveedor.Ancho(rejilla, campos.Campo("Alcance", txtEurochocAlcance), 3);
        CamposProveedor.Ancho(rejilla, campos.Campo("Resultado", txtEurochocResultado), 4);
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // EVALUACIÓN DE LA FRECUENCIA Y EL ALCANCE DEL MUESTREO... (columnas AD-AJ)
    private Control TarjetaMuestreo()
    {
        var tarjeta = Componentes.Tarjeta(
            "Evaluación de la frecuencia y el alcance del muestreo de verificación de MMPP, M. Aux. y productos terminados",
            "Frecuencia muestreo: impacto + estatus + control recepción + reclamaciones; 1-3 A, 4-6 B, 7-9 C y 10-12 D.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Impacto del producto", cmbImpacto));
        // En el Excel es una fórmula: el estatus del proveedor
        rejilla.Controls.Add(CamposProveedor.DatoTexto("Estatus del producto", lblEstatusProducto));
        rejilla.Controls.Add(campos.Campo("Control recepción", cmbControl));
        rejilla.Controls.Add(campos.Campo("Reclamaciones", cmbReclamaciones));
        // Las columnas calculadas del Excel, con su nombre. No se escriben: salen de las notas.
        rejilla.Controls.Add(CamposProveedor.DatoNota("Frecuencia muestreo", lblMuestreo, lblFaltaMuestreo));
        CamposProveedor.Ancho(rejilla, CamposProveedor.DatoTexto("Alcance muestreo", lblAlcance, 900), 3);
        CamposProveedor.Ancho(rejilla, campos.Campo("Parámetro", txtParametro), 4);
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // ANÁLISIS DE LA VULNERABILIDAD - FRAUDE ALIMENTARIO (columnas AK-AR)
    private Control TarjetaFraude()
    {
        var tarjeta = Componentes.Tarjeta("Análisis de la vulnerabilidad - Fraude alimentario",
            "O * D: la ocurrencia (P.) más alta por la detección (D.) más alta.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("P. historial", cmbPHistorial));
        rejilla.Controls.Add(campos.Campo("P. motivación económica", cmbPMotivacion));
        rejilla.Controls.Add(campos.Campo("P. facilidad", cmbPFacilidad));
        rejilla.Controls.Add(CamposProveedor.DatoTexto("O * D", lblOxD));
        rejilla.Controls.Add(campos.Campo("D. origen", cmbDOrigen));
        rejilla.Controls.Add(campos.Campo("D. cadena suministro", cmbDCadena));
        rejilla.Controls.Add(campos.Campo("D. medidas control", cmbDMedidas));
        rejilla.Controls.Add(CamposProveedor.DatoNota("Riesgo", lblVulnerabilidad));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // RIESGO DEL PROVEEDOR PARA COMETER FRAUDE (columnas AS-AW)
    private Control TarjetaRiesgoProveedor()
    {
        var tarjeta = Componentes.Tarjeta("Riesgo del proveedor para cometer fraude",
            "Puntuación: O * D por el valor más alto de los tres; 1-6 BAJO, 7-12 MEDIO y 13-27 ALTO.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Relación", cmbRelacion));
        rejilla.Controls.Add(campos.Campo("Historial", cmbHistorial));
        rejilla.Controls.Add(campos.Campo("P. subcontratado", cmbSubcontratado));
        rejilla.Controls.Add(CamposProveedor.Hueco());
        rejilla.Controls.Add(CamposProveedor.DatoTexto("Puntuación", lblPuntuacion));
        rejilla.Controls.Add(CamposProveedor.DatoNota("Riesgo fraude", lblRiesgo, lblFaltaRiesgo));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // PLAN DE MITIGACIÓN (columnas AX-AY)
    private Control TarjetaMitigacion()
    {
        var tarjeta = Componentes.Tarjeta("Plan de mitigación",
            "Actuación control: sale del riesgo fraude (ALTO, dos lotes aleatorios al año; MEDIO, un lote al año; BAJO, sin análisis).");
        var rejilla = Componentes.Rejilla(4);
        CamposProveedor.Ancho(rejilla, CamposProveedor.DatoTexto("Actuación control", lblActuacion, 900), 4);
        CamposProveedor.Ancho(rejilla, campos.Campo("Medidas preventivas de control", txtMedidas), 4);
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Leer y poner

    private void Poner(ProductoProveedor x)
    {
        txtCodigo.Text = x.Codigo ?? "";
        txtDescripcion.Text = x.Descripcion ?? "";
        CamposProveedor.PonerEstado(cmbEstado, x.Baja);
        CamposProveedor.PonerTipo(cmbTipo, x.TipoProducto);
        CamposProveedor.PonerFecha(dtpAlta, x.FechaAlta);
        CamposProveedor.PonerFecha(dtpRevision, x.FechaRevision);
        txtAlergenos.Text = x.Alergenos ?? "";
        txtPosibles.Text = x.PosiblesAlergenos ?? "";
        txtFicha.Text = x.FichaTecnica ?? "";
        txtQuimicos.Text = x.AnalisisQuimicos ?? "";
        txtMicro.Text = x.AnalisisMicro ?? "";
        txtAnalisisAlergenos.Text = x.AnalisisAlergenos ?? "";
        CamposProveedor.PonerFecha(dtpEurochoc, x.EurochocFecha);
        txtEurochocAlcance.Text = x.EurochocAlcance ?? "";
        txtEurochocResultado.Text = x.EurochocResultado ?? "";
        CamposProveedor.PonerNota(cmbImpacto, x.Impacto);
        CamposProveedor.PonerNota(cmbControl, x.ControlRecepcion);
        CamposProveedor.PonerNota(cmbReclamaciones, x.Reclamaciones);
        txtParametro.Text = x.Parametro ?? "";
        CamposProveedor.PonerNota(cmbPHistorial, x.PHistorial);
        CamposProveedor.PonerNota(cmbPMotivacion, x.PMotivacion);
        CamposProveedor.PonerNota(cmbPFacilidad, x.PFacilidad);
        CamposProveedor.PonerNota(cmbDOrigen, x.DOrigen);
        CamposProveedor.PonerNota(cmbDCadena, x.DCadena);
        CamposProveedor.PonerNota(cmbDMedidas, x.DMedidas);
        CamposProveedor.PonerNota(cmbRelacion, x.Relacion);
        CamposProveedor.PonerNota(cmbHistorial, x.Historial);
        CamposProveedor.PonerNota(cmbSubcontratado, x.Subcontratado);
        txtMedidas.Text = x.MedidasPreventivas ?? "";
        txtObservaciones.Text = x.Observaciones ?? "";
    }

    private ProductoProveedor Leer() => original with
    {
        Codigo = txtCodigo.Text,
        Descripcion = txtDescripcion.Text,
        Baja = CamposProveedor.LeerBaja(cmbEstado),
        TipoProducto = CamposProveedor.LeerTipo(cmbTipo),
        FechaAlta = CamposProveedor.LeerFecha(dtpAlta),
        FechaRevision = CamposProveedor.LeerFecha(dtpRevision),
        Alergenos = txtAlergenos.Text,
        PosiblesAlergenos = txtPosibles.Text,
        FichaTecnica = txtFicha.Text,
        AnalisisQuimicos = txtQuimicos.Text,
        AnalisisMicro = txtMicro.Text,
        AnalisisAlergenos = txtAnalisisAlergenos.Text,
        EurochocFecha = CamposProveedor.LeerFecha(dtpEurochoc),
        EurochocAlcance = txtEurochocAlcance.Text,
        EurochocResultado = txtEurochocResultado.Text,
        Impacto = CamposProveedor.LeerNota(cmbImpacto),
        ControlRecepcion = CamposProveedor.LeerNota(cmbControl),
        Reclamaciones = CamposProveedor.LeerNota(cmbReclamaciones),
        Parametro = txtParametro.Text,
        PHistorial = CamposProveedor.LeerNota(cmbPHistorial),
        PMotivacion = CamposProveedor.LeerNota(cmbPMotivacion),
        PFacilidad = CamposProveedor.LeerNota(cmbPFacilidad),
        DOrigen = CamposProveedor.LeerNota(cmbDOrigen),
        DCadena = CamposProveedor.LeerNota(cmbDCadena),
        DMedidas = CamposProveedor.LeerNota(cmbDMedidas),
        Relacion = CamposProveedor.LeerNota(cmbRelacion),
        Historial = CamposProveedor.LeerNota(cmbHistorial),
        Subcontratado = CamposProveedor.LeerNota(cmbSubcontratado),
        MedidasPreventivas = txtMedidas.Text,
        Observaciones = txtObservaciones.Text
    };

    // Datos calculados con lo que hay escrito
    private void ActualizarNotas()
    {
        var x = Leer();
        lblEstatusProducto.Text = proveedor.Estatus?.ToString() ?? CamposProveedor.SinNota;

        string? muestreo = ProveedorServicio.FrecuenciaMuestreo(proveedor, x);
        CamposProveedor.PintarNota(lblMuestreo, muestreo);
        lblFaltaMuestreo.Text = muestreo == null ? "Falta el impacto, el estatus del proveedor o el control en recepción" : "";
        lblFaltaMuestreo.Visible = muestreo == null;
        lblAlcance.Text = ProveedorServicio.AlcanceMuestreo(muestreo) ?? CamposProveedor.SinNota;

        lblOxD.Text = ProveedorServicio.OcurrenciaPorDeteccion(x).ToString();
        CamposProveedor.PintarNota(lblVulnerabilidad, ProveedorServicio.RiesgoVulnerabilidad(x));
        lblPuntuacion.Text = ProveedorServicio.Puntuacion(x).ToString();

        string? riesgo = ProveedorServicio.RiesgoFraude(x);
        CamposProveedor.PintarNota(lblRiesgo, riesgo);
        lblFaltaRiesgo.Text = riesgo == null ? "Faltan las notas de ocurrencia y detección, o la relación, el historial o el P. subcontratado" : "";
        lblFaltaRiesgo.Visible = riesgo == null;
        lblActuacion.Text = ProveedorServicio.Actuacion(riesgo) ?? CamposProveedor.SinNota;
    }

    // Guardado

    private void Guardar()
    {
        campos.LimpiarErrores();
        var leido = ProveedorServicio.Normalizar(Leer());

        var controles = new Dictionary<string, Control>
        {
            [nameof(ProductoProveedor.Codigo)] = txtCodigo,
            [nameof(ProductoProveedor.Descripcion)] = txtDescripcion,
            [nameof(ProductoProveedor.Alergenos)] = txtAlergenos,
            [nameof(ProductoProveedor.PosiblesAlergenos)] = txtPosibles,
            [nameof(ProductoProveedor.FichaTecnica)] = txtFicha,
            [nameof(ProductoProveedor.AnalisisQuimicos)] = txtQuimicos,
            [nameof(ProductoProveedor.AnalisisMicro)] = txtMicro,
            [nameof(ProductoProveedor.AnalisisAlergenos)] = txtAnalisisAlergenos,
            [nameof(ProductoProveedor.EurochocAlcance)] = txtEurochocAlcance,
            [nameof(ProductoProveedor.EurochocResultado)] = txtEurochocResultado,
            [nameof(ProductoProveedor.Parametro)] = txtParametro,
            [nameof(ProductoProveedor.MedidasPreventivas)] = txtMedidas
        };
        Control? primerError = null;
        foreach (var (campo, mensaje) in ProveedorServicio.ErroresProducto(leido))
        {
            campos.MarcarError(controles[campo], mensaje);
            primerError ??= controles[campo];
        }
        if (primerError != null)
        {
            primerError.Focus();
            return;
        }

        try
        {
            IdGuardado = servicio.GuardarProducto(leido);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede guardar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar el producto.", ex);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (!Mensajes.DescartarCambios(this, "Hay cambios sin guardar. ¿Salir sin guardarlos?"))
            e.Cancel = true;
    }
}
