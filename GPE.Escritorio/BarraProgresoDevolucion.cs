
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GPE.Escritorio
{
    public class BarraProgresoDevolucion : UserControl
    {
        private readonly string[] fases =
        {
            "Solicitud\nrecibida",
            "Recogida\nsolicitada",
            "Mercancía\nrecibida",
            "Revisada\nalmacén",
            "Abono\nrealizado",
            "Cierre"
        };

        private int faseActual = 1;
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.EditorBrowsable(
            System.ComponentModel.EditorBrowsableState.Never)]
        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int FaseActual
        {
            get => faseActual;
            set
            {
                faseActual = Math.Max(1, Math.Min(6, value));
                Invalidate();
            }
        }
        [System.ComponentModel.Browsable(false)]
        [System.ComponentModel.DesignerSerializationVisibility(
            System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int FasesCompletadas { get; set; } = 1;


        public BarraProgresoDevolucion()
        {
            Height = 115;
            MinimumSize = new Size(600, 115);
            BackColor = Color.White;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int ancho = ClientSize.Width;
            int centroY = 30;
            int margen = 45;
            int paso = (ancho - margen * 2) / 5;

            Color verde = Color.FromArgb(43, 137, 103);
            Color gris = Color.FromArgb(218, 226, 221);
            Color texto = Color.FromArgb(35, 48, 42);

            using var fuente = new Font("Segoe UI", 8.5f);
            using var negrita = new Font(
                "Segoe UI", 8.5f, FontStyle.Bold);

            using var lapiz = new Pen(gris, 3);
            using var lapizVerde = new Pen(verde, 3);

            // Línea de progreso
            g.DrawLine(lapiz, margen, centroY,
                ancho - margen, centroY);

            int xActual = margen + (faseActual - 1) * paso;

            if (faseActual > 1)
            {
                g.DrawLine(lapizVerde, margen, centroY,
                    xActual, centroY);
            }

            for (int i = 0; i < fases.Length; i++)
            {
                int x = margen + i * paso;
                int numero = i + 1;

                bool completada = numero <= FasesCompletadas;
                bool actual = numero == faseActual;

                Color colorCirculo = completada
                 ? verde : Color.White;

                Color colorBorde = completada || actual
                 ? verde : gris;

                using var relleno = new SolidBrush(colorCirculo);
                using var borde = new Pen(colorBorde, 2);

                g.FillEllipse(relleno, x - 15, centroY - 15, 30, 30);
                g.DrawEllipse(borde, x - 15, centroY - 15, 30, 30);

                string simbolo = completada ? "✓" : numero.ToString();

                TextRenderer.DrawText(
                    g, simbolo,
                    new Font("Segoe UI", 9, FontStyle.Bold),
                    new Rectangle(x - 14, centroY - 13, 28, 26),
                    completada || actual ? Color.White : verde,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter);

                var rectTexto = new Rectangle(
                    x - paso / 2, 52, paso, 42);

                TextRenderer.DrawText(
                    g, fases[i], actual || completada
                        ? negrita : fuente,
                    rectTexto, texto,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak);
            }
        }
    }
}
