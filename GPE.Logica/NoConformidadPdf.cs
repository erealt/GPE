using GPE.Datos;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using System.Globalization;

namespace GPE.Logica;

// Informe en PDF (A4) de una no conformidad, incidencia o acción de mejora, para auditorías.
// Sigue las fases de la ficha y solo pone los datos que están rellenados.
public static class NoConformidadPdf
{
    private static readonly CultureInfo Es = new("es-ES");

    private const double Margen = 40;
    private const double AltoCabeceraTabla = 18;

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

    static NoConformidadPdf()
    {
        // PDFsharp no busca las fuentes del sistema si no se le dice
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;

        Titulo = new XFont("Arial", 18, XFontStyleEx.Bold);
        Seccion = new XFont("Arial", 10, XFontStyleEx.Bold);
        Etiqueta = new XFont("Arial", 7.5, XFontStyleEx.Bold);
        Normal = new XFont("Arial", 9, XFontStyleEx.Regular);
        Negrita = new XFont("Arial", 9, XFontStyleEx.Bold);
        Pie = new XFont("Arial", 7.5, XFontStyleEx.Regular);
    }

    // "No conformidad 12/2026" o "Incidencia del 03 nov 2026"
    public static string TituloRegistro(NoConformidad nc) =>
        nc.Numero == null
            ? $"{TipoRegistro.Nombre(nc.TipoRegistro)} del {nc.FechaDetectado.ToString("dd/MM/yyyy", Es)}"
            : $"{TipoRegistro.Nombre(nc.TipoRegistro)} {nc.Numero}";

    // origen: el origen con su descripción ("I · Interna"), si se conoce
    public static byte[] Crear(NoConformidad nc, IReadOnlyList<AccionNc> acciones, string? origen)
    {
        string titulo = TituloRegistro(nc);
        using var documento = new PdfDocument();
        documento.Info.Title = titulo;
        documento.Info.Subject = "Informe de calidad (MT-RG09)";

        var hoja = new Hoja(documento, titulo);

        // ---- Cabecera ----
        hoja.G.DrawString("CALIDAD · INFORME DE REGISTRO (MT-RG09)", Etiqueta, new XSolidBrush(ColorSuave), Margen, hoja.Y + 8);
        hoja.G.DrawString(titulo, Titulo, new XSolidBrush(ColorTexto), Margen, hoja.Y + 30);
        string estado = NoConformidadServicio.Estado(nc);
        if (nc.FechaFin is DateTime fin) estado += " el " + fin.ToString("dd/MM/yyyy", Es);
        hoja.G.DrawString("Estado: " + estado, Negrita, new XSolidBrush(ColorTexto), Margen, hoja.Y + 48);
        hoja.Y += 66;

        // ---- 1. Registro ----
        hoja.TituloSeccion("1. REGISTRO");
        hoja.Datos(new (string, string?)[]
        {
            ("TIPO DE REGISTRO", TipoRegistro.Nombre(nc.TipoRegistro)),
            ("Nº", nc.Numero),
            ("FECHA DETECTADO", nc.FechaDetectado.ToString("dd/MM/yyyy", Es)),
            ("ORIGEN", origen ?? nc.Origen),
            ("SECCIÓN", nc.Seccion),
            ("TIPO DE NO CONFORMIDAD", nc.Tipo),
            ("CLIENTE", nc.Cliente),
            ("PROVEEDOR", nc.Proveedor),
            ("RESPONSABLE", nc.Responsable),
            ("FECHA PREVISTA", nc.FechaPrevista?.ToString("dd/MM/yyyy", Es))
        });
        hoja.Texto("PROBLEMA", nc.Problema);

        // ---- 2. Acción inmediata ----
        hoja.TituloSeccion("2. ACCIÓN INMEDIATA");
        hoja.TablaAcciones(acciones.Where(a => a.Clase == ClaseAccion.Inmediata).ToList(), "No hay acciones inmediatas.");

        // ---- 3. Análisis de la causa ----
        hoja.TituloSeccion("3. ANÁLISIS DE LA CAUSA");
        hoja.Texto("DESCRIPCIÓN DE LA CAUSA", nc.DescripcionCausa);
        hoja.Texto("ANÁLISIS DE LA CAUSA RAÍZ", nc.AnalisisCausaRaiz);
        hoja.Datos(new (string, string?)[]
        {
            ("CAPÍTULO DE LA NORMA", nc.CapituloNorma),
            ("MC / PCC / BPM", nc.McPccBpm),
            ("PARÁMETRO", nc.Parametro),
            ("OPERARIO CAUSANTE", nc.Operario)
        });
        if (nc.DescripcionCausa == null && nc.AnalisisCausaRaiz == null && nc.CapituloNorma == null
            && nc.McPccBpm == null && nc.Parametro == null && nc.Operario == null)
            hoja.Vacio("Sin análisis de la causa.");

        // ---- 4. Acción correctora ----
        hoja.TituloSeccion("4. ACCIÓN CORRECTORA");
        hoja.TablaAcciones(acciones.Where(a => a.Clase == ClaseAccion.Correctora).ToList(), "No hay acciones correctoras.");
        hoja.Datos(new (string, string?)[]
        {
            ("COSTE ECONÓMICO", nc.CosteEconomico?.ToString("N2", Es) + (nc.CosteEconomico == null ? "" : " €")),
            ("HORAS DE MANO DE OBRA", nc.HorasManoObra?.ToString("0.##", Es))
        });

        // ---- 5. Verificación y cierre ----
        hoja.TituloSeccion("5. VERIFICACIÓN Y CIERRE");
        // La fecha fin y el estado ya van arriba, junto al título
        hoja.TablaSeguimientos(acciones.Where(a => a.Clase == ClaseAccion.Seguimiento).ToList());

        hoja.Cerrar();

        using var salida = new MemoryStream();
        documento.Save(salida, false);
        return salida.ToArray();
    }

    // Las hojas A4 una tras otra: lleva por dónde se va escribiendo y pasa de hoja cuando no cabe
    private class Hoja
    {
        private readonly PdfDocument documento;
        private readonly string titulo;
        private int numeroPagina;
        private double limite;

        public XGraphics G { get; private set; } = null!;
        public double Ancho { get; private set; }
        public double Y;

        public Hoja(PdfDocument documento, string titulo)
        {
            this.documento = documento;
            this.titulo = titulo;
            Nueva();
        }

        private void Nueva()
        {
            numeroPagina++;
            var pagina = documento.AddPage();
            pagina.Size = PdfSharp.PageSize.A4;
            G = XGraphics.FromPdfPage(pagina);
            Ancho = pagina.Width.Point - 2 * Margen;
            limite = pagina.Height.Point - Margen - 20;
            Y = Margen;
        }

        // Si no cabe "alto" en lo que queda de hoja, sigue en la siguiente
        public void Asegurar(double alto)
        {
            if (Y + alto <= limite) return;
            Cerrar();
            Nueva();
        }

        // El pie: título, fecha de generación y nº de página
        public void Cerrar()
        {
            double yPie = limite + 20;
            var pincel = new XSolidBrush(ColorSuave);
            G.DrawLine(new XPen(ColorLinea, 0.5), Margen, yPie - 8, Margen + Ancho, yPie - 8);
            G.DrawString($"{titulo} · Generado el {DateTime.Now.ToString("dd/MM/yyyy HH:mm", Es)}",
                Pie, pincel, Margen, yPie + 4);
            G.DrawString($"Página {numeroPagina}", Pie, pincel,
                new XRect(Margen, yPie - 4, Ancho, 12), XStringFormats.CenterRight);
            G.Dispose();
        }

        // ---------- Bloques ----------

        public void TituloSeccion(string texto)
        {
            Asegurar(60);   // que el título no se quede solo al final de una hoja
            Y += 6;
            G.DrawString(texto, Seccion, new XSolidBrush(ColorTexto), Margen, Y + 10);
            G.DrawLine(new XPen(ColorLinea, 0.8), Margen, Y + 15, Margen + Ancho, Y + 15);
            Y += 24;
        }

        // Los datos rellenados, de tres en tres por fila (etiqueta pequeña y valor debajo)
        public void Datos(IEnumerable<(string Etiqueta, string? Valor)> datos)
        {
            var rellenos = datos.Where(d => !string.IsNullOrWhiteSpace(d.Valor)).ToList();
            double tercio = Ancho / 3;
            for (int i = 0; i < rellenos.Count; i += 3)
            {
                var fila = rellenos.Skip(i).Take(3).ToList();
                double alto = fila.Max(d => 12 + AltoTexto(G, d.Valor!, tercio - 10, Normal)) + 8;
                Asegurar(alto);
                for (int j = 0; j < fila.Count; j++)
                {
                    double x = Margen + j * tercio;
                    G.DrawString(fila[j].Etiqueta, Etiqueta, new XSolidBrush(ColorSuave), x, Y + 8);
                    Parrafo(G, fila[j].Valor!, x, Y + 12, tercio - 10, Normal);
                }
                Y += alto;
            }
        }

        // Etiqueta y texto largo a todo el ancho; se parte en varias hojas si hace falta
        public void Texto(string etiqueta, string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return;
            Asegurar(12 + Normal.GetHeight() * 2);
            G.DrawString(etiqueta, Etiqueta, new XSolidBrush(ColorSuave), Margen, Y + 8);
            Y += 12;
            foreach (string linea in Lineas(G, valor.Trim(), Ancho, Normal))
            {
                Asegurar(Normal.GetHeight());
                G.DrawString(linea, Normal, new XSolidBrush(ColorTexto), new XRect(Margen, Y, Ancho, Normal.GetHeight()), XStringFormats.TopLeft);
                Y += Normal.GetHeight();
            }
            Y += 8;
        }

        public void Vacio(string texto)
        {
            Asegurar(18);
            G.DrawString(texto, Normal, new XSolidBrush(ColorSuave), Margen, Y + 10);
            Y += 20;
        }

        // Inmediatas y correctoras: acción (con sus observaciones), responsable, fecha, prioridad y si está hecha
        public void TablaAcciones(List<AccionNc> acciones, string sinAcciones)
        {
            if (acciones.Count == 0) { Vacio(sinAcciones); return; }
            double[] anchos = { Ancho * 0.46, Ancho * 0.19, Ancho * 0.12, Ancho * 0.11, Ancho * 0.12 };
            string[] cabeceras = { "ACCIÓN", "RESPONSABLE", "FECHA", "PRIORIDAD", "HECHA" };
            Tabla(anchos, cabeceras, acciones.Select(a => new[]
            {
                a.Accion + (string.IsNullOrWhiteSpace(a.Observaciones) ? "" : "\nObservaciones: " + a.Observaciones),
                a.Responsable ?? "—",
                a.Fecha?.ToString("dd/MM/yyyy", Es) ?? "—",
                a.Prioridad ?? "—",
                a.Hecha ? "Sí" : "No"
            }).ToList());
        }

        // Seguimientos: comprobación, fecha y observaciones
        public void TablaSeguimientos(List<AccionNc> seguimientos)
        {
            if (seguimientos.Count == 0) { Vacio("No hay seguimientos."); return; }
            double[] anchos = { Ancho * 0.5, Ancho * 0.14, Ancho * 0.36 };
            string[] cabeceras = { "SEGUIMIENTO", "FECHA", "OBSERVACIONES" };
            Tabla(anchos, cabeceras, seguimientos.Select(a => new[]
            {
                a.Accion,
                a.Fecha?.ToString("dd/MM/yyyy", Es) ?? "—",
                a.Observaciones ?? ""
            }).ToList());
        }

        private void Tabla(double[] anchos, string[] cabeceras, List<string[]> filas)
        {
            const double relleno = 5;
            Asegurar(AltoCabeceraTabla + 20);
            Cabecera(anchos, cabeceras);
            foreach (var fila in filas)
            {
                double alto = fila.Select((t, i) => AltoTexto(G, t, anchos[i] - 2 * relleno, Normal)).Max() + 2 * relleno;
                if (Y + alto > limite)
                {
                    Asegurar(alto);
                    Cabecera(anchos, cabeceras);   // la cabecera se repite en la hoja nueva
                }
                double x = Margen;
                for (int i = 0; i < fila.Length; i++)
                {
                    Parrafo(G, fila[i], x + relleno, Y + relleno, anchos[i] - 2 * relleno, Normal);
                    x += anchos[i];
                }
                Y += alto;
                G.DrawLine(new XPen(ColorLinea, 0.5), Margen, Y, Margen + Ancho, Y);
            }
            Y += 10;
        }

        private void Cabecera(double[] anchos, string[] cabeceras)
        {
            G.DrawRectangle(new XSolidBrush(ColorCabecera), Margen, Y, Ancho, AltoCabeceraTabla);
            double x = Margen;
            for (int i = 0; i < cabeceras.Length; i++)
            {
                G.DrawString(cabeceras[i], Etiqueta, new XSolidBrush(ColorSuave),
                    new XRect(x + 5, Y, anchos[i] - 10, AltoCabeceraTabla), XStringFormats.CenterLeft);
                x += anchos[i];
            }
            Y += AltoCabeceraTabla;
        }
    }

    // ---------- Texto ----------

    // Escribe el texto partiendo las líneas largas
    private static void Parrafo(XGraphics g, string texto, double x, double y, double ancho, XFont fuente)
    {
        var lineas = Lineas(g, texto, ancho, fuente);
        var pincel = new XSolidBrush(ColorTexto);
        for (int i = 0; i < lineas.Count; i++)
            g.DrawString(lineas[i], fuente, pincel, new XRect(x, y + i * fuente.GetHeight(), ancho, fuente.GetHeight()), XStringFormats.TopLeft);
    }

    private static double AltoTexto(XGraphics g, string texto, double ancho, XFont fuente) =>
        Lineas(g, texto, ancho, fuente).Count * fuente.GetHeight();

    // Las líneas del texto tal como caben en el ancho (las palabras muy largas se cortan)
    private static List<string> Lineas(XGraphics g, string texto, double ancho, XFont fuente)
    {
        var lineas = new List<string>();
        foreach (string parrafo in texto.Replace("\r", "").Split('\n'))
        {
            string actual = "";
            foreach (string palabra in parrafo.Split(' '))
            {
                string prueba = actual.Length == 0 ? palabra : actual + " " + palabra;
                if (g.MeasureString(prueba, fuente).Width <= ancho) { actual = prueba; continue; }
                if (actual.Length > 0) lineas.Add(actual);
                actual = palabra;
                while (actual.Length > 1 && g.MeasureString(actual, fuente).Width > ancho)
                {
                    int corte = actual.Length - 1;
                    while (corte > 1 && g.MeasureString(actual[..corte], fuente).Width > ancho) corte--;
                    lineas.Add(actual[..corte]);
                    actual = actual[corte..];
                }
            }
            lineas.Add(actual);
        }
        return lineas;
    }
}
