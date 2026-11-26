namespace GPE.Datos;

// Clasificación de las incidencias (26/11/2026, nombres que dio calidad): primero el parámetro
// (De almacén, De obrador...) y, dentro de él, una de sus opciones (MC, PCC, buenas prácticas...).
// El parámetro se guarda en NO_CONFORMIDAD.parametro y la opción en mc_pcc_bpm.
// Las incidencias de antes tienen los valores de la hoja LISTAS (CALIDAD_LISTA): se siguen viendo tal cual.
// Lista fija: si calidad la cambia, se cambia aquí.
public static class ParametrosIncidencia
{
    private const string Mc00 = "MC00 - Cuerpos extraños en mmpp y m.auxiliares";
    private const string Mc03 = "MC03 - Formulación/trazabilidad";
    private const string Mc08 = "MC08 - Reprocesados";
    private const string Mc09 = "MC09 - Etiquetado y peso neto";
    private const string Mc10 = "MC10 - Alérgeno en superficies de contacto";
    private const string Mc11 = "MC11 - Alérgeno";
    private const string Mc12 = "MC12 - Integridad de útiles y recipientes";
    private const string Pcc2 = "PCC 2 - Metales";
    private const string Autocontrol = "Autocontrol";
    private const string Bpm = "Buenas prácticas de manipulación";
    private const string Embalaje = "Embalaje/Paletizado";
    private const string Orden = "Orden en el puesto de trabajo";
    private const string Organoleptico = "Organoléptico (color, sabor, aspecto, textura, etc.)";
    private const string ParametroEquipo = "Parámetro de equipo (Tª, velocidad, vibración, etc.)";
    private const string Partes = "Partes/Especificaciones de fabricación";

    private static readonly (string Parametro, string[] Opciones)[] lista =
    {
        ("De almacén", new[]
        {
            "MC01 - Estado cisterna", "MC02 - Tª almacén frío", Mc03, Mc08, Mc09, Mc11, Mc12,
            Bpm, Embalaje, "FIFO", Partes
        }),
        ("De obrador", new[]
        {
            Mc00, Mc03, "MC04 - Filtros ATC SOLICH", "MC05 - Imán molino NEZSCH",
            "MC06 - Filtros AWEMA, atemperadoras, depósitos", "MC07 - Cuchillas cortadoras",
            "MC08 - Mermas/Reprocesados", Mc10, Mc11, Mc12, "MC13 - Dosificación lecitina de soja",
            "PCC 1 - Aditivos", Pcc2, Autocontrol, Bpm, Orden, Organoleptico, ParametroEquipo, Partes
        }),
        ("Envoltura y envasado", new[]
        {
            Mc00, Mc03, Mc08, Mc09, Mc10, Mc11, Mc12, Pcc2, Autocontrol, Bpm, Embalaje,
            "Integridad envase", Orden, Organoleptico, ParametroEquipo, Partes
        }),
        ("Limpieza", new[] { Mc10, Mc11, Mc12, Bpm }),
        ("Mantenimiento", new[]
        {
            Mc12, Bpm, "Fallos en el equipo", "Falta de mantenimiento", "Utensilios fuera de su sitio"
        }),
        ("Higiene", new[] { "Comida/Bebida", "Guantes", "Manos/Uñas", "Maquillaje/Joyas/Pestañas", "Ropa" }),
        ("Otros", new[] { "Insectocaptores/Trampas", "Botiquín" })
    };

    public static readonly string[] Todos = lista.Select(p => p.Parametro).ToArray();

    // Las opciones de un parámetro (vacío si no hay parámetro o no es de la lista)
    public static IReadOnlyList<string> Opciones(string? parametro) =>
        lista.FirstOrDefault(p => p.Parametro == parametro).Opciones ?? Array.Empty<string>();
}
