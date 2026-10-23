using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Pantalla de inicio: saludo y una tarjeta por módulo con un dato del momento
// (se vuelve a calcular cada vez que se entra en el inicio).
public class ModuloInicio : UserControl
{
    private readonly List<(Label Etiqueta, Func<string?> Calcular)> resumenes = new();

    public ModuloInicio(List<(string Nombre, string Descripcion, Func<string?> Resumen, Action Abrir)> modulos)
    {
        var (scroll, pila) = Componentes.Pagina();

        string nombre = Sesion.UsuarioActual?.Nombre.Split(' ')[0] ?? "";
        pila.Controls.Add(Componentes.Cabecera("INICIO", $"Hola, {nombre}", "¿Qué quieres hacer hoy?"));

        var tarjetas = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Margin = new Padding(0)
        };
        foreach (var modulo in modulos)
            tarjetas.Controls.Add(Tarjeta(modulo.Nombre, modulo.Descripcion, modulo.Resumen, modulo.Abrir));
        pila.Controls.Add(tarjetas);

        Controls.Add(scroll);

        VisibleChanged += (s, e) => { if (Visible) ActualizarResumenes(); };
    }

    private Control Tarjeta(string titulo, string descripcion, Func<string?> resumen, Action abrir)
    {
        var tarjeta = new TableLayoutPanel
        {
            Size = new Size(360, 190),
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.White,
            Padding = new Padding(25, 22, 25, 18),
            Margin = new Padding(0, 0, 20, 20),
            Cursor = Cursors.Hand
        };
        tarjeta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tarjeta.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        tarjeta.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        tarjeta.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblResumen = new Label
        {
            AutoSize = true,
            ForeColor = Tema.Acento,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };
        resumenes.Add((lblResumen, resumen));

        tarjeta.Controls.Add(new Label
        {
            Text = titulo,
            AutoSize = true,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            ForeColor = Tema.Texto,
            Margin = new Padding(0, 0, 0, 6)
        });
        tarjeta.Controls.Add(new Label
        {
            Text = descripcion,
            Dock = DockStyle.Fill,
            ForeColor = Tema.Suave
        });
        tarjeta.Controls.Add(lblResumen);
        tarjeta.Controls.Add(new Label
        {
            Text = "Abrir  →",
            AutoSize = true,
            ForeColor = Tema.Texto,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Margin = new Padding(0)
        });

        // Toda la tarjeta se puede pulsar; al pasar el ratón se marca el borde en morado
        bool encima = false;
        tarjeta.Paint += (s, e) =>
        {
            using var borde = new Pen(encima ? Tema.Acento : Tema.Linea, encima ? 2 : 1);
            e.Graphics.DrawRectangle(borde, 0, 0, tarjeta.Width - 1, tarjeta.Height - 1);
        };
        foreach (Control control in new[] { tarjeta }.Concat(tarjeta.Controls.Cast<Control>()))
        {
            control.Cursor = Cursors.Hand;
            control.Click += (s, e) => abrir();
            control.MouseEnter += (s, e) => { encima = true; tarjeta.Invalidate(); };
            control.MouseLeave += (s, e) =>
            {
                // Al pasar de la tarjeta a una de sus etiquetas también salta MouseLeave
                encima = tarjeta.ClientRectangle.Contains(tarjeta.PointToClient(Cursor.Position));
                tarjeta.Invalidate();
            };
        }

        return tarjeta;
    }

    private void ActualizarResumenes()
    {
        foreach (var (etiqueta, calcular) in resumenes)
        {
            etiqueta.Text = calcular() ?? "";
            etiqueta.Visible = etiqueta.Text != "";
        }
    }

    // Datos de cada tarjeta (null si no se pueden leer)

    public static string? ResumenDevoluciones()
    {
        try
        {
            var lista = new DevolucionServicio().ObtenerDevolucionesVisibles();
            int enCurso = lista.Count(d => d.Fase < Fase.Cierre);
            int retrasadas = Sesion.TieneRol(Rol.Administracion) ? lista.Count(DevolucionServicio.AbonoRetrasado) : 0;
            return $"{enCurso} en curso" + (retrasadas > 0 ? $"  ·  ⚠ {retrasadas} esperando abono" : "");
        }
        catch (DbException)
        {
            return null;
        }
    }
}
