using GPE.Datos;

namespace GPE.Escritorio;

// Puesto de una máquina: quién está, marcha, paradas con su motivo y la barra del turno.
// PRUEBA para enseñarla: máquinas, motivos, turnos y maquinistas están en el código y no se guarda nada
// (se pierde al cerrar GPE). Cuando se conecte a la máquina, Marcha y Parada los dará ella;
// al maquinista le quedará decir quién es y poner el motivo.
public class ModuloMaquina : UserControl
{
    // Las listas, el turno, los colores, Tramo y la barra los usa también la supervisión
    internal static readonly string[] Maquinas = { "Máquina 1", "Máquina 2", "Máquina 3", "Máquina 4" };
    internal static readonly string[] Motivos =
    {
        "Avería mecánica", "Avería eléctrica", "Atasco", "Falta de material",
        "Cambio de formato", "Limpieza", "Descanso", "Otro"
    };
    // Paradas que se sabe que van a pasar: no cuentan contra la disponibilidad
    private static readonly HashSet<string> MotivosPrevistos = new() { "Cambio de formato", "Limpieza", "Descanso" };
    // Nombres inventados para la prueba
    internal static readonly string[] Maquinistas =
    {
        "Ana García", "Luis Martín", "Marta López", "Pedro Sánchez", "Lucía Fernández", "Jorge Ruiz"
    };

    private const int LargoComentario = 300;

    // Turnos de ejemplo de 8 horas: mañana a las 6, tarde a las 14 y noche a las 22 (acaba al día siguiente)
    internal static (string Nombre, DateTime Inicio, DateTime Fin) TurnoDe(DateTime hora)
    {
        var dia = hora.Date;
        var (nombre, inicio) = hora.Hour switch
        {
            >= 6 and < 14 => ("Mañana", dia.AddHours(6)),
            >= 14 and < 22 => ("Tarde", dia.AddHours(14)),
            >= 22 => ("Noche", dia.AddHours(22)),
            _ => ("Noche", dia.AddHours(-2))
        };
        return (nombre, inicio, inicio.AddHours(8));
    }

    // Una parada más corta que esto, si nadie le pone motivo, es una microparada y no se pide
    internal static readonly TimeSpan Microparada = TimeSpan.FromMinutes(2);

    internal static readonly Color Verde = Color.FromArgb(70, 150, 40);
    internal static readonly Color Rojo = Color.FromArgb(215, 60, 60);
    internal static readonly Color Naranja = Color.FromArgb(240, 150, 30);   // el rato de la parada con el mecánico en la máquina
    internal const string LeyendaColores = "Verde: en marcha  ·  Rojo: parada  ·  Naranja: parada con el mecánico";

    // Una parada que pasa de esto sin mecánico se avisa: aquí parpadea el botón y en la supervisión se resalta la fila
    internal static readonly TimeSpan ParadaLarga = TimeSpan.FromMinutes(10);

    // Un tramo seguido en marcha o parada; Fin null: el que está en curso
    internal class Tramo
    {
        public bool EnMarcha;
        public DateTime Inicio;
        public DateTime? Fin;
        public string? Motivo;
        public string? Comentario;
        // El mecánico: cuándo se le avisó, cuándo llegó y cuándo quedó resuelto
        public DateTime? AvisoMecanico, LlegaMecanico, ResueltoMecanico;
        public string Turno = "";
        public string? Maquinista;   // quien estaba en la máquina cuando empezó
        public int? IdAviso;         // el aviso en la API del móvil, si llegó
        public string? QueHaHecho;   // el informe del mecánico desde el móvil
        public string? Mecanico;     // el que pulsó "He llegado" en el móvil

        public TimeSpan Duracion(DateTime ahora) => (Fin ?? ahora) - Inicio;
        public bool Prevista => Motivo != null && MotivosPrevistos.Contains(Motivo);
        public bool EsMicroparada => !EnMarcha && Motivo == null && Fin != null && Fin - Inicio < Microparada;
        public bool FaltaMotivo => !EnMarcha && Motivo == null && !EsMicroparada;
        // Parada en curso que ya es larga y a la que todavía no ha llegado el mecánico
        public bool LargaSinMecanico(DateTime ahora) =>
            !EnMarcha && Fin == null && LlegaMecanico == null && ahora - Inicio >= ParadaLarga;

        // Lo que dura dentro de [desde, hasta]
        public TimeSpan DentroDe(DateTime desde, DateTime hasta)
        {
            var fin = Fin ?? hasta;
            var inicio = Inicio < desde ? desde : Inicio;
            if (fin > hasta) fin = hasta;
            return fin > inicio ? fin - inicio : TimeSpan.Zero;
        }

        // "Avisado 10:42 · esperando", "Llegó en 5 min · trabajando" o "Llegó en 5 min · resuelto en 12 min"
        // Sin el informe: para las tablas que lo enseñan en su propia columna
        public string TextoMecanico(bool conInforme = true)
        {
            if (AvisoMecanico is not DateTime aviso) return "";
            if (LlegaMecanico is not DateTime llega)
                return ResueltoMecanico == null ? $"Avisado {aviso:HH:mm} · esperando" : $"Avisado {aviso:HH:mm} · no llegó";
            string llegada = (Mecanico is string quien ? $"{quien} llegó" : "Llegó") + $" en {ModuloMaquina.Duracion(llega - aviso)}";
            return ResueltoMecanico is DateTime resuelto
                ? $"{llegada} · resuelto en {ModuloMaquina.Duracion(resuelto - llega)}" + (conInforme && QueHaHecho is string hecho ? $" · {hecho}" : "")
                : $"{llegada} · trabajando";
        }
    }

    // Lo de cada máquina: sus tramos y quién está en ella en este turno
    private class Puesto
    {
        public List<Tramo> Tramos = new();
        public string? Maquinista;
        public DateTime TurnoDelMaquinista;   // al cambiar de turno hay que volver a decir quién está
    }

    // Compartido para que la supervisión vea lo que se pulsa aquí (solo mientras GPE esté abierto)
    private static readonly Dictionary<string, Puesto> puestos = Maquinas.ToDictionary(m => m, m => new Puesto());
    internal static List<Tramo> TramosDe(string maquina) => puestos[maquina].Tramos;

    private string maquina = Maquinas[0];

    private TableLayoutPanel layout = null!;
    private const int FilaAviso = 3, AltoAviso = 60;
    private bool pantallaCompleta;
    private readonly Button btnPantallaCompleta = Tema.BotonSecundario("⛶  Pantalla completa", 190);

    private readonly List<Button> botonesMaquina = new();
    private readonly Label lblEstado = new();
    private readonly Button btnMaquinista = new();
    private readonly Label lblTurno = new();
    private readonly Button btnSinMotivo = new();
    private readonly Button btnMarcha = BotonGrande("▶   Marcha", Verde);
    private readonly Button btnParada = BotonGrande("■   Parada", Rojo);
    private readonly Button btnMecanico = BotonGrande("🔧  Avisar mecánico", Tema.Aviso);
    private readonly Label lblBarra = new();
    private readonly BarraTramos barra = new();
    private readonly Label lblDesde = new(), lblHasta = new();
    private readonly Label lblMarcha = new(), lblParado = new(), lblPrevisto = new(), lblParadas = new(), lblDisponibilidad = new();
    private readonly DataGridView grid = new();
    private readonly System.Windows.Forms.Timer reloj = new() { Interval = 1000 };

    private const string ColParada = "parada", ColVuelta = "vuelta", ColDuracion = "duracion", ColMotivo = "motivo",
        ColComentario = "comentario", ColTurno = "turno", ColMaquinista = "maquinista", ColMecanico = "mecanico";

    public ModuloMaquina()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());

        btnMarcha.Click += (s, e) => Marcha();
        btnParada.Click += (s, e) => Parada();
        btnMecanico.Click += (s, e) => AvisarMecanico();

        // Cada segundo avanzan la barra, los totales y la parada en curso
        reloj.Tick += (s, e) => Refrescar();
        reloj.Start();
        Disposed += (s, e) => reloj.Dispose();
        AvisosMovil.Empezar();

        ElegirMaquina(maquina);
    }

    private Puesto Actual => puestos[maquina];
    private List<Tramo> Actuales => Actual.Tramos;
    private Tramo? EnCurso => Actuales.LastOrDefault(t => t.Fin == null);

    private Control CrearContenido()
    {
        layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
            Padding = new Padding(40, 30, 40, 20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0));     // aviso de paradas sin motivo, solo si hay
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearMaquinas(), 0, 1);
        layout.Controls.Add(CrearPuesto(), 0, 2);
        layout.Controls.Add(CrearAvisoSinMotivo(), 0, FilaAviso);
        layout.Controls.Add(CrearBotones(), 0, 4);
        layout.Controls.Add(CrearBarra(), 0, 5);
        layout.Controls.Add(CrearTotales(), 0, 6);
        layout.Controls.Add(CrearTabla(), 0, 7);
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
            Text = "Prueba: máquinas, motivos, turnos y maquinistas están puestos en el código y no se guarda nada " +
                   "(lo que se pulse se ve en Supervisión de máquinas mientras GPE esté abierto).",
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

        btnPantallaCompleta.Height = 46;
        btnPantallaCompleta.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnPantallaCompleta.Margin = new Padding(0, 10, 0, 0);
        btnPantallaCompleta.Click += (s, e) => PantallaCompleta(!pantallaCompleta);

        cabecera.Controls.Add(titulos, 0, 0);
        cabecera.Controls.Add(btnPantallaCompleta, 1, 0);
        return cabecera;
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

    // Quién está en la máquina (se pulsa para cambiarlo) y el turno, que sale de la hora
    private Control CrearPuesto()
    {
        var fila = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };

        btnMaquinista.AutoSize = true;
        btnMaquinista.MinimumSize = new Size(300, 50);
        btnMaquinista.Margin = new Padding(0, 6, 20, 0);
        btnMaquinista.Padding = new Padding(10, 0, 10, 0);
        btnMaquinista.FlatStyle = FlatStyle.Flat;
        btnMaquinista.Font = Tema.FuenteNegrita;
        btnMaquinista.Cursor = Cursors.Hand;
        btnMaquinista.Click += (s, e) => ElegirMaquinista(null);

        lblTurno.AutoSize = true;
        lblTurno.ForeColor = Tema.Suave;
        lblTurno.Margin = new Padding(0, 21, 0, 0);

        fila.Controls.Add(btnMaquinista);
        fila.Controls.Add(lblTurno);
        return fila;
    }

    // Franja naranja que avisa de las paradas sin motivo; al pulsarla se pone el de la más antigua
    private Control CrearAvisoSinMotivo()
    {
        btnSinMotivo.Dock = DockStyle.Fill;
        btnSinMotivo.Margin = new Padding(0, 6, 0, 6);
        btnSinMotivo.FlatStyle = FlatStyle.Flat;
        btnSinMotivo.FlatAppearance.BorderColor = Tema.Aviso;
        btnSinMotivo.BackColor = Tema.AvisoFondo;
        btnSinMotivo.ForeColor = Tema.Aviso;
        btnSinMotivo.Font = Tema.FuenteAviso;
        btnSinMotivo.TextAlign = ContentAlignment.MiddleLeft;
        btnSinMotivo.Padding = new Padding(10, 0, 0, 0);
        btnSinMotivo.Cursor = Cursors.Hand;
        btnSinMotivo.Visible = false;
        btnSinMotivo.Click += (s, e) =>
        {
            if (Actuales.FirstOrDefault(t => t.FaltaMotivo) is Tramo parada)
                ElegirMotivo(parada, btnSinMotivo.PointToScreen(new Point(0, btnSinMotivo.Height)));
        };
        return btnSinMotivo;
    }

    private Control CrearBotones()
    {
        var botones = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 10, 0, 10) };
        botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        botones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24));
        btnMarcha.Margin = new Padding(0, 0, 8, 0);
        btnParada.Margin = new Padding(8, 0, 8, 0);
        btnMecanico.Margin = new Padding(8, 0, 0, 0);
        botones.Controls.Add(btnMarcha, 0, 0);
        botones.Controls.Add(btnParada, 1, 0);
        botones.Controls.Add(btnMecanico, 2, 0);
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

    // La barra del turno, con la hora de arranque y la de ahora debajo
    private Control CrearBarra()
    {
        var zona = new Panel { Dock = DockStyle.Fill };

        lblBarra.Dock = DockStyle.Top;
        lblBarra.Height = 36;
        lblBarra.TextAlign = ContentAlignment.MiddleLeft;
        lblBarra.ForeColor = Tema.Suave;
        lblBarra.Font = Tema.FuentePequena;

        barra.Dock = DockStyle.Top;
        barra.Height = 44;

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
        zona.Controls.Add(lblBarra);
        return zona;
    }

    private Control CrearTotales()
    {
        var datos = new[]
        {
            ("En marcha en el turno", lblMarcha),
            ("Parada en el turno", lblParado),
            ("Parada prevista", lblPrevisto),
            ("Nº de paradas", lblParadas),
            ("Disponibilidad", lblDisponibilidad)
        };
        var totales = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = datos.Length, Padding = new Padding(0, 8, 0, 8) };
        for (int i = 0; i < datos.Length; i++)
        {
            var (titulo, valor) = datos[i];
            totales.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / datos.Length));

            var tarjeta = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(14, 8, 14, 0),
                Margin = new Padding(i == 0 ? 0 : 6, 0, i == datos.Length - 1 ? 0 : 6, 0)
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

    private Control CrearTabla()
    {
        var zona = new Panel { Dock = DockStyle.Fill };

        Componentes.EstiloTabla(grid, altoFila: 48);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.MultiSelect = false;

        // Ancho 0: se reparten lo que sobra
        foreach (var (nombre, cabecera, ancho) in new[]
        {
            (ColParada, "PARADA", 90), (ColVuelta, "VUELTA", 90), (ColDuracion, "DURACIÓN", 110), (ColMotivo, "MOTIVO", 0),
            (ColComentario, "COMENTARIO", 0), (ColTurno, "TURNO", 85), (ColMaquinista, "MAQUINISTA", 140), (ColMecanico, "MECÁNICO", 250)
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
        grid.Columns[ColComentario]!.DefaultCellStyle.ForeColor = Tema.Suave;
        grid.CellClick += (s, e) =>
        {
            if (e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not Tramo parada) return;
            string columna = grid.Columns[e.ColumnIndex].Name;
            if (columna == ColMotivo) ElegirMotivo(parada, DebajoDe(e.RowIndex, ColMotivo));
            else if (columna == ColComentario) EscribirComentario(parada, DebajoDe(e.RowIndex, ColComentario));
        };

        zona.Controls.Add(grid);
        zona.Controls.Add(new Label
        {
            Text = $"Paradas (la última arriba). Pulsa el motivo o el comentario para ponerlo o cambiarlo. " +
                   $"Las de menos de {Microparada.TotalMinutes:0} min sin motivo cuentan como microparadas y no lo piden.",
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

    // despues: lo que se hace al elegir (al arrancar sin maquinista se pide antes)
    private void ElegirMaquinista(Action? despues)
    {
        var puesto = Actual;
        var panel = new ElegirOpcion($"¿Quién está en la {maquina}?",
            Maquinistas.Select(m => (m, m, true)).ToList(), puesto.Maquinista);
        panel.Abrir(this, btnMaquinista.PointToScreen(new Point(0, btnMaquinista.Height)), () =>
        {
            if (panel.Elegida is not string nombre) return;
            puesto.Maquinista = nombre;
            puesto.TurnoDelMaquinista = TurnoDe(DateTime.Now).Inicio;
            Refrescar();
            despues?.Invoke();
        });
    }

    // Sin nadie en la máquina, primero se pregunta quién está
    private void Marcha()
    {
        if (EnCurso is { EnMarcha: true }) return;
        if (Actual.Maquinista == null)
        {
            ElegirMaquinista(Marcha);
            return;
        }
        var ahora = DateTime.Now;
        if (EnCurso is Tramo parada)
        {
            parada.Fin = ahora;
            // Si vuelve a andar con el mecánico avisado, se da por resuelto
            if (parada.AvisoMecanico != null) parada.ResueltoMecanico ??= ahora;
        }
        Actuales.Add(NuevoTramo(enMarcha: true, ahora));
        Refrescar();
    }

    // La parada se apunta al pulsar; el motivo se elige ahora o, si se cierra el panel, más tarde en la lista
    private void Parada()
    {
        if (EnCurso is not { EnMarcha: true } marcha) return;
        var ahora = DateTime.Now;
        marcha.Fin = ahora;
        var parada = NuevoTramo(enMarcha: false, ahora);
        Actuales.Add(parada);
        LlenarTabla();
        Refrescar();

        ElegirMotivo(parada, btnParada.PointToScreen(new Point(0, btnParada.Height)));
    }

    private Tramo NuevoTramo(bool enMarcha, DateTime ahora) => new()
    {
        EnMarcha = enMarcha,
        Inicio = ahora,
        Turno = TurnoDe(ahora).Nombre,
        Maquinista = Actual.Maquinista
    };

    // Esquina de abajo a la izquierda de una celda, en la pantalla: ahí se abren los paneles
    private Point DebajoDe(int fila, string columna)
    {
        var celda = grid.GetCellDisplayRectangle(grid.Columns[columna]!.Index, fila, false);
        return grid.PointToScreen(new Point(celda.Left, celda.Bottom));
    }

    // Con "Otro" el motivo no dice nada: se pide también el comentario
    private void ElegirMotivo(Tramo parada, Point donde)
    {
        var panel = new ElegirOpcion($"Motivo de la parada de las {parada.Inicio:HH:mm}",
            Motivos.Select(m => (m, m, true)).ToList(), parada.Motivo);
        panel.Abrir(this, donde, () =>
        {
            if (panel.Elegida is not string motivo) return;
            parada.Motivo = motivo;
            Refrescar();
            if (motivo == "Otro" && parada.Comentario == null) BeginInvoke(() => EscribirComentario(parada, donde));
        });
    }

    private void EscribirComentario(Tramo parada, Point donde)
    {
        var panel = new EscribirTexto($"Comentario de la parada de las {parada.Inicio:HH:mm}", parada.Comentario, LargoComentario);
        panel.Abrir(this, donde, () =>
        {
            if (panel.Texto is not string texto) return;
            parada.Comentario = texto == "" ? null : texto;
            Refrescar();
        });
    }

    // Un botón para todo el aviso: avisar, que ha llegado y que está resuelto.
    // El aviso va a la API que lee la aplicación del móvil; lo que pulse el mecánico allí llega aquí solo.
    private async void AvisarMecanico()
    {
        if (EnCurso is not { EnMarcha: false } parada || parada.ResueltoMecanico != null) return;
        var ahora = DateTime.Now;
        // Una segunda pulsación seguida (doble clic) no cuenta como que ha llegado el mecánico
        if (parada.AvisoMecanico is DateTime avisado && ahora - avisado < TimeSpan.FromSeconds(5)) return;
        if (parada.AvisoMecanico == null)
        {
            parada.AvisoMecanico = ahora;
            Refrescar();
            await AvisosMovil.Avisar(maquina, parada);
            Mensajes.Informacion(this, "Aviso al mecánico", parada.IdAviso != null
                ? $"Aviso enviado a los móviles a las {ahora:HH:mm}."
                : $"Aviso apuntado a las {ahora:HH:mm}, pero no se ha podido enviar a los móviles " +
                  "(la API no está arrancada). Se enviará en cuanto se pueda.");
            return;
        }
        if (parada.LlegaMecanico == null) parada.LlegaMecanico = ahora;
        else parada.ResueltoMecanico = ahora;
        Refrescar();
    }

    private void PantallaCompleta(bool activar)
    {
        if (activar == pantallaCompleta || FindForm() is not FormPrincipal ventana) return;
        pantallaCompleta = activar;
        layout.Padding = activar ? new Padding(20, 12, 20, 12) : new Padding(40, 30, 40, 20);
        btnPantallaCompleta.Text = activar ? "Salir (Esc)" : "⛶  Pantalla completa";
        ventana.PantallaCompleta(activar);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || (keyData == Keys.Escape && pantallaCompleta))
        {
            PantallaCompleta(!pantallaCompleta);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // Pantalla

    // Las paradas de la máquina elegida, la última arriba
    private void LlenarTabla()
    {
        grid.Rows.Clear();
        foreach (var parada in Actuales.Where(t => !t.EnMarcha).Reverse())
        {
            int i = grid.Rows.Add(parada.Inicio.ToString("HH:mm:ss"), "", "", "", "", parada.Turno, parada.Maquinista ?? "—", "");
            grid.Rows[i].Tag = parada;
        }
        grid.ClearSelection();
    }

    private void Refrescar()
    {
        var ahora = DateTime.Now;
        var turno = TurnoDe(ahora);
        var puesto = Actual;
        var enCurso = EnCurso;

        // Al empezar otro turno, el maquinista del anterior ya no está
        if (puesto.Maquinista != null && puesto.TurnoDelMaquinista != turno.Inicio) puesto.Maquinista = null;
        btnMaquinista.Text = puesto.Maquinista is string nombre
            ? $"👤  {nombre}  ·  cambiar"
            : "👤  ¿Quién está en la máquina? Pulsa para elegir";
        btnMaquinista.BackColor = puesto.Maquinista == null ? Tema.AvisoFondo : Color.White;
        btnMaquinista.ForeColor = puesto.Maquinista == null ? Tema.Aviso : Tema.Texto;
        btnMaquinista.FlatAppearance.BorderColor = puesto.Maquinista == null ? Tema.Aviso : Tema.Borde;
        lblTurno.Text = $"Turno de {turno.Nombre.ToLower()} ({turno.Inicio:HH:mm} a {turno.Fin:HH:mm})";

        btnMarcha.Enabled = enCurso is not { EnMarcha: true };
        btnParada.Enabled = enCurso is { EnMarcha: true };
        btnMecanico.Enabled = enCurso is { EnMarcha: false, ResueltoMecanico: null };
        btnMecanico.Text = enCurso switch
        {
            { EnMarcha: false, ResueltoMecanico: DateTime resuelto } => $"Resuelto a las {resuelto:HH:mm}",
            { EnMarcha: false, LlegaMecanico: not null } => "Mecánico trabajando\npulsa al resolverse",
            { EnMarcha: false, AvisoMecanico: DateTime avisado } =>
                $"Avisado {avisado:HH:mm} ({Duracion(ahora - avisado)})\npulsa cuando llegue",
            { EnMarcha: false } when enCurso.LargaSinMecanico(ahora) =>
                $"🔧  Avisar mecánico\nlleva {Duracion(enCurso.Duracion(ahora))} parada",
            _ => "🔧  Avisar mecánico"
        };
        // Parada larga sin avisar: el botón parpadea en rojo
        if (btnMecanico.Enabled)
            btnMecanico.BackColor = enCurso is { AvisoMecanico: null } && enCurso.LargaSinMecanico(ahora) && ahora.Second % 2 == 0
                ? Rojo : Tema.Aviso;

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
            { EnMarcha: true } => $"En marcha desde las {enCurso.Inicio:HH:mm}  ({Duracion(enCurso.Duracion(ahora))})",
            not null => $"Parada desde las {enCurso.Inicio:HH:mm}  ({Duracion(enCurso.Duracion(ahora))})  ·  {enCurso.Motivo ?? "sin motivo"}",
            _ => "Sin arrancar"
        };

        // Aviso de paradas sin motivo: la fila solo ocupa sitio cuando hay alguna
        int sinMotivo = Actuales.Count(t => t.FaltaMotivo);
        btnSinMotivo.Text = sinMotivo == 1
            ? "⚠   1 parada sin motivo  ·  pulsa aquí para ponerlo"
            : $"⚠   {sinMotivo} paradas sin motivo  ·  pulsa aquí para ponerlos";
        if (btnSinMotivo.Visible != sinMotivo > 0)
        {
            btnSinMotivo.Visible = sinMotivo > 0;
            layout.RowStyles[FilaAviso].Height = sinMotivo > 0 ? AltoAviso : 0;
        }

        // Totales del turno en curso (lo de antes de que empezara no cuenta)
        TimeSpan Suma(Func<Tramo, bool> cuales) => Actuales.Where(cuales)
            .Aggregate(TimeSpan.Zero, (total, t) => total + t.DentroDe(turno.Inicio, ahora));
        var delTurno = Actuales.Where(t => !t.EnMarcha && t.Inicio >= turno.Inicio).ToList();
        var enMarcha = Suma(t => t.EnMarcha);
        var parado = Suma(t => !t.EnMarcha && !t.Prevista);
        int microparadas = delTurno.Count(t => t.EsMicroparada);

        lblMarcha.Text = Duracion(enMarcha);
        lblParado.Text = Duracion(parado);
        lblPrevisto.Text = Duracion(Suma(t => !t.EnMarcha && t.Prevista));
        lblParadas.Text = microparadas == 0 ? delTurno.Count.ToString() : $"{delTurno.Count}  ({microparadas} micro)";
        // Lo que ha estado en marcha del tiempo en que debía estarlo (las previstas no cuentan)
        var debia = enMarcha + parado;
        lblDisponibilidad.Text = debia.TotalSeconds < 1 ? "—" : $"{enMarcha / debia:0 %}";

        // La barra va desde que arrancó en este turno hasta ahora: así ocupa todo el ancho y se ve grande
        var arranque = Actuales.FirstOrDefault(t => (t.Fin ?? ahora) > turno.Inicio)?.Inicio;
        var desde = arranque is DateTime a && a > turno.Inicio ? a : turno.Inicio;
        lblBarra.Text = $"Turno de {turno.Nombre.ToLower()}, desde que arrancó.   {LeyendaColores}";
        barra.Mostrar(arranque == null ? new() : Actuales, desde, ahora, ahora);
        lblDesde.Text = arranque == null ? "" : desde.ToString("HH:mm");
        lblHasta.Text = arranque == null ? "" : $"Ahora ({ahora:HH:mm})";

        foreach (DataGridViewRow fila in grid.Rows)
        {
            if (fila.Tag is not Tramo parada) continue;
            fila.Cells[ColVuelta].Value = parada.Fin?.ToString("HH:mm:ss") ?? "Sigue parada";
            fila.Cells[ColDuracion].Value = Duracion(parada.Duracion(ahora));
            fila.Cells[ColMecanico].Value = parada.TextoMecanico();
            fila.Cells[ColComentario].Value = parada.Comentario ?? (parada.EnMarcha ? "" : "+ añadir");

            // Sin motivo: en naranja, para que se vea que falta. Las microparadas, en gris.
            var motivo = fila.Cells[ColMotivo];
            var (fondoMotivo, textoMotivo) = parada switch
            {
                { FaltaMotivo: true } => (Tema.AvisoFondo, Tema.Aviso),
                { EsMicroparada: true } => (Color.White, Tema.Suave),
                _ => (Color.White, Tema.Texto)
            };
            motivo.Value = parada switch
            {
                { FaltaMotivo: true } => "Sin motivo · pulsa para ponerlo",
                { EsMicroparada: true } => "Microparada",
                { Prevista: true } => $"{parada.Motivo}  (prevista)",
                _ => parada.Motivo
            };
            motivo.Style.ForeColor = motivo.Style.SelectionForeColor = textoMotivo;
            motivo.Style.BackColor = motivo.Style.SelectionBackColor = fondoMotivo;
        }
    }

    // "1 h 5 min", "12 min 30 s" o "40 s": en la prueba las paradas duran poco
    internal static string Duracion(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min"
        : t.TotalMinutes >= 1 ? $"{t.Minutes} min {t.Seconds} s"
        : $"{t.Seconds} s";

    // Tramos verdes (en marcha) y rojos (parada) entre desde y hasta; dentro de una parada, en naranja
    // desde que llega el mecánico hasta que se resuelve.
    // Si hasta es más tarde que ahora, lo que no ha pasado queda en gris, con una raya en la hora actual.
    // Con el ratón encima de un tramo sale un cuadro con sus horas y, si es una parada, su motivo.
    internal class BarraTramos : Control
    {
        private List<Tramo> tramos = new();
        private DateTime desde, hasta, ahora;
        private readonly ToolTip ayuda = new() { InitialDelay = 0, ReshowDelay = 0 };
        private Tramo? senalado;   // el del cuadro que se ve, para no volver a sacarlo a cada movimiento

        public BarraTramos()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Tema.CabeceraTabla;
            Disposed += (s, e) => ayuda.Dispose();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var tramo = TramoEn(e.X);
            if (tramo == senalado) return;
            senalado = tramo;
            if (tramo == null) ayuda.Hide(this);
            else ayuda.Show(Descripcion(tramo), this, e.X + 12, Height + 4);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            senalado = null;
            ayuda.Hide(this);
        }

        // Las paradas cortas son una raya fina: se cogen aunque el ratón quede a unos píxeles
        private Tramo? TramoEn(int x)
        {
            Tramo? marcha = null;
            foreach (var tramo in tramos)
            {
                var fin = tramo.Fin ?? ahora;
                if (fin <= desde || tramo.Inicio >= hasta) continue;
                int x1 = X(tramo.Inicio), x2 = Math.Max(X(fin), x1 + 2);
                if (!tramo.EnMarcha && x >= x1 - 4 && x <= x2 + 4) return tramo;
                if (tramo.EnMarcha && x >= x1 && x <= x2) marcha = tramo;
            }
            return marcha;
        }

        private string Descripcion(Tramo tramo)
        {
            string horas = $"{tramo.Inicio:HH:mm} a {(tramo.Fin is DateTime fin ? fin.ToString("HH:mm") : "ahora")}  " +
                           $"({Duracion(tramo.Duracion(ahora))})";
            if (tramo.EnMarcha) return $"En marcha de {horas}";

            var lineas = new List<string>
            {
                $"Parada de {horas}",
                tramo switch
                {
                    { EsMicroparada: true } => "Microparada",
                    { FaltaMotivo: true } => "Sin motivo",
                    { Prevista: true } => $"Motivo: {tramo.Motivo} (prevista)",
                    _ => $"Motivo: {tramo.Motivo}"
                }
            };
            if (tramo.Comentario != null) lineas.Add($"Comentario: {tramo.Comentario}");
            if (tramo.TextoMecanico() is { Length: > 0 } mecanico) lineas.Add($"Mecánico: {mecanico}");
            if (tramo.Maquinista != null) lineas.Add($"Maquinista: {tramo.Maquinista}");
            return string.Join("\n", lineas);
        }

        public void Mostrar(List<Tramo> lista, DateTime inicio, DateTime fin, DateTime hora)
        {
            tramos = lista;
            desde = inicio;
            hasta = fin;
            ahora = hora;
            Invalidate();
        }

        private int X(DateTime hora)
        {
            double total = Math.Max((hasta - desde).TotalSeconds, 1);
            return (int)Math.Round(Math.Clamp((hora - desde).TotalSeconds / total, 0, 1) * Width);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (tramos.Count == 0)
                TextRenderer.DrawText(e.Graphics, "Pulsa Marcha para empezar", Tema.FuentePequena, ClientRectangle, Tema.Suave,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            else
                foreach (var tramo in tramos)
                {
                    var fin = tramo.Fin ?? ahora;
                    if (fin <= desde || tramo.Inicio >= hasta) continue;
                    int x1 = X(tramo.Inicio), x2 = X(fin);
                    using var pincel = new SolidBrush(tramo.EnMarcha ? Verde : Rojo);
                    // Al menos 2 px, para que una parada corta se vea en un turno de 8 horas
                    e.Graphics.FillRectangle(pincel, x1, 0, Math.Max(x2 - x1, 2), Height);

                    if (tramo.LlegaMecanico is DateTime llega)
                    {
                        int m1 = X(llega), m2 = X(tramo.ResueltoMecanico ?? fin);
                        using var naranja = new SolidBrush(Naranja);
                        e.Graphics.FillRectangle(naranja, m1, 0, Math.Max(m2 - m1, 2), Height);
                    }
                }

            if (ahora > desde && ahora < hasta)
            {
                using var raya = new Pen(Tema.Texto, 2);
                e.Graphics.DrawLine(raya, X(ahora), 0, X(ahora), Height);
            }
            using var borde = new Pen(Tema.Borde);
            e.Graphics.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
        }
    }
}
