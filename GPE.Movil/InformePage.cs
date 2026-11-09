namespace GPE.Movil;

// Al pulsar "Resuelto": qué ha hecho el mecánico
public class InformePage : ContentPage
{
    private const int Largo = 500;

    public InformePage(Aviso aviso)
    {
        Title = "Resuelto";
        BackgroundColor = Colors.White;

        var cuenta = new Label { TextColor = Estilo.Suave, FontSize = 13, HorizontalOptions = LayoutOptions.End };
        var texto = new Editor
        {
            Text = aviso.QueHaHecho,
            Placeholder = "Por ejemplo: cambiada la correa del rodillo",
            MaxLength = Largo,
            HeightRequest = 160,
            FontSize = 17,
            TextColor = Estilo.Texto,
            BackgroundColor = Colors.Transparent,
            AutoSize = EditorAutoSizeOption.Disabled
        };
        cuenta.Text = $"{texto.Text?.Length ?? 0}/{Largo}";
        texto.TextChanged += (s, e) => cuenta.Text = $"{e.NewTextValue?.Length ?? 0}/{Largo}";

        var error = new Label { TextColor = Estilo.Rojo, FontAttributes = FontAttributes.Bold };
        var guardar = Estilo.Boton("✓  Guardar y dar por resuelto", Estilo.Verde);
        guardar.Clicked += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(texto.Text))
            {
                error.Text = "Escribe qué has hecho.";
                return;
            }
            guardar.IsEnabled = false;
            if (await Api.Resuelto(aviso.Id, texto.Text.Trim())) await Navigation.PopAsync();
            else error.Text = "No se ha podido guardar. Prueba otra vez.";
            guardar.IsEnabled = true;
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(20, 16),
                Spacing = 8,
                Children =
                {
                    Estilo.Pastilla("● RESOLVIENDO", Estilo.NaranjaSuave, Estilo.NaranjaTexto),
                    new Label { Text = aviso.Maquina, FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Estilo.Texto, Margin = new Thickness(0, 6, 0, 0) },
                    new Label { Text = aviso.Motivo ?? "Sin motivo", FontSize = 16, TextColor = Estilo.Suave },
                    new Label { Text = "¿Qué has hecho?", FontAttributes = FontAttributes.Bold, TextColor = Estilo.Texto, Margin = new Thickness(0, 14, 0, 0) },
                    Estilo.Campo(texto),
                    cuenta,
                    error,
                    guardar
                }
            }
        };
        Loaded += (s, e) => texto.Focus();
    }
}
