namespace GPE.Datos;

// Desplegables del plan de análisis, copiados de la hoja LISTAS del Excel PLAN DE ANALISIS 2026.
// Lista fija: si calidad cambia la hoja, se cambia aquí.
public static class ListasAnalisis
{
    // ENVUELTA = envases (cartón, aluminio, bobinas)
    public static readonly string[] Tipos = { "PRODUCTO", "MMPP", "ENVUELTA" };

    public static readonly string[] Parametros =
    {
        "ALÉRGENO", "FISICO-QUIMICO", "METALES", "NUTRICIONAL", "GMO", "PCC", "MICROBIOLÓGICO"
    };

    // PARAMETRO - DETALLE (tabla TIPOS de la hoja LISTAS)
    public static readonly string[] Detalles =
    {
        "A - CACAHUETE", "A - GLUTEN", "A - HUEVO", "A - MOSTAZA", "A - SÉSAMO", "A - SULFITOS",
        "FQ - ° BRIX", "FQ - ACRILAMIDA", "FQ - AFLATOXINAS", "FQ - AW", "FQ - BENZOPIRENO",
        "FQ - CURVA DE SOLIDOS", "FQ - ETILENO", "FQ - HIDROCARBUROS", "FQ - HUMEDAD", "FQ - I.REFRACCIÓN",
        "FQ - MELAMINA", "FQ - OCRATOXINAS", "FQ - PERÓXIDOS", "FQ - PH", "FQ - PLAGUICIDAS",
        "FQ - TEOBROMINA", "FQ - TOXINAS", "GMO", "M - CADMIO", "M - METALES PESADOS",
        "AEROBIOS MESOFILOS", "ESCHERICHIA COLI", "SALMONELLA", "LISTERIA", "MOHOS Y LEVADURAS",
        "ENTEROBACTERIAS", "SHIGELLA", "S. AUREUS", "NUTRICIONAL", "PCC", "FQ - ACIDEZ", "BACILUS",
        "COLIFORMES", "ESTAFILOCOCO", "GRASA", "Á. grasos saturados", "A. grasos poliinsaturados",
        "A. grasos monoinsaturados", "CONSERVANTES", "ACIDO CITRICO", "SORBATO POTASICO", "ACIDO SORBICO",
        "CURCUMINA", "COCHINILLA"
    };
}
