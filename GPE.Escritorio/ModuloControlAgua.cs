using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Control de agua (la hoja AGUA, MT-RG09): como en la rutina de calidad, se elige el día y
// debajo se apuntan en una tabla los puntos de agua revisados, con el cloro y los
// organolépticos. Vive dentro de FormPrincipal.
public class ModuloControlAgua : UserControl
{
    private readonly AguaServicio servicio = new();
    private List<AguaToma> tomas = new();
    private DateTime fecha = DateTime.Today;
    private List<ControlAgua> originales = new();   // los del día tal como se leyeron
    private bool hayCambios;
    private bool cargando;

    private readonly DateTimePicker dtpFecha = new();
    private readonly Label lblDia = new();
    private readonly Label lblEstado = new();
    private readonly DataGridView grid = new();
    private readonly DataGridViewComboBoxColumn columnaToma = new();
    private readonly Button btnGuardar = Tema.BotonPrincipal("Guardar", 150);

    private const string ColToma = "toma", ColCloro = "cloro", ColSabor = "sabor", ColOlor = "olor",
        ColColor = "color", ColTurbidez = "turbidez", ColComentario = "comentario";

    public ModuloControlAgua()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());
        Load += (s, e) =>
        {
            Cargar(DateTime.Today, recargarTomas: true);
            // La ventana principal no sabe de este día: avisa él al cerrar con cambios
            if (FindForm() is Form ventana) ventana.FormClosing += AvisarAlCerrar;
        };
    }

    private Control CrearContenido()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(40, 30, 40, 20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearBarra(), 0, 1);
        layout.Controls.Add(CrearAcciones(), 0, 2);
        layout.Controls.Add(ConfigurarGrid(), 0, 3);
        return layout;
    }

    private Control CrearCabecera()
    {
        var cabecera = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titulos = new Panel { Dock = DockStyle.Fill };
        titulos.Controls.Add(new Label
        {
            Text = "Cloro y organolépticos de los puntos de agua (MT-RG09).",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        });
        titulos.Controls.Add(new Label
        {
            Text = "Control de agua",
            Dock = DockStyle.Top,
            Height = 50,
            Font = Tema.FuenteTitulo,
            ForeColor = Tema.Texto
        });

        btnGuardar.Height = 46;
        btnGuardar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnGuardar.Margin = new Padding(0, 10, 0, 0);
        btnGuardar.Enabled = false;
        btnGuardar.Click += (s, e) => Guardar();

        cabecera.Controls.Add(titulos, 0, 0);
        cabecera.Controls.Add(btnGuardar, 1, 0);
        return cabecera;
    }

    // El día, como en la rutina: calendario, flechas y "Hoy"
    private Control CrearBarra()
    {
        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 15, 0)
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        dtpFecha.Width = 135;
        dtpFecha.Format = DateTimePickerFormat.Short;
        dtpFecha.MaxDate = DateTime.Today;
        dtpFecha.ValueChanged += (s, e) => { if (!cargando) CambiarDia(dtpFecha.Value.Date); };

        Button Flecha(string texto, int dias)
        {
            var boton = Tema.BotonSecundario(texto, 40);
            boton.Height = 32;
            boton.Margin = new Padding(0, 22, 6, 0);
            boton.Click += (s, e) =>
            {
                var dia = dtpFecha.Value.Date.AddDays(dias);
                if (dia <= dtpFecha.MaxDate) CambiarDia(dia);
            };
            return boton;
        }
        var btnHoy = Tema.BotonSecundario("Hoy", 60);
        btnHoy.Height = 32;
        btnHoy.Margin = new Padding(0, 22, 20, 0);
        btnHoy.Click += (s, e) => CambiarDia(DateTime.Today);

        var filtros = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        filtros.Controls.Add(Componentes.CampoFiltro("Día", dtpFecha));
        filtros.Controls.Add(Flecha("◀", -1));
        filtros.Controls.Add(Flecha("▶", 1));
        filtros.Controls.Add(btnHoy);

        var datos = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10, 6, 0, 0)
        };
        lblDia.AutoSize = true;
        lblDia.ForeColor = Tema.Texto;
        lblDia.Font = Tema.FuenteNegrita;
        lblEstado.AutoSize = true;
        lblEstado.ForeColor = Tema.Suave;
        lblEstado.Margin = new Padding(0, 4, 0, 0);
        datos.Controls.Add(lblDia);
        datos.Controls.Add(lblEstado);

        var btnRecargar = Tema.BotonSecundario("Recargar", 110);
        btnRecargar.Height = 34;
        btnRecargar.Margin = new Padding(0, 14, 0, 0);
        btnRecargar.Click += (s, e) =>
        {
            if (DescartarCambios()) Cargar(fecha, recargarTomas: true);
        };

        barra.Controls.Add(filtros, 0, 0);
        barra.Controls.Add(datos, 1, 0);
        barra.Controls.Add(btnRecargar, 2, 0);
        return barra;
    }

    private Control CrearAcciones()
    {
        var acciones = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 14, 0, 0) };
        var btnAnadir = Tema.BotonSecundario("+  Añadir fila", 160);
        btnAnadir.Height = 34;
        btnAnadir.Click += (s, e) => AnadirFila();
        acciones.Controls.Add(btnAnadir);
        acciones.Controls.Add(new Label
        {
            Text = "Elige el punto de agua y apunta lo revisado. Para quitar una fila, selecciónala y pulsa Supr.",
            AutoSize = true,
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena,
            Margin = new Padding(14, 9, 0, 0)
        });
        return acciones;
    }

    private Control ConfigurarGrid()
    {
        Componentes.EstiloTabla(grid, altoFila: 36);
        grid.Dock = DockStyle.Fill;
        grid.AllowUserToDeleteRows = true;
        grid.EditMode = DataGridViewEditMode.EditOnEnter;
        grid.GridColor = Tema.LineaTabla;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.MultiSelect = false;

        columnaToma.Name = ColToma;
        columnaToma.HeaderText = "PUNTO DE AGUA";
        columnaToma.FillWeight = 160;
        columnaToma.FlatStyle = FlatStyle.Flat;
        columnaToma.DisplayMember = nameof(AguaToma.Nombre);
        columnaToma.ValueMember = nameof(AguaToma.Id);
        grid.Columns.Add(columnaToma);

        var cloro = new DataGridViewTextBoxColumn
        {
            Name = ColCloro,
            HeaderText = "CLORO (ppm)",
            MaxInputLength = 5,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 110
        };
        cloro.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns.Add(cloro);

        foreach (var (nombre, cabecera) in new[] { (ColSabor, "SABOR OK"), (ColOlor, "OLOR OK"), (ColColor, "COLOR OK"), (ColTurbidez, "TURBIDEZ OK") })
            grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = nombre,
                HeaderText = cabecera,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Width = 100
            });

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ColComentario,
            HeaderText = "COMENTARIO",
            FillWeight = 140,
            MaxInputLength = AguaServicio.LargoComentario
        });

        // El punto y las casillas se apuntan en cuanto se eligen, sin esperar a salir de la celda
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.CurrentCell is DataGridViewComboBoxCell or DataGridViewCheckBoxCell)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (s, e) => { if (!cargando) MarcarCambios(); };
        grid.UserDeletedRow += (s, e) => MarcarCambios();
        grid.CellFormatting += Formatear;
        // Un punto que ya no está en la lista no rompe la tabla: se queda en blanco
        grid.DataError += (s, e) => e.ThrowException = false;
        return grid;
    }

    // El cloro fuera del rango del punto (si lo tiene), en rojo
    private void Formatear(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null || grid.Columns[e.ColumnIndex].Name != ColCloro) return;
        var fila = grid.Rows[e.RowIndex];
        if (Toma(fila) is AguaToma toma && LeerCloro(fila, out var cloro) && AguaServicio.FueraDeRango(cloro, toma))
        {
            e.CellStyle.BackColor = e.CellStyle.SelectionBackColor = Tema.FondoError;
            e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Tema.Error;
        }
    }

    // Filas

    private void AnadirFila()
    {
        grid.EndEdit();
        int i = grid.Rows.Add(DBNull.Value, "", true, true, true, true, "");
        grid.Focus();
        grid.CurrentCell = grid.Rows[i].Cells[ColToma];
        grid.BeginEdit(true);
        MarcarCambios();
    }

    private AguaToma? Toma(DataGridViewRow fila) =>
        fila.Cells[ColToma].Value is int id ? tomas.FirstOrDefault(t => t.Id == id) : null;

    // false si lo escrito no es un número. Acepta coma o punto.
    private static bool LeerCloro(DataGridViewRow fila, out decimal? cloro)
    {
        cloro = null;
        string texto = (fila.Cells[ColCloro].Value as string ?? "").Trim().Replace('.', ',');
        if (texto == "") return true;
        if (!decimal.TryParse(texto, System.Globalization.NumberStyles.Number, Componentes.Es, out var valor)) return false;
        cloro = valor;
        return true;
    }

    // Las filas como controles. Si un cloro no es un número, el mensaje para enseñar.
    private (List<ControlAgua> Controles, string? Error) LeerFilas()
    {
        var controles = new List<ControlAgua>();
        foreach (DataGridViewRow fila in grid.Rows)
        {
            if (fila.IsNewRow) continue;
            if (!LeerCloro(fila, out var cloro))
                return (controles, $"En la fila {fila.Index + 1}, el cloro tiene que ser un número (por ejemplo 0,55).");

            var original = fila.Tag as ControlAgua;
            controles.Add(new ControlAgua(
                Id: original?.Id ?? 0,
                Fecha: fecha,
                TomaId: fila.Cells[ColToma].Value as int? ?? 0,
                Cloro: cloro,
                SaborOk: fila.Cells[ColSabor].Value is true,
                OlorOk: fila.Cells[ColOlor].Value is true,
                ColorOk: fila.Cells[ColColor].Value is true,
                TurbidezOk: fila.Cells[ColTurbidez].Value is true,
                Comentario: fila.Cells[ColComentario].Value as string,
                CreadoPor: original?.CreadoPor,
                Version: original?.Version));
        }
        return (controles, null);
    }

    // Cargar y guardar

    private void Cargar(DateTime dia, bool recargarTomas)
    {
        try
        {
            if (recargarTomas) tomas = servicio.ObtenerTomas();
            originales = servicio.ObtenerDia(dia);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar el control de agua de la base de datos.", ex);
            return;
        }
        fecha = dia.Date;

        cargando = true;
        dtpFecha.MaxDate = DateTime.Today;   // por si GPE lleva abierto desde ayer
        dtpFecha.Value = fecha;
        columnaToma.DataSource = tomas;
        grid.Rows.Clear();
        foreach (var control in originales)
        {
            int i = grid.Rows.Add(control.TomaId, control.Cloro?.ToString("0.00", Componentes.Es) ?? "",
                control.SaborOk, control.OlorOk, control.ColorOk, control.TurbidezOk, control.Comentario ?? "");
            var fila = grid.Rows[i];
            fila.Tag = control;
            fila.Cells[ColToma].ToolTipText = $"Apuntado por {control.CreadoPor}";
        }
        grid.ClearSelection();
        cargando = false;

        hayCambios = false;
        btnGuardar.Enabled = false;
        ActualizarResumen();
    }

    private void CambiarDia(DateTime dia)
    {
        if (dia == fecha) return;
        if (!DescartarCambios())
        {
            cargando = true;
            dtpFecha.Value = fecha;
            cargando = false;
            return;
        }
        Cargar(dia, recargarTomas: false);
    }

    private void Guardar()
    {
        grid.EndEdit();
        var (controles, error) = LeerFilas();
        try
        {
            if (error != null) throw new ReglaNegocioException(error);
            servicio.GuardarDia(fecha, controles, originales, tomas);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede guardar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar el control de agua.", ex);
            return;
        }

        // Se vuelve a leer para tener los ids y las versiones nuevas
        Cargar(fecha, recargarTomas: false);
    }

    private void MarcarCambios()
    {
        hayCambios = true;
        btnGuardar.Enabled = true;
        ActualizarResumen();
    }

    private void ActualizarResumen()
    {
        string dia = fecha == DateTime.Today ? "hoy" : Componentes.Fecha(fecha);
        lblDia.Text = $"Control de agua de {dia}";

        int filas = grid.Rows.Cast<DataGridViewRow>().Count(f => !f.IsNewRow);
        lblEstado.Text = string.Join("  ·  ", new[]
        {
            filas == 0 ? "Sin controles" : $"{filas} {(filas == 1 ? "punto revisado" : "puntos revisados")}",
            hayCambios ? "Cambios sin guardar" : null
        }.OfType<string>());
        lblEstado.ForeColor = hayCambios ? Tema.Aviso : Tema.Suave;
    }

    private bool DescartarCambios() =>
        !hayCambios || Mensajes.DescartarCambios(this, "Hay cambios sin guardar en el control de agua. ¿Descartarlos?");

    private void AvisarAlCerrar(object? sender, FormClosingEventArgs e)
    {
        if (!e.Cancel && !DescartarCambios()) e.Cancel = true;
    }
}
