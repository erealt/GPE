using GPE.Datos;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using QRCoder;
using System.Globalization;

namespace GPE.Logica;

// Dibuja el PDF de un DeCA (A4) con los mismos datos que el formulario y el QR arriba a la derecha.
// Si los palets no caben en una hoja, la tabla sigue en la siguiente.
public static class DecaPdf
{
    private static readonly CultureInfo Es = new("es-ES");

    private const double Margen = 40;
    private const double TamanoQr = 95;
    private const double AltoFilaPalet = 18;

    // Empresa que emite el DeCA, arriba a la izquierda
    private const string EmpresaNombre = "EUROCHOC, S.L.";
    private static readonly string[] EmpresaDatos =
    [
        "P.I. EL MUGRÓN. C/ ENCOFRADORES, PARC. 1",
        "02640 ALMANSA (ALBACETE)   Tfno: 967 311 563"
    ];

    private static readonly XColor ColorTexto = XColor.FromArgb(20, 35, 30);
    private static readonly XColor ColorSuave = XColor.FromArgb(110, 120, 115);
    private static readonly XColor ColorLinea = XColor.FromArgb(200, 205, 200);
    private static readonly XColor ColorCabecera = XColor.FromArgb(243, 244, 241);

    private static readonly XFont Titulo;
    private static readonly XFont Seccion;
    private static readonly XFont Etiqueta;
    private static readonly XFont Normal;
    private static readonly XFont Negrita;
    private static readonly XFont Pie;

    static DecaPdf()
    {
        // PDFsharp no busca las fuentes del sistema si no se le dice
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        Titulo = new XFont("Arial", 20, XFontStyleEx.Bold);
        Seccion = new XFont("Arial", 10, XFontStyleEx.Bold);
        Etiqueta = new XFont("Arial", 7.5, XFontStyleEx.Bold);
        Normal = new XFont("Arial", 9.5, XFontStyleEx.Regular);
        Negrita = new XFont("Arial", 9.5, XFontStyleEx.Bold);
        Pie = new XFont("Arial", 7.5, XFontStyleEx.Regular);
    }

    public static byte[] Crear(DecaGuardado deca, string urlQr)
    {
        using var documento = new PdfDocument();
        documento.Info.Title = deca.Numero;
        // La normativa pide la fecha y hora de creación y de modificación como metadatos del PDF.
        // Cada cambio genera un PDF nuevo, así que las dos son el momento de generarlo.
        var ahora = DateTime.Now;
        documento.Info.CreationDate = ahora;
        documento.Info.ModificationDate = ahora;

        var pagina = new Hoja(documento, deca.Numero);
        var g = pagina.G;
        double ancho = pagina.Ancho;
        var d = deca.Datos;

        // ---- Cabecera: empresa, número y fecha a la izquierda, QR a la derecha ----
        double y = Margen;
        g.DrawString(EmpresaNombre, Seccion, new XSolidBrush(ColorTexto), Margen, y + 8);
        for (int i = 0; i < EmpresaDatos.Length; i++)
            g.DrawString(EmpresaDatos[i], Pie, new XSolidBrush(ColorSuave), Margen, y + 20 + i * 10);

        double yTitulo = y + 52;
        g.DrawString("DOCUMENTO DE CONTROL DEL TRANSPORTE", Etiqueta, new XSolidBrush(ColorSuave),
            Margen, yTitulo + 8);
        g.DrawString(deca.Numero, Titulo, new XSolidBrush(ColorTexto), Margen, yTitulo + 32);
        g.DrawString("Fecha de carga: " + d.FechaCarga.ToString("dd/MM/yyyy", Es), Negrita,
            new XSolidBrush(ColorTexto), Margen, yTitulo + 52);

        double xQr = Margen + ancho - TamanoQr;
        DibujarQr(g, urlQr, xQr, y, TamanoQr);

        y = Math.Max(y + TamanoQr, yTitulo + 52) + 20;

        // ---- Remitente y destinatario, uno al lado del otro ----
        double medio = (ancho - 15) / 2;
        double altoRemitente = Parte(g, "REMITENTE", d.Remitente, Margen, y, medio);
        double altoDestinatario = Parte(g, "DESTINATARIO", d.Destinatario, Margen + medio + 15, y, medio);
        y += Math.Max(altoRemitente, altoDestinatario) + 15;

        // ---- Cargador contractual y transportista efectivo: las dos figuras que pide la normativa ----
        string tituloCargador = "CARGADOR CONTRACTUAL" + d.CargadorTipo switch
        {
            TipoCargador.Remitente => " (EL REMITENTE)",
            TipoCargador.Destinatario => " (EL DESTINATARIO)",
            _ => ""
        };
        double altoCargador = d.Cargador is ParteEnvio cargador
            ? Parte(g, tituloCargador, cargador, Margen, y, medio) : 0;
        double altoTransportista = Transportista(g, d, Margen + medio + 15, y, medio);
        y += Math.Max(altoCargador, altoTransportista) + 15;

        // ---- Transporte: vehículo, origen y destino ----
        y = TituloSeccion(g, "TRANSPORTE", Margen, y, ancho);
        double tercio = ancho / 3;
        Dato(g, "FECHA DE CARGA", d.FechaCarga.ToString("dd/MM/yyyy", Es), Margen, y, tercio);
        Dato(g, "MATRÍCULA DEL VEHÍCULO", d.Matricula, Margen + tercio, y, tercio);
        if (d.MatriculaRemolque != null)
            Dato(g, "MATRÍCULA DEL REMOLQUE", d.MatriculaRemolque, Margen + tercio * 2, y, tercio);
        y += 34;

        double altoOrigen = Texto(g, "ORIGEN", d.LugarCarga ?? Domicilio(d.Remitente), Margen, y, medio);
        double altoDestino = Texto(g, "DESTINO", d.LugarDescarga ?? Domicilio(d.Destinatario),
            Margen + medio + 15, y, medio);
        y += Math.Max(altoOrigen, altoDestino) + 15;

        // ---- Mercancía y palets ----
        y = TituloSeccion(g, "MERCANCÍA", Margen, y, ancho);
        y += Texto(g, "NATURALEZA DE LA MERCANCÍA", d.Mercancia, Margen, y, ancho) + 10;

        double[] columnas = { 0, ancho * 0.4, ancho * 0.7 };   // palet | peso | cajas
        y = CabeceraPalets(g, columnas, y, ancho);
        for (int i = 0; i < d.Palets.Count; i++)
        {
            if (y + AltoFilaPalet > pagina.Limite)
            {
                pagina = pagina.Siguiente();
                g = pagina.G;
                y = CabeceraPalets(g, columnas, Margen, ancho);
            }
            var p = d.Palets[i];
            FilaPalet(g, columnas, y, ancho, $"Palet {i + 1}",
                p.PesoKg.ToString("N2", Es), p.NumCajas.ToString("N0", Es), Normal);
            y += AltoFilaPalet;
        }

        if (y + AltoFilaPalet > pagina.Limite)
        {
            pagina = pagina.Siguiente();
            g = pagina.G;
            y = Margen;
        }
        string totalPalets = $"Total: {d.Palets.Count} {(d.Palets.Count == 1 ? "palet" : "palets")}";
        FilaPalet(g, columnas, y, ancho, totalPalets,
            d.Palets.Sum(p => p.PesoKg).ToString("N2", Es),
            d.Palets.Sum(p => p.NumCajas).ToString("N0", Es), Negrita);
        y += AltoFilaPalet + 15;

        // ---- Observaciones ----
        if (!string.IsNullOrWhiteSpace(d.Observaciones))
        {
            double alto = AltoTexto(g, d.Observaciones, ancho) + 30;
            if (y + alto > pagina.Limite)
            {
                pagina = pagina.Siguiente();
                g = pagina.G;
                y = Margen;
            }
            y = TituloSeccion(g, "OBSERVACIONES", Margen, y, ancho);
            Parrafo(g, d.Observaciones, Margen, y, ancho);
        }

        pagina.Cerrar();

        using var salida = new MemoryStream();
        documento.Save(salida, false);
        return salida.ToArray();
    }

    // ---------- Bloques ----------

    // Devuelve el alto ocupado
    private static double Parte(XGraphics g, string titulo, ParteEnvio p, double x, double y, double ancho)
    {
        double inicio = y;
        y = TituloSeccion(g, titulo, x, y, ancho);

        y += Parrafo(g, p.Nombre, x, y, ancho, Negrita) + 2;

        var lineas = new List<string>
        {
            "NIF: " + p.Nif,
            p.Direccion,
            $"{p.CodigoPostal} {p.Poblacion}" + (string.IsNullOrEmpty(p.Provincia) ? "" : $" ({p.Provincia})"),
            p.Pais
        };
        if (!string.IsNullOrEmpty(p.Telefono)) lineas.Add("Tel.: " + p.Telefono);

        foreach (string linea in lineas)
        {
            y += Parrafo(g, linea, x, y, ancho);
        }
        return y - inicio;
    }

    // Devuelve el alto ocupado
    private static double Transportista(XGraphics g, DatosDeca d, double x, double y, double ancho)
    {
        double inicio = y;
        y = TituloSeccion(g, "TRANSPORTISTA EFECTIVO", x, y, ancho);
        y += Parrafo(g, d.TransportistaNombre, x, y, ancho, Negrita) + 2;
        y += Parrafo(g, "NIF: " + d.TransportistaNif, x, y, ancho);
        return y - inicio;
    }

    // Dirección completa en una línea, para el origen y el destino
    private static string Domicilio(ParteEnvio p) =>
        $"{p.Direccion}, {p.CodigoPostal} {p.Poblacion}"
        + (string.IsNullOrEmpty(p.Provincia) ? "" : $" ({p.Provincia})") + $", {p.Pais}";

    private static double TituloSeccion(XGraphics g, string titulo, double x, double y, double ancho)
    {
        g.DrawString(titulo, Seccion, new XSolidBrush(ColorTexto), x, y + 10);
        g.DrawLine(new XPen(ColorLinea, 0.8), x, y + 15, x + ancho, y + 15);
        return y + 22;
    }

    // Etiqueta pequeña y el valor debajo, en una línea
    private static void Dato(XGraphics g, string etiqueta, string valor, double x, double y, double ancho)
    {
        g.DrawString(etiqueta, Etiqueta, new XSolidBrush(ColorSuave), x, y + 8);
        g.DrawString(Recortar(g, valor, ancho - 8), Normal, new XSolidBrush(ColorTexto), x, y + 22);
    }

    // Etiqueta pequeña y el valor debajo, en varias líneas si hace falta. Devuelve el alto.
    private static double Texto(XGraphics g, string etiqueta, string valor, double x, double y, double ancho)
    {
        g.DrawString(etiqueta, Etiqueta, new XSolidBrush(ColorSuave), x, y + 8);
        return 12 + Parrafo(g, valor, x, y + 12, ancho);
    }

    private static double CabeceraPalets(XGraphics g, double[] columnas, double y, double ancho)
    {
        g.DrawRectangle(new XSolidBrush(ColorCabecera), Margen, y, ancho, AltoFilaPalet);
        FilaPalet(g, columnas, y, ancho, "PALET", "PESO (KG)", "Nº DE CAJAS", Etiqueta, linea: false);
        return y + AltoFilaPalet;
    }

    private static void FilaPalet(XGraphics g, double[] columnas, double y, double ancho,
        string palet, string peso, string cajas, XFont fuente, bool linea = true)
    {
        var pincel = new XSolidBrush(fuente == Etiqueta ? ColorSuave : ColorTexto);
        double xPeso = Margen + columnas[2] - 15;   // peso y cajas alineados a la derecha
        double xCajas = Margen + ancho - 10;

        g.DrawString(palet, fuente, pincel, new XRect(Margen + 8, y, columnas[1], AltoFilaPalet), XStringFormats.CenterLeft);
        g.DrawString(peso, fuente, pincel, new XRect(xPeso - 150, y, 150, AltoFilaPalet), XStringFormats.CenterRight);
        g.DrawString(cajas, fuente, pincel, new XRect(xCajas - 150, y, 150, AltoFilaPalet), XStringFormats.CenterRight);
        if (linea)
            g.DrawLine(new XPen(ColorLinea, 0.5), Margen, y + AltoFilaPalet, Margen + ancho, y + AltoFilaPalet);
    }

    // El QR se dibuja con cuadrados para que salga nítido a cualquier tamaño
    private static void DibujarQr(XGraphics g, string url, double x, double y, double lado)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var modulos = datos.ModuleMatrix;   // ya incluye el margen blanco alrededor
        double celda = lado / modulos.Count;

        for (int fila = 0; fila < modulos.Count; fila++)
            for (int col = 0; col < modulos[fila].Count; col++)
                if (modulos[fila][col])
                    g.DrawRectangle(XBrushes.Black, x + col * celda, y + fila * celda, celda + 0.1, celda + 0.1);
    }

    // ---------- Texto ----------

    // Escribe el texto partiendo las líneas largas y devuelve el alto ocupado
    private static double Parrafo(XGraphics g, string texto, double x, double y, double ancho,
        XFont? fuente = null)
    {
        fuente ??= Normal;
        double alto = AltoTexto(g, texto, ancho, fuente);
        var formateador = new XTextFormatter(g);
        formateador.DrawString(texto, fuente, new XSolidBrush(ColorTexto),
            new XRect(x, y, ancho, alto), XStringFormats.TopLeft);
        return alto;
    }

    private static double AltoTexto(XGraphics g, string texto, double ancho, XFont? fuente = null)
    {
        fuente ??= Normal;
        double altoLinea = fuente.GetHeight();
        int lineas = 0;
        foreach (string parrafo in texto.Replace("\r", "").Split('\n'))
        {
            lineas++;
            double actual = 0;
            foreach (string palabra in parrafo.Split(' '))
            {
                double w = g.MeasureString(palabra + " ", fuente).Width;
                if (actual > 0 && actual + w > ancho) { lineas++; actual = 0; }
                actual += w;
            }
        }
        return lineas * altoLinea + 2;
    }

    private static string Recortar(XGraphics g, string texto, double ancho)
    {
        if (g.MeasureString(texto, Normal).Width <= ancho) return texto;
        while (texto.Length > 1 && g.MeasureString(texto + "…", Normal).Width > ancho)
            texto = texto[..^1];
        return texto + "…";
    }

    // Una hoja A4 con el pie (número del DeCA, fecha de generación y nº de página)
    private class Hoja
    {
        private readonly PdfDocument documento;
        private readonly string numero;
        private readonly int numeroPagina;

        public XGraphics G { get; }
        public double Ancho { get; }
        public double Limite { get; }   // hasta dónde se puede escribir sin pisar el pie

        public Hoja(PdfDocument documento, string numero, int numeroPagina = 1)
        {
            this.documento = documento;
            this.numero = numero;
            this.numeroPagina = numeroPagina;

            var pagina = documento.AddPage();
            pagina.Size = PdfSharp.PageSize.A4;
            G = XGraphics.FromPdfPage(pagina);
            Ancho = pagina.Width.Point - 2 * Margen;
            Limite = pagina.Height.Point - Margen - 20;
        }

        public Hoja Siguiente()
        {
            Cerrar();
            return new Hoja(documento, numero, numeroPagina + 1);
        }

        public void Cerrar()
        {
            double yPie = Limite + 20;
            var pincel = new XSolidBrush(ColorSuave);
            G.DrawLine(new XPen(ColorLinea, 0.5), Margen, yPie - 8, Margen + Ancho, yPie - 8);
            G.DrawString($"{numero} · Generado el {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Es)}",
                Pie, pincel, Margen, yPie + 4);
            G.DrawString($"Página {numeroPagina}", Pie, pincel,
                new XRect(Margen, yPie - 4, Ancho, 12), XStringFormats.CenterRight);
            G.Dispose();
        }
    }
}
