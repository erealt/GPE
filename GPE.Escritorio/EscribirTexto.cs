namespace GPE.Escritorio;

// Panel para escribir un texto corto junto a una casilla (por ejemplo, el comentario de una parada).
// En la tablet, al pulsar la caja Windows saca su teclado.
public class EscribirTexto : PanelEmergente
{
    // null si se cerró sin guardar; "" si se guardó vacío
    public string? Texto { get; private set; }

    public EscribirTexto(string titulo, string? actual, int largoMaximo)
    {
        var rejilla = Rejilla();
        AnadirAncho(rejilla, Titulo(titulo));

        var caja = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(AnchoDoble, 110),
            MaxLength = largoMaximo,
            Text = actual ?? "",
            Margin = new Padding(0, 0, 0, 10)
        };
        AnadirAncho(rejilla, caja);

        var guardar = BotonGrande("✓  Guardar", Tema.AcentoSuave, Tema.Acento);
        guardar.Click += (s, e) =>
        {
            Texto = caja.Text.Trim();
            Close();
        };
        var cancelar = BotonGrande("Cancelar", Tema.CabeceraTabla, Tema.Texto);
        cancelar.Click += (s, e) => Close();
        rejilla.Controls.Add(guardar);
        rejilla.Controls.Add(cancelar);

        Controls.Add(rejilla);
        Shown += (s, e) =>
        {
            caja.Focus();
            caja.SelectionStart = caja.TextLength;
        };
    }
}
