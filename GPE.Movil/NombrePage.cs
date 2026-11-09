namespace GPE.Movil;

// Quién es el mecánico: se pregunta la primera vez y se guarda en el móvil.
// Aquí también se cambia la dirección del servidor.
public class NombrePage : ContentPage
{
    private readonly TaskCompletionSource cerrada = new();
    public Task Cerrada => cerrada.Task;

    public NombrePage(bool primeraVez)
    {
        BackgroundColor = Colors.White;

        var nombre = new Entry
        {
            Text = Api.Mecanico,
            Placeholder = "Tu nombre",
            FontSize = 18,
            MaxLength = 40,
            Keyboard = Keyboard.Create(KeyboardFlags.CapitalizeWord),
            TextColor = Estilo.Texto
        };
        var servidor = new Entry { Text = Api.Direccion, FontSize = 15, Keyboard = Keyboard.Url, TextColor = Estilo.Texto };
        var error = new Label { TextColor = Estilo.Rojo, FontAttributes = FontAttributes.Bold };

        var entrar = Estilo.Boton("Entrar", Estilo.Acento);
        entrar.Clicked += async (s, e) =>
        {
            var texto = string.Join(" ", (nombre.Text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
            if (texto == "")
            {
                error.Text = "Escribe tu nombre.";
                return;
            }
            if (string.IsNullOrWhiteSpace(servidor.Text))
            {
                error.Text = "Escribe la dirección del servidor.";
                return;
            }
            Api.Mecanico = texto;
            Api.Direccion = servidor.Text;
            await Navigation.PopModalAsync();
        };

        var cancelar = new Button { Text = "Cancelar", BackgroundColor = Colors.Transparent, TextColor = Estilo.Suave, IsVisible = !primeraVez };
        cancelar.Clicked += async (s, e) => await Navigation.PopModalAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 56, 24, 24),
                Spacing = 8,
                Children =
                {
                    Estilo.Marca(),
                    new Label { Text = "¿Quién eres?", FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Estilo.Texto, Margin = new Thickness(0, 14, 0, 0) },
                    new Label { Text = "Se apuntará tu nombre cuando llegues a una máquina y cuando la dejes resuelta.", TextColor = Estilo.Suave, FontSize = 16 },
                    new Label { Text = "Nombre", FontAttributes = FontAttributes.Bold, TextColor = Estilo.Texto, Margin = new Thickness(0, 18, 0, 0) },
                    Estilo.Campo(nombre),
                    new Label { Text = "Servidor", FontAttributes = FontAttributes.Bold, TextColor = Estilo.Texto, Margin = new Thickness(0, 12, 0, 0) },
                    Estilo.Campo(servidor),
                    error,
                    entrar,
                    cancelar
                }
            }
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        cerrada.TrySetResult();
    }

    // El botón "atrás" de Android solo cierra si ya hay nombre
    protected override bool OnBackButtonPressed() => string.IsNullOrEmpty(Api.Mecanico) || base.OnBackButtonPressed();
}
