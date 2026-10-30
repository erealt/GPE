using GPE.Datos;

namespace GPE.Escritorio;

// Puesto de una máquina: marcha, paradas con su motivo y la barra de lo que lleva hecho.
// PRUEBA para enseñarla: máquinas y motivos están en el código y no se guarda nada (se pierde al cerrar GPE).
public class ModuloMaquina : UserControl
{
    private static readonly string[] Maquinas = { "Máquina 1", "Máquina 2", "Máquina 3", "Máquina 4" };
    private static readonly string[] Motivos =
    {
        "Avería mecánica", "Avería eléctrica", "Atasco", "Falta de material",
        "Cambio de formato", "Limpieza", "Descanso", "Otro"
    };

    private static readonly Color Verde = Color.FromArgb(70, 150, 40);
    private static readonly Color Rojo = Color.FromArgb(215, 60, 60);

    // Un tramo seguido en marcha o parada; Fin null: el que está en curso
    private class Tramo
    {
        public bool EnMarcha;
        public DateTime Inicio;
        public DateTime? Fin;
        public string? Motivo;
    }

    private readonly Dictionary<string, List<Tramo>> tramos = Maquinas.ToDictionary(m => m, m => new List<Tramo>());
    private string maquina = Maquinas[0];

    private readonly List<Button> botonesMaquina = new();
    private readonly Label lblEstado = new();
    private readonly Button btnMarcha = BotonGrande("▶   Marcha", Verde);
    private readonly Button btnParada = BotonGrande("■   Parada", Rojo);
    private readonly BarraTramos barra = new();
    private readonly Label lblDesde = new(), lblHasta = new();
    private readonly Label lblMarcha = new(), lblParado = new(), lblParadas = new();
    private readonly DataGridView grid = new();
    private readonly System.Windows.Forms.Timer reloj = new() { Interval = 1000 };

    private const string ColParada = "parada", ColVuelta = "vuelta", ColDuracion = "duracion", ColMotivo = "motivo";

    public ModuloMaquina()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());

        btnMarcha.Click += (s, e) => Marcha();
        btnParada.Click += (s, e) => Parada();

        // Cada segundo avanzan la barra, los totales y la parada en curso
        reloj.Tick += (s, e) => Refrescar();
        reloj.Start();
        Disposed += (s, e) => reloj.Dispose();

        ElegirMaquina(maquina);
    }

    private List<Tramo> Actuales => tramos[maquina];
    private Tramo? EnCurso => Actuales.LastOrDefault(t => t.Fin == null);

    private Control CrearContenido()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(40, 30, 40, 20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearMaquinas(), 0, 1);
        layout.Controls.Add(CrearBotones(), 0, 2);
        layout.Controls.Add(CrearBarra(), 0, 3);
        layout.Controls.Add(CrearTotales(), 0, 4);
        layout.Controls.Add(CrearTabla(), 0, 5);
        return layout;
    }

    private static Control CrearCabecera()
    {
        var titulos = new Panel { Dock = DockStyle.Fill };
        titulos.Controls.Add(new Label
        {
            Text = "Prueba: las máquinas y los motivos están puestos en el código y no se guarda nada.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Aviso
        });
        titulos.Controls.Add(new Label
        {
            Text = "Máquina",
            Dock = DockStyle.Top,
            Height = 50,
            Font = Tema.FuenteTitulo,
            ForeColor = Tema.Texto
        });
        return titulos;
    }

    // Un botón por máquina y, al lado, cómo está la elegida
    private Control CrearMaquinas()
    {
        var fila = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        foreach (var nombre in Maquinas)
        {
            var boton = Tema.BotonSecundario(nombre, 140);
            boton.Height = 50;
            boton.Margin = new Padding(0, 6, 8, 0);
            boton.Click += (s, e) => ElegirMaquina(nombre);
            botonesMaquina.Add(boton);
            fila.Controls.Add(boton);
        }

        lblEstado.AutoSize = true;
        lblEstado.Font = Tema.FuenteAviso;
        lblEstado.Padding = new Padding(14, 8, 14, 8);
        lblEstado.Margin = new Padding(20, 9, 0, 0);
        fila.Controls.Add(lblEstado);
        return fila;
    }

    private Control CrearBotones()
    {
        var botones = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 10, 0, 10) };
        botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        btnMarcha.Margin = new Padding(0, 0, 8, 0);
        btnParada.Margin = new Padding(8, 0, 0, 0);
        botones.Controls.Add(btnMarcha, 0, 0);
        botones.Controls.Add(btnParada, 1, 0);
        return botones;
    }

    private static Button BotonGrande(string texto, Color color)
    {
        var boton = new Button
        {
            Text = texto,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = color,
            ForeColor = Color.White,
            Font = Tema.FuenteAviso,
            Cursor = Cursors.Hand
        };
        boton.EnabledChanged += (s, e) => boton.BackColor = boton.Enabled ? color : Tema.Linea;
        return boton;
    }

    // La barra va desde el primer arranque hasta ahora, con la hora de cada punta debajo
    private Control CrearBarra()
    {
        var zona = new Panel { Dock = DockStyle.Fill };
        var titulo = new Label
        {
            Text = "Desde que arrancó",
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena
        };
        barra.Dock = DockStyle.Top;
        barra.Height = 34;

        var horas = new Panel { Dock = DockStyle.Top, Height = 22 };
        lblDesde.Dock = DockStyle.Left;
        lblHasta.Dock = DockStyle.Right;
        foreach (var lbl in new[] { lblDesde, lblHasta })
        {
            lbl.AutoSize = true;
            lbl.ForeColor = Tema.Suave;
            lbl.Font = Tema.FuentePequena;
            horas.Controls.Add(lbl);
        }

        // El último añadido queda arriba
        zona.Controls.Add(horas);
        zona.Controls.Add(barra);
        zona.Controls.Add(titulo);
        return zona;
    }

    private Control CrearTotales()
    {
        var totales = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 8, 0, 8) };
        for (int i = 0; i < 3; i++) totales.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));

        Control Total(string titulo, Label valor, int columna)
        {
            var tarjeta = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14, 8, 14, 0),
                Margin = new Padding(columna == 0 ? 0 : 6, 0, columna == 2 ? 0 : 6, 0)
            };
            valor.Dock = DockStyle.Top;
            valor.Height = 32;
            valor.Font = Tema.FuenteTituloTarjeta;
            valor.ForeColor = Tema.Texto;
            tarjeta.Controls.Add(valor);
            tarjeta.Controls.Add(new Label
            {
                Text = titulo,
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = Tema.Suave,
                Font = Tema.FuentePequena
            });
            return tarjeta;
        }

        totales.Controls.Add(Total("En marcha", lblMarcha, 0), 0, 0);
        totales.Controls.Add(Total("Parada", lblParado, 1), 1, 0);
        totales.Controls.Add(Total("Nº de paradas", lblParadas, 2), 2, 0);
        return totales;
    }

    private Control CrearTabla()
    {
        var zona = new Panel { Dock = DockStyle.Fill };

        Componentes.EstiloTabla(grid, altoFila: 48);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.MultiSelect = false;

        foreach (var (nombre, cabecera, ancho) in new[]
        {
            (ColParada, "PARADA", 130), (ColVuelta, "VUELTA A MARCHA", 170), (ColDuracion, "DURACIÓN", 150), (ColMotivo, "MOTIVO", 0)
        })
        {
            var columna = new DataGridViewTextBoxColumn
            {
                Name = nombre,
                HeaderText = cabecera,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (ancho > 0)
            {
                columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                columna.Width = ancho;
            }
            grid.Columns.Add(columna);
        }
        grid.Columns[ColMotivo]!.DefaultCellStyle.Font = Tema.FuenteNegrita;
        grid.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Columns[e.ColumnIndex].Name == ColMotivo) CambiarMotivo(grid.Rows[e.RowIndex]);
        };

        zona.Controls.Add(grid);
        zona.Controls.Add(new Label
        {
            Text = "Paradas (la última arriba). Pulsa el motivo para ponerlo o cambiarlo.",
            Dock = DockStyle.Top,
            Height = 30,
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena
        });
        return zona;
    }

    // Acciones

    private void ElegirMaquina(string nombre)
    {
        maquina = nombre;
        foreach (var boton in botonesMaquina)
        {
            bool elegida = boton.Text == nombre;
            boton.BackColor = elegida ? Tema.AcentoSuave : Color.White;
            boton.ForeColor = elegida ? Tema.Acento : Tema.Texto;
            boton.FlatAppearance.BorderColor = elegida ? Tema.Acento : Tema.Borde;
        }
        LlenarTabla();
        Refrescar();
    }

    private void Marcha()
    {
        if (EnCurso is { EnMarcha: true }) return;
        var ahora = DateTime.Now;
        if (EnCurso is Tramo parada) parada.Fin = ahora;
        Actuales.Add(new Tramo { EnMarcha = true, Inicio = ahora });
        Refrescar();
    }

    // La parada se apunta al pulsar; el motivo se elige ahora o, si se cierra el panel, más tarde en la lista
    private void Parada()
    {
        if (EnCurso is not { EnMarcha: true } marcha) return;
        var ahora = DateTime.Now;
        marcha.Fin = ahora;
        var parada = new Tramo { EnMarcha = false, Inicio = ahora };
        Actuales.Add(parada);
        LlenarTabla();
        Refrescar();

        ElegirMotivo(parada, btnParada.PointToScreen(new Point(0, btnParada.Height)));
    }

    private void CambiarMotivo(DataGridViewRow fila)
    {
        if (fila.Tag is not Tramo parada) return;
        var celda = grid.GetCellDisplayRectangle(grid.Columns[ColMotivo]!.Index, fila.Index, false);
        ElegirMotivo(parada, grid.PointToScreen(new Point(celda.Left, celda.Bottom)));
    }

    private void ElegirMotivo(Tramo parada, Point donde)
    {
        var panel = new ElegirOpcion("Motivo de la parada", Motivos.Select(m => (m, m, true)).ToList(), parada.Motivo);
        panel.Abrir(this, donde, () =>
        {
            if (panel.Elegida is not string motivo) return;
            parada.Motivo = motivo;
            Refrescar();
        });
    }

    // Pantalla

    // Las paradas de la máquina elegida, la última arriba
    private void LlenarTabla()
    {
        grid.Rows.Clear();
        foreach (var parada in Actuales.Where(t => !t.EnMarcha).Reverse())
        {
            int i = grid.Rows.Add(parada.Inicio.ToString("HH:mm:ss"), "", "", "");
            grid.Rows[i].Tag = parada;
        }
        grid.ClearSelection();
    }

    private void Refrescar()
    {
        var ahora = DateTime.Now;
        var enCurso = EnCurso;

        btnMarcha.Enabled = enCurso is not { EnMarcha: true };
        btnParada.Enabled = enCurso is { EnMarcha: true };

        var (fondo, texto) = Tema.ColorEstadoCasilla(enCurso switch
        {
            { EnMarcha: true } => EstadoCasilla.Correcto,
            not null => EstadoCasilla.NoCorrecto,
            _ => ""
        });
        lblEstado.BackColor = fondo;
        lblEstado.ForeColor = texto;
        lblEstado.Text = enCurso switch
        {
            { EnMarcha: true } => $"En marcha desde las {enCurso.Inicio:HH:mm}",
            not null => $"Parada desde las {enCurso.Inicio:HH:mm} · {enCurso.Motivo ?? "sin motivo"}",
            _ => "Sin arrancar"
        };

        TimeSpan Suma(bool enMarcha) => Actuales.Where(t => t.EnMarcha == enMarcha)
            .Aggregate(TimeSpan.Zero, (total, t) => total + ((t.Fin ?? ahora) - t.Inicio));
        lblMarcha.Text = Duracion(Suma(true));
        lblParado.Text = Duracion(Suma(false));
        lblParadas.Text = Actuales.Count(t => !t.EnMarcha).ToString();

        barra.Mostrar(Actuales, ahora);
        lblDesde.Text = Actuales.Count == 0 ? "" : Actuales[0].Inicio.ToString("HH:mm");
        lblHasta.Text = Actuales.Count == 0 ? "" : $"Ahora ({ahora:HH:mm})";

        foreach (DataGridViewRow fila in grid.Rows)
        {
            if (fila.Tag is not Tramo parada) continue;
            fila.Cells[ColVuelta].Value = parada.Fin?.ToString("HH:mm:ss") ?? "Sigue parada";
            fila.Cells[ColDuracion].Value = Duracion((parada.Fin ?? ahora) - parada.Inicio);

            // Sin motivo: en naranja, para que se vea que falta
            var motivo = fila.Cells[ColMotivo];
            motivo.Value = parada.Motivo ?? "Sin motivo · pulsa para ponerlo";
            motivo.Style.ForeColor = motivo.Style.SelectionForeColor = parada.Motivo == null ? Tema.Aviso : Tema.Texto;
            motivo.Style.BackColor = motivo.Style.SelectionBackColor = parada.Motivo == null ? Tema.AvisoFondo : Color.White;
        }
    }

    // "1 h 5 min", "12 min 30 s" o "40 s": en la prueba las paradas duran poco
    private static string Duracion(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min"
        : t.TotalMinutes >= 1 ? $"{t.Minutes} min {t.Seconds} s"
        : $"{t.Seconds} s";

    // Tramos verdes (en marcha) y rojos (parada), repartidos a lo ancho según lo que duran
    private class BarraTramos : Control
    {
        private List<Tramo> tramos = new();
        private DateTime ahora;

        public BarraTramos()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Tema.CabeceraTabla;
        }

        public void Mostrar(List<Tramo> lista, DateTime hora)
        {
            tramos = lista;
            ahora = hora;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (tramos.Count == 0)
                TextRenderer.DrawText(e.Graphics, "Pulsa Marcha para empezar", Tema.FuentePequena, ClientRectangle, Tema.Suave,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            else
            {
                var desde = tramos[0].Inicio;
                double total = Math.Max((ahora - desde).TotalSeconds, 1);
                foreach (var tramo in tramos)
                {
                    int x1 = (int)((tramo.Inicio - desde).TotalSeconds / total * Width);
                    int x2 = (int)Math.Ceiling(((tramo.Fin ?? ahora) - desde).TotalSeconds / total * Width);
                    using var pincel = new SolidBrush(tramo.EnMarcha ? Verde : Rojo);
                    e.Graphics.FillRectangle(pincel, x1, 0, Math.Max(x2 - x1, 1), Height);
                }
            }
            using var borde = new Pen(Tema.Borde);
            e.Graphics.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
        }
    }
}
