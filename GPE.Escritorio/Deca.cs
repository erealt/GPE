using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Crear o modificar un DeCA: remitente, destinatario, cargador contractual, transporte, mercancía y palets.
// Al escribir el nombre o el NIF de remitente, destinatario, cargador o transportista salen los ya usados.
public class FormDeca : Form
{
    private readonly DecaServicio servicio = new();
    private readonly DecaGuardado? existente;   // null = nuevo

    public int IdGuardado { get; private set; }

    // Campos de un remitente o un destinatario (los dos son iguales)
    private class ControlesParte
    {
        public readonly TextBox Nombre = Texto(150, "Nombre o razón social");
        public readonly TextBox Nif = Texto(20, "B12345674");
        public readonly TextBox Telefono = Texto(30, "Opcional");
        public readonly TextBox Direccion = Texto(200, "Calle, número, piso...");
        public readonly TextBox CodigoPostal = Texto(10, "02001");
        public readonly TextBox Poblacion = Texto(100, "");
        public readonly TextBox Provincia = Texto(100, "Opcional");
        public readonly TextBox Pais = Texto(60, "");

        public ControlesParte() => Pais.Text = DecaServicio.PaisPorDefecto;

        public ParteEnvio Leer() => new(Nombre.Text, Nif.Text, Direccion.Text, CodigoPostal.Text,
            Poblacion.Text, Provincia.Text, Pais.Text, Telefono.Text);

        public void Rellenar(ParteEnvio p)
        {
            Nombre.Text = p.Nombre;
            Nif.Text = p.Nif;
            Telefono.Text = p.Telefono ?? "";
            Direccion.Text = p.Direccion;
            CodigoPostal.Text = p.CodigoPostal;
            Poblacion.Text = p.Poblacion;
            Provincia.Text = p.Provincia ?? "";
            Pais.Text = p.Pais;
        }

        // Comprobación de cada campo, en el orden en que aparecen. Los mensajes van debajo
        // del campo, dentro de su tarjeta, así que no hace falta decir si es del remitente.
        public (Control, Func<string?>)[] Comprobaciones() =>
        [
            (Nombre, () => DecaServicio.ErrorObligatorio(Nombre.Text, "el nombre", 150)),
            (Nif, () => DecaServicio.ErrorNif(Nif.Text, Pais.Text)),
            (Telefono, () => DecaServicio.ErrorOpcional(Telefono.Text, 30)),
            (Direccion, () => DecaServicio.ErrorObligatorio(Direccion.Text, "la dirección", 200)),
            (CodigoPostal, () => DecaServicio.ErrorCodigoPostal(CodigoPostal.Text, Pais.Text)),
            (Poblacion, () => DecaServicio.ErrorObligatorio(Poblacion.Text, "la población", 100)),
            (Provincia, () => DecaServicio.ErrorOpcional(Provincia.Text, 100)),
            (Pais, () => DecaServicio.ErrorObligatorio(Pais.Text, "el país", 60))
        ];
    }

    private readonly ControlesParte remitente = new();
    private readonly ControlesParte destinatario = new();

    // Cargador contractual: quien contrata el transporte. No se marca ninguno por defecto.
    private readonly FlowLayoutPanel panelCargador = new() { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
    private readonly RadioButton rbCargadorRemitente = Opcion("El remitente");
    private readonly RadioButton rbCargadorDestinatario = Opcion("El destinatario");
    private readonly RadioButton rbCargadorOtro = Opcion("Otro");
    private readonly ControlesParte cargadorOtro = new();
    private readonly TableLayoutPanel rejillaCargadorOtro = Componentes.Rejilla(4);

    // Transporte y mercancía
    private readonly DateTimePicker dtpCarga = new() { Format = DateTimePickerFormat.Short };
    private readonly TextBox txtTransportista = Texto(150, "Nombre o razón social");
    private readonly TextBox txtNifTransportista = Texto(20, "");
    private readonly TextBox txtMatricula = Texto(20, "1234ABC");
    private readonly TextBox txtMatriculaRemolque = Texto(20, "Solo si lleva remolque");
    private readonly TextBox txtLugarCarga = Texto(250, "Si no es la dirección del remitente");
    private readonly TextBox txtLugarDescarga = Texto(250, "Si no es la dirección del destinatario");
    private readonly TextBox txtMercancia = Texto(250, "Ej. Chocolate en cajas de cartón");
    private readonly TextBox txtObservaciones = Texto(4000, "Cualquier información útil para el transporte...");

    // Palets: una fila por palet
    private record FilaPalet(TableLayoutPanel Panel, Label Numero, NumericUpDown Peso, NumericUpDown Cajas, Label Error);
    private readonly List<FilaPalet> palets = new();
    private readonly TableLayoutPanel contenedorPalets = new();
    private readonly Label lblTotales = new();
    private readonly Label lblErrorPalets = CamposFormulario.EtiquetaError();

    // PDF y QR (solo en un DeCA ya guardado)
    private readonly Button btnPdf = Tema.BotonSecundario("Generar PDF y QR", 175);
    private Label? lblEstadoPdf;
    private bool pdfAlDia;

    private readonly CamposFormulario campos = new();
    private bool hayCambios;
    private bool cargando = true;   // mientras se rellenan los datos iniciales no cuenta como cambio

    public FormDeca(DecaGuardado? existente)
    {
        this.existente = existente;

        Text = existente == null ? "Nuevo DeCA" : existente.Numero;
        Size = new Size(1200, 800);
        MinimumSize = new Size(1150, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        txtObservaciones.Multiline = true;
        txtObservaciones.Height = 70;
        txtObservaciones.ScrollBars = ScrollBars.Vertical;

        Controls.Add(CrearContenido());

        if (existente != null)
            Rellenar(existente.Datos);
        else
        {
            dtpCarga.Value = DateTime.Today;
            AnadirPalet();
        }

        VigilarCambios(this);
        cargando = false;

        FormClosing += AvisarCambiosSinGuardar;
    }

    private static TextBox Texto(int maximo, string ejemplo) => new() { MaxLength = maximo, PlaceholderText = ejemplo };

    private static RadioButton Opcion(string texto) =>
        new() { Text = texto, AutoSize = true, Margin = new Padding(0, 0, 25, 0) };

    // ---------- Estructura ----------

    private Panel CrearContenido()
    {
        var (scroll, pila) = Componentes.Pagina();

        if (existente == null)
            pila.Controls.Add(Componentes.Cabecera("DECA / NUEVO", "Nuevo DeCA",
                "Rellena los datos del envío. Después podrás generar el PDF con su código QR."));
        else
        {
            pila.Controls.Add(Componentes.Cabecera($"DECA / {existente.Numero}", existente.Numero,
                existente.ArchivoPdf == null
                    ? "Modifica los datos del envío."
                    : "Si guardas cambios, habrá que volver a generar el PDF. El QR ya impreso seguirá llevando al PDF anterior."));
            var resumen = (TableLayoutPanel)Componentes.Resumen(
                ("CREADO", $"{Componentes.Fecha(existente.CreadoEn)} · {existente.CreadoPor}"),
                ("ÚLTIMO CAMBIO", existente.ModificadoEn == null
                    ? "—" : $"{Componentes.Fecha(existente.ModificadoEn)} · {existente.ModificadoPor}"),
                ("PDF", existente.PdfGeneradoEn != null
                    ? "Generado el " + Componentes.Fecha(existente.PdfGeneradoEn)
                    : existente.ArchivoPdf != null ? "Por volver a generar" : "Sin generar"));
            lblEstadoPdf = (Label)resumen.Controls[resumen.Controls.Count - 1].Controls[1];

            // El botón del PDF va en el resumen, a la derecha del estado del PDF
            pdfAlDia = existente.PdfGeneradoEn != null;
            btnPdf.Height = 40;
            btnPdf.Anchor = AnchorStyles.Right;
            btnPdf.Margin = new Padding(0);
            btnPdf.Click += (s, e) => PulsarPdf();
            ActualizarBotonPdf();
            resumen.ColumnCount++;
            resumen.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            resumen.Controls.Add(btnPdf, resumen.ColumnCount - 1, 0);
            pila.Controls.Add(resumen);
        }

        List<ParteEnvio> partesUsadas = [];
        List<TransportistaDeca> transportistasUsados = [];
        try
        {
            partesUsadas = servicio.ObtenerPartesUsadas();
            transportistasUsados = servicio.ObtenerTransportistasUsados();
        }
        catch (DbException)
        {
            // Sin sugerencias se puede seguir escribiendo a mano
        }

        pila.Controls.Add(TarjetaParte("1   Remitente", "Quién envía la mercancía", remitente, partesUsadas));
        pila.Controls.Add(TarjetaParte("2   Destinatario", "Quién la recibe", destinatario, partesUsadas));
        pila.Controls.Add(TarjetaCargador(partesUsadas));
        pila.Controls.Add(TarjetaTransporte(transportistasUsados));
        pila.Controls.Add(TarjetaMercancia());
        pila.Controls.Add(CrearAcciones());

        return scroll;
    }

    private Control TarjetaParte(string titulo, string subtitulo, ControlesParte c,
        List<ParteEnvio> usados)
    {
        var tarjeta = Componentes.Tarjeta(titulo, subtitulo);
        SugerirPartes(c, usados);
        tarjeta.Controls.Add(RejillaParte(c, Componentes.Rejilla(4)));
        return tarjeta;
    }

    // Quien contrata el transporte con el transportista. La normativa pide que figure expresamente;
    // si es el remitente o el destinatario no hace falta volver a escribir sus datos.
    private Control TarjetaCargador(List<ParteEnvio> usados)
    {
        var tarjeta = Componentes.Tarjeta("3   Cargador contractual", "Quién contrata el transporte con el transportista");
        SugerirPartes(cargadorOtro, usados);

        panelCargador.Controls.AddRange(new Control[] { rbCargadorRemitente, rbCargadorDestinatario, rbCargadorOtro });
        foreach (var opcion in new[] { rbCargadorRemitente, rbCargadorDestinatario, rbCargadorOtro })
            opcion.CheckedChanged += (s, e) =>
            {
                if (!opcion.Checked) return;
                campos.OcultarError(panelCargador);
                rejillaCargadorOtro.Visible = rbCargadorOtro.Checked;
                MarcarCambio();
            };
        tarjeta.Controls.Add(campos.Campo("El cargador contractual es", panelCargador));

        rejillaCargadorOtro.Visible = false;
        rejillaCargadorOtro.Margin = new Padding(0, 10, 0, 0);
        tarjeta.Controls.Add(RejillaParte(cargadorOtro, rejillaCargadorOtro));
        return tarjeta;
    }

    // Al escribir el nombre o el NIF salen los ya usados; al elegir uno se rellenan todos sus datos
    private static void SugerirPartes(ControlesParte c, List<ParteEnvio> usados)
    {
        _ = new Sugerencias<ParteEnvio>(c.Nombre, usados, p => p.Nombre, c.Rellenar);
        _ = new Sugerencias<ParteEnvio>(c.Nif, usados, p => p.Nif, c.Rellenar);
    }

    private Control RejillaParte(ControlesParte c, TableLayoutPanel rejilla)
    {
        var nombre = campos.Campo("Nombre", c.Nombre);
        rejilla.Controls.Add(nombre);
        rejilla.SetColumnSpan(nombre, 2);
        rejilla.Controls.Add(campos.Campo("NIF", c.Nif));
        rejilla.Controls.Add(campos.Campo("Teléfono", c.Telefono));

        var direccion = campos.Campo("Dirección", c.Direccion);
        rejilla.Controls.Add(direccion);
        rejilla.SetColumnSpan(direccion, 2);
        rejilla.Controls.Add(campos.Campo("Código postal", c.CodigoPostal));
        rejilla.Controls.Add(campos.Campo("Población", c.Poblacion));

        rejilla.Controls.Add(campos.Campo("Provincia", c.Provincia));
        rejilla.Controls.Add(campos.Campo("País", c.Pais));
        return rejilla;
    }

    private Control TarjetaTransporte(List<TransportistaDeca> usados)
    {
        var tarjeta = Componentes.Tarjeta("4   Transporte", "Quién lleva la mercancía, en qué vehículo y de dónde a dónde");

        // Al elegir uno ya usado se rellenan su NIF y la matrícula de su último DeCA
        void Rellenar(TransportistaDeca t)
        {
            txtTransportista.Text = t.Nombre;
            txtNifTransportista.Text = t.Nif;
            txtMatricula.Text = t.Matricula;
        }
        _ = new Sugerencias<TransportistaDeca>(txtTransportista, usados, t => t.Nombre, Rellenar);
        _ = new Sugerencias<TransportistaDeca>(txtNifTransportista, usados, t => t.Nif, Rellenar);

        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Fecha de carga", dtpCarga));
        rejilla.Controls.Add(campos.Campo("Transportista efectivo", txtTransportista));
        rejilla.Controls.Add(campos.Campo("NIF del transportista", txtNifTransportista));
        rejilla.Controls.Add(campos.Campo("Matrícula del vehículo", txtMatricula));

        // Si es un camión articulado, la normativa pide también la del remolque o semirremolque
        rejilla.Controls.Add(campos.Campo("Matrícula del remolque", txtMatriculaRemolque), 3, 1);

        var carga = campos.Campo("Lugar de carga", txtLugarCarga);
        rejilla.Controls.Add(carga, 0, 2);
        rejilla.SetColumnSpan(carga, 2);
        var descarga = campos.Campo("Lugar de descarga", txtLugarDescarga);
        rejilla.Controls.Add(descarga, 2, 2);
        rejilla.SetColumnSpan(descarga, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control TarjetaMercancia()
    {
        var btnAnadir = Tema.BotonSecundario("+  Añadir palet", 170);
        btnAnadir.Click += (s, e) => AnadirPalet();

        var tarjeta = Componentes.Tarjeta("5   Mercancía y palets",
            "", null, lblTotales);

        var mercancia = campos.Campo("Naturaleza de la mercancía", txtMercancia);
        tarjeta.Controls.Add(mercancia);

        tarjeta.Controls.Add(new Label
        {
            Text = "Cada palet nuevo copia el peso y las cajas del anterior.",
            AutoSize = true,
            ForeColor = Tema.Suave,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0, 0, 0, 8)
        });

        contenedorPalets.Dock = DockStyle.Fill;
        contenedorPalets.AutoSize = true;
        contenedorPalets.ColumnCount = 1;
        contenedorPalets.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contenedorPalets.Margin = new Padding(0);
        btnAnadir.Margin = new Padding(0, 6, 0, 15);   // debajo del último palet, que es donde se escribe

        lblErrorPalets.Margin = new Padding(0, 0, 0, 10);

        tarjeta.Controls.Add(EncabezadoPalets());
        tarjeta.Controls.Add(contenedorPalets);
        tarjeta.Controls.Add(btnAnadir);
        tarjeta.Controls.Add(lblErrorPalets);
        tarjeta.Controls.Add(campos.Campo("Observaciones", txtObservaciones));
        return tarjeta;
    }

    private Control CrearAcciones()
    {
        var btnGuardar = Tema.BotonPrincipal(existente == null ? "Guardar DeCA" : "Guardar cambios", 180);
        btnGuardar.Click += (s, e) => Guardar();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.Click += (s, e) => Close();
        CancelButton = btnCancelar;   // Esc cierra (avisando si hay cambios)

        return Componentes.Acciones(btnGuardar, btnCancelar);
    }

    // ---------- Palets ----------

    private static TableLayoutPanel PlantillaFilaPalet()
    {
        var fila = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5 };
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));   // el error, a la derecha
        return fila;
    }

    private static Control EncabezadoPalets()
    {
        var encabezado = PlantillaFilaPalet();
        encabezado.BackColor = Tema.CabeceraTabla;

        string[] titulos = { "PALET", "PESO (KG)", "Nº DE CAJAS", "", "" };
        for (int i = 0; i < titulos.Length; i++)
        {
            encabezado.Controls.Add(new Label
            {
                Text = titulos[i],
                Dock = DockStyle.Fill,
                Height = 36,
                Padding = new Padding(i == 0 ? 10 : 0, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Tema.Suave,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            }, i, 0);
        }
        return encabezado;
    }

    private void AnadirPalet(PaletDeca? datos = null)
    {
        // Sin datos, copia el último palet: lo normal es que sean todos iguales
        var anterior = palets.LastOrDefault();

        var panel = PlantillaFilaPalet();
        panel.Margin = new Padding(0, 6, 0, 0);

        var lblNumero = new Label { TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 0, 0) };
        // El máximo es amplio a propósito: si se pasa del permitido, lo dice la validación
        // en vez de recortarlo sin avisar
        var numPeso = new NumericUpDown { DecimalPlaces = 2, Maximum = 99999, Increment = 10, ThousandsSeparator = true };
        var numCajas = new NumericUpDown { Maximum = 100000, ThousandsSeparator = true };
        numPeso.Value = datos?.PesoKg ?? anterior?.Peso.Value ?? 0;
        numCajas.Value = datos?.NumCajas ?? anterior?.Cajas.Value ?? 0;

        var btnQuitar = new Button
        {
            Text = "✕",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Firebrick,
            Cursor = Cursors.Hand
        };
        btnQuitar.FlatAppearance.BorderSize = 0;

        var lblError = CamposFormulario.EtiquetaError();
        lblError.Margin = new Padding(8, 6, 0, 0);

        var fila = new FilaPalet(panel, lblNumero, numPeso, numCajas, lblError);

        numPeso.ValueChanged += (s, e) => { lblError.Visible = false; ActualizarTotales(); };
        numCajas.ValueChanged += (s, e) => { lblError.Visible = false; ActualizarTotales(); };
        btnQuitar.Click += (s, e) =>
        {
            if (palets.Count <= 1) return;   // siempre queda al menos uno
            palets.Remove(fila);
            contenedorPalets.Controls.Remove(panel);
            panel.Dispose();
            MarcarCambio();
            ActualizarTotales();
        };

        Control[] controles = { lblNumero, numPeso, numCajas, btnQuitar, lblError };
        for (int i = 0; i < controles.Length; i++)
        {
            if (i < 4) controles[i].Dock = DockStyle.Fill;
            if (i is 1 or 2) controles[i].Margin = new Padding(0, 0, 15, 0);
            panel.Controls.Add(controles[i], i, 0);
        }

        palets.Add(fila);
        contenedorPalets.Controls.Add(panel);
        lblErrorPalets.Visible = false;
        if (!cargando)
        {
            VigilarCambios(panel);
            MarcarCambio();
            numPeso.Focus();
            numPeso.Select(0, numPeso.Text.Length);
        }
        ActualizarTotales();
    }

    // Renumera los palets y pone los totales en el subtítulo de la tarjeta
    private void ActualizarTotales()
    {
        for (int i = 0; i < palets.Count; i++)
            palets[i].Numero.Text = $"Palet {i + 1}";

        int cajas = (int)palets.Sum(p => p.Cajas.Value);
        decimal peso = palets.Sum(p => p.Peso.Value);
        lblTotales.Text = $"{palets.Count} {(palets.Count == 1 ? "palet" : "palets")}  ·  " +
                          $"{cajas.ToString("N0", Componentes.Es)} cajas  ·  {peso.ToString("N2", Componentes.Es)} kg";
    }

    // ---------- Datos ----------

    private void Rellenar(DatosDeca d)
    {
        remitente.Rellenar(d.Remitente);
        destinatario.Rellenar(d.Destinatario);
        rbCargadorRemitente.Checked = d.CargadorTipo == TipoCargador.Remitente;
        rbCargadorDestinatario.Checked = d.CargadorTipo == TipoCargador.Destinatario;
        rbCargadorOtro.Checked = d.CargadorTipo == TipoCargador.Otro;
        if (d.CargadorOtro != null) cargadorOtro.Rellenar(d.CargadorOtro);
        dtpCarga.Value = d.FechaCarga;
        txtTransportista.Text = d.TransportistaNombre;
        txtNifTransportista.Text = d.TransportistaNif;
        txtMatricula.Text = d.Matricula;
        txtMatriculaRemolque.Text = d.MatriculaRemolque ?? "";
        txtLugarCarga.Text = d.LugarCarga ?? "";
        txtLugarDescarga.Text = d.LugarDescarga ?? "";
        txtMercancia.Text = d.Mercancia;
        txtObservaciones.Text = d.Observaciones ?? "";
        foreach (var palet in d.Palets)
            AnadirPalet(palet);
    }

    private string CargadorElegido() =>
        rbCargadorRemitente.Checked ? TipoCargador.Remitente
        : rbCargadorDestinatario.Checked ? TipoCargador.Destinatario
        : rbCargadorOtro.Checked ? TipoCargador.Otro
        : "";

    private DatosDeca Leer() => new(
        remitente.Leer(),
        destinatario.Leer(),
        CargadorElegido(),
        rbCargadorOtro.Checked ? cargadorOtro.Leer() : null,
        dtpCarga.Value.Date,
        txtTransportista.Text,
        txtNifTransportista.Text,
        txtMatricula.Text,
        txtMatriculaRemolque.Text,
        txtLugarCarga.Text,
        txtLugarDescarga.Text,
        txtMercancia.Text,
        txtObservaciones.Text,
        palets.Select(p => new PaletDeca(p.Peso.Value, (int)p.Cajas.Value)).ToList());

    // ---------- Guardado ----------

    private void Guardar()
    {
        // 1. Se marcan a la vez todos los campos con error y el foco va al primero
        campos.LimpiarErrores();
        lblErrorPalets.Visible = false;
        Control? primerError = null;

        bool fechaCambiada = existente == null || dtpCarga.Value.Date != existente.Datos.FechaCarga.Date;
        var comprobaciones = remitente.Comprobaciones()
            .Concat(destinatario.Comprobaciones())
            .Append((panelCargador, () => DecaServicio.ErrorCargadorTipo(CargadorElegido())))
            .Concat(rbCargadorOtro.Checked ? cargadorOtro.Comprobaciones() : [])
            .Concat(new (Control, Func<string?>)[]
            {
                (dtpCarga, () => fechaCambiada ? DecaServicio.ErrorFechaCarga(dtpCarga.Value) : null),
                (txtTransportista, () => DecaServicio.ErrorObligatorio(txtTransportista.Text, "el transportista", 150)),
                (txtNifTransportista, () => DecaServicio.ErrorNifTransportista(txtNifTransportista.Text)),
                (txtMatricula, () => DecaServicio.ErrorObligatorio(txtMatricula.Text, "la matrícula", 20)),
                (txtMatriculaRemolque, () => DecaServicio.ErrorOpcional(txtMatriculaRemolque.Text, 20)),
                (txtLugarCarga, () => DecaServicio.ErrorOpcional(txtLugarCarga.Text, 250)),
                (txtLugarDescarga, () => DecaServicio.ErrorOpcional(txtLugarDescarga.Text, 250)),
                (txtMercancia, () => DecaServicio.ErrorObligatorio(txtMercancia.Text, "la mercancía", 250))
            });

        foreach (var (control, comprobar) in comprobaciones)
        {
            if (comprobar() is not string error) continue;
            campos.MarcarError(control, error);
            primerError ??= control;
        }

        foreach (var palet in palets)
        {
            palet.Error.Visible = false;
            if (DecaServicio.ErrorPalet(palet.Peso.Value, (int)palet.Cajas.Value) is not string error) continue;
            palet.Error.Text = error;
            palet.Error.Visible = true;
            primerError ??= palet.Peso.Value <= 0 || palet.Peso.Value > DecaServicio.PesoMaximoPalet
                ? palet.Peso : palet.Cajas;
        }

        if (primerError != null)
        {
            primerError.Focus();
            return;
        }

        // 2. Guardar (el servicio vuelve a comprobarlo todo)
        try
        {
            if (existente == null)
                IdGuardado = servicio.CrearDeca(Leer());
            else
            {
                servicio.ModificarDeca(existente.Id, Leer());
                IdGuardado = existente.Id;
            }
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "Revisa los datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo guardar el DeCA.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    // ---------- PDF y QR ----------

    private void ActualizarBotonPdf() => btnPdf.Text = pdfAlDia ? "Ver PDF y QR" : "Generar PDF y QR";

    // El PDF sale de lo guardado: si hay cambios, primero se guardan (y el formulario se cierra después)
    private void PulsarPdf()
    {
        if (hayCambios)
        {
            var respuesta = MessageBox.Show(this,
                "Para generar el PDF hay que guardar antes los cambios.\n\n¿Guardar y generar el PDF?",
                "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (respuesta != DialogResult.Yes) return;

            Guardar();
            if (DialogResult != DialogResult.OK) return;   // había errores en los datos
            pdfAlDia = false;   // al guardar cambios el PDF anterior deja de valer
        }
        GenerarPdfYQr();
    }

    // Genera el PDF y el QR, sube el PDF a la web y abre la carpeta con los dos. Con "Ver", si ya
    // están en la carpeta de este ordenador solo se abre; si no (se generaron en otro), se vuelven
    // a generar: es un archivo nuevo en la web, pero el anterior sigue ahí.
    private void GenerarPdfYQr()
    {
        int id = existente!.Id;
        string ruta;
        Cursor = Cursors.WaitCursor;
        try
        {
            string? guardado = pdfAlDia ? servicio.ObtenerDeca(id)?.ArchivoPdf : null;
            ruta = guardado != null && File.Exists(Path.Combine(DecaServicio.CarpetaPdf, guardado))
                ? Path.Combine(DecaServicio.CarpetaPdf, guardado)
                : servicio.GenerarPdfYQr(id);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "DeCA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo generar el PDF.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            Cursor = Cursors.Default;
        }

        if (!pdfAlDia && lblEstadoPdf != null)
            lblEstadoPdf.Text = "Generado el " + Componentes.Fecha(DateTime.Now);
        pdfAlDia = true;
        ActualizarBotonPdf();

        // Explorador abierto en la carpeta con el PDF marcado; el QR está justo al lado
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{ruta}\"");
    }

    // ---------- Cambios sin guardar ----------

    // Cualquier cambio en un campo de texto, número o fecha cuenta como cambio
    private void VigilarCambios(Control raiz)
    {
        foreach (Control control in raiz.Controls)
        {
            switch (control)
            {
                case TextBox texto: texto.TextChanged += (s, e) => MarcarCambio(); break;
                case NumericUpDown numero: numero.ValueChanged += (s, e) => MarcarCambio(); break;
                case DateTimePicker fecha: fecha.ValueChanged += (s, e) => MarcarCambio(); break;
                default: VigilarCambios(control); break;
            }
        }
    }

    private void MarcarCambio()
    {
        if (!cargando) hayCambios = true;
    }

    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        var respuesta = MessageBox.Show(this,
            "Tienes cambios sin guardar. ¿Quieres salir sin guardarlos?",
            "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
        if (respuesta == DialogResult.No) e.Cancel = true;
    }
}
