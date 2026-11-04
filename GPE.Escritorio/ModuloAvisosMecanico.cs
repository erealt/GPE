using System.Media;
using Tramo = GPE.Escritorio.ModuloMaquina.Tramo;

namespace GPE.Escritorio;

// Avisos del mecánico: lo que verá en la aplicación del móvil, aquí en el escritorio para probarlo.
// PRUEBA: salen los avisos que se den hoy en la pantalla Máquina (mientras GPE esté abierto),
// y lo que se pulse aquí se ve allí y en la supervisión.
public class ModuloAvisosMecanico : UserControl
{
    private readonly Panel pendientes = new();
    private readonly Label lblPendientes = new();
    private readonly DataGridView grid = new();
    private readonly List<(Tramo Parada, Label Tiempo)> tarjetas = new();
    private string pintado = "";      // qué avisos, y en qué estado, tienen tarjeta; si cambia, se vuelven a pintar
    private int avisosVistos = -1;    // para que suene al llegar uno nuevo (no al abrir la pantalla)
    private readonly System.Windows.Forms.Timer reloj = new() { Interval = 1000 };

    public ModuloAvisosMecanico()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());

        reloj.Tick += (s, e) => { if (Visible) Refrescar(); };
        reloj.Start();
        VisibleChanged += (s, e) => { if (Visible) Refrescar(); };
        Disposed += (s, e) => reloj.Dispose();

        Refrescar();
    }

    private Control CrearContenido()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(40, 30, 40, 20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        var titulos = new Panel { Dock = DockStyle.Fill };
        titulos.Controls.Add(new Label
        {
            Text = "Prueba de lo que será la aplicación del móvil: los avisos que se den en la pantalla Máquina salen aquí al momento.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Aviso
        });
        titulos.Controls.Add(new Label
        {
            Text = "Avisos del mecánico",
            Dock = DockStyle.Top,
            Height = 50,
            Font = Tema.FuenteTitulo,
            ForeColor = Tema.Texto
        });

        lblPendientes.Dock = DockStyle.Fill;
        lblPendientes.Font = Tema.FuenteApartado;
        lblPendientes.ForeColor = Tema.Texto;

        pendientes.Dock = DockStyle.Fill;
        pendientes.AutoScroll = true;

        Componentes.EstiloTabla(grid, altoFila: 40);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        foreach (var (nombre, ancho) in new[]
        {
            ("MÁQUINA", 130), ("MOTIVO", 0), ("AVISADO", 100), ("LLEGÓ EN", 130), ("RESUELTO EN", 130), ("MAQUINISTA", 160)
        })
        {
            var columna = new DataGridViewTextBoxColumn { HeaderText = nombre, SortMode = DataGridViewColumnSortMode.NotSortable };
            if (ancho > 0)
            {
                columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                columna.Width = ancho;
            }
            grid.Columns.Add(columna);
        }

        layout.Controls.Add(titulos, 0, 0);
        layout.Controls.Add(lblPendientes, 0, 1);
        layout.Controls.Add(pendientes, 0, 2);
        layout.Controls.Add(new Label
        {
            Text = "Resueltos hoy",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Font = Tema.FuenteApartado,
            ForeColor = Tema.Texto
        }, 0, 3);
        layout.Controls.Add(grid, 0, 4);
        return layout;
    }

    // Los avisos de hoy de todas las máquinas
    private static List<(string Maquina, Tramo Parada)> Avisos() =>
        ModuloMaquina.Maquinas
            .SelectMany(m => ModuloMaquina.TramosDe(m)
                .Where(t => t.AvisoMecanico is DateTime aviso && aviso.Date == DateTime.Today)
                .Select(t => (m, t)))
            .ToList();

    private void Refrescar()
    {
        var ahora = DateTime.Now;
        var avisos = Avisos();
        var abiertos = avisos.Where(a => a.Parada.ResueltoMecanico == null).OrderBy(a => a.Parada.AvisoMecanico).ToList();

        // En el móvil será una notificación; aquí, un sonido
        if (avisosVistos >= 0 && avisos.Count > avisosVistos) SystemSounds.Exclamation.Play();
        avisosVistos = avisos.Count;

        string estado = string.Join("|", abiertos.Select(a =>
            $"{a.Maquina}{a.Parada.AvisoMecanico:HHmmss}{a.Parada.LlegaMecanico != null}{a.Parada.Motivo}{a.Parada.Comentario}"));
        if (estado != pintado)
        {
            pintado = estado;
            PintarTarjetas(abiertos);
            LlenarResueltos(avisos);
        }
        foreach (var (parada, tiempo) in tarjetas) tiempo.Text = TextoTiempo(parada, ahora);
        lblPendientes.Text = abiertos.Count == 0 ? "Pendientes" : $"Pendientes ({abiertos.Count})";
    }

    private static string TextoTiempo(Tramo parada, DateTime ahora) => parada.LlegaMecanico is DateTime llega
        ? $"Llegaste a las {llega:HH:mm}  ·  trabajando {ModuloMaquina.Duracion(ahora - llega)}"
        : $"Avisado a las {parada.AvisoMecanico:HH:mm}  ·  hace {ModuloMaquina.Duracion(ahora - parada.AvisoMecanico!.Value)}";

    // Una tarjeta por aviso, el más antiguo arriba. Van con Dock Top: se añaden al revés.
    private void PintarTarjetas(List<(string Maquina, Tramo Parada)> abiertos)
    {
        pendientes.SuspendLayout();
        foreach (Control control in pendientes.Controls.Cast<Control>().ToList()) control.Dispose();
        tarjetas.Clear();

        if (abiertos.Count == 0)
            pendientes.Controls.Add(new Label
            {
                Text = "No hay avisos pendientes",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Tema.Suave,
                Font = Tema.FuenteApartado
            });

        foreach (var (maquina, parada) in Enumerable.Reverse(abiertos))
        {
            pendientes.Controls.Add(Tarjeta(maquina, parada));
            pendientes.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10 });
        }
        pendientes.ResumeLayout();
    }

    // Rojo mientras se espera al mecánico y naranja con él trabajando, como en las barras
    private Control Tarjeta(string maquina, Tramo parada)
    {
        bool trabajando = parada.LlegaMecanico != null;
        var tarjeta = new Panel { Dock = DockStyle.Top, Height = 110, BackColor = Color.White };

        var info = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 12, 0, 0) };
        var tiempo = new Label { Dock = DockStyle.Top, Height = 24, ForeColor = trabajando ? Tema.Aviso : Tema.Error, Font = Tema.FuenteNegrita };
        info.Controls.Add(tiempo);
        info.Controls.Add(new Label
        {
            Text = string.Join("  ·  ", new[]
            {
                parada.Maquinista is string m ? $"Maquinista: {m}" : null,
                parada.Comentario
            }.OfType<string>()),
            Dock = DockStyle.Top,
            Height = 26,
            ForeColor = Tema.Suave
        });
        info.Controls.Add(new Label
        {
            Text = $"{maquina}  ·  {parada.Motivo ?? "sin motivo todavía"}",
            Dock = DockStyle.Top,
            Height = 34,
            Font = Tema.FuenteAviso,
            ForeColor = Tema.Texto
        });

        var boton = Tema.BotonPrincipal(trabajando ? "✓   Resuelto" : "He llegado", 220);
        boton.Dock = DockStyle.Fill;
        boton.Font = Tema.FuenteAviso;
        boton.BackColor = trabajando ? ModuloMaquina.Verde : Tema.Aviso;
        boton.Click += (s, e) =>
        {
            if (trabajando) parada.ResueltoMecanico = DateTime.Now;
            else parada.LlegaMecanico = DateTime.Now;
            Refrescar();
        };
        var botonera = new Panel { Dock = DockStyle.Right, Width = 250, Padding = new Padding(0, 24, 20, 24) };
        botonera.Controls.Add(boton);

        // Con Dock, el que se añade primero es el que rellena lo que sobra
        tarjeta.Controls.Add(info);
        tarjeta.Controls.Add(botonera);
        tarjeta.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 8, BackColor = trabajando ? ModuloMaquina.Naranja : ModuloMaquina.Rojo });

        tarjetas.Add((parada, tiempo));
        return tarjeta;
    }

    // El último resuelto arriba. Si la máquina arrancó antes de que llegara, no hay tiempo de llegada.
    private void LlenarResueltos(List<(string Maquina, Tramo Parada)> avisos)
    {
        grid.Rows.Clear();
        foreach (var (maquina, parada) in avisos.Where(a => a.Parada.ResueltoMecanico != null)
                     .OrderByDescending(a => a.Parada.ResueltoMecanico))
        {
            var aviso = parada.AvisoMecanico!.Value;
            var resuelto = parada.ResueltoMecanico!.Value;
            grid.Rows.Add(maquina, parada.Motivo ?? "Sin motivo", aviso.ToString("HH:mm"),
                parada.LlegaMecanico is DateTime llega ? ModuloMaquina.Duracion(llega - aviso) : "—",
                ModuloMaquina.Duracion(resuelto - (parada.LlegaMecanico ?? aviso)),
                parada.Maquinista ?? "—");
        }
        grid.ClearSelection();
    }
}
