using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;

// Ficha de una incidencia, NC o acción de mejora (fila de la hoja INCIDENCIAS).
// Una tarjeta por fase, todas a la vista una debajo de otra; la barra de fases marca lo hecho y salta a cada una.
public class FormNoConformidad : Form
{
    private readonly NoConformidadServicio servicio = new();
    private readonly NoConformidad? original;   // null si es nuevo
    private readonly NoConformidad? borrador;

    // Si otro GPE tiene la ficha abierta, aquí solo se consulta
    private readonly BloqueoNc? bloqueo;
    private bool SoloLectura => bloqueo != null;
    private readonly System.Windows.Forms.Timer renovarBloqueo = new();

    public int IdGuardado { get; private set; }

    private Dictionary<string, List<OpcionLista>> listas = new();
    private List<ProveedorDeLista> proveedores = new();
    private bool cargaCorrecta;
    private bool hayCambios;

    // Opción vacía de los desplegables que no son obligatorios
    private static readonly OpcionLista SinIndicar = new("—", null);

    // Identificación
    private readonly FlowLayoutPanel selectorTipo = new();
    private readonly Dictionary<string, RadioButton> botonesTipo = new();
    private readonly DateTimePicker dtpDetectado = new();
    private readonly TextBox txtNumero = new();
    private readonly Label lblNumero = new();
    private readonly ComboBox cmbOrigen = new();
    private readonly ComboBox cmbSeccion = new();
    private readonly ComboBox cmbTipo = new();
    private readonly TextBox txtCliente = new();
    private readonly ComboBox cmbProveedor = new();
    // Solo NC de un proveedor de la lista: cuenta para su nota de reclamaciones
    private readonly ComboBox cmbSeguridad = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private const string SeguridadSi = "Sí, afecta";
    private const string SeguridadNo = "No afecta";

    // Descripción y acción inmediata
    private readonly TextBox txtProblema = new();
    private readonly TextBox txtCausa = new();
    private readonly DateTimePicker dtpPrevista = new();
    private readonly DateTimePicker dtpFin = new();

    // Solo incidencias
    private readonly ComboBox cmbMcPccBpm = new();
    private readonly ComboBox cmbParametro = new();
    private readonly ComboBox cmbOperario = new();

    // Análisis y acción correctora
    private readonly TextBox[] txtPreguntas = NuevasCajas();
    private readonly TextBox[] txtRespuestas = NuevasCajas();
    private readonly ComboBox cmbCausaRaiz = new();
    private readonly ComboBox cmbCapitulo = new();
    private readonly TextBox txtAnalisis = new();
    // Textos libres de antes de los 5 porqués: solo se ven en los registros que los tienen
    private Control campoCausaAnterior = null!;
    private Control campoAnalisisAnterior = null!;
    private readonly TextBox txtCoste = new();
    private readonly TextBox txtHoras = new();

    // Acciones inmediatas, correctoras y seguimientos: una tabla cada uno
    private TablaAcciones tablaInmediatas = null!;
    private TablaAcciones tablaCorrectoras = null!;
    private TablaAcciones tablaSeguimientos = null!;
    private readonly ControlFotosNc fotos = new();
    private List<AccionNc> acciones = new();

    private readonly Label lblEstado = new();

    private readonly CamposFormulario campos = new();
    private Control campoTipo = null!;
    private Control campoSeguridad = null!;
    private Control campoPorQues = null!;   // solo en las NC
    private readonly Label lblSubtituloAnalisis = new();
    private Control tarjetaIncidencia = null!;

    private readonly BarraFasesCalidad barraFases = new();
    private List<EstadoFase> fases = new();
    private readonly Dictionary<string, Control> tarjetasFase = new();
    private Panel scroll = null!;

    // borrador: valores con los que empieza uno nuevo (una incidencia abierta desde la rutina de calidad)
    public FormNoConformidad(NoConformidad? registro = null, NoConformidad? borrador = null)
    {
        original = registro;
        this.borrador = registro == null ? borrador : null;
        if (registro != null)
            bloqueo = Bloquear(registro.Id);

        Text = registro == null ? "Nuevo registro" : Titulo(registro) + (SoloLectura ? " (solo lectura)" : "");
        Size = new Size(1200, 800);
        MinimumSize = new Size(1100, 600);
        StartPosition = FormStartPosition.CenterParent;
        Componentes.AjustarAPantalla(this);
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        PrepararControles();
        Controls.Add(CrearContenido());
        if (SoloLectura) Controls.Add(CrearFranjaBloqueo());
        Controls.Add(CrearFranjaFases());   // después: así se coloca arriba y el contenido debajo
        CargarValores();
        fotos.Cargar(original?.Id ?? 0);
        ActualizarSegunTipo();
        ActualizarEstado();

        // Las NC de auditoría se numeran por año o por fecha: el número cambia con ellas
        cmbOrigen.SelectedIndexChanged += (s, e) => ActualizarSecciones();
        cmbProveedor.SelectedIndexChanged += (s, e) => ActualizarCampoSeguridad();
        cmbSeccion.SelectedIndexChanged += (s, e) => ActualizarNumero();
        dtpDetectado.ValueChanged += (s, e) => ActualizarNumero();

        // A partir de aquí, cualquier cambio cuenta como "sin guardar"
        foreach (var control in Entradas(this))
        {
            switch (control)
            {
                case ComboBox combo: combo.SelectedIndexChanged += (s, e) => hayCambios = true; break;
                case RadioButton opcion: opcion.CheckedChanged += (s, e) => hayCambios = true; break;
                case DateTimePicker fecha: fecha.ValueChanged += (s, e) => hayCambios = true; break;
                default: control.TextChanged += (s, e) => hayCambios = true; break;
            }
        }
        tablaInmediatas.Cambiado += (s, e) => hayCambios = true;
        tablaCorrectoras.Cambiado += (s, e) => hayCambios = true;
        tablaSeguimientos.Cambiado += (s, e) => hayCambios = true;
        fotos.Cambiado += (s, e) => hayCambios = true;
        FormClosing += AvisarCambiosSinGuardar;
        // Lo que trae el borrador también se pierde si se cierra sin guardar
        hayCambios = borrador != null;

        // La barra de fases se pone al día con lo que decide cada fase
        foreach (var texto in new[] { txtProblema, txtCausa, txtAnalisis }.Concat(txtPreguntas).Concat(txtRespuestas))
            texto.TextChanged += (s, e) => ActualizarFases();
        cmbCausaRaiz.SelectedIndexChanged += (s, e) => ActualizarFases();
        dtpFin.ValueChanged += (s, e) => ActualizarFases();
        tablaInmediatas.Cambiado += (s, e) => ActualizarFases();
        tablaCorrectoras.Cambiado += (s, e) => ActualizarFases();
        barraFases.FasePulsada += IrAFase;

        if (SoloLectura)
        {
            BloquearEntradas();
            Shown += (s, e) => Mensajes.Informacion(this, "Ficha abierta por otra persona",
                NoConformidadServicio.DescribirBloqueo(bloqueo!) + "\n\n" +
                "Para que no se pisen los cambios, se abre en solo lectura. " +
                "Cuando la cierre, vuelve a abrirla para poder editarla.");
        }
        else if (original != null)
        {
            int id = original.Id;
            renovarBloqueo.Interval = NoConformidadServicio.SegundosRenovarBloqueo * 1000;
            renovarBloqueo.Tick += (s, e) => Bloquear(id);
            renovarBloqueo.Start();
            FormClosed += (s, e) => Desbloquear(id);
        }
    }

    // Ficha abierta por otra persona

    // Si falla la base se deja editar: la versión sigue impidiendo pisar cambios de otro
    private BloqueoNc? Bloquear(int id)
    {
        try
        {
            return servicio.Bloquear(id);
        }
        catch (Exception ex) when (ex is DbException or ReglaNegocioException)
        {
            return null;
        }
    }

    private void Desbloquear(int id)
    {
        renovarBloqueo.Dispose();
        try
        {
            servicio.Desbloquear(id);
        }
        catch (DbException)
        {
            // Si no se puede quitar, caduca solo a los pocos minutos
        }
    }

    // Franja amarilla fija bajo la barra de fases: quién la tiene abierta
    private Control CrearFranjaBloqueo()
    {
        var franja = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Tema.AvisoFondo,
            Padding = new Padding(40, 0, 40, 0)
        };
        franja.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = NoConformidadServicio.DescribirBloqueo(bloqueo!) + " Solo puedes consultarla.",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Tema.Aviso,
            Font = Tema.FuenteNegrita,
            AutoEllipsis = true
        });
        return franja;
    }

    // Deja la ficha solo para consultar: los textos se pueden leer y copiar, pero no cambiar
    private void BloquearEntradas()
    {
        foreach (var control in Entradas(this).ToList())
        {
            if (control is TextBox texto)
            {
                texto.ReadOnly = true;
                texto.BackColor = Color.White;
            }
            else
                control.Enabled = false;
        }
        tablaInmediatas.SoloLectura = true;
        tablaCorrectoras.SoloLectura = true;
        tablaSeguimientos.SoloLectura = true;
        fotos.SoloLectura = true;
    }

    private static string Titulo(NoConformidad nc) =>
        nc.Numero == null
            ? $"{TipoRegistro.Nombre(nc.TipoRegistro)} del {Componentes.Fecha(nc.FechaDetectado)}"
            : $"{TipoRegistro.Nombre(nc.TipoRegistro)} {nc.Numero}";

    // Preparación de controles

    private void PrepararControles()
    {
        List<string> clientes = new();
        try
        {
            listas = servicio.ObtenerListas();
            clientes = servicio.ObtenerClientes();
            proveedores = servicio.ObtenerProveedores();
            if (original != null) acciones = servicio.ObtenerAcciones(original.Id);
            cargaCorrecta = true;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar las listas de la base de datos.", ex);
        }

        PrepararSelectorTipo();

        dtpDetectado.Format = DateTimePickerFormat.Short;
        dtpDetectado.MaxDate = DateTime.Today;

        foreach (var fecha in new[] { dtpPrevista, dtpFin })
        {
            fecha.Format = DateTimePickerFormat.Short;
            fecha.ShowCheckBox = true;   // sin marcar = sin fecha
            fecha.Checked = false;
            fecha.ValueChanged += (s, e) => ActualizarEstado();
        }
        dtpFin.MaxDate = DateTime.Today;

        // El número no se escribe: lo calcula la aplicación (ActualizarNumero)
        txtNumero.ReadOnly = true;
        txtNumero.TabStop = false;
        lblNumero.AutoSize = true;
        lblNumero.ForeColor = Tema.Suave;
        lblNumero.Font = Tema.FuenteNota;
        lblNumero.Margin = new Padding(0, 4, 0, 0);

        Sugerencias(txtCliente, clientes);

        Multilinea(txtProblema, 90, "Qué se ha detectado: producto, lote, cliente...");
        Multilinea(txtCausa, 70, "");
        Multilinea(txtAnalisis, 70, "");
        for (int i = 0; i < NoConformidad.NumeroPorQues; i++)
        {
            Multilinea(txtPreguntas[i], 46, i == 0 ? "¿Por qué ha pasado...?" : "¿Por qué...?");
            Multilinea(txtRespuestas[i], 46, "Porque...");
        }

        txtCoste.PlaceholderText = "Ej. 180,50";
        txtHoras.PlaceholderText = "Ej. 1,5";

        var personas = (listas.GetValueOrDefault(ListaCalidad.Persona) ?? new()).Select(o => o.Valor).ToList();
        tablaInmediatas = new TablaAcciones(ClaseAccion.Inmediata, personas, "Todavía no hay acciones inmediatas.");
        tablaCorrectoras = new TablaAcciones(ClaseAccion.Correctora, personas, "Todavía no hay acciones correctoras.");
        tablaSeguimientos = new TablaAcciones(ClaseAccion.Seguimiento, personas,
            "Todavía no hay seguimientos. Añade uno cada vez que se compruebe si las acciones han funcionado.");
    }

    // RadioButton con aspecto de botón: en el mismo panel solo puede haber uno marcado.
    private void PrepararSelectorTipo()
    {
        selectorTipo.AutoSize = false;
        selectorTipo.Height = 36;
        selectorTipo.WrapContents = false;
        selectorTipo.Margin = new Padding(0);

        foreach (string tipo in TipoRegistro.Todos)
        {
            var boton = new RadioButton
            {
                Text = TipoRegistro.Nombre(tipo),
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderColor = Tema.Borde, CheckedBackColor = Tema.Acento },
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(165, 34),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            boton.CheckedChanged += (s, e) =>
            {
                PintarBotonTipo(boton);
                if (boton.Checked) ActualizarSegunTipo();
            };
            PintarBotonTipo(boton);

            botonesTipo[tipo] = boton;
            selectorTipo.Controls.Add(boton);
        }
    }

    private static void PintarBotonTipo(RadioButton boton)
    {
        boton.BackColor = boton.Checked ? Tema.Acento : Color.White;
        boton.ForeColor = boton.Checked ? Color.White : Tema.Texto;
        boton.Font = Tema.FuenteDeOpcion(boton.Checked);
        boton.FlatAppearance.BorderColor = boton.Checked ? Tema.Acento : Tema.Borde;
        boton.FlatAppearance.MouseOverBackColor = boton.Checked ? Tema.Acento : Tema.AcentoSuave;
    }

    private static void Multilinea(TextBox texto, int alto, string ayuda)
    {
        texto.Multiline = true;
        texto.Height = alto;
        texto.ScrollBars = ScrollBars.Vertical;
        texto.PlaceholderText = ayuda;
    }

    // Al escribir se proponen los nombres ya usados, pero se puede poner uno nuevo
    private static void Sugerencias(TextBox texto, List<string> nombres)
    {
        texto.MaxLength = 150;
        texto.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        texto.AutoCompleteSource = AutoCompleteSource.CustomSource;
        texto.AutoCompleteCustomSource.AddRange(nombres.ToArray());
    }

    // Si el valor guardado ya no está en la lista (opción antigua o desactivada), se añade para no perderlo.
    private void LlenarLista(ComboBox combo, string lista, string? valor, Func<OpcionLista, bool>? filtro = null)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.Clear();
        combo.Items.Add(SinIndicar);
        foreach (var opcion in listas.GetValueOrDefault(lista) ?? new())
            if (filtro == null || filtro(opcion))
                combo.Items.Add(opcion);

        Elegir(combo, valor);
        AjustarAnchoLista(combo);
    }

    // Que se lean enteras las opciones largas (capítulos de la norma...)
    private static void AjustarAnchoLista(ComboBox combo)
    {
        int ancho = combo.Items.Cast<object>()
            .Max(o => TextRenderer.MeasureText(o.ToString(), combo.Font).Width) + 25;
        combo.DropDownWidth = Math.Max(combo.Width, Math.Min(ancho, 700));
    }

    // Grupos de la hoja CAUSA RAIZ (lista fija, no de CALIDAD_LISTA)
    private void LlenarCausasRaiz(string? causa)
    {
        cmbCausaRaiz.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbCausaRaiz.Items.Clear();
        cmbCausaRaiz.Items.Add(SinIndicar);
        foreach (string grupo in CausasRaiz.Todas)
            cmbCausaRaiz.Items.Add(new OpcionLista(grupo, null));
        Elegir(cmbCausaRaiz, causa);
    }

    private static TextBox[] NuevasCajas() =>
        Enumerable.Range(0, NoConformidad.NumeroPorQues).Select(_ => new TextBox { MaxLength = 1000 }).ToArray();

    private static void Elegir(ComboBox combo, string? valor)
    {
        if (valor == null)
        {
            combo.SelectedItem = SinIndicar;
            return;
        }
        var opcion = combo.Items.OfType<OpcionLista>().FirstOrDefault(o => o.Valor == valor);
        if (opcion == null)
        {
            opcion = new OpcionLista(valor, null);
            combo.Items.Add(opcion);
        }
        combo.SelectedItem = opcion;
    }

    // Proveedor: los de la lista de la pantalla Proveedores que están activos, más el que ya tuviera la NC
    // (aunque esté de baja). Una NC antigua con el nombre escrito a mano lo conserva como opción aparte
    // hasta que se elija su proveedor de la lista.
    private void LlenarProveedores(int? proveedorId, string? escrito)
    {
        cmbProveedor.DropDownStyle = ComboBoxStyle.DropDownList;
        // Al escribir, salta al que empieza así (con DropDownList, la fuente tiene que ir antes que el modo)
        cmbProveedor.AutoCompleteSource = AutoCompleteSource.ListItems;
        cmbProveedor.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        cmbProveedor.Items.Clear();
        cmbProveedor.Items.Add(SinIndicar);
        foreach (var p in proveedores)
            if (!p.Baja || p.Id == proveedorId)
                cmbProveedor.Items.Add(p);

        object? elegido = proveedorId == null ? null : cmbProveedor.Items.OfType<ProveedorDeLista>().FirstOrDefault(p => p.Id == proveedorId);
        if (elegido == null && escrito != null)
            cmbProveedor.Items.Add(elegido = new ProveedorEscrito(escrito));
        cmbProveedor.SelectedItem = elegido ?? SinIndicar;

        int ancho = cmbProveedor.Items.Cast<object>()
            .Max(o => TextRenderer.MeasureText(o.ToString(), cmbProveedor.Font).Width) + 25;
        cmbProveedor.DropDownWidth = Math.Max(cmbProveedor.Width, Math.Min(ancho, 700));
    }

    // Nombre escrito a mano en una NC antigua, sin enlazar a la lista de proveedores
    private sealed record ProveedorEscrito(string Nombre)
    {
        public override string ToString() => $"{Nombre}  (escrito a mano, sin enlazar)";
    }

    private static string? Valor(ComboBox combo) =>
        combo.SelectedItem is OpcionLista opcion && opcion != SinIndicar ? opcion.Valor : null;

    // Estructura general

    private Panel CrearContenido()
    {
        (scroll, var pila) = Componentes.Pagina();

        pila.Controls.Add(Componentes.Cabecera(
            original == null ? "CALIDAD / NUEVO REGISTRO" : "CALIDAD / " + Titulo(original).ToUpper(Componentes.Es),
            original == null ? "Nuevo registro" : Titulo(original),
            original == null
                ? "Para darlo de alta basta con el registro; el resto de fases se pueden completar después."
                : SoloLectura ? "Solo consulta: otra persona tiene abierta esta ficha."
                : "Completa o corrige los datos del registro."));

        var registro = CrearTarjetaRegistro();
        var inmediata = CrearTarjetaAccionInmediata();
        var analisis = CrearTarjetaAnalisis();
        var correctora = CrearTarjetaAccionCorrectora();
        var cierre = CrearTarjetaCierre();

        pila.Controls.Add(registro);
        pila.Controls.Add(tarjetaIncidencia = CrearTarjetaIncidencia());
        PintarRaya(tarjetaIncidencia, Tema.ColorSeccionCalidad(1));   // es parte del registro: lleva su color
        var tarjetaFotos = Componentes.Tarjeta("Fotos", "Fotos del problema. Van adjuntas en el correo a los responsables de las acciones nuevas.");
        tarjetaFotos.Controls.Add(fotos);
        PintarRaya(tarjetaFotos, Tema.ColorSeccionCalidad(1));
        pila.Controls.Add(tarjetaFotos);
        pila.Controls.Add(inmediata);
        pila.Controls.Add(analisis);
        pila.Controls.Add(correctora);
        pila.Controls.Add(cierre);
        pila.Controls.Add(CrearAcciones());

        tarjetasFase[FaseCalidad.Registro] = registro;
        tarjetasFase[FaseCalidad.AccionInmediata] = inmediata;
        tarjetasFase[FaseCalidad.Analisis] = analisis;
        tarjetasFase[FaseCalidad.Correccion] = correctora;
        tarjetasFase[FaseCalidad.Acciones] = correctora;
        tarjetasFase[FaseCalidad.Cierre] = cierre;

        return scroll;
    }

    // Franja blanca fija con la barra de fases: no se mueve al bajar por la ficha
    private Control CrearFranjaFases()
    {
        var franja = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.White,
            Padding = new Padding(40, 12, 40, 0)
        };
        barraFases.Dock = DockStyle.Fill;

        franja.Controls.Add(barraFases);
        // El informe, arriba para no tener que bajar hasta el final (solo en los ya guardados)
        if (original != null)
        {
            var btnPdf = Tema.BotonSecundario("Descargar PDF", 160);
            btnPdf.Height = 40;
            btnPdf.Enabled = cargaCorrecta;
            btnPdf.Click += (s, e) => DescargarPdf(original.Id);
            var hueco = new Panel { Dock = DockStyle.Right, Width = btnPdf.Width + 20, Padding = new Padding(20, 0, 0, 12) };
            btnPdf.Dock = DockStyle.Fill;
            hueco.Controls.Add(btnPdf);
            franja.Controls.Add(hueco);
        }
        franja.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Tema.Linea });
        return franja;
    }

    private Control CrearTarjetaRegistro()
    {
        var tarjeta = TarjetaFase(1, "Registro", "Qué es, cuándo se detectó, de dónde viene y quién se encarga");

        // Los tres botones del tipo no caben en una columna: ocupan dos
        var rejilla = Componentes.Rejilla(3);
        var campoTipoRegistro = campos.Campo("Tipo de registro", selectorTipo);
        rejilla.Controls.Add(campoTipoRegistro, 0, 0);
        rejilla.SetColumnSpan(campoTipoRegistro, 2);
        rejilla.Controls.Add(campos.Campo("Nº", txtNumero, lblNumero), 2, 0);
        rejilla.Controls.Add(campos.Campo("Fecha detectado", dtpDetectado), 0, 1);
        rejilla.Controls.Add(campos.Campo("Origen", cmbOrigen), 1, 1);
        rejilla.Controls.Add(campos.Campo("Sección", cmbSeccion), 2, 1);
        rejilla.Controls.Add(campos.Campo("Cliente", txtCliente), 0, 2);
        rejilla.Controls.Add(campos.Campo("Proveedor", cmbProveedor), 1, 2);
        // Al final de su fila, para que al ocultarse (no es NC) no deje un hueco en medio
        rejilla.Controls.Add(campoTipo = campos.Campo("Tipo de no conformidad", cmbTipo), 2, 2);

        var problema = campos.Campo("Problema", txtProblema);
        rejilla.Controls.Add(problema, 0, 3);
        rejilla.SetColumnSpan(problema, 3);

        // La fecha prevista ya no se pide (25/11): dtpPrevista no se pone en la ficha, solo
        // guarda la que ya tuvieran las antiguas para no borrarla al guardar.
        rejilla.Controls.Add(campoSeguridad = campos.Campo("¿Afecta a la seguridad alimentaria?", cmbSeguridad,
            new Label
            {
                Text = "Cuenta para la nota de reclamaciones del proveedor.",
                AutoSize = true,
                Font = Tema.FuenteNota,
                ForeColor = Tema.Suave,
                Margin = new Padding(0, 4, 0, 0)
            }), 0, 4);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control CrearTarjetaAccionInmediata()
    {
        var tarjeta = TarjetaFase(2, "Acción inmediata", "Lo que se ha hecho en el momento para frenar el problema");
        tarjeta.Controls.Add(campos.Campo("Acciones inmediatas", tablaInmediatas));
        return tarjeta;
    }

    private Control CrearTarjetaAnalisis()
    {
        var tarjeta = TarjetaFase(3, "Análisis de la causa", "", etiquetaSubtitulo: lblSubtituloAnalisis);

        var rejilla = Componentes.Rejilla(2);
        campoPorQues = campos.Campo("5 porqués", CrearPorQues());
        rejilla.Controls.Add(campoPorQues, 0, 0);
        rejilla.SetColumnSpan(campoPorQues, 2);
        rejilla.Controls.Add(campos.Campo("Causa raíz", cmbCausaRaiz), 0, 1);
        rejilla.Controls.Add(campos.Campo("Capítulo de la norma", cmbCapitulo), 1, 1);

        campoCausaAnterior = campos.Campo("Descripción de la causa (texto de antes de los 5 porqués)", txtCausa);
        campoAnalisisAnterior = campos.Campo("Análisis de la causa raíz (texto de antes de los 5 porqués)", txtAnalisis);
        rejilla.Controls.Add(campoCausaAnterior, 0, 3);
        rejilla.Controls.Add(campoAnalisisAnterior, 0, 4);
        rejilla.SetColumnSpan(campoCausaAnterior, 2);
        rejilla.SetColumnSpan(campoAnalisisAnterior, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Cinco filas: número, "¿Por qué?" y "Porque"
    private Control CrearPorQues()
    {
        var tabla = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Margin = new Padding(0)
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        Label Cabecera(string texto) => new()
        {
            Text = texto,
            AutoSize = true,
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena,
            Margin = new Padding(0, 0, 0, 4)
        };
        tabla.Controls.Add(new Label { AutoSize = true, Margin = new Padding(0) }, 0, 0);
        tabla.Controls.Add(Cabecera("¿Por qué?"), 1, 0);
        tabla.Controls.Add(Cabecera("Porque"), 2, 0);

        for (int i = 0; i < NoConformidad.NumeroPorQues; i++)
        {
            tabla.Controls.Add(new Label
            {
                Text = $"{i + 1}.",
                AutoSize = true,
                Font = Tema.FuentePequenaNegrita,
                Margin = new Padding(0, 6, 0, 0)
            }, 0, i + 1);
            txtPreguntas[i].Dock = DockStyle.Fill;
            txtRespuestas[i].Dock = DockStyle.Fill;
            txtPreguntas[i].Margin = new Padding(0, 0, 8, 8);
            txtRespuestas[i].Margin = new Padding(0, 0, 0, 8);
            tabla.Controls.Add(txtPreguntas[i], 1, i + 1);
            tabla.Controls.Add(txtRespuestas[i], 2, i + 1);
        }
        return tabla;
    }

    private Control CrearTarjetaIncidencia()
    {
        var tarjeta = Componentes.Tarjeta("Clasificación de la incidencia",
            "Solo para incidencias. MC/PCC/BPM únicamente si afecta a un MC, un PCC o a las buenas prácticas");

        var rejilla = Componentes.Rejilla(3);
        rejilla.Controls.Add(campos.Campo("MC / PCC / BPM", cmbMcPccBpm), 0, 0);
        rejilla.Controls.Add(campos.Campo("Parámetro", cmbParametro), 1, 0);
        rejilla.Controls.Add(campos.Campo("Operario causante", cmbOperario), 2, 0);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control CrearTarjetaAccionCorrectora()
    {
        var tarjeta = TarjetaFase(4, "Acción correctora",
            "Para que no vuelva a pasar. La fase queda hecha cuando todas las acciones están hechas");

        var rejilla = Componentes.Rejilla(2);
        var accion = campos.Campo("Acciones correctoras", tablaCorrectoras);
        rejilla.Controls.Add(accion, 0, 0);
        rejilla.SetColumnSpan(accion, 2);

        rejilla.Controls.Add(campos.Campo("Coste económico (€)", txtCoste), 0, 1);
        rejilla.Controls.Add(campos.Campo("Horas de mano de obra", txtHoras), 1, 1);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Con la fecha fin el registro queda cerrado
    private Control CrearTarjetaCierre()
    {
        var tarjeta = TarjetaFase(5, "Verificación y cierre", "", etiquetaSubtitulo: lblEstado);

        var rejilla = Componentes.Rejilla(3);
        var seguimiento = campos.Campo("Seguimientos", tablaSeguimientos);
        rejilla.Controls.Add(seguimiento, 0, 0);
        rejilla.SetColumnSpan(seguimiento, 3);
        rejilla.Controls.Add(campos.Campo("Fecha fin", dtpFin), 0, 1);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control CrearAcciones()
    {
        var btnGuardar = Tema.BotonPrincipal(original == null ? "Guardar registro" : "Guardar cambios", 190);
        btnGuardar.Enabled = cargaCorrecta;
        btnGuardar.Click += (s, e) => Guardar();

        var btnCancelar = Tema.BotonSecundario(SoloLectura ? "Cerrar" : "Cancelar", 130);
        btnCancelar.Click += (s, e) => Close();
        CancelButton = btnCancelar;   // Esc cierra (avisando si hay cambios)

        var botones = SoloLectura ? new[] { btnCancelar } : new[] { btnGuardar, btnCancelar };
        return Componentes.Acciones(botones);
    }

    // Informe para auditorías: lo guarda donde se elija y lo abre
    private void DescargarPdf(int id)
    {
        if (hayCambios && !Mensajes.Confirmar(this, "Descargar PDF",
                "El PDF lleva lo que está guardado: los cambios que aún no has guardado no salen.\n\n¿Descargarlo igualmente?"))
            return;

        byte[] pdf;
        string nombre;
        try
        {
            (pdf, nombre) = servicio.Informe(id);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Informacion(this, "No se puede descargar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo preparar el PDF.", ex);
            return;
        }

        using var dialogo = new SaveFileDialog
        {
            Title = "Guardar el informe en PDF",
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = nombre,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) is string perfil
                && Directory.Exists(Path.Combine(perfil, "Downloads")) ? Path.Combine(perfil, "Downloads") : "",
            OverwritePrompt = true
        };
        if (dialogo.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            File.WriteAllBytes(dialogo.FileName, pdf);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Mensajes.Informacion(this, "No se ha guardado",
                "No se pudo guardar el PDF. Si ya está abierto con ese nombre, ciérralo y vuelve a probar.\n\n" + ex.Message);
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dialogo.FileName) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Sin programa para abrir PDF: ya está guardado, no pasa nada
        }
    }

    // Valores

    private void CargarValores()
    {
        var nc = original ?? borrador;

        botonesTipo[nc?.TipoRegistro ?? TipoRegistro.Incidencia].Checked = true;
        // Un registro antiguo puede tener una fecha que ya no cabe en MaxDate (no pasa, pero por si acaso)
        dtpDetectado.Value = nc == null ? DateTime.Today : Min(nc.FechaDetectado, DateTime.Today);

        LlenarLista(cmbOrigen, ListaCalidad.Origen, nc?.Origen);
        LlenarLista(cmbSeccion, ListaCalidad.Seccion, nc?.Seccion);
        LlenarLista(cmbTipo, ListaCalidad.Tipo, nc?.Tipo);
        LlenarLista(cmbMcPccBpm, ListaCalidad.McPccBpm, nc?.McPccBpm);
        LlenarLista(cmbParametro, ListaCalidad.Parametro, nc?.Parametro);
        LlenarLista(cmbOperario, ListaCalidad.Operario, nc?.Operario);
        LlenarLista(cmbCapitulo, ListaCalidad.CapituloNorma, nc?.CapituloNorma);

        txtCliente.Text = nc?.Cliente ?? "";
        LlenarProveedores(nc?.ProveedorId, nc?.Proveedor);
        cmbSeguridad.Items.Clear();
        cmbSeguridad.Items.AddRange(new object[] { SinIndicar.ToString()!, SeguridadSi, SeguridadNo });
        cmbSeguridad.SelectedItem = nc?.SeguridadAlimentaria switch
        {
            true => SeguridadSi,
            false => SeguridadNo,
            null => SinIndicar.ToString()
        };
        txtProblema.Text = nc?.Problema ?? "";
        txtCausa.Text = nc?.DescripcionCausa ?? "";
        PonerFecha(dtpPrevista, nc?.FechaPrevista);
        PonerFecha(dtpFin, nc?.FechaFin);
        txtAnalisis.Text = nc?.AnalisisCausaRaiz ?? "";
        campoCausaAnterior.Visible = !string.IsNullOrWhiteSpace(nc?.DescripcionCausa);
        campoAnalisisAnterior.Visible = !string.IsNullOrWhiteSpace(nc?.AnalisisCausaRaiz);
        for (int i = 0; i < NoConformidad.NumeroPorQues; i++)
        {
            var porQue = nc?.PorQues?.ElementAtOrDefault(i);
            txtPreguntas[i].Text = porQue?.Pregunta ?? "";
            txtRespuestas[i].Text = porQue?.Respuesta ?? "";
        }
        LlenarCausasRaiz(nc?.CausaRaiz);
        txtCoste.Text = nc?.CosteEconomico?.ToString("0.##", Componentes.Es) ?? "";
        txtHoras.Text = nc?.HorasManoObra?.ToString("0.##", Componentes.Es) ?? "";

        tablaInmediatas.Cargar(acciones);
        tablaCorrectoras.Cargar(acciones);
        tablaSeguimientos.Cargar(acciones);
    }

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private static void PonerFecha(DateTimePicker control, DateTime? fecha)
    {
        if (fecha == null)
        {
            control.Checked = false;
            return;
        }
        control.Value = Min(fecha.Value, control.MaxDate);
        control.Checked = true;
    }

    private static DateTime? LeerFecha(DateTimePicker control) => control.Checked ? control.Value.Date : null;

    private List<PorQue> LeerPorQues() =>
        Enumerable.Range(0, NoConformidad.NumeroPorQues)
            .Select(i => new PorQue(txtPreguntas[i].Text, txtRespuestas[i].Text)).ToList();

    private string TipoElegido =>
        botonesTipo.FirstOrDefault(b => b.Value.Checked).Key ?? TipoRegistro.Incidencia;

    // Muestra los campos de este tipo de registro y el número que le toca
    private void ActualizarSegunTipo()
    {
        bool esNc = TipoElegido == TipoRegistro.NoConformidad;
        campoTipo.Visible = esNc;
        campoPorQues.Visible = esNc;
        lblSubtituloAnalisis.Text = esNc
            ? "Por qué ha pasado: se pregunta por qué hasta cinco veces y se elige el grupo de la causa raíz a la que se llega"
            : "Por qué ha pasado: el grupo de la causa raíz";
        ActualizarCampoSeguridad();
        ActualizarSecciones();
        ActualizarNumero();
        ActualizarFases();
        tarjetaIncidencia.Visible = TipoElegido == TipoRegistro.Incidencia;
    }

    // Solo se pregunta en una NC con un proveedor de la lista
    private void ActualizarCampoSeguridad() =>
        campoSeguridad.Visible = TipoElegido == TipoRegistro.NoConformidad && cmbProveedor.SelectedItem is ProveedorDeLista;

    // Fases

    private void ActualizarFases()
    {
        var actual = (original ?? Vacio) with
        {
            TipoRegistro = TipoElegido,
            Problema = txtProblema.Text,
            DescripcionCausa = txtCausa.Text,
            AnalisisCausaRaiz = txtAnalisis.Text,
            PorQues = LeerPorQues(),
            CausaRaiz = Valor(cmbCausaRaiz),
            FechaFin = LeerFecha(dtpFin)
        };
        fases = NoConformidadServicio.Fases(actual, tablaInmediatas.Borrador().Concat(tablaCorrectoras.Borrador()));
        barraFases.Mostrar(fases.Select((f, i) => ($"{i + 1}. {TextoFase(f.Fase)}", f.Hecha, f.Opcional)));
    }

    // Tarjeta de una fase: número en el título y raya a la izquierda con el color de la sección
    private static TableLayoutPanel TarjetaFase(int numero, string titulo, string subtitulo, Label? etiquetaSubtitulo = null)
    {
        var tarjeta = Componentes.Tarjeta($"{numero}. {titulo}", subtitulo, etiquetaSubtitulo: etiquetaSubtitulo);
        PintarRaya(tarjeta, Tema.ColorSeccionCalidad(numero));
        return tarjeta;
    }

    private static void PintarRaya(Control tarjeta, Color color) =>
        tarjeta.Paint += (s, e) =>
        {
            using var pincel = new SolidBrush(color);
            e.Graphics.FillRectangle(pincel, 0, 0, 6, tarjeta.Height);
        };

    // Cortos: tienen que caber en el botón con el ✓ o "(opcional)"
    private static string TextoFase(string fase) => fase switch
    {
        FaseCalidad.Registro => "Registro",
        FaseCalidad.AccionInmediata => "Acción inmediata",
        FaseCalidad.Analisis => "Análisis",
        FaseCalidad.Correccion => "Corrección",
        FaseCalidad.Acciones => "Acciones de mejora",
        _ => "Verificación y cierre"
    };

    // Baja hasta la tarjeta de la fase (arriba del todo) y pone el cursor en su primer campo,
    // al final del texto, sin seleccionarlo, para no borrarlo al escribir
    private void IrAFase(int indice)
    {
        var tarjeta = tarjetasFase[fases[indice].Fase];
        var campo = Entradas(tarjeta).FirstOrDefault(c => c.Visible && c.Enabled);
        campo?.Focus();
        if (campo is TextBox texto)
            texto.Select(texto.TextLength, 0);

        // Después del Focus, que también mueve la página para que se vea el campo
        int arriba = tarjeta.PointToScreen(Point.Empty).Y - scroll.PointToScreen(Point.Empty).Y
            - scroll.AutoScrollPosition.Y;
        scroll.AutoScrollPosition = new Point(0, Math.Max(0, arriba - scroll.Padding.Top));
    }

    private void IrAlError(Control control)
    {
        control.Focus();
        scroll.ScrollControlIntoView(control);
    }

    // Con origen auditoría solo se ofrecen las secciones de auditoría
    private void ActualizarSecciones()
    {
        bool soloAuditoria = NoConformidadServicio.SoloSeccionesAuditoria(Valor(cmbOrigen));
        string? seccion = Valor(cmbSeccion);
        if (soloAuditoria && !NoConformidadServicio.SeccionesAuditoria.Contains(seccion))
            seccion = null;

        LlenarLista(cmbSeccion, ListaCalidad.Seccion, seccion,
            soloAuditoria ? o => NoConformidadServicio.SeccionesAuditoria.Contains(o.Valor) : null);
    }

    // El número es automático: depende del tipo, la sección (auditorías) y la fecha
    private void ActualizarNumero()
    {
        var actual = (original ?? Vacio) with
        {
            TipoRegistro = TipoElegido,
            Seccion = Valor(cmbSeccion),
            FechaDetectado = dtpDetectado.Value.Date
        };

        string? numero;
        try
        {
            numero = servicio.NumeroPrevisto(actual, original);
        }
        catch (DbException)
        {
            txtNumero.Text = "";
            lblNumero.Text = "Se asigna al guardar.";
            return;
        }

        txtNumero.Text = numero ?? "Sin número";
        lblNumero.Text = numero == null ? "Las acciones de mejora no llevan número."
            : numero == original?.Numero ? "Automático."
            : "Automático: se asigna al guardar.";
    }

    // Registro en blanco para calcular el número de uno nuevo
    private static readonly NoConformidad Vacio = new(
        0, TipoRegistro.Incidencia, null, DateTime.Today, null, null, null, "",
        null, null, null, null, null, null, null, null, null, null, null, null, null);

    private void ActualizarEstado()
    {
        lblEstado.Text = NoConformidadServicio.Estado(LeerFecha(dtpFin)) == EstadoRegistro.Cerrada
            ? "Estado: cerrada." : "Estado: abierta.";
        lblEstado.ForeColor = Tema.Suave;
    }

    // null si el coste o las horas no son números (el error va en errorNumero).
    private NoConformidad? LeerRegistro(out Control? errorNumero)
    {
        Control? primero = null;
        decimal? Numero(TextBox caja, string error)
        {
            string texto = caja.Text.Trim();
            if (texto == "") return null;
            // "13.5" también vale: en español el punto se tomaría como separador de miles
            if (!texto.Contains(',')) texto = texto.Replace('.', ',');
            if (decimal.TryParse(texto, NumberStyles.Number, Componentes.Es, out decimal valor))
                return Math.Round(valor, 2);
            campos.MarcarError(caja, error);
            primero ??= caja;
            return null;
        }

        decimal? coste = Numero(txtCoste, "Escribe solo el importe, por ejemplo 180,50.");
        decimal? horas = Numero(txtHoras, "Escribe solo las horas, por ejemplo 1,5 (45 minutos = 0,75).");
        errorNumero = primero;
        if (primero != null) return null;

        return new NoConformidad(
            Id: original?.Id ?? 0,
            TipoRegistro: TipoElegido,
            Numero: original?.Numero,   // el servicio decide el número al guardar
            FechaDetectado: dtpDetectado.Value.Date,
            Origen: Valor(cmbOrigen),
            Seccion: Valor(cmbSeccion),
            Tipo: Valor(cmbTipo),
            Problema: txtProblema.Text,
            DescripcionCausa: txtCausa.Text,
            FechaPrevista: LeerFecha(dtpPrevista),
            FechaFin: LeerFecha(dtpFin),
            Cliente: txtCliente.Text,
            Proveedor: cmbProveedor.SelectedItem switch
            {
                ProveedorDeLista p => p.Nombre,
                ProveedorEscrito e => e.Nombre,
                _ => null
            },
            ProveedorId: (cmbProveedor.SelectedItem as ProveedorDeLista)?.Id,
            SeguridadAlimentaria: (cmbSeguridad.SelectedItem as string) switch
            {
                SeguridadSi => true,
                SeguridadNo => false,
                _ => null
            },
            McPccBpm: Valor(cmbMcPccBpm),
            Parametro: Valor(cmbParametro),
            Operario: Valor(cmbOperario),
            CapituloNorma: Valor(cmbCapitulo),
            AnalisisCausaRaiz: txtAnalisis.Text,
            CosteEconomico: coste,
            HorasManoObra: horas,
            PorQues: LeerPorQues(),
            CausaRaiz: Valor(cmbCausaRaiz),
            Version: original?.Version);
    }

    // Guardado

    private void Guardar()
    {
        // Se marcan todos los campos con error a la vez
        campos.LimpiarErrores();

        var leido = LeerRegistro(out Control? primerError);
        if (leido != null)
        {
            var controles = new Dictionary<string, Control>
            {
                [nameof(NoConformidad.TipoRegistro)] = selectorTipo,
                [nameof(NoConformidad.FechaDetectado)] = dtpDetectado,
                [nameof(NoConformidad.Origen)] = cmbOrigen,
                [nameof(NoConformidad.Seccion)] = cmbSeccion,
                [nameof(NoConformidad.Tipo)] = cmbTipo,
                [nameof(NoConformidad.Cliente)] = txtCliente,
                [nameof(NoConformidad.Proveedor)] = cmbProveedor,
                [nameof(NoConformidad.SeguridadAlimentaria)] = cmbSeguridad,
                [nameof(NoConformidad.Problema)] = txtProblema,
                [nameof(NoConformidad.FechaPrevista)] = dtpPrevista,
                [nameof(NoConformidad.FechaFin)] = dtpFin,
                [nameof(NoConformidad.CosteEconomico)] = txtCoste,
                [nameof(NoConformidad.HorasManoObra)] = txtHoras
            };

            foreach (var (campo, mensaje) in NoConformidadServicio.Errores(NoConformidadServicio.Normalizar(leido)))
            {
                var control = controles[campo];
                campos.MarcarError(control, mensaje);
                primerError ??= control;
            }
        }

        // Las acciones marcan sus errores en la propia tabla
        var inmediatas = tablaInmediatas.Leer(dtpDetectado.Value);
        var correctoras = tablaCorrectoras.Leer(dtpDetectado.Value);
        var seguimientos = tablaSeguimientos.Leer(dtpDetectado.Value);
        if (inmediatas == null) primerError ??= tablaInmediatas;
        if (correctoras == null) primerError ??= tablaCorrectoras;
        if (seguimientos == null) primerError ??= tablaSeguimientos;

        if (primerError != null || leido == null || inmediatas == null || correctoras == null || seguimientos == null)
        {
            if (primerError != null) IrAlError(primerError);
            return;
        }

        var registro = NoConformidadServicio.Normalizar(leido);
        bool leidaAntes = false;
        string? evaluacionAntes = registro.ProveedorId is int idProveedor ? EvaluacionProveedor(idProveedor, out leidaAntes) : null;

        // El número lo pone el servicio
        try
        {
            IdGuardado = servicio.Guardar(registro, inmediatas.Concat(correctoras).Concat(seguimientos),
                fotos.Nuevas.ToList(), fotos.Quitadas.ToList());
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede guardar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar el registro.", ex);
            return;
        }

        hayCambios = false;
        if (leidaAntes && registro.ProveedorId is int proveedorId)
            AvisarSiEmpeoraEvaluacion(proveedorId, evaluacionAntes);
        DialogResult = DialogResult.OK;
    }

    // La evaluación del proveedor sale de sus NC: si al guardar pasa a pedir más controles, se avisa
    private void AvisarSiEmpeoraEvaluacion(int proveedorId, string? antes)
    {
        string? despues = EvaluacionProveedor(proveedorId, out bool leida);
        if (!leida || !ProveedorServicio.Empeora(antes, despues)) return;

        string nombre = (cmbProveedor.SelectedItem as ProveedorDeLista)?.Nombre ?? "El proveedor";
        string cambio = antes == null ? $"pasa a tener la evaluación {despues}" : $"pasa de la evaluación {antes} a la {despues}";
        Mensajes.Aviso(this, "Cambia la evaluación del proveedor",
            $"Con esta no conformidad, {nombre} {cambio}.");
    }

    // Si no se puede leer, no se avisa: el registro ya está guardado
    private static string? EvaluacionProveedor(int proveedorId, out bool leida)
    {
        leida = false;
        try
        {
            var evaluacion = new ProveedorServicio().EvaluacionActual(proveedorId);
            leida = true;
            return evaluacion;
        }
        catch (DbException) { return null; }
    }

    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (!Mensajes.DescartarCambios(this, "Hay cambios sin guardar. ¿Salir sin guardarlos?"))
            e.Cancel = true;
    }

    // Todos los controles donde se escribe o se elige algo
    private static IEnumerable<Control> Entradas(Control padre)
    {
        foreach (Control hijo in padre.Controls)
        {
            if (hijo is TextBox { ReadOnly: false } or ComboBox or DateTimePicker or RadioButton)
                yield return hijo;
            foreach (var nieto in Entradas(hijo))
                yield return nieto;
        }
    }
}
