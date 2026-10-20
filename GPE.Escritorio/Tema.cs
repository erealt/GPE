namespace GPE.Escritorio;

// Colores y fuentes comunes a todas las pantallas
public static class Tema
{
    public static readonly Color Fondo = Color.FromArgb(245, 246, 244);
    public static readonly Color Acento = Color.FromArgb(89, 0, 246);
    public static readonly Color AcentoSuave = Color.FromArgb(240, 236, 255);
    public static readonly Color Texto = Color.FromArgb(20, 35, 30);
    public static readonly Color Suave = Color.FromArgb(110, 120, 115);
    public static readonly Color Borde = Color.FromArgb(200, 205, 200);
    public static readonly Color Linea = Color.FromArgb(230, 232, 228);
    public static readonly Color CabeceraTabla = Color.FromArgb(243, 244, 241);
    public static readonly Color Error = Color.FromArgb(190, 30, 45);
    public static readonly Color AvisoFondo = Color.FromArgb(255, 241, 214);
    public static readonly Color Aviso = Color.FromArgb(150, 90, 0);

    // Celdas de las tablas: columna que no se edita, valor distinto del esperado, valor con error
    public static readonly Color FondoSoloLectura = Color.FromArgb(247, 248, 246);
    public static readonly Color FondoDiferencia = AvisoFondo;
    public static readonly Color FondoError = Color.FromArgb(253, 228, 230);

    private static readonly (Color Fondo, Color Texto)[] ColoresFase =
    {
        (Color.FromArgb(228, 238, 255), Color.FromArgb(30, 80, 170)),   // Solicitud recibida
        (Color.FromArgb(255, 241, 214), Color.FromArgb(150, 90, 0)),    // Recogida solicitada
        (Color.FromArgb(240, 236, 255), Color.FromArgb(89, 0, 246)),    // Mercancía recibida
        (Color.FromArgb(222, 245, 245), Color.FromArgb(0, 110, 115)),   // Revisada almacén
        (Color.FromArgb(225, 245, 232), Color.FromArgb(25, 120, 70)),   // Abono realizado
        (Color.FromArgb(236, 238, 236), Color.FromArgb(90, 100, 95)),   // Cierre
    };

    public static (Color Fondo, Color Texto) ColorFase(int fase) =>
        ColoresFase[Math.Clamp(fase, 1, ColoresFase.Length) - 1];

    public static Button BotonPrincipal(string texto, int ancho)
    {
        var boton = new Button
        {
            Text = texto,
            Size = new Size(ancho, 40),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderSize = 0 },
            BackColor = Acento,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    
        boton.EnabledChanged += (s, e) =>
            boton.BackColor = boton.Enabled ? Acento : Color.FromArgb(205, 190, 250);
        return boton;
    }

    public static Button BotonSecundario(string texto, int ancho)
    {
        var boton = new Button
        {
            Text = texto,
            Size = new Size(ancho, 40),
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderColor = Borde },
            BackColor = Color.White,
            ForeColor = Texto,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        boton.EnabledChanged += (s, e) =>
            boton.FlatAppearance.BorderColor = boton.Enabled ? Borde : Linea;
        return boton;
    }
}
