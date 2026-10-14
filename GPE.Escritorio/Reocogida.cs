using GPE.Datos;

namespace GPE.Escritorio
{
    public class Recogida : Form
    {
        public Recogida(DevolucionFila devolucion)
        {
            Text = $"Gestión de recogida · {devolucion.Numero}";
            Size = new Size(1000, 700);
            MinimumSize = new Size(800, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(245, 246, 244);
            Font = new Font("Segoe UI", 10);

            var titulo = new Label
            {
                Text = "Gestión de recogida",
                Font = new Font("Segoe UI", 22, FontStyle.Bold),
                ForeColor = Color.FromArgb(20, 35, 30),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 60,
                Padding = new Padding(40, 15, 0, 0)
            };

            var subtitulo = new Label
            {
                Text = $"Gestiona la recogida de la devolución {devolucion.Numero} de {devolucion.Cliente}.",
                ForeColor = Color.FromArgb(110, 120, 115),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(42, 5, 0, 0)
            };

            var barra = new BarraProgresoDevolucion
            {
                FasesCompletadas = devolucion.Fase,
                FaseActual = Math.Min(devolucion.Fase + 1, 6),
                Dock = DockStyle.Top
            };

            Controls.Add(barra);
            Controls.Add(subtitulo);
            Controls.Add(titulo);
        }
    }
}