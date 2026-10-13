
using System;
using System.Drawing;
using System.Windows.Forms;

namespace GPE.Escritorio
{
    public class Recogida : Form
    {
        public Recogida()
        {
            Text = "Gestión de recogida";
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
                AutoSize = true,
                Location = new Point(40, 35)
            };

            var subtitulo = new Label
            {
                Text = "Gestiona la recogida de la devolución.",
                ForeColor = Color.FromArgb(110, 120, 115),
                AutoSize = true,
                Location = new Point(42, 85)
            };
            

            Controls.Add(titulo);
            Controls.Add(subtitulo);
        }
    }
}
