using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;

// Alta y edición de una incidencia, no conformidad o acción de mejora: una fila de la
// hoja INCIDENCIAS del Excel. Los campos "solo para NC" y "solo para incidencias" se
// muestran u ocultan según el tipo de registro elegido.
// Hay una tarjeta por fase (registro, acción inmediata, análisis, corrección, cierre) y solo se
// ve la de la fase elegida. Arriba, la barra de fases hace de pestañas: marca las hechas
// y al pulsar una se pasa a ella.
public class FormNoConformidad : Form
{
    private readonly NoConformidadServicio servicio = new();
    private readonly NoConformidad? original;   // null si es nuevo
    private readonly NoConformidad? borrador;

    // Quién tiene abierta la ficha si es otro GPE: entonces solo se consulta.
    // Si no, este GPE la bloquea y renueva el bloqueo cada minuto hasta cerrarla.
    private readonly BloqueoNc? bloqueo;
    private bool SoloLectura => bloqueo != null;
    private readonly System.Windows.Forms.Timer renovarBloqueo = new();

    public int IdGuardado { get; private set; }

    private Dictionary<string, List<OpcionLista>> listas = new();
    private bool cargaCorrecta;
    private bool hayCambios;

    // Opción vacía de los desplegables que no son obligatorios
    private static readonly OpcionLista SinIndicar = new("—", null);

    // Identificación
    // Tipo de registro: tres botones de los que solo se puede elegir uno
    private readonly FlowLayoutPanel selectorTipo = new();
    private readonly Dictionary<string, RadioButton> botonesTipo = new();
    private readonly DateTimePicker dtpDetectado = new();
    private readonly TextBox txtNumero = new();
    private readonly Label lblNumero = new();
    private readonly ComboBox cmbOrigen = new();
    private readonly ComboBox cmbSeccion = new();
    private readonly ComboBox cmbTipo = new();
    private readonly TextBox txtCliente = new();
    private readonly TextBox txtProveedor = new();

    // Descripción y acción inmediata
    private readonly TextBox txtProblema = new();
    private readonly TextBox txtCausa = new();
    private readonly ComboBox cmbResponsable = new();
    private readonly DateTimePicker dtpPrevista = new();
    private readonly DateTimePicker dtpFin = new();

    // Solo incidencias
    private readonly ComboBox cmbMcPccBpm = new();
    private readonly ComboBox cmbParametro = new();
    private readonly ComboBox cmbOperario = new();

    // Análisis y acción correctora
    private readonly ComboBox cmbCapitulo = new();
    private readonly TextBox txtAnalisis = new();
    private readonly TextBox txtCoste = new();
    private readonly TextBox txtHoras = new();

    // Acciones inmediatas, correctoras y seguimientos: una tabla cada uno
    private TablaAcciones tablaInmediatas = null!;
    private TablaAcciones tablaCorrectoras = null!;
    private TablaAcciones tablaSeguimientos = null!;
    private List<AccionNc> acciones = new();

    private readonly Label lblEstado = new();

    private readonly CamposFormulario campos = new();
    private Control campoTipo = null!;
    private Control tarjetaIncidencia = null!;

    // Fases del registro según lo que hay escrito ahora, la tarjeta de cada una y la
    // que se está viendo (-1 mientras se monta la ficha: se ven todas)
    private readonly BarraFasesCalidad barraFases = new();
    private List<EstadoFase> fases = new();
    private readonly Dictionary<string, Control> tarjetasFase = new();
    private int faseElegida = -1;
    private Panel scroll = null!;
    private TableLayoutPanel pila = null!;

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
        ActualizarSegunTipo();
        ActualizarEstado();

        // Las NC de auditoría se numeran por año o por fecha: el número cambia con ellas
        cmbOrigen.SelectedIndexChanged += (s, e) => ActualizarSecciones();
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
        FormClosing += AvisarCambiosSinGuardar;
        // Lo que trae el borrador también se pierde si se cierra sin guardar
        hayCambios = borrador != null;

        // La barra de fases se pone al día con lo que decide cada fase
        foreach (var texto in new[] { txtProblema, txtCausa, txtAnalisis })
            texto.TextChanged += (s, e) => ActualizarFases();
        dtpFin.ValueChanged += (s, e) => ActualizarFases();
        tablaInmediatas.Cambiado += (s, e) => ActualizarFases();
        tablaCorrectoras.Cambiado += (s, e) => ActualizarFases();
        barraFases.FasePulsada += IrAFase;

        // Uno nuevo empieza por el registro; uno ya guardado, por la fase en curso
        MostrarFase(original == null ? 0 : NoConformidadServicio.FaseEnCurso(fases) ?? 0);

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

    // Si falla la base de datos, se deja editar: al guardar, la versión del registro
    // sigue impidiendo que se pisen los cambios de otro
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
    }

    private static string Titulo(NoConformidad nc) =>
        nc.Numero == null
            ? $"{TipoRegistro.Nombre(nc.TipoRegistro)} del {Componentes.Fecha(nc.FechaDetectado)}"
            : $"{TipoRegistro.Nombre(nc.TipoRegistro)} {nc.Numero}";

    // Preparación de controles

    private void PrepararControles()
    {
        List<string> clientes = new(), proveedores = new();
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
        Sugerencias(txtProveedor, proveedores);

        Multilinea(txtProblema, 90, "Qué se ha detectado: producto, lote, cliente...");
        Multilinea(txtCausa, 70, "Por qué ha pasado");
        Multilinea(txtAnalisis, 70, "Causa de fondo, para que no se repita");

        txtCoste.PlaceholderText = "Ej. 180,50";
        txtHoras.PlaceholderText = "Ej. 1,5";

        var personas = (listas.GetValueOrDefault(ListaCalidad.Persona) ?? new()).Select(o => o.Valor).ToList();
        tablaInmediatas = new TablaAcciones(ClaseAccion.Inmediata, personas, "Todavía no hay acciones inmediatas.");
        tablaCorrectoras = new TablaAcciones(ClaseAccion.Correctora, personas, "Todavía no hay acciones correctoras.");
        tablaSeguimientos = new TablaAcciones(ClaseAccion.Seguimiento, personas,
            "Todavía no hay seguimientos. Añade uno cada vez que se compruebe si las acciones han funcionado.");
    }

    // Botones pegados (No conformidad · Incidencia · Acción de mejora); el elegido, en morado.
    // Son RadioButton con aspecto de botón: al estar en el mismo panel, solo puede haber uno marcado.
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

    // Desplegable con las opciones de una lista; si el valor guardado ya no está en la
    // lista (una opción antigua o desactivada), se añade para no perderlo.
    // Con filtro, solo se ponen las opciones que lo cumplen.
    private void LlenarLista(ComboBox combo, string lista, string? valor, Func<OpcionLista, bool>? filtro = null)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.Clear();
        combo.Items.Add(SinIndicar);
        foreach (var opcion in listas.GetValueOrDefault(lista) ?? new())
            if (filtro == null || filtro(opcion))
                combo.Items.Add(opcion);

        Elegir(combo, valor);

        // Que se lean enteras las opciones largas (capítulos de la norma...)
        int ancho = combo.Items.Cast<object>()
            .Max(o => TextRenderer.MeasureText(o.ToString(), combo.Font).Width) + 25;
        combo.DropDownWidth = Math.Max(combo.Width, Math.Min(ancho, 700));
    }

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

    private static string? Valor(ComboBox combo) =>
        combo.SelectedItem is OpcionLista opcion && opcion != SinIndicar ? opcion.Valor : null;

    // Estructura general

    private Panel CrearContenido()
    {
        (scroll, pila) = Componentes.Pagina();

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
        franja.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Tema.Linea });
        return franja;
    }

    private Control CrearTarjetaRegistro()
    {
        var tarjeta = Componentes.Tarjeta("Registro", "Qué es, cuándo se detectó, de dónde viene y quién se encarga");

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
        rejilla.Controls.Add(campos.Campo("Proveedor", txtProveedor), 1, 2);
        // Al final de su fila, para que al ocultarse (no es NC) no deje un hueco en medio
        rejilla.Controls.Add(campoTipo = campos.Campo("Tipo de no conformidad", cmbTipo), 2, 2);

        var problema = campos.Campo("Problema", txtProblema);
        rejilla.Controls.Add(problema, 0, 3);
        rejilla.SetColumnSpan(problema, 3);

        rejilla.Controls.Add(campos.Campo("Responsable", cmbResponsable), 0, 4);
        rejilla.Controls.Add(campos.Campo("Fecha prevista", dtpPrevista), 1, 4);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control CrearTarjetaAccionInmediata()
    {
        var tarjeta = Componentes.Tarjeta("Acción inmediata", "Lo que se ha hecho en el momento para frenar el problema");
        tarjeta.Controls.Add(campos.Campo("Acciones inmediatas", tablaInmediatas));
        return tarjeta;
    }

    private Control CrearTarjetaAnalisis()
    {
        var tarjeta = Componentes.Tarjeta("Análisis de la causa", "Por qué ha pasado");

        var rejilla = Componentes.Rejilla(2);
        var causa = campos.Campo("Descripción de la causa", txtCausa);
        var analisis = campos.Campo("Análisis de la causa raíz", txtAnalisis);
        rejilla.Controls.Add(causa, 0, 0);
        rejilla.Controls.Add(campos.Campo("Capítulo de la norma", cmbCapitulo), 0, 1);
        rejilla.Controls.Add(analisis, 0, 2);
        rejilla.SetColumnSpan(causa, 2);
        rejilla.SetColumnSpan(analisis, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
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
        var tarjeta = Componentes.Tarjeta("Acción correctora",
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
        var tarjeta = Componentes.Tarjeta("Verificación y cierre", "", etiquetaSubtitulo: lblEstado);

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

        return SoloLectura ? Componentes.Acciones(btnCancelar) : Componentes.Acciones(btnGuardar, btnCancelar);
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
        LlenarLista(cmbResponsable, ListaCalidad.Persona, nc?.Responsable);
        LlenarLista(cmbMcPccBpm, ListaCalidad.McPccBpm, nc?.McPccBpm);
        LlenarLista(cmbParametro, ListaCalidad.Parametro, nc?.Parametro);
        LlenarLista(cmbOperario, ListaCalidad.Operario, nc?.Operario);
        LlenarLista(cmbCapitulo, ListaCalidad.CapituloNorma, nc?.CapituloNorma);

        txtCliente.Text = nc?.Cliente ?? "";
        txtProveedor.Text = nc?.Proveedor ?? "";
        txtProblema.Text = nc?.Problema ?? "";
        txtCausa.Text = nc?.DescripcionCausa ?? "";
        PonerFecha(dtpPrevista, nc?.FechaPrevista);
        PonerFecha(dtpFin, nc?.FechaFin);
        txtAnalisis.Text = nc?.AnalisisCausaRaiz ?? "";
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

    private string TipoElegido =>
        botonesTipo.FirstOrDefault(b => b.Value.Checked).Key ?? TipoRegistro.Incidencia;

    // Muestra los campos de este tipo de registro y el número que le toca
    private void ActualizarSegunTipo()
    {
        campoTipo.Visible = TipoElegido == TipoRegistro.NoConformidad;
        ActualizarSecciones();
        ActualizarNumero();
        ActualizarFases();
        if (faseElegida >= 0) MostrarFase(faseElegida);   // la tarjeta de incidencia va y viene
    }

    // Fases

    private void ActualizarFases()
    {
        var actual = (original ?? Vacio) with
        {
            TipoRegistro = TipoElegido,
            Problema = txtProblema.Text,
            DescripcionCausa = txtCausa.Text,
            AnalisisCausaRaiz = txtAnalisis.Text,
            FechaFin = LeerFecha(dtpFin)
        };
        fases = NoConformidadServicio.Fases(actual, tablaInmediatas.Borrador().Concat(tablaCorrectoras.Borrador()));
        barraFases.Mostrar(fases.Select(f => (TextoFase(f.Fase), f.Hecha, f.Opcional)));
    }

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

    // Enseña solo la tarjeta de la fase (en el registro, también la de clasificación
    // si es una incidencia) y vuelve arriba de la página
    private void MostrarFase(int indice)
    {
        faseElegida = indice;
        var tarjeta = tarjetasFase[fases[indice].Fase];

        pila.SuspendLayout();
        foreach (var otra in tarjetasFase.Values.Distinct())
            otra.Visible = otra == tarjeta;
        tarjetaIncidencia.Visible = fases[indice].Fase == FaseCalidad.Registro
            && TipoElegido == TipoRegistro.Incidencia;
        pila.ResumeLayout();

        barraFases.Elegir(indice);
        scroll.AutoScrollPosition = Point.Empty;
    }

    // Al pasar a otra fase, el cursor va a su primer campo, al final del texto (sin
    // seleccionarlo, para no borrarlo al escribir)
    private void IrAFase(int indice)
    {
        MostrarFase(indice);
        var campo = Entradas(tarjetasFase[fases[indice].Fase]).FirstOrDefault(c => c.Visible && c.Enabled);
        campo?.Focus();
        if (campo is TextBox texto)
            texto.Select(texto.TextLength, 0);
        scroll.AutoScrollPosition = Point.Empty;
    }

    // Con un error en otra fase, se pasa a esa fase antes de llevar el cursor al campo
    private void IrAlError(Control control)
    {
        for (Control? padre = control; padre != null; padre = padre.Parent)
        {
            int indice = padre == tarjetaIncidencia ? 0 : fases.FindIndex(f => tarjetasFase[f.Fase] == padre);
            if (indice < 0) continue;
            if (indice != faseElegida) MostrarFase(indice);
            break;
        }
        control.Focus();
    }

    // En una NC de auditoría solo se ofrecen las secciones de auditoría (interna,
    // externa, cliente). Si la sección elegida no es una de ellas, se quita.
    private void ActualizarSecciones()
    {
        bool soloAuditoria = NoConformidadServicio.SoloSeccionesAuditoria(TipoElegido, Valor(cmbOrigen));
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
        DateTime? prevista = LeerFecha(dtpPrevista);
        string estado = NoConformidadServicio.Estado(prevista, LeerFecha(dtpFin));
        lblEstado.Text = estado switch
        {
            EstadoRegistro.Cerrada => "Estado: cerrada.",
            EstadoRegistro.SinPlazo => "Estado: abierta, sin fecha prevista.",
            EstadoRegistro.EnPlazo => $"Estado: en plazo, hasta el {Componentes.Fecha(prevista)}.",
            _ => $"Estado: fuera de plazo desde el {Componentes.Fecha(prevista!.Value.AddDays(1))}."
        };
        lblEstado.ForeColor = estado == EstadoRegistro.FueraPlazo ? Tema.Error : Tema.Suave;
    }

    // Lee los campos de la pantalla. Si el coste o las horas no son números, deja el
    // error marcado, lo devuelve en errorNumero y el registro es null.
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
            Responsable: Valor(cmbResponsable),
            FechaPrevista: LeerFecha(dtpPrevista),
            FechaFin: LeerFecha(dtpFin),
            Cliente: txtCliente.Text,
            Proveedor: txtProveedor.Text,
            McPccBpm: Valor(cmbMcPccBpm),
            Parametro: Valor(cmbParametro),
            Operario: Valor(cmbOperario),
            CapituloNorma: Valor(cmbCapitulo),
            AnalisisCausaRaiz: txtAnalisis.Text,
            CosteEconomico: coste,
            HorasManoObra: horas,
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
                [nameof(NoConformidad.Proveedor)] = txtProveedor,
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

        // El número lo pone el servicio
        try
        {
            IdGuardado = servicio.Guardar(registro, inmediatas.Concat(correctoras).Concat(seguimientos));
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
        DialogResult = DialogResult.OK;
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
