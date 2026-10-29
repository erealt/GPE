namespace GPE.Escritorio;

// Teclado de números grande para apuntar un valor con el dedo (el cloro del control de agua).
// También se puede escribir con el teclado del ordenador. Lo escrito se queda al pulsar
// "Aceptar" o fuera del panel; con Esc no se cambia nada.
public class TecladoNumerico : PanelEmergente
{
    private const int AnchoTecla = 72;

    private readonly Label lblValor = new();
    private readonly int largoMaximo;
    private string texto;

    // El valor tal como se ha escrito ("0,55"); null si se cerró con Esc
    public string? Valor => Cancelado ? null : texto;

    public TecladoNumerico(string titulo, string? actual, int largoMaximo)
    {
        this.largoMaximo = largoMaximo;
        texto = actual ?? "";

        var pila = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Margin = new Padding(0) };
        for (int c = 0; c < 3; c++)
            pila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoTecla + 8));

        void Ancho(Control control)
        {
            pila.Controls.Add(control);
            pila.SetColumnSpan(control, 3);
        }

        var lblTitulo = Titulo(titulo);
        lblTitulo.MaximumSize = new Size(AnchoTecla * 3 + 16, 0);
        Ancho(lblTitulo);

        lblValor.AutoSize = false;
        lblValor.Size = new Size(AnchoTecla * 3 + 16, 56);
        lblValor.Margin = new Padding(0, 0, 0, 10);
        lblValor.TextAlign = ContentAlignment.MiddleRight;
        lblValor.Padding = new Padding(0, 0, 12, 0);
        lblValor.BackColor = Tema.CabeceraTabla;
        lblValor.ForeColor = Tema.Texto;
        lblValor.Font = Tema.FuenteTituloVentana;
        Ancho(lblValor);

        foreach (string tecla in new[] { "7", "8", "9", "4", "5", "6", "1", "2", "3", ",", "0", "⌫" })
        {
            var boton = BotonGrande(tecla, Color.White, Tema.Texto, ancho: AnchoTecla);
            boton.TextAlign = ContentAlignment.MiddleCenter;
            boton.Padding = new Padding(0);
            boton.Font = Tema.FuenteAviso;
            boton.FlatAppearance.BorderSize = 1;
            boton.FlatAppearance.BorderColor = Tema.Borde;
            string pulsada = tecla;
            boton.Click += (s, e) => Pulsar(pulsada);
            pila.Controls.Add(boton);
        }

        var borrar = Tema.BotonSecundario("Borrar", AnchoTecla);
        borrar.Height = 48;
        borrar.Margin = new Padding(0, 2, 8, 0);
        borrar.Click += (s, e) => { texto = ""; Mostrar(); };
        pila.Controls.Add(borrar);

        var aceptar = Tema.BotonPrincipal("Aceptar", AnchoTecla * 2 + 8);
        aceptar.Height = 48;
        aceptar.Margin = new Padding(0, 2, 0, 0);
        aceptar.Click += (s, e) => Close();
        pila.Controls.Add(aceptar);
        pila.SetColumnSpan(aceptar, 2);
        AcceptButton = aceptar;   // Enter

        Controls.Add(pila);
        Mostrar();

        // Con el teclado del ordenador: números, coma o punto y retroceso
        KeyPress += (s, e) =>
        {
            if (char.IsDigit(e.KeyChar)) Pulsar(e.KeyChar.ToString());
            else if (e.KeyChar is ',' or '.') Pulsar(",");
            else if (e.KeyChar == '\b') Pulsar("⌫");
            else return;
            e.Handled = true;
        };
    }

    private void Pulsar(string tecla)
    {
        if (tecla == "⌫")
            texto = texto.Length > 0 ? texto[..^1] : "";
        else if (texto.Length < largoMaximo && !(tecla == "," && texto.Contains(',')))
            texto = tecla == "," && texto == "" ? "0," : texto + tecla;
        Mostrar();
    }

    private void Mostrar() => lblValor.Text = texto == "" ? "—" : texto;
}
