namespace GPE.Escritorio;

// Lista que sale debajo de un cuadro de texto mientras se escribe, con los ya usados que contienen
// lo escrito. Al elegir uno (clic, o flechas + Intro) se rellena todo; si no, se sigue escribiendo.
public class Sugerencias<T> where T : class
{
    private const int MaximoVisibles = 8;

    private readonly TextBox caja;
    private readonly List<T> usados;
    private readonly Func<T, string> buscarEn;
    private readonly Action<T> elegir;
    private readonly ListBox lista = new()
    {
        Visible = false,
        IntegralHeight = false,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Segoe UI", 10),
        Cursor = Cursors.Hand
    };
    private bool rellenando;   // el texto lo cambia la propia elección, no quien escribe

    // buscarEn: el dato del que se busca (nombre o NIF); en la lista se ve el ToString de cada uno
    public Sugerencias(TextBox caja, List<T> usados, Func<T, string> buscarEn, Action<T> elegir)
    {
        this.caja = caja;
        this.usados = usados;
        this.buscarEn = buscarEn;
        this.elegir = elegir;
        if (usados.Count == 0) return;

        caja.TextChanged += (s, e) => { if (!rellenando && caja.Focused) Mostrar(); };   // no al rellenarse desde otro campo
        caja.MouseWheel += (s, e) => Ocultar();   // la página se mueve y la lista se quedaría fuera de sitio
        caja.Leave += (s, e) => caja.BeginInvoke(() => { if (!lista.Focused) Ocultar(); });
        caja.PreviewKeyDown += (s, e) =>
        {
            // Con la lista abierta, Esc la cierra a ella y no a la ventana
            if (lista.Visible && e.KeyCode is Keys.Escape or Keys.Enter or Keys.Up or Keys.Down)
                e.IsInputKey = true;
        };
        caja.KeyDown += Teclas;

        lista.MouseClick += (s, e) => Elegir();
        lista.Leave += (s, e) => Ocultar();
    }

    private void Mostrar()
    {
        string texto = caja.Text.Trim();
        var coincidencias = texto.Length == 0 ? [] : usados
            .Where(u => buscarEn(u).Contains(texto, StringComparison.OrdinalIgnoreCase))
            .Take(MaximoVisibles)
            .ToList();
        if (coincidencias.Count == 0 || caja.FindForm() is not Form ventana)
        {
            Ocultar();
            return;
        }

        lista.Items.Clear();
        lista.Items.AddRange(coincidencias.Cast<object>().ToArray());
        if (lista.Parent != ventana) ventana.Controls.Add(lista);

        // Justo debajo del cuadro, por encima de todo lo demás
        lista.Location = ventana.PointToClient(caja.PointToScreen(new Point(0, caja.Height)));
        lista.Width = Math.Max(caja.Width, 420);
        lista.Height = coincidencias.Count * lista.ItemHeight + 4;
        lista.Visible = true;
        lista.BringToFront();
    }

    private void Ocultar() => lista.Visible = false;

    private void Teclas(object? sender, KeyEventArgs e)
    {
        if (!lista.Visible) return;
        switch (e.KeyCode)
        {
            case Keys.Down:
                lista.SelectedIndex = Math.Min(lista.SelectedIndex + 1, lista.Items.Count - 1);
                break;
            case Keys.Up:
                lista.SelectedIndex = Math.Max(lista.SelectedIndex - 1, -1);
                break;
            case Keys.Enter:
                if (lista.SelectedIndex >= 0) Elegir(); else Ocultar();
                break;
            case Keys.Escape:
                Ocultar();
                break;
            default:
                return;
        }
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void Elegir()
    {
        if (lista.SelectedItem is not T elegido) return;
        Ocultar();
        rellenando = true;
        elegir(elegido);
        rellenando = false;
        caja.Focus();
        caja.SelectionStart = caja.Text.Length;
    }
}
