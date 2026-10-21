using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;

// Alta y edición de una incidencia, no conformidad o acción de mejora: una fila de la
// hoja INCIDENCIAS del Excel. Los campos "solo para NC" y "solo para incidencias" se
// muestran u ocultan según el tipo de registro elegido.
public class FormNoConformidad : Form
{
    private readonly NoConformidadServicio servicio = new();
    private readonly NoConformidad? original;   // null si es nuevo

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
    private readonly TextBox txtAccionInmediata = new();
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
    private readonly TextBox txtAccionCorrectora = new();
    private readonly ComboBox cmbResponsableAccion = new();
    private readonly TextBox txtCoste = new();
    private readonly TextBox txtHoras = new();

    private readonly TextBox txtSeguimiento = new();
    private readonly Label lblEstado = new();

    private readonly CamposFormulario campos = new();
    private Control campoTipo = null!;
    private Control tarjetaIncidencia = null!;

    public FormNoConformidad(NoConformidad? registro = null)
    {
        original = registro;

        Text = registro == null ? "Nuevo registro" : Titulo(registro);
        Size = new Size(1200, 800);
        MinimumSize = new Size(1100, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        PrepararControles();
        Controls.Add(CrearContenido());
        CargarValores();
        ActualizarSegunTipo();
        ActualizarEstado();

        // Las NC de auditoría se numeran por año o por fecha: el número cambia con ellas
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
        FormClosing += AvisarCambiosSinGuardar;
    }

    private static string Titulo(NoConformidad nc) =>
        nc.Numero == null
            ? $"{TipoRegistro.Nombre(nc.TipoRegistro)} del {Componentes.Fecha(nc.FechaDetectado)}"
            : $"{TipoRegistro.Nombre(nc.TipoRegistro)} {nc.Numero}";

    // ---------- Preparación de controles ----------

    private void PrepararControles()
    {
        List<string> clientes = new(), proveedores = new();
        try
        {
            listas = servicio.ObtenerListas();
            clientes = servicio.ObtenerClientes();
            proveedores = servicio.ObtenerProveedores();
            cargaCorrecta = true;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this,
                "No se pudieron cargar las listas de la base de datos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        lblNumero.Font = new Font("Segoe UI", 8.5f);
        lblNumero.Margin = new Padding(0, 4, 0, 0);

        Sugerencias(txtCliente, clientes);
        Sugerencias(txtProveedor, proveedores);

        Multilinea(txtProblema, 90, "Qué se ha detectado: producto, lote, cliente...");
        Multilinea(txtCausa, 70, "Por qué ha pasado");
        Multilinea(txtAccionInmediata, 70, "Qué se ha hecho al detectarlo");
        Multilinea(txtAnalisis, 70, "Causa de fondo, para que no se repita");
        Multilinea(txtAccionCorrectora, 70, "Qué se va a cambiar");
        Multilinea(txtSeguimiento, 90, "Comprobaciones posteriores, con su fecha. Ej. 20/06/2026 Todos los palets correctos.");

        txtCoste.PlaceholderText = "Ej. 180,50";
        txtHoras.PlaceholderText = "Ej. 1,5";
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
        boton.Font = new Font("Segoe UI", 9.5f, boton.Checked ? FontStyle.Bold : FontStyle.Regular);
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
    // lista (una opción antigua o desactivada), se añade para no perderlo
    private void LlenarLista(ComboBox combo, string lista, string? valor)
    {
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.Items.Clear();
        combo.Items.Add(SinIndicar);
        foreach (var opcion in listas.GetValueOrDefault(lista) ?? new())
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

    // ---------- Estructura general ----------

    private Panel CrearContenido()
    {
        var (scroll, pila) = Componentes.Pagina();

        pila.Controls.Add(Componentes.Cabecera(
            original == null ? "CALIDAD / NUEVO REGISTRO" : "CALIDAD / " + Titulo(original).ToUpper(Componentes.Es),
            original == null ? "Nuevo registro" : Titulo(original),
            original == null
                ? "Anota una incidencia, una no conformidad o una acción de mejora."
                : "Completa o corrige los datos del registro."));
        pila.Controls.Add(CrearTarjetaIdentificacion());
        pila.Controls.Add(CrearTarjetaDescripcion());
        pila.Controls.Add(tarjetaIncidencia = CrearTarjetaIncidencia());
        pila.Controls.Add(CrearTarjetaAccionCorrectora());
        pila.Controls.Add(CrearTarjetaSeguimiento());
        pila.Controls.Add(CrearAcciones());

        return scroll;
    }

    private Control CrearTarjetaIdentificacion()
    {
        var tarjeta = Componentes.Tarjeta("Identificación", "Qué es, cuándo se detectó y de dónde viene");

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
        // Al final, para que al ocultarse (no es NC) no deje un hueco en medio
        rejilla.Controls.Add(campoTipo = campos.Campo("Tipo de no conformidad", cmbTipo), 2, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control CrearTarjetaDescripcion()
    {
        var tarjeta = Componentes.Tarjeta("Problema y acción inmediata", "Lo que ha pasado y cómo se ha resuelto en el momento");

        var rejilla = Componentes.Rejilla(3);
        var problema = campos.Campo("Problema", txtProblema);
        var causa = campos.Campo("Descripción de la causa", txtCausa);
        var accion = campos.Campo("Acción inmediata", txtAccionInmediata);
        rejilla.Controls.Add(problema, 0, 0);
        rejilla.Controls.Add(causa, 0, 1);
        rejilla.Controls.Add(accion, 0, 2);
        foreach (var campo in new[] { problema, causa, accion })
            rejilla.SetColumnSpan(campo, 3);

        rejilla.Controls.Add(campos.Campo("Responsable", cmbResponsable), 0, 3);
        rejilla.Controls.Add(campos.Campo("Fecha prevista", dtpPrevista), 1, 3);
        rejilla.Controls.Add(campos.Campo("Fecha fin", dtpFin), 2, 3);

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
        var tarjeta = Componentes.Tarjeta("Análisis y acción correctora", "Para que no vuelva a pasar");

        var rejilla = Componentes.Rejilla(2);
        rejilla.Controls.Add(campos.Campo("Capítulo de la norma", cmbCapitulo), 0, 0);
        rejilla.Controls.Add(campos.Campo("Responsable de la acción correctora", cmbResponsableAccion), 1, 0);

        var analisis = campos.Campo("Análisis de la causa raíz", txtAnalisis);
        var accion = campos.Campo("Acción correctora", txtAccionCorrectora);
        rejilla.Controls.Add(analisis, 0, 1);
        rejilla.Controls.Add(accion, 0, 2);
        rejilla.SetColumnSpan(analisis, 2);
        rejilla.SetColumnSpan(accion, 2);

        rejilla.Controls.Add(campos.Campo("Coste económico (€)", txtCoste), 0, 3);
        rejilla.Controls.Add(campos.Campo("Horas de mano de obra", txtHoras), 1, 3);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control CrearTarjetaSeguimiento()
    {
        var tarjeta = Componentes.Tarjeta("Seguimiento", "", etiquetaSubtitulo: lblEstado);
        tarjeta.Controls.Add(campos.Campo("Seguimiento", txtSeguimiento));
        return tarjeta;
    }

    private Control CrearAcciones()
    {
        var btnGuardar = Tema.BotonPrincipal(original == null ? "Guardar registro" : "Guardar cambios", 190);
        btnGuardar.Enabled = cargaCorrecta;
        btnGuardar.Click += (s, e) => Guardar();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.Click += (s, e) => Close();
        CancelButton = btnCancelar;   // Esc cierra (avisando si hay cambios)

        return Componentes.Acciones(btnGuardar, btnCancelar);
    }

    // ---------- Valores ----------

    private void CargarValores()
    {
        var nc = original;

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
        LlenarLista(cmbResponsableAccion, ListaCalidad.Persona, nc?.ResponsableAccionCorrectora);

        txtCliente.Text = nc?.Cliente ?? "";
        txtProveedor.Text = nc?.Proveedor ?? "";
        txtProblema.Text = nc?.Problema ?? "";
        txtCausa.Text = nc?.DescripcionCausa ?? "";
        txtAccionInmediata.Text = nc?.AccionInmediata ?? "";
        PonerFecha(dtpPrevista, nc?.FechaPrevista);
        PonerFecha(dtpFin, nc?.FechaFin);
        txtAnalisis.Text = nc?.AnalisisCausaRaiz ?? "";
        txtAccionCorrectora.Text = nc?.AccionCorrectora ?? "";
        txtCoste.Text = nc?.CosteEconomico?.ToString("0.##", Componentes.Es) ?? "";
        txtHoras.Text = nc?.HorasManoObra?.ToString("0.##", Componentes.Es) ?? "";
        txtSeguimiento.Text = nc?.Seguimiento ?? "";
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
        tarjetaIncidencia.Visible = TipoElegido == TipoRegistro.Incidencia;
        ActualizarNumero();
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
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

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
            AccionInmediata: txtAccionInmediata.Text,
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
            AccionCorrectora: txtAccionCorrectora.Text,
            ResponsableAccionCorrectora: Valor(cmbResponsableAccion),
            CosteEconomico: coste,
            HorasManoObra: horas,
            Seguimiento: txtSeguimiento.Text);
    }

    // ---------- Guardado ----------

    private void Guardar()
    {
        // 1. Validaciones: se marcan todos los campos con error a la vez
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

        if (primerError != null || leido == null)
        {
            primerError?.Focus();
            return;
        }

        var registro = NoConformidadServicio.Normalizar(leido);

        // 2. Guardar en la base de datos (el número lo pone el servicio)
        try
        {
            IdGuardado = servicio.Guardar(registro);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se puede guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this,
                "No se pudo guardar el registro.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        var respuesta = MessageBox.Show(this,
            "Hay cambios sin guardar. ¿Salir sin guardarlos?",
            "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (respuesta != DialogResult.Yes)
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
