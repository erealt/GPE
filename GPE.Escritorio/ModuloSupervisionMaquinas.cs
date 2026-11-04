using Tramo = GPE.Escritorio.ModuloMaquina.Tramo;

namespace GPE.Escritorio;

// Supervisión de las máquinas: cómo van todas a la vez, sus paradas y los motivos que más se repiten.
// PRUEBA: hoy se ve lo que se pulsa en la pantalla Máquina (mientras GPE esté abierto), con las barras
// desde el primer arranque hasta ahora. Los días anteriores son inventados (para un mismo día salen
// siempre los mismos), con los turnos de mañana y tarde, de 6:00 a 22:00.
public class ModuloSupervisionMaquinas : UserControl
{
    private DateTime dia = DateTime.Today;
    private string? filtro;     // null: todas las máquinas
    private string? elegida = ModuloMaquina.Maquinas[0];   // la máquina cuyas paradas se ven abajo
    private Dictionary<string, List<Tramo>> datos = new();
    private DateTime desde, hasta;   // lo que abarcan las barras

    private readonly DateTimePicker dtpDia = new();
    private readonly List<Button> botonesFiltro = new();
    private readonly Label lblMarchaAhora = new(), lblMarcha = new(), lblParado = new(), lblParadas = new(), lblDisponibilidad = new();
    private readonly Panel filas = new();
    private readonly Label lblDesde = new(), lblHasta = new();
    private readonly MotivosFrecuentes motivos = new();
    private readonly Label lblDetalle = new();
    private readonly DataGridView grid = new();
    private readonly System.Windows.Forms.Timer reloj = new() { Interval = 2000 };

    private const int AltoFila = 66;

    public ModuloSupervisionMaquinas()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());

        // Hoy se vuelve a mirar la pantalla Máquina cada poco; los días pasados no cambian
        reloj.Tick += (s, e) => { if (dia == DateTime.Today && Visible) Cargar(); };
        VisibleChanged += (s, e) => { if (dia == DateTime.Today && Visible) Cargar(); };
        reloj.Start();
        Disposed += (s, e) => reloj.Dispose();

        CrearFilas();
        Cargar();
    }

    private Control CrearContenido()
    {
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(40, 30, 40, 20)
        };
        int altoFilas = ModuloMaquina.Maquinas.Length * AltoFila + 60;
        int[] altos = { 95, 80, 95, altoFilas, 380 };
        foreach (int alto in altos) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, alto));
        layout.Height = altos.Sum() + layout.Padding.Vertical;

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearFiltros(), 0, 1);
        layout.Controls.Add(CrearTotales(), 0, 2);
        layout.Controls.Add(CrearZonaFilas(), 0, 3);
        layout.Controls.Add(CrearAbajo(), 0, 4);

        scroll.Controls.Add(layout);
        return scroll;
    }

    private static Control CrearCabecera()
    {
        var titulos = new Panel { Dock = DockStyle.Fill };
        titulos.Controls.Add(new Label
        {
            Text = "Prueba: hoy se ve lo que se pulsa en la pantalla Máquina (mientras GPE esté abierto). Los días anteriores son inventados.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Aviso
        });
        titulos.Controls.Add(new Label
        {
            Text = "Supervisión de máquinas",
            Dock = DockStyle.Top,
            Height = 50,
            Font = Tema.FuenteTitulo,
            ForeColor = Tema.Texto
        });
        return titulos;
    }

    // El día, como en el control de agua, y qué máquinas se ven
    private Control CrearFiltros()
    {
        var barra = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 15, 0),
            WrapContents = false
        };

        dtpDia.Width = 135;
        dtpDia.Format = DateTimePickerFormat.Short;
        dtpDia.MaxDate = DateTime.Today;
        dtpDia.Value = dia;
        dtpDia.ValueChanged += (s, e) => CambiarDia(dtpDia.Value.Date);
        barra.Controls.Add(Componentes.CampoFiltro("Día", dtpDia));

        Button Boton(string texto, int ancho, Action alPulsar)
        {
            var boton = Tema.BotonSecundario(texto, ancho);
            boton.Margin = new Padding(0, 18, 6, 0);
            boton.Click += (s, e) => alPulsar();
            barra.Controls.Add(boton);
            return boton;
        }
        Boton("◀", 48, () => CambiarDia(dia.AddDays(-1)));
        Boton("▶", 48, () => { if (dia < DateTime.Today) CambiarDia(dia.AddDays(1)); });
        Boton("Hoy", 70, () => CambiarDia(DateTime.Today)).Margin = new Padding(0, 18, 30, 0);

        foreach (var maquina in new string?[] { null }.Concat(ModuloMaquina.Maquinas))
        {
            var boton = Boton(maquina ?? "Todas", maquina == null ? 90 : 120, () => Filtrar(maquina));
            boton.Tag = maquina;
            botonesFiltro.Add(boton);
        }
        return barra;
    }

    private Control CrearTotales()
    {
        var datosTotales = new[]
        {
            ("En marcha ahora", lblMarchaAhora),
            ("Tiempo en marcha", lblMarcha),
            ("Tiempo parado", lblParado),
            ("Nº de paradas", lblParadas),
            ("Disponibilidad", lblDisponibilidad)
        };
        var totales = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = datosTotales.Length, Padding = new Padding(0, 8, 0, 8) };
        for (int i = 0; i < datosTotales.Length; i++)
        {
            var (titulo, valor) = datosTotales[i];
            totales.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / datosTotales.Length));
            var tarjeta = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14, 8, 14, 0),
                Margin = new Padding(i == 0 ? 0 : 6, 0, i == datosTotales.Length - 1 ? 0 : 6, 0)
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
            totales.Controls.Add(tarjeta, i, 0);
        }
        return totales;
    }

    // Una fila por máquina (se rellenan en CrearFilas) y debajo las horas de las barras
    private Control CrearZonaFilas()
    {
        var zona = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(15, 10, 15, 0) };

        filas.Dock = DockStyle.Top;
        filas.Height = ModuloMaquina.Maquinas.Length * AltoFila;

        // Con el mismo margen que las barras, para que las horas caigan en sus puntas
        var horas = new Panel { Dock = DockStyle.Top, Height = 24, Padding = new Padding(246, 0, 136, 0) };
        lblDesde.Dock = DockStyle.Left;
        lblHasta.Dock = DockStyle.Right;
        foreach (var lbl in new[] { lblDesde, lblHasta })
        {
            lbl.AutoSize = true;
            lbl.ForeColor = Tema.Suave;
            lbl.Font = Tema.FuentePequena;
            horas.Controls.Add(lbl);
        }

        zona.Controls.Add(horas);
        zona.Controls.Add(filas);
        zona.Controls.Add(new Label
        {
            Text = $"Pulsa una máquina para ver sus paradas.   {ModuloMaquina.LeyendaColores}",
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena
        });
        return zona;
    }

    // Motivos a la izquierda y las paradas de la máquina elegida a la derecha
    private Control CrearAbajo()
    {
        var abajo = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 14, 0, 0) };
        abajo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        abajo.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));

        var izquierda = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(15, 10, 15, 10), Margin = new Padding(0, 0, 8, 0) };
        motivos.Dock = DockStyle.Fill;
        izquierda.Controls.Add(motivos);
        izquierda.Controls.Add(Titulo("Motivos que más paran"));

        var derecha = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(15, 10, 15, 10), Margin = new Padding(8, 0, 0, 0) };
        Componentes.EstiloTabla(grid, altoFila: 40);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        foreach (var (nombre, ancho) in new[] { ("PARADA", 80), ("DURACIÓN", 110), ("MOTIVO", 0), ("MAQUINISTA", 130), ("MECÁNICO", 220) })
        {
            var columna = new DataGridViewTextBoxColumn { HeaderText = nombre, SortMode = DataGridViewColumnSortMode.NotSortable };
            if (ancho > 0)
            {
                columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                columna.Width = ancho;
            }
            grid.Columns.Add(columna);
        }
        lblDetalle.Dock = DockStyle.Top;
        lblDetalle.Height = 30;
        lblDetalle.Font = Tema.FuenteNegrita;
        lblDetalle.ForeColor = Tema.Texto;
        derecha.Controls.Add(grid);
        derecha.Controls.Add(lblDetalle);

        abajo.Controls.Add(izquierda, 0, 0);
        abajo.Controls.Add(derecha, 1, 0);
        return abajo;
    }

    private static Label Titulo(string texto) => new()
    {
        Text = texto,
        Dock = DockStyle.Top,
        Height = 30,
        Font = Tema.FuenteNegrita,
        ForeColor = Tema.Texto
    };

    // Filas: nombre y estado, barra y disponibilidad. Se crean una vez; Mostrar las rellena.
    // Van con Dock Top, así que se añaden al revés para que la primera máquina quede arriba.
    private readonly Dictionary<string, (TableLayoutPanel Fila, Label Estado, ModuloMaquina.BarraTramos Barra, Label Disponibilidad)> controlesFila = new();

    private void CrearFilas()
    {
        foreach (var maquina in ModuloMaquina.Maquinas.Reverse())
        {
            var fila = new TableLayoutPanel
            {
                Height = AltoFila,
                Dock = DockStyle.Top,
                ColumnCount = 3,
                Margin = new Padding(0),
                Padding = new Padding(6, 8, 6, 8),
                Cursor = Cursors.Hand
            };
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

            var info = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            var estado = new Label { Dock = DockStyle.Top, Height = 22, Font = Tema.FuentePequenaNegrita };
            info.Controls.Add(estado);
            info.Controls.Add(new Label { Text = maquina, Dock = DockStyle.Top, Height = 24, Font = Tema.FuenteNegrita, ForeColor = Tema.Texto });

            var barra = new ModuloMaquina.BarraTramos { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 10, 4) };
            var disponibilidad = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                Font = Tema.FuenteTituloTarjeta,
                ForeColor = Tema.Texto
            };

            fila.Controls.Add(info, 0, 0);
            fila.Controls.Add(barra, 1, 0);
            fila.Controls.Add(disponibilidad, 2, 0);

            // Toda la fila se puede pulsar
            foreach (var control in new Control[] { fila, info, estado, barra, disponibilidad }.Concat(info.Controls.Cast<Control>()))
                control.Click += (s, e) => Elegir(maquina);

            controlesFila[maquina] = (fila, estado, barra, disponibilidad);
            filas.Controls.Add(fila);
        }
    }

    // Acciones

    private void CambiarDia(DateTime nuevo)
    {
        if (nuevo > DateTime.Today) nuevo = DateTime.Today;
        dia = nuevo.Date;
        if (dtpDia.Value.Date != dia) dtpDia.Value = dia;
        Cargar();
    }

    private void Filtrar(string? maquina)
    {
        filtro = maquina;
        if (maquina != null) elegida = maquina;
        Mostrar();
    }

    private void Elegir(string maquina)
    {
        elegida = maquina;
        Mostrar();
    }

    // Hoy, lo pulsado en la pantalla Máquina, desde el primer arranque de cualquiera hasta ahora;
    // así se ve grande aunque se pruebe unos minutos. Otro día, los datos inventados de 6:00 a 22:00.
    private void Cargar()
    {
        if (dia == DateTime.Today)
        {
            datos = ModuloMaquina.Maquinas.ToDictionary(m => m, ModuloMaquina.TramosDe);
            hasta = DateTime.Now;
            desde = datos.Values.SelectMany(l => l).Select(t => (DateTime?)t.Inicio).Min() ?? hasta;
        }
        else
        {
            desde = dia.AddHours(6);
            hasta = dia.AddHours(22);
            datos = ModuloMaquina.Maquinas.Select((m, i) => (m, DatosInventados(dia, i)))
                .ToDictionary(x => x.m, x => x.Item2);
        }
        Mostrar();
    }

    // Pantalla

    private void Mostrar()
    {
        var hoy = dia == DateTime.Today;
        var vistas = ModuloMaquina.Maquinas.Where(m => filtro == null || m == filtro).ToList();

        foreach (var boton in botonesFiltro)
        {
            bool activo = Equals(boton.Tag, filtro);
            boton.BackColor = activo ? Tema.AcentoSuave : Color.White;
            boton.ForeColor = activo ? Tema.Acento : Tema.Texto;
            boton.FlatAppearance.BorderColor = activo ? Tema.Acento : Tema.Borde;
        }

        // Totales de las máquinas que se ven
        var tramos = vistas.SelectMany(m => datos[m]).ToList();
        TimeSpan Suma(Func<Tramo, bool> cuales) => tramos.Where(cuales)
            .Aggregate(TimeSpan.Zero, (total, t) => total + t.DentroDe(desde, hasta));
        var enMarcha = Suma(t => t.EnMarcha);
        var parado = Suma(t => !t.EnMarcha && !t.Prevista);
        int paradas = tramos.Count(t => !t.EnMarcha);
        int microparadas = tramos.Count(t => t.EsMicroparada);

        lblMarchaAhora.Text = hoy ? $"{vistas.Count(m => datos[m].LastOrDefault() is { EnMarcha: true, Fin: null })} de {vistas.Count}" : "—";
        lblMarcha.Text = ModuloMaquina.Duracion(enMarcha);
        lblParado.Text = ModuloMaquina.Duracion(parado);
        lblParadas.Text = microparadas == 0 ? paradas.ToString() : $"{paradas}  ({microparadas} micro)";
        lblDisponibilidad.Text = Disponibilidad(enMarcha, parado);

        bool hayDatos = tramos.Count > 0;
        lblDesde.Text = hayDatos ? desde.ToString("HH:mm") : "";
        lblHasta.Text = !hayDatos ? "" : hoy ? $"Ahora ({hasta:HH:mm})" : hasta.ToString("HH:mm");

        filas.SuspendLayout();
        foreach (var maquina in ModuloMaquina.Maquinas)
        {
            var (fila, estado, barra, disponibilidad) = controlesFila[maquina];
            fila.Visible = vistas.Contains(maquina);
            fila.BackColor = maquina == elegida ? Tema.AcentoSuave : Color.White;

            var lista = datos[maquina];
            var ultimo = lista.LastOrDefault();
            (estado.Text, estado.ForeColor) = !hoy ? ($"{lista.Count(t => !t.EnMarcha)} paradas", Tema.Suave)
                : ultimo == null ? ("Sin arrancar", Tema.Suave)
                : ultimo is { Fin: not null } ? ("Apagada", Tema.Suave)
                : ultimo.EnMarcha ? ($"En marcha · {ultimo.Maquinista ?? "sin maquinista"}", Color.FromArgb(25, 120, 70))
                : ($"Parada {ModuloMaquina.Duracion(hasta - ultimo.Inicio)} · {ultimo.Motivo ?? "sin motivo"}", Tema.Error);

            barra.Mostrar(lista, desde, hasta, hasta);
            disponibilidad.Text = Disponibilidad(
                lista.Where(t => t.EnMarcha).Aggregate(TimeSpan.Zero, (s, t) => s + t.DentroDe(desde, hasta)),
                lista.Where(t => !t.EnMarcha && !t.Prevista).Aggregate(TimeSpan.Zero, (s, t) => s + t.DentroDe(desde, hasta)));
        }
        filas.ResumeLayout();

        motivos.Mostrar(tramos.Where(t => !t.EnMarcha).ToList(), hasta);
        MostrarDetalle();
    }

    // Las paradas de la máquina elegida, la última arriba
    private void MostrarDetalle()
    {
        grid.Rows.Clear();
        if (elegida == null || (filtro != null && elegida != filtro))
        {
            lblDetalle.Text = "Pulsa una máquina para ver sus paradas";
            lblDetalle.ForeColor = Tema.Suave;
            return;
        }

        var paradas = datos[elegida].Where(t => !t.EnMarcha).Reverse().ToList();
        lblDetalle.Text = $"Paradas de la {elegida} ({paradas.Count})";
        lblDetalle.ForeColor = Tema.Texto;
        foreach (var parada in paradas)
        {
            string motivo = parada switch
            {
                { FaltaMotivo: true } => "Sin motivo",
                { EsMicroparada: true } => "Microparada",
                { Comentario: string comentario } => $"{parada.Motivo} · {comentario}",
                _ => parada.Motivo ?? ""
            };
            int i = grid.Rows.Add(parada.Inicio.ToString("HH:mm"), ModuloMaquina.Duracion(parada.Duracion(hasta)), motivo,
                parada.Maquinista ?? "—", parada.TextoMecanico());
            var celda = grid.Rows[i].Cells[2];
            celda.Style.ForeColor = parada switch
            {
                { FaltaMotivo: true } => Tema.Aviso,
                { EsMicroparada: true } => Tema.Suave,
                _ => Tema.Texto
            };
        }
        grid.ClearSelection();
    }

    private static string Disponibilidad(TimeSpan enMarcha, TimeSpan parado)
    {
        var debia = enMarcha + parado;
        return debia.TotalSeconds < 1 ? "—" : $"{enMarcha / debia:0 %}";
    }

    // Datos inventados de una máquina para un día pasado, siempre los mismos (la semilla es el día y la máquina)
    private static List<Tramo> DatosInventados(DateTime dia, int maquina)
    {
        var azar = new Random(DateOnly.FromDateTime(dia).DayNumber * 10 + maquina);
        string[] comentarios = { "Sensor de la cinta sucio", "Faltaba film", "Revisar la cuchilla", "Ruido raro en el motor" };
        var tramos = new List<Tramo>();
        var t = dia.AddHours(6);
        var fin = dia.AddHours(22);

        Tramo Nuevo(bool enMarcha, DateTime inicio, TimeSpan duracion)
        {
            var turno = ModuloMaquina.TurnoDe(inicio);
            var tramo = new Tramo
            {
                EnMarcha = enMarcha,
                Inicio = inicio,
                Fin = inicio + duracion,
                Turno = turno.Nombre,
                // Cada máquina tiene el mismo maquinista todo el turno
                Maquinista = ModuloMaquina.Maquinistas[(maquina * 2 + (turno.Nombre == "Mañana" ? 0 : 1)) % ModuloMaquina.Maquinistas.Length]
            };
            tramos.Add(tramo);
            return tramo;
        }

        while (t < fin)
        {
            var marcha = TimeSpan.FromMinutes(azar.Next(15, 80));
            Nuevo(true, t, marcha);
            t += marcha;
            if (t >= fin) break;

            if (azar.NextDouble() < 0.25)
            {
                var corta = TimeSpan.FromSeconds(azar.Next(20, 110));
                Nuevo(false, t, corta);
                t += corta;
                continue;
            }

            var duracion = TimeSpan.FromMinutes(azar.Next(3, 35));
            var parada = Nuevo(false, t, duracion);
            parada.Motivo = azar.NextDouble() < 0.08 ? null : ModuloMaquina.Motivos[azar.Next(ModuloMaquina.Motivos.Length)];
            if (parada.Motivo == "Otro" || azar.NextDouble() < 0.15) parada.Comentario = comentarios[azar.Next(comentarios.Length)];
            if (parada.Motivo is "Avería mecánica" or "Avería eléctrica" or "Atasco" && duracion.TotalMinutes > 6 && azar.NextDouble() < 0.7)
            {
                parada.AvisoMecanico = t.AddMinutes(1);
                parada.LlegaMecanico = parada.AvisoMecanico.Value.AddMinutes(azar.Next(2, Math.Min(10, (int)duracion.TotalMinutes - 2)));
                parada.ResueltoMecanico = t + duracion;
            }
            t += duracion;
        }
        // A las 22:00 se apaga
        if (tramos.Count > 0) tramos[^1].Fin = fin;
        return tramos;
    }

    // Barras horizontales con los minutos de parada de cada motivo, de más a menos
    private class MotivosFrecuentes : Control
    {
        private List<(string Motivo, int Veces, TimeSpan Tiempo, Color Color)> grupos = new();

        public MotivosFrecuentes()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        public void Mostrar(List<Tramo> paradas, DateTime hasta)
        {
            grupos = paradas
                .GroupBy(p => p switch
                {
                    { EsMicroparada: true } => "Microparadas",
                    { FaltaMotivo: true } => "Sin motivo",
                    _ => p.Motivo!
                })
                .Select(g => (g.Key, g.Count(), g.Aggregate(TimeSpan.Zero, (s, p) => s + p.Duracion(hasta)),
                    g.Key switch
                    {
                        "Sin motivo" => Tema.Aviso,
                        "Microparadas" => Tema.Borde,
                        _ => ModuloMaquina.Rojo
                    }))
                .OrderByDescending(g => g.Item3)
                .ToList();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (grupos.Count == 0)
            {
                TextRenderer.DrawText(e.Graphics, "Sin paradas", Tema.Fuente, ClientRectangle, Tema.Suave,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            const int alto = 30, anchoNombre = 150, anchoDato = 120;
            double maximo = grupos.Max(g => g.Tiempo.TotalSeconds);
            int anchoBarra = Math.Max(Width - anchoNombre - anchoDato, 20);
            for (int i = 0; i < grupos.Count && (i + 1) * alto <= Height; i++)
            {
                var (motivo, veces, tiempo, color) = grupos[i];
                int y = i * alto;
                TextRenderer.DrawText(e.Graphics, motivo, Tema.FuentePequena, new Rectangle(0, y, anchoNombre, alto), Tema.Texto,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                using var pincel = new SolidBrush(color);
                int largo = Math.Max((int)(tiempo.TotalSeconds / maximo * anchoBarra), 3);
                e.Graphics.FillRectangle(pincel, anchoNombre, y + 8, largo, alto - 16);
                TextRenderer.DrawText(e.Graphics, $"{veces} · {ModuloMaquina.Duracion(tiempo)}", Tema.FuentePequena,
                    new Rectangle(Width - anchoDato, y, anchoDato, alto), Tema.Suave,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
            }
        }
    }
}
