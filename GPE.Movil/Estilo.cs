using Microsoft.Maui.Controls.Shapes;

namespace GPE.Movil;

// Los colores y piezas comunes, los mismos que la página web de prueba y que GPE de escritorio
public static class Estilo
{
    public static readonly Color Fondo = Color.FromArgb("#F3F4F1");
    public static readonly Color Texto = Color.FromArgb("#14231E");
    public static readonly Color Suave = Color.FromArgb("#6E7873");
    public static readonly Color Tenue = Color.FromArgb("#9AA29E");
    public static readonly Color Linea = Color.FromArgb("#E8EAE6");
    public static readonly Color Acento = Color.FromArgb("#5900F6");
    public static readonly Color AcentoOscuro = Color.FromArgb("#3F00B0");
    public static readonly Color AcentoSuave = Color.FromArgb("#F0ECFF");
    public static readonly Color Rojo = Color.FromArgb("#D73C3C");
    public static readonly Color RojoSuave = Color.FromArgb("#FDECEC");
    public static readonly Color Naranja = Color.FromArgb("#F0961E");
    public static readonly Color NaranjaTexto = Color.FromArgb("#8A5300");
    public static readonly Color NaranjaSuave = Color.FromArgb("#FFF3DD");
    public static readonly Color Verde = Color.FromArgb("#3F8F24");
    public static readonly Color VerdeSuave = Color.FromArgb("#E6F4DF");

    public static Border Tarjeta(View contenido, Thickness? relleno = null, double radio = 18) => new()
    {
        Content = contenido,
        BackgroundColor = Colors.White,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = radio },
        Padding = relleno ?? new Thickness(16, 14),
        Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.08f, Radius = 12, Offset = new Point(0, 3) }
    };

    // Pastilla de color: estados, fichas, contadores
    public static Border Pastilla(string texto, Color fondo, Color color, double tamano = 12, bool negrita = true) => new()
    {
        BackgroundColor = fondo,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 999 },
        Padding = new Thickness(10, 4),
        HorizontalOptions = LayoutOptions.Start,
        VerticalOptions = LayoutOptions.Center,
        Content = new Label
        {
            Text = texto,
            FontSize = tamano,
            FontAttributes = negrita ? FontAttributes.Bold : FontAttributes.None,
            TextColor = color,
            LineBreakMode = LineBreakMode.TailTruncation
        }
    };

    // Círculo con una letra o un símbolo
    public static Border Circulo(string texto, Color fondo, Color color, double tamano) => new()
    {
        WidthRequest = tamano,
        HeightRequest = tamano,
        BackgroundColor = fondo,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = tamano / 2 },
        Content = new Label
        {
            Text = texto,
            TextColor = color,
            FontAttributes = FontAttributes.Bold,
            FontSize = tamano * 0.5,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }
    };

    public static Button Boton(string texto, Color fondo) => new()
    {
        Text = texto,
        BackgroundColor = fondo,
        TextColor = Colors.White,
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        HeightRequest = 56,
        CornerRadius = 14,
        Shadow = new Shadow { Brush = new SolidColorBrush(fondo), Opacity = 0.3f, Radius = 10, Offset = new Point(0, 4) }
    };

    // Caja gris redondeada para un campo de texto (Entry o Editor)
    public static Border Campo(View campo) => new()
    {
        Content = campo,
        BackgroundColor = Fondo,
        Stroke = Linea,
        StrokeThickness = 1.5,
        StrokeShape = new RoundRectangle { CornerRadius = 12 },
        Padding = new Thickness(10, 2)
    };

    // El cuadrado morado con "GPE"
    public static Border Marca() => new()
    {
        WidthRequest = 56,
        HeightRequest = 56,
        HorizontalOptions = LayoutOptions.Start,
        BackgroundColor = Acento,
        StrokeThickness = 0,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Content = new Label { Text = "GPE", TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 17, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
    };

    // Título de apartado con el número al lado: "Pendientes (2)"
    public static (View Vista, Label Numero) Titulo(string texto)
    {
        var numero = new Label { Text = "0", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Suave };
        var pastilla = new Border
        {
            BackgroundColor = Linea,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Padding = new Thickness(9, 1),
            VerticalOptions = LayoutOptions.Center,
            Content = numero
        };
        var vista = new HorizontalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(2, 16, 0, 2),
            Children = { new Label { Text = texto, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Texto }, pastilla }
        };
        return (vista, numero);
    }

    // Igual que en el escritorio: "45 s", "3 min 20 s", "1 h 5 min"
    public static string Duracion(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min"
        : t.TotalMinutes >= 1 ? $"{t.Minutes} min {t.Seconds} s"
        : $"{Math.Max(0, t.Seconds)} s";
}
