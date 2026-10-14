
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

            Color acento = Tema.Acento;
            Color gris = Color.FromArgb(218, 226, 221);
            Color texto = Tema.Texto;

            using var fuente = new Font("Segoe UI", 8.5f);
            using var negrita = new Font(
                "Segoe UI", 8.5f, FontStyle.Bold);
            using var fuenteNumero = new Font("Segoe UI", 9, FontStyle.Bold);

            using var lapiz = new Pen(gris, 3);
            using var lapizAcento = new Pen(acento, 3);

            // Línea de progreso
            g.DrawLine(lapiz, margen, centroY,
                ancho - margen, centroY);

            int xActual = margen + (faseActual - 1) * paso;

            if (faseActual > 1)
            {
                g.DrawLine(lapizAcento, margen, centroY,
                    xActual, centroY);
            }

            for (int i = 0; i < fases.Length; i++)
            {
                int x = margen + i * paso;
                int numero = i + 1;

                bool completada = numero <= FasesCompletadas;
                bool actual = numero == faseActual;

                // Completada: círculo lleno. Actual: círculo suave con borde de acento.
                Color colorCirculo = completada ? acento
                    : actual ? Tema.AcentoSuave
                    : Color.White;

                Color colorBorde = completada || actual
                 ? acento : gris;

                Color colorNumero = completada ? Color.White
                    : actual ? acento
                    : Tema.Suave;

                using var relleno = new SolidBrush(colorCirculo);
                using var borde = new Pen(colorBorde, 2);

                g.FillEllipse(relleno, x - 15, centroY - 15, 30, 30);
                g.DrawEllipse(borde, x - 15, centroY - 15, 30, 30);

                string simbolo = completada ? "✓" : numero.ToString();

                TextRenderer.DrawText(
                    g, simbolo,
                    fuenteNumero,
                    new Rectangle(x - 14, centroY - 13, 28, 26),
                    colorNumero,
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
