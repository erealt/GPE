namespace GPE.Datos;

// Grupos de la hoja CAUSA RAIZ del Excel MT-RG09 (en el Excel son imágenes en inglés:
// People, Equipment, Economics, Processes, Food). La causa raíz de un registro es uno de ellos
// y, desde el 25/11/2026, uno de sus subgrupos (las frases de la hoja, en el mismo orden).
// El 26/11/2026 calidad dio los nombres exactos: "Personal" pasa a "Personas" y cambian los
// subgrupos de Equipos y Procesos. Lista fija: si calidad la cambia, se cambia aquí.
public static class CausasRaiz
{
    public static readonly string[] Todas = { "Personas", "Equipos", "Economía", "Procesos", "Alimentos" };

    private static readonly Dictionary<string, string[]> subgrupos = new()
    {
        ["Personas"] = new[]
        {
            "Falta de una cultura de inocuidad alimentaria",
            "Falta de supervisión gerencial",
            "Barrera lingüística",
            "Alta rotación de personal",
            "Falta de capacitación de los empleados en los procesos específicos",
            "Personal insuficiente"
        },
        ["Equipos"] = new[]
        {
            "Fallo del equipo",
            "Equipo insuficiente",
            "Uso inadecuado de los equipos",
            "Falta de mantenimiento preventivo de los equipos",
            "Reparaciones temporales no aptas",
            "Diseño deficiente de instalaciones",
            "Falta de limpieza"
        },
        ["Economía"] = new[]
        {
            "Falta de suministros necesarios para operar",
            "Otros incentivos económicos para adoptar buenas prácticas"
        },
        ["Procesos"] = new[]
        {
            "Procedimiento inadecuado para mitigar el peligro",
            "Falta de procedimiento para mitigar el peligro"
        },
        ["Alimentos"] = new[]
        {
            "Las mmpp o auxiliares no han sido correctamente tratados comprometiendo la seguridad alimentaria"
        }
    };

    // Los subgrupos de un grupo (vacío si no hay grupo o no es de la lista)
    public static IReadOnlyList<string> Subgrupos(string? grupo) =>
        grupo != null && subgrupos.TryGetValue(grupo, out var lista) ? lista : Array.Empty<string>();
}
