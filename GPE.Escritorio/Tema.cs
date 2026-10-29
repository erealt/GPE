using GPE.Datos;
using GPE.Logica;

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

    // Fuentes compartidas: no se deben liberar con Dispose, las usan todas las pantallas.
    // Todas las pantallas sacan de aquí sus fuentes, para que los tamaños sean los mismos en todas.
    public static readonly Font Fuente = new("Segoe UI", 10);
    public static readonly Font FuenteNegrita = new("Segoe UI", 10, FontStyle.Bold);
    public static readonly Font FuentePequena = new("Segoe UI", 9);
    public static readonly Font FuentePequenaNegrita = new("Segoe UI", 9, FontStyle.Bold);
    public static readonly Font FuenteNota = new("Segoe UI", 8.5f);                       // notas bajo un campo, textos de la barra de progreso
    public static readonly Font FuenteEtiqueta = new("Segoe UI", 8.5f, FontStyle.Bold);   // cabeceras de tabla y etiquetas
    public static readonly Font FuenteOpcion = new("Segoe UI", 9.5f);                     // pestañas y botones de elegir una opción
    public static readonly Font FuenteOpcionElegida = new("Segoe UI", 9.5f, FontStyle.Bold);
    public static readonly Font FuenteMenu = new("Segoe UI", 10.5f);                      // opciones del menú lateral
    public static readonly Font FuenteMenuElegido = new("Segoe UI", 10.5f, FontStyle.Bold);
    public static readonly Font FuenteDestacada = new("Segoe UI", 11.5f, FontStyle.Bold); // títulos dentro de una lista (historial)
    public static readonly Font FuenteApartado = new("Segoe UI", 13);                     // título de cada tarjeta
    public static readonly Font FuenteAviso = new("Segoe UI", 13, FontStyle.Bold);        // titular de un aviso
    public static readonly Font FuenteTituloTarjeta = new("Segoe UI", 15, FontStyle.Bold); // tarjetas del inicio
    public static readonly Font FuenteLogo = new("Segoe UI", 16, FontStyle.Bold);
    public static readonly Font FuenteIcono = new("Segoe UI", 16);                         // botones con un símbolo (☰)
    public static readonly Font FuenteTituloVentana = new("Segoe UI", 20, FontStyle.Bold); // título de una ventana aparte
    public static readonly Font FuenteTitulo = new("Segoe UI", 22, FontStyle.Bold);       // título de cada módulo

    // Pestañas y botones de opción: la elegida en negrita
    public static Font FuenteDeOpcion(bool elegida) => elegida ? FuenteOpcionElegida : FuenteOpcion;

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

    // Etiquetas de la lista de incidencias y no conformidades
    public static (Color Fondo, Color Texto) ColorTipoRegistro(string tipo) => tipo switch
    {
        TipoRegistro.NoConformidad => (AcentoSuave, Acento),
        TipoRegistro.Incidencia => (Color.FromArgb(228, 238, 255), Color.FromArgb(30, 80, 170)),
        _ => (Color.FromArgb(222, 245, 245), Color.FromArgb(0, 110, 115))
    };

    public static (Color Fondo, Color Texto) ColorEstadoRegistro(string estado) => estado switch
    {
        EstadoRegistro.EnPlazo => (Color.FromArgb(225, 245, 232), Color.FromArgb(25, 120, 70)),
        EstadoRegistro.FueraPlazo => (FondoError, Error),
        EstadoRegistro.SinPlazo => (AvisoFondo, Aviso),
        _ => (Color.FromArgb(236, 238, 236), Color.FromArgb(90, 100, 95))
    };

    // Casillas de la rutina de calidad. FondoNoRevisa: casilla que la plantilla no revisa.
    public static readonly Color FondoNoRevisa = Color.FromArgb(226, 228, 224);
    // Líneas de la tabla (más marcadas que Linea: es una tabla grande que se recorre con la vista)
    public static readonly Color LineaTabla = Color.FromArgb(180, 186, 180);

    public static (Color Fondo, Color Texto) ColorEstadoCasilla(string estado) => estado switch
    {
        EstadoCasilla.Correcto => (Color.FromArgb(225, 245, 232), Color.FromArgb(25, 120, 70)),
        EstadoCasilla.NoCorrecto => (FondoError, Error),
        EstadoCasilla.Mantenimiento => (AvisoFondo, Aviso),
        _ => (Color.FromArgb(236, 238, 236), Color.FromArgb(90, 100, 95))
    };

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
            Font = FuenteNegrita,
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
            Font = FuenteNegrita,
            Cursor = Cursors.Hand
        };
        boton.EnabledChanged += (s, e) =>
            boton.FlatAppearance.BorderColor = boton.Enabled ? Borde : Linea;
        return boton;
    }
}
