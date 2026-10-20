using GPE.Datos;
using System.Globalization;

namespace GPE.Escritorio;

// Piezas de interfaz comunes a las pantallas de cada fase (recogida, almacén...)
public static class Componentes
{
    public static readonly CultureInfo Es = new("es-ES");

    // Fecha como se enseña en resúmenes y tablas: "20 oct 2026", o "—" si no hay
    public static string Fecha(DateTime? fecha) => fecha?.ToString("dd MMM yyyy", Es) ?? "—";

    // Panel con scroll y una columna donde se van apilando las secciones
    public static (Panel Scroll, TableLayoutPanel Pila) Pagina()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(40, 25, 40, 25)
        };

        var pila = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1
        };
        pila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        scroll.Controls.Add(pila);
        return (scroll, pila);
    }

    public static Control Cabecera(string ruta, string titulo, string subtitulo)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 115, Margin = new Padding(0, 0, 0, 10) };

        var lblSubtitulo = new Label
        {
            Text = subtitulo,
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        };
        var lblTitulo = new Label
        {
            Text = titulo,
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Tema.Texto
        };
        var lblRuta = new Label
        {
            Text = ruta,
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = Tema.Suave,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        // El último añadido queda arriba
        panel.Controls.Add(lblSubtitulo);
        panel.Controls.Add(lblTitulo);
        panel.Controls.Add(lblRuta);
        return panel;
    }

    // Fases completadas hasta la actual; la siguiente aparece como "en curso"
    public static Control Progreso(int fase) => new BarraProgresoDevolucion
    {
        FasesCompletadas = fase,
        FaseActual = Fase.PasoEnCurso(fase),
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 0, 0, 20)
    };

    // Fila de datos de referencia: ETIQUETA arriba, valor debajo
    public static Control Resumen(params (string Etiqueta, string Valor)[] datos)
    {
        var resumen = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = datos.Length,
            BackColor = Color.White,
            Padding = new Padding(25, 15, 25, 15),
            Margin = new Padding(0, 0, 0, 20)
        };

        foreach (var (etiqueta, valor) in datos)
        {
            resumen.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / datos.Length));

            var dato = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Margin = new Padding(0, 0, 15, 0)
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
                ForeColor = Tema.Texto,
                Margin = new Padding(0, 4, 0, 0)
            });
            resumen.Controls.Add(dato);
        }

        return resumen;
    }

    // Tarjeta blanca con título y subtítulo; el contenido se añade después con Controls.Add.
    // Opcional: un botón a la derecha del título, y la etiqueta del subtítulo si la
    // pantalla quiere cambiar su texto después.
    public static TableLayoutPanel Tarjeta(string titulo, string subtitulo,
        Control? accion = null, Label? etiquetaSubtitulo = null)
    {
        var tarjeta = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.White,
            Padding = new Padding(25),
            Margin = new Padding(0, 0, 0, 20)
        };
        tarjeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var lblTitulo = new Label
        {
            Text = titulo,
            AutoSize = true,
            Font = new Font("Segoe UI", 13),
            ForeColor = Tema.Texto
        };
        var lblSubtitulo = etiquetaSubtitulo ?? new Label();
        lblSubtitulo.Text = subtitulo;
        lblSubtitulo.AutoSize = true;
        lblSubtitulo.ForeColor = Tema.Suave;
        lblSubtitulo.Font = new Font("Segoe UI", 9);
        lblSubtitulo.Margin = new Padding(0, 2, 0, 15);

        if (accion == null)
        {
            tarjeta.Controls.Add(lblTitulo);
            tarjeta.Controls.Add(lblSubtitulo);
            return tarjeta;
        }

        // Con botón: los títulos a la izquierda y el botón a la derecha
        var cabecera = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0)
        };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        cabecera.Controls.Add(lblTitulo, 0, 0);
        cabecera.Controls.Add(lblSubtitulo, 0, 1);
        accion.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cabecera.Controls.Add(accion, 1, 0);
        cabecera.SetRowSpan(accion, 2);

        tarjeta.Controls.Add(cabecera);
        return tarjeta;
    }

    // Rejilla de columnas del mismo ancho para colocar campos de formulario
    public static TableLayoutPanel Rejilla(int columnas)
    {
        var rejilla = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = columnas };
        for (int i = 0; i < columnas; i++)
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnas));
        return rejilla;
    }

    // Aspecto común de las tablas: sin bordes, cabecera gris y una línea entre filas.
    // Cada pantalla ajusta después lo suyo (edición, selección, columnas...).
    public static void EstiloTabla(DataGridView grid, int altoFila = 40)
    {
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Tema.Linea;

        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowTemplate.Height = altoFila;

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 40;
        var cabecera = grid.ColumnHeadersDefaultCellStyle;
        cabecera.BackColor = Tema.CabeceraTabla;
        cabecera.SelectionBackColor = Tema.CabeceraTabla;
        cabecera.ForeColor = Tema.Suave;
        cabecera.SelectionForeColor = Tema.Suave;
        cabecera.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

        grid.DefaultCellStyle.ForeColor = Tema.Texto;
        grid.DefaultCellStyle.SelectionBackColor = Color.White;
        grid.DefaultCellStyle.SelectionForeColor = Tema.Texto;
        grid.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
    }

    // Tabla sin scroll propio dentro de una página que ya lo tiene: mide lo que sus filas
    public static void AjustarAltoTabla(DataGridView grid)
    {
        grid.ScrollBars = ScrollBars.None;
        grid.Height = grid.ColumnHeadersHeight + grid.Rows.Count * grid.RowTemplate.Height + 2;
    }

    // Botones de pie de página alineados a la derecha (el primero queda más a la derecha)
    public static FlowLayoutPanel Acciones(params Button[] botones)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 20)
        };

        foreach (var boton in botones)
        {
            boton.Height = 45;
            boton.Margin = new Padding(10, 0, 0, 0);
            panel.Controls.Add(boton);
        }
        return panel;
    }
}
