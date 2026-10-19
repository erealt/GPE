using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;

public class FormHistorial : Form
{
    private static readonly CultureInfo Es = new("es-ES");

    private readonly DevolucionServicio servicio = new();
    private readonly DevolucionFila devolucion;

    private readonly Panel scroll = new();
    private readonly TableLayoutPanel lista = new();

    public FormHistorial(DevolucionFila devolucion)
    {
        this.devolucion = devolucion;

        Text = $"Historial · {devolucion.Numero}";
        Size = new Size(900, 700);
        MinimumSize = new Size(650, 450);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        var titulo = new Label
        {
            Text = "Historial de cambios",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Tema.Texto,
            Dock = DockStyle.Top,
            Height = 50
        };

        var subtitulo = new Label
        {
            Text = $"Quién ha hecho qué en la devolución {devolucion.Numero} de {devolucion.Cliente}.",
            ForeColor = Tema.Suave,
            Dock = DockStyle.Top,
            Height = 40
        };

        scroll.Dock = DockStyle.Fill;
        scroll.AutoScroll = true;

        lista.Dock = DockStyle.Top;
        lista.AutoSize = true;
        lista.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        lista.ColumnCount = 1;
        lista.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(lista);

        var contenido = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30, 20, 30, 20) };
        contenido.Controls.Add(scroll);
        contenido.Controls.Add(subtitulo);
        contenido.Controls.Add(titulo);
        Controls.Add(contenido);

        Load += (s, e) => CargarHistorial();
        scroll.Resize += (s, e) => AjustarAnchoTextos();
    }

    private void CargarHistorial()
    {
        List<EntradaHistorial> entradas;
        try
        {
            entradas = servicio.ObtenerHistorial(devolucion.Id);
        }
        catch (DbException ex)
        {
            MessageBox.Show(this,
                "No se pudo cargar el historial de la base de datos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        lista.SuspendLayout();

        if (entradas.Count == 0)
        {
            lista.Controls.Add(new Label
            {
                Text = "Todavía no hay cambios registrados.",
                AutoSize = true,
                ForeColor = Tema.Suave,
                Margin = new Padding(0, 20, 0, 0)
            });
        }

        foreach (var entrada in entradas)
            lista.Controls.Add(CrearTarjeta(entrada));

        lista.ResumeLayout();
        AjustarAnchoTextos();
    }

    private Control CrearTarjeta(EntradaHistorial entrada)
    {
        // Franja de color a la izquierda: el de la fase si la entrada cambió de fase
        Color colorFranja = entrada.Fase != null ? Tema.ColorFase(entrada.Fase.FaseNueva).Texto : Tema.Borde;

        var tarjeta = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(0)
        };
        tarjeta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 5));
        tarjeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var franja = new Panel { Dock = DockStyle.Fill, BackColor = colorFranja, Margin = new Padding(0) };

        var cuerpo = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18, 14, 18, 14),
            Margin = new Padding(0)
        };

        cuerpo.Controls.Add(new Label
        {
            Text = entrada.Titulo,
            AutoSize = true,
            Font = new Font("Segoe UI", 11.5f, FontStyle.Bold),
            ForeColor = Tema.Texto
        });
        cuerpo.Controls.Add(new Label
        {
            Text = $"{entrada.Usuario} · {entrada.Fecha.ToString("d 'de' MMMM 'de' yyyy, HH:mm", Es)}",
            AutoSize = true,
            ForeColor = Tema.Suave,
            Font = new Font("Segoe UI", 9),
            Margin = new Padding(0, 2, 0, 0)
        });

        if (entrada.Fase != null)
            cuerpo.Controls.Add(CrearCambioFase(entrada.Fase));

        if (entrada.Detalles.Count > 0)
        {
            var detalles = new Label
            {
                Text = string.Join(Environment.NewLine, entrada.Detalles.Select(d => "•  " + d)),
                AutoSize = true,
                ForeColor = Tema.Texto,
                Margin = new Padding(0, 10, 0, 0),
                Tag = "detalle"   // su ancho máximo se ajusta al de la ventana
            };
            cuerpo.Controls.Add(detalles);
        }

        tarjeta.Controls.Add(franja, 0, 0);
        tarjeta.Controls.Add(cuerpo, 1, 0);
        return tarjeta;
    }

    private static Control CrearCambioFase(CambioFase fase)
    {
        var fila = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 10, 0, 0)
        };

        if (fase.Anterior != null)
        {
            fila.Controls.Add(Etiqueta(fase.Anterior, fase.FaseAnterior ?? 1));
            fila.Controls.Add(new Label
            {
                Text = "→",
                AutoSize = true,
                ForeColor = Tema.Suave,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Margin = new Padding(6, 3, 6, 0)
            });
        }
        fila.Controls.Add(Etiqueta(fase.Nueva, fase.FaseNueva));
        return fila;

        static Label Etiqueta(string texto, int numeroFase)
        {
            var (fondo, color) = Tema.ColorFase(numeroFase);
            return new Label
            {
                Text = texto,
                AutoSize = true,
                BackColor = fondo,
                ForeColor = color,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Padding = new Padding(10, 4, 10, 4),
                Margin = new Padding(0)
            };
        }
    }


    private void AjustarAnchoTextos()
    {
        int ancho = Math.Max(200, scroll.ClientSize.Width - 70);
        foreach (Control tarjeta in lista.Controls)
            foreach (Control parte in tarjeta.Controls)
                foreach (Control control in parte.Controls)
                    if (control.Tag as string == "detalle")
                        control.MaximumSize = new Size(ancho, 0);
    }
}
