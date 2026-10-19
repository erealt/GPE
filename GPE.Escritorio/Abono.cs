using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;

// Pantalla del abono, que también cierra la devolución. Muestra los pasos 5 y 6:
//  - En la fase 4 se rellena el abono y "Registrar abono y cerrar" pasa a la fase 6.
//  - En la fase 6 se ve una ficha del abono registrado, con "Reabrir abono" para corregirlo.
public class FormAbono : PantallaFase
{
    public const int PasoAbono = 5, PasoCierre = 6;

    private static readonly CultureInfo Es = new("es-ES");

    private static readonly Color FondoSoloLectura = Color.FromArgb(247, 248, 246);
    private static readonly Color FondoDiferencia = Color.FromArgb(255, 241, 214);
    private static readonly Color TextoAviso = Color.FromArgb(150, 90, 0);
    private static readonly Color FondoCorrecto = Color.FromArgb(225, 245, 232);
    private static readonly Color TextoCorrecto = Color.FromArgb(25, 120, 70);

    private const decimal ImporteMaximo = 99_999_999.99m;   // DECIMAL(10,2)

    private readonly DevolucionServicio servicio = new();
    private DevolucionFila devolucion;
    private int paso;
    private AbonoGuardado? abono;
    private List<LineaRevision> lineas = new();

    // Se crean de nuevo cada vez que se reconstruye la pantalla
    private FlowLayoutPanel panelResultado = new();
    private RadioButton rbTotal = new(), rbParcial = new(), rbNinguno = new();
    private TextBox txtNumero = new();
    private Label lblAvisoNumero = new();
    private DateTimePicker dtpFecha = new();
    private TextBox txtImporte = new();
    private TextBox txtObservaciones = new();
    private ControlAdjuntos documento = null!;
    private Control campoNumero = new(), campoImporte = new(), campoDocumento = new();
    private Label lblFaltan = new();
    private readonly Dictionary<Control, Label> errores = new();

    private bool cargando;     // mientras se rellenan los campos con lo guardado
    private bool hayCambios;   // para avisar al cerrar sin registrar

    private bool Editable => devolucion.Fase == 4;

    public FormAbono(DevolucionFila devolucion, int paso)
    {
        this.devolucion = devolucion;
        this.paso = paso;

        Text = $"Abono · {devolucion.Numero}";
        Size = new Size(1150, 900);
        MinimumSize = new Size(950, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        FormClosing += AvisarCambiosSinGuardar;

        Construir();
    }

    // Se vuelve a llamar al reabrir el abono
    private void Construir()
    {
        SuspendLayout();
        Controls.Clear();
        CancelButton = null;
        errores.Clear();
        hayCambios = false;

        try
        {
            lineas = servicio.ObtenerLineasRevision(devolucion.Id);
            abono = servicio.ObtenerAbono(devolucion.Id);
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudieron cargar los datos de la devolución.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        var (scroll, pila) = Componentes.Pagina();

        bool cerrada = devolucion.Fase >= 6;
        pila.Controls.Add(Componentes.Cabecera(
            $"DEVOLUCIONES / {devolucion.Numero} / ABONO",
            cerrada ? "Devolución cerrada" : "Abono y cierre",
            cerrada
                ? $"Abono de la devolución {devolucion.Numero} de {devolucion.Cliente}."
                : $"Registra el abono de la devolución {devolucion.Numero} de {devolucion.Cliente} para cerrarla."));
        pila.Controls.Add(Progreso(devolucion, paso));
        pila.Controls.Add(Componentes.Resumen(
            ("CLIENTE", devolucion.Cliente),
            ("ALBARÁN", devolucion.Albaran ?? "—"),
            ("MOTIVO", devolucion.Motivo),
            ("FECHA LLEGADA", Fecha(devolucion.FechaLlegada))));

        if (devolucion.Fase < 4)
        {
            pila.Controls.Add(Componentes.Tarjeta("La revisión del almacén no ha terminado",
                "El abono se registra cuando el almacén finaliza la revisión de la mercancía."));
            pila.Controls.Add(Componentes.Acciones(BotonCerrar()));
        }
        else if (Editable)
        {
            pila.Controls.Add(CrearTarjetaRevision());
            pila.Controls.Add(CrearTarjetaAbono());
            pila.Controls.Add(CrearAccionesRegistro());
            CargarAbono();
        }
        else
        {
            // Cerrada: primero lo que se abonó, la revisión queda como referencia debajo
            pila.Controls.Add(CrearFichaAbono());
            pila.Controls.Add(CrearTarjetaRevision());
            pila.Controls.Add(CrearAccionesConsulta());
        }

        Controls.Add(scroll);
        ResumeLayout();
    }

    private Button BotonCerrar()
    {
        var boton = Tema.BotonSecundario("Cerrar", 130);
        boton.DialogResult = DialogResult.Cancel;
        CancelButton = boton;
        return boton;
    }

    private static Button BotonReabrir(string texto, int ancho, Action accion)
    {
        var boton = Tema.BotonSecundario(texto, ancho);
        boton.ForeColor = Tema.Error;
        boton.Click += (s, e) => accion();
        return boton;
    }

    // Etiqueta de color con texto, p. ej. "✓ Todo correcto"
    private static Label Etiqueta(string texto, Color fondo, Color color, float tamano = 9.5f) => new()
    {
        Text = texto,
        AutoSize = true,
        BackColor = fondo,
        ForeColor = color,
        Font = new Font("Segoe UI", tamano, FontStyle.Bold),
        Padding = new Padding(10, 5, 10, 5),
        Margin = new Padding(0, 0, 10, 0)
    };

    // ---------- Resumen de la revisión ----------

    // Si todo llegó y está apto basta una línea; la tabla solo se abre de entrada si hay diferencias
    private Control CrearTarjetaRevision()
    {
        bool hayDiferencias = lineas.Any(l => l.Recibida != l.Solicitada || l.Apta != l.Recibida);

        var tarjeta = Componentes.Tarjeta("Resultado de la revisión", hayDiferencias
            ? "Hay diferencias entre lo solicitado, lo recibido y lo apto: revísalas antes de decidir el abono."
            : "Lo que el almacén ha recibido y revisado.");

        int solicitadas = lineas.Sum(l => l.Solicitada);
        int recibidas = lineas.Sum(l => l.Recibida ?? 0);
        int aptas = lineas.Sum(l => l.Apta ?? 0);

        var cifras = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 10) };
        cifras.Controls.Add(hayDiferencias
            ? Etiqueta("⚠ Hay diferencias", FondoDiferencia, TextoAviso)
            : Etiqueta("✓ Todo llegó y está en buen estado", FondoCorrecto, TextoCorrecto));
        cifras.Controls.Add(new Label
        {
            Text = $"{solicitadas} solicitadas  ·  {recibidas} recibidas  ·  {aptas} aptas  ·  {recibidas - aptas} no aptas",
            AutoSize = true,
            ForeColor = Tema.Texto,
            Margin = new Padding(0, 5, 0, 0)
        });
        tarjeta.Controls.Add(cifras);

        var tabla = CrearTablaRevision();
        tabla.Visible = hayDiferencias;
        tarjeta.Controls.Add(tabla);

        string? observaciones = null;
        List<AdjuntoInfo> fotos = new();
        try
        {
            observaciones = servicio.ObtenerObservacionesRevision(devolucion.Id);
            fotos = servicio.ObtenerAdjuntos(devolucion.Id, TipoAdjunto.Foto);
        }
        catch (DbException)
        {
            // Son informativas: sin ellas la pantalla sigue siendo útil
        }

        if (!string.IsNullOrWhiteSpace(observaciones))
        {
            tarjeta.Controls.Add(new Label
            {
                Text = "Observaciones del almacén",
                AutoSize = true,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 4)
            });
            tarjeta.Controls.Add(new Label
            {
                Text = observaciones,
                AutoSize = true,
                MaximumSize = new Size(1000, 0),
                ForeColor = Tema.Texto
            });
        }

        var botones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 12, 0, 0) };

        var btnProductos = Tema.BotonSecundario(hayDiferencias ? "Ocultar productos" : "Ver productos", 170);
        btnProductos.Height = 36;
        btnProductos.Margin = new Padding(0, 0, 8, 0);
        btnProductos.Click += (s, e) =>
        {
            tabla.Visible = !tabla.Visible;
            btnProductos.Text = tabla.Visible ? "Ocultar productos" : "Ver productos";
        };
        botones.Controls.Add(btnProductos);

        var btnFotos = Tema.BotonSecundario(fotos.Count == 0 ? "Sin fotos" : $"Ver fotos ({fotos.Count})", 150);
        btnFotos.Height = 36;
        btnFotos.Enabled = fotos.Count > 0;
        btnFotos.Click += (s, e) =>
        {
            using var visor = new FormVisorImagen(fotos, 0, servicio.ObtenerContenidoAdjunto);
            visor.ShowDialog(this);
        };
        botones.Controls.Add(btnFotos);

        tarjeta.Controls.Add(botones);
        return tarjeta;
    }

    private DataGridView CrearTablaRevision()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Top,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Tema.Linea,
            ScrollBars = ScrollBars.None,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 40,
            Margin = new Padding(0, 0, 0, 5)
        };
        grid.RowTemplate.Height = 36;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Tema.CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Tema.CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Suave;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        grid.DefaultCellStyle.ForeColor = Tema.Texto;
        grid.DefaultCellStyle.SelectionBackColor = Color.White;
        grid.DefaultCellStyle.SelectionForeColor = Tema.Texto;
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);

        foreach (var (titulo, peso, numero) in new[]
                 {
                     ("PRODUCTO", 25f, false), ("LOTE", 10f, false), ("SOLICITADA", 13f, true),
                     ("RECIBIDA", 11f, true), ("APTA", 9f, true), ("NO APTA", 10f, true),
                     ("OBSERVACIONES", 28f, false)
                 })
        {
            var columna = new DataGridViewTextBoxColumn
            {
                HeaderText = titulo,
                FillWeight = peso,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (numero) columna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns.Add(columna);
        }

        foreach (var linea in lineas)
        {
            int? noApta = linea.Recibida - linea.Apta;
            int i = grid.Rows.Add(linea.Producto, linea.Lote ?? "—", linea.Solicitada,
                linea.Recibida?.ToString() ?? "—", linea.Apta?.ToString() ?? "—",
                noApta?.ToString() ?? "—", linea.Observaciones ?? "");

            // Diferencias en ámbar, como en la revisión del almacén
            if (linea.Recibida != linea.Solicitada)
                grid.Rows[i].Cells[3].Style.BackColor = FondoDiferencia;
            if (noApta > 0)
                grid.Rows[i].Cells[5].Style.BackColor = FondoDiferencia;
        }

        if (lineas.Count > 1)
        {
            int total = grid.Rows.Add("Total", "",
                lineas.Sum(l => l.Solicitada), lineas.Sum(l => l.Recibida ?? 0),
                lineas.Sum(l => l.Apta ?? 0), lineas.Sum(l => (l.Recibida ?? 0) - (l.Apta ?? 0)), "");
            grid.Rows[total].DefaultCellStyle.BackColor = FondoSoloLectura;
            grid.Rows[total].DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        }

        // Sin scroll propio: la tabla mide lo que sus filas
        grid.Height = grid.ColumnHeadersHeight + grid.Rows.Count * grid.RowTemplate.Height + 2;
        grid.ClearSelection();
        return grid;
    }

    // ---------- Abono registrado (solo consulta) ----------

    private Control CrearFichaAbono()
    {
        var tarjeta = Componentes.Tarjeta("Abono registrado", abono?.FechaCierre is DateTime cierre
            ? $"La devolución se cerró el {Fecha(cierre)}. Si hay algún error, reabre el abono para corregirlo."
            : "Si hay algún error, reabre el abono para corregirlo.");
        if (abono == null) return tarjeta;

        bool hayAbono = abono.Resultado != ResultadoAbono.Ninguno;
        var (fondo, color) = abono.Resultado switch
        {
            ResultadoAbono.Total => (FondoCorrecto, TextoCorrecto),
            ResultadoAbono.Parcial => (FondoDiferencia, TextoAviso),
            _ => (Tema.CabeceraTabla, Tema.Suave)
        };

        var cabecera = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 15) };
        cabecera.Controls.Add(new Label
        {
            Text = hayAbono ? abono.Importe?.ToString("C", Es) : "Sin abono",
            AutoSize = true,
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = Tema.Texto,
            Margin = new Padding(0, 0, 15, 0)
        });
        var etiqueta = Etiqueta(NombreResultado(abono.Resultado), fondo, color, 10);
        etiqueta.Margin = new Padding(0, 14, 0, 0);
        cabecera.Controls.Add(etiqueta);
        tarjeta.Controls.Add(cabecera);

        var datos = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 0, 0, 10) };
        for (int i = 0; i < 3; i++) datos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        datos.Controls.Add(Dato("Nº DE ABONO", abono.Numero ?? "—"));
        datos.Controls.Add(Dato(hayAbono ? "FECHA DEL ABONO" : "FECHA DE LA DECISIÓN", Fecha(abono.Fecha)));
        datos.Controls.Add(Dato("FECHA DE CIERRE", Fecha(abono.FechaCierre)));
        tarjeta.Controls.Add(datos);

        if (!string.IsNullOrWhiteSpace(abono.Observaciones))
            tarjeta.Controls.Add(Dato("OBSERVACIONES", abono.Observaciones));

        if (hayAbono)
        {
            tarjeta.Controls.Add(new Label
            {
                Text = "DOCUMENTO DEL ABONO",
                AutoSize = true,
                ForeColor = Tema.Suave,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 4)
            });
            tarjeta.Controls.Add(new ControlAdjuntos(devolucion.Id, TipoAdjunto.Abono, "Adjuntar abono")
            {
                Dock = DockStyle.Top,
                Height = 130,
                SoloLectura = true
            });
        }

        return tarjeta;
    }

    // ETIQUETA arriba, valor debajo (como en Componentes.Resumen)
    private static Control Dato(string etiqueta, string valor)
    {
        var dato = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 15, 10)
        };
        dato.Controls.Add(new Label
        {
            Text = etiqueta,
            AutoSize = true,
            ForeColor = Tema.Suave,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        });
        dato.Controls.Add(new Label
        {
            Text = valor,
            AutoSize = true,
            MaximumSize = new Size(1000, 0),
            ForeColor = Tema.Texto,
            Margin = new Padding(0, 4, 0, 0)
        });
        return dato;
    }

    private static string NombreResultado(string resultado) => resultado switch
    {
        ResultadoAbono.Total => "Abono total",
        ResultadoAbono.Parcial => "Abono parcial",
        _ => "Sin abono"
    };

    // ---------- Datos del abono (editable) ----------

    private Control CrearTarjetaAbono()
    {
        var tarjeta = Componentes.Tarjeta("Abono",
            "Al registrar el abono la devolución queda cerrada. El documento se guarda al momento.");

        panelResultado = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        rbTotal = Opcion("Abono total");
        rbParcial = Opcion("Abono parcial");
        rbNinguno = Opcion("Sin abono");
        panelResultado.Controls.AddRange(new Control[] { rbTotal, rbParcial, rbNinguno });

        txtNumero = new TextBox { MaxLength = 30, PlaceholderText = "Nº del abono o factura rectificativa" };
        txtNumero.TextChanged += (s, e) => { lblAvisoNumero.Visible = false; AlCambiar(); };
        txtNumero.Leave += (s, e) => ComprobarNumeroRepetido();

        lblAvisoNumero = new Label
        {
            AutoSize = true,
            ForeColor = TextoAviso,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0, 4, 0, 0),
            Visible = false
        };

        dtpFecha = new DateTimePicker { Format = DateTimePickerFormat.Short };
        dtpFecha.ValueChanged += (s, e) => AlCambiar();

        txtImporte = new TextBox { MaxLength = 15, TextAlign = HorizontalAlignment.Right, PlaceholderText = "0,00" };
        txtImporte.TextChanged += (s, e) => AlCambiar();
        txtImporte.Leave += (s, e) =>
        {
            // Se reescribe con formato para que se vea cómo se ha entendido (3000.5 → 3.000,50)
            if (LeerImporte(txtImporte.Text) is decimal importe)
                txtImporte.Text = importe.ToString("N2", Es);
        };

        txtObservaciones = new TextBox
        {
            Multiline = true,
            Height = 70,
            MaxLength = 4000,
            ScrollBars = ScrollBars.Vertical
        };
        txtObservaciones.TextChanged += (s, e) => AlCambiar();

        // Se guarda al momento: no cuenta como cambio sin guardar
        documento = new ControlAdjuntos(devolucion.Id, TipoAdjunto.Abono, "Adjuntar abono")
        {
            Dock = DockStyle.Top,
            Height = 150
        };
        documento.ArchivosCambiados += (s, e) => Refrescar();

        // Resultado / Nº, fecha e importe en una fila / observaciones / documento
        var rejilla = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        for (int i = 0; i < 3; i++) rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));

        var campoResultado = Campo("Resultado", panelResultado);
        rejilla.Controls.Add(campoResultado);
        rejilla.SetColumnSpan(campoResultado, 3);

        campoNumero = Campo("Nº de abono", txtNumero, lblAvisoNumero);
        rejilla.Controls.Add(campoNumero);
        rejilla.Controls.Add(Campo("Fecha del abono", dtpFecha));
        campoImporte = Campo("Importe", ConSufijo(txtImporte, "€"), clave: txtImporte);
        rejilla.Controls.Add(campoImporte);

        var campoObservaciones = Campo("Observaciones", txtObservaciones);
        rejilla.Controls.Add(campoObservaciones);
        rejilla.SetColumnSpan(campoObservaciones, 3);

        campoDocumento = Campo("Documento del abono (PDF o imagen)", documento);
        rejilla.Controls.Add(campoDocumento);
        rejilla.SetColumnSpan(campoDocumento, 3);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Caja de texto con una unidad a la derecha ("€")
    private static Control ConSufijo(TextBox texto, string sufijo)
    {
        var panel = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        texto.Dock = DockStyle.Fill;
        texto.Margin = new Padding(0);
        panel.Controls.Add(texto, 0, 0);
        panel.Controls.Add(new Label
        {
            Text = sufijo,
            AutoSize = true,
            ForeColor = Tema.Suave,
            Margin = new Padding(6, 4, 0, 0)
        }, 1, 0);
        return panel;
    }

    private RadioButton Opcion(string texto)
    {
        var opcion = new RadioButton { Text = texto, AutoSize = true, Margin = new Padding(0, 0, 25, 0) };
        opcion.CheckedChanged += (s, e) => { if (opcion.Checked) AlCambiar(); };
        return opcion;
    }

    private string? ResultadoElegido() =>
        rbTotal.Checked ? ResultadoAbono.Total
        : rbParcial.Checked ? ResultadoAbono.Parcial
        : rbNinguno.Checked ? ResultadoAbono.Ninguno
        : null;

    private void CargarAbono()
    {
        cargando = true;

        if (abono != null)
        {
            // Abono reabierto: se parte de lo que se registró
            rbTotal.Checked = abono.Resultado == ResultadoAbono.Total;
            rbParcial.Checked = abono.Resultado == ResultadoAbono.Parcial;
            rbNinguno.Checked = abono.Resultado == ResultadoAbono.Ninguno;
            txtNumero.Text = abono.Numero ?? "";
            txtImporte.Text = abono.Importe?.ToString("N2", Es) ?? "";
            txtObservaciones.Text = abono.Observaciones ?? "";
        }
        else if (lineas.Count > 0 && lineas.All(l => l.Recibida == l.Solicitada && l.Apta == l.Recibida))
        {
            rbTotal.Checked = true;   // todo llegó y en buen estado: lo habitual es abonarlo todo
        }

        // Entre la llegada de la mercancía y hoy
        dtpFecha.MinDate = (devolucion.FechaLlegada ?? devolucion.FechaSolicitud).Date;
        dtpFecha.MaxDate = DateTime.Today < dtpFecha.MinDate ? dtpFecha.MinDate : DateTime.Today;
        DateTime fecha = abono?.Fecha ?? DateTime.Today;
        dtpFecha.Value = fecha < dtpFecha.MinDate ? dtpFecha.MinDate
            : fecha > dtpFecha.MaxDate ? dtpFecha.MaxDate : fecha;

        cargando = false;
        Refrescar();
    }

    // Cualquier cambio del usuario en los campos
    private void AlCambiar()
    {
        if (cargando) return;
        hayCambios = true;
        Refrescar();
    }

    // Muestra solo los campos que aplican, quita los errores ya corregidos y actualiza "Falta: ..."
    private void Refrescar()
    {
        if (!Editable || cargando) return;

        string? resultado = ResultadoElegido();
        bool hayAbono = resultado != ResultadoAbono.Ninguno;
        campoNumero.Visible = campoImporte.Visible = campoDocumento.Visible = hayAbono;

        txtObservaciones.PlaceholderText = resultado switch
        {
            ResultadoAbono.Parcial => "Obligatorio: por qué no se abona todo",
            ResultadoAbono.Ninguno => "Obligatorio: por qué no se abona",
            _ => "Opcional"
        };

        var pendientes = Pendientes();
        foreach (var (campo, etiqueta) in errores)
            if (etiqueta.Visible && pendientes.All(p => p.Campo != campo))
                etiqueta.Visible = false;

        if (pendientes.Count == 0)
        {
            lblFaltan.Text = "✓ Listo para registrar";
            lblFaltan.ForeColor = TextoCorrecto;
        }
        else
        {
            lblFaltan.Text = "Falta: " + string.Join(", ", pendientes.Select(p => p.Corto));
            lblFaltan.ForeColor = Tema.Suave;
        }
    }

    // Lo que impide registrar: campo, mensaje debajo del campo y nombre corto para "Falta: ..."
    private List<(Control Campo, string Mensaje, string Corto)> Pendientes()
    {
        var pendientes = new List<(Control, string, string)>();
        string? resultado = ResultadoElegido();

        if (resultado == null)
        {
            pendientes.Add((panelResultado, "Indica si se abona todo, una parte o nada.", "resultado"));
            return pendientes;
        }

        if (resultado != ResultadoAbono.Ninguno)
        {
            if (txtNumero.Text.Trim() == "")
                pendientes.Add((txtNumero, "Indica el número del abono.", "nº de abono"));

            string textoImporte = txtImporte.Text.Trim();
            decimal? importe = LeerImporte(textoImporte);
            if (textoImporte == "")
                pendientes.Add((txtImporte, "Indica el importe abonado.", "importe"));
            else if (importe == null)
                pendientes.Add((txtImporte, "Importe no válido. Escríbelo así: 1.250,50", "importe válido"));
            else if (importe <= 0)
                pendientes.Add((txtImporte, "El importe debe ser mayor que 0.", "importe"));

            if (documento.Cantidad == 0)
                pendientes.Add((documento, "Adjunta el documento del abono.", "documento"));
        }

        if (resultado != ResultadoAbono.Total && txtObservaciones.Text.Trim() == "")
            pendientes.Add((txtObservaciones, resultado == ResultadoAbono.Parcial
                ? "Explica por qué el abono es parcial."
                : "Explica por qué no se abona.", "observaciones"));

        return pendientes;
    }

    // Acepta "1250", "1.250,50", "1250,5" y también "1250.50" (punto como decimal si no es de miles)
    private static decimal? LeerImporte(string texto)
    {
        texto = texto.Replace("€", "").Replace(" ", "").Trim();
        if (texto == "") return null;

        int punto = texto.LastIndexOf('.');
        if (punto >= 0 && !texto.Contains(',') && texto.Length - punto - 1 != 3)
            texto = texto.Replace('.', ',');

        if (!decimal.TryParse(texto, NumberStyles.Number, Es, out decimal importe)) return null;
        if (importe != Math.Round(importe, 2) || importe > ImporteMaximo) return null;
        return importe;
    }

    // Aviso (no error): un mismo abono podría cubrir varias devoluciones
    private void ComprobarNumeroRepetido()
    {
        string numero = txtNumero.Text.Trim();
        if (numero == "") return;

        try
        {
            string? otra = servicio.DevolucionConNumeroAbono(numero, devolucion.Id);
            lblAvisoNumero.Text = otra == null ? "" : $"⚠ Este nº ya se usó en la devolución {otra}.";
            lblAvisoNumero.Visible = otra != null;
        }
        catch (DbException)
        {
            // Es solo un aviso: si no se puede comprobar, se sigue
        }
    }

    // ---------- Botones ----------

    private Control CrearAccionesRegistro()
    {
        var btnRegistrar = Tema.BotonPrincipal("Registrar abono y cerrar", 240);
        btnRegistrar.Click += (s, e) => RegistrarAbono();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;

        var btnReabrir = BotonReabrir("Reabrir revisión", 180, () =>
            Reabrir("Reabrir revisión",
                "La devolución volverá a \"Mercancía recibida\" para poder corregir la revisión. " +
                "Lo que hayas rellenado aquí sin registrar se perderá; el documento del abono se conserva.",
                () => IrAPaso(FormAlmacen.PasoRevision)));

        var acciones = Componentes.Acciones(btnRegistrar, btnCancelar, btnReabrir);

        // A la izquierda de los botones, lo que falta para poder registrar
        lblFaltan = new Label { AutoSize = true, Margin = new Padding(0, 13, 15, 0) };
        acciones.Controls.Add(lblFaltan);
        return acciones;
    }

    private Control CrearAccionesConsulta() =>
        Componentes.Acciones(BotonCerrar(), BotonReabrir("Reabrir abono", 170, () =>
            Reabrir("Reabrir abono",
                "La devolución dejará de estar cerrada y volverá a \"Revisada almacén\" para poder corregir el abono. " +
                "Los datos del abono se conservan.",
                () => Recargar(PasoAbono))));

    private void Reabrir(string titulo, string explicacion, Action despues)
    {
        string? motivo = PedirMotivo(titulo, explicacion);
        if (motivo == null) return;

        try
        {
            servicio.Reabrir(devolucion.Id, devolucion.Fase, motivo);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo reabrir", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo reabrir.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        hayCambios = false;
        despues();
    }

    private void Recargar(int nuevoPaso)
    {
        try
        {
            devolucion = servicio.ObtenerDevolucion(devolucion.Id) ?? devolucion;
        }
        catch (DbException)
        {
            // Si falla la lectura se sigue con los datos que había; FormPrincipal recarga al cerrar
        }
        paso = nuevoPaso;
        Construir();
    }

    // ---------- Registrar ----------

    private void RegistrarAbono()
    {
        // 1. Validaciones: se marcan todos los campos con error a la vez
        var pendientes = Pendientes();
        foreach (var (campo, mensaje, _) in pendientes)
        {
            errores[campo].Text = mensaje;
            errores[campo].Visible = true;
        }
        if (pendientes.Count > 0)
        {
            pendientes[0].Campo.Focus();
            return;
        }

        string resultado = ResultadoElegido()!;
        bool hayAbono = resultado != ResultadoAbono.Ninguno;
        string numero = txtNumero.Text.Trim();
        decimal? importe = hayAbono ? LeerImporte(txtImporte.Text) : null;
        string observaciones = txtObservaciones.Text.Trim();

        // 2. Confirmación (con aviso si el nº de abono ya se usó en otra devolución)
        string? repetido = null;
        if (hayAbono)
        {
            try { repetido = servicio.DevolucionConNumeroAbono(numero, devolucion.Id); }
            catch (DbException) { }
        }

        string pregunta = hayAbono
            ? $"Se registrará el abono {numero} de {importe!.Value.ToString("C", Es)} " +
              $"con fecha {dtpFecha.Value.ToString("dd/MM/yyyy", Es)}"
            : "Se registrará que esta devolución no tiene abono";
        string aviso = repetido == null ? "" : $"⚠ El nº de abono {numero} ya se usó en la devolución {repetido}.\n\n";

        if (MessageBox.Show(this,
                aviso + pregunta + " y la devolución quedará cerrada.\n" +
                "Si hay algún error podrás reabrir el abono.\n\n¿Registrar y cerrar?",
                "Registrar abono y cerrar", MessageBoxButtons.YesNo,
                repetido == null ? MessageBoxIcon.Question : MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        // 3. Guardar
        var datos = new DatosAbono(
            devolucion.Id,
            resultado,
            hayAbono ? numero : null,
            dtpFecha.Value.Date,
            importe,
            observaciones == "" ? null : observaciones);

        try
        {
            servicio.RegistrarAbono(datos, devolucion.FechaLlegada);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se pudo registrar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this, "No se pudo registrar el abono.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    // Los datos del abono solo se guardan al registrar: si se cierra antes, se avisa
    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (MessageBox.Show(this,
                "Los datos del abono solo se guardan al registrarlo.\n" +
                "Si sales ahora se perderán (el documento adjunto sí se ha guardado).\n\n¿Salir de todos modos?",
                "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            e.Cancel = true;
            DialogResult = DialogResult.None;
        }
    }

    // ---------- Campos ----------

    // Etiqueta + control + mensaje de error debajo. "clave" es el control al que se asocia
    // el error cuando el que se muestra es un contenedor (p. ej. el importe con su "€").
    private Control Campo(string etiqueta, Control control, Control? ayuda = null, Control? clave = null)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 15, 15)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Text = etiqueta,
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        });

        control.Dock = DockStyle.Top;
        panel.Controls.Add(control);
        if (ayuda != null) panel.Controls.Add(ayuda);

        var error = new Label
        {
            AutoSize = true,
            ForeColor = Tema.Error,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0, 4, 0, 0),
            Visible = false
        };
        errores[clave ?? control] = error;
        panel.Controls.Add(error);

        return panel;
    }

    private static string Fecha(DateTime? fecha) => fecha?.ToString("dd MMM yyyy", Es) ?? "—";
}
