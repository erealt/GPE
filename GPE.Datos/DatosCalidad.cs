namespace GPE.Datos;

// Columna "NC/I" del Excel: qué clase de registro es
public static class TipoRegistro
{
    public const string NoConformidad = "NC";
    public const string Incidencia = "INCIDENCIA";
    public const string Mejora = "MEJORA";

    public static readonly string[] Todos = { NoConformidad, Incidencia, Mejora };

    public static string Nombre(string tipo) => tipo switch
    {
        NoConformidad => "No conformidad",
        Incidencia => "Incidencia",
        Mejora => "Acción de mejora",
        _ => tipo
    };
}

// Desplegables de la hoja LISTAS (columna CALIDAD_LISTA.lista)
public static class ListaCalidad
{
    public const string Origen = "ORIGEN";
    public const string Seccion = "SECCION";
    public const string Tipo = "TIPO";
    public const string Persona = "PERSONA";
    public const string McPccBpm = "MC_PCC_BPM";
    public const string Parametro = "PARAMETRO";
    public const string Operario = "OPERARIO";
    public const string CapituloNorma = "CAPITULO_NORMA";
}

// Una opción de un desplegable. Solo los orígenes I, R, A, P llevan descripción.
public record OpcionLista(string Valor, string? Descripcion)
{
    public override string ToString() => Descripcion == null ? Valor : $"{Valor} · {Descripcion}";
}

// Serie de numeración: el número es Prefijo + el siguiente de la serie
// (Prefijo "" para 1, 2, 3...; "AC-2026-" para AC-2026-1, AC-2026-2...)
public record SerieNumero(string TipoRegistro, string Prefijo);

// Una fila de la hoja INCIDENCIAS. Id = 0 en un registro nuevo.
// Tipo solo se usa en las NC; McPccBpm, Parametro y Operario solo en las incidencias.
public record NoConformidad(
    int Id,
    string TipoRegistro,
    string? Numero,
    DateTime FechaDetectado,
    string? Origen,
    string? Seccion,
    string? Tipo,
    string Problema,
    string? DescripcionCausa,
    string? AccionInmediata,
    string? Responsable,
    DateTime? FechaPrevista,
    DateTime? FechaFin,
    string? Cliente,
    string? Proveedor,
    string? McPccBpm,
    string? Parametro,
    string? Operario,
    string? CapituloNorma,
    string? AnalisisCausaRaiz,
    string? AccionCorrectora,
    string? ResponsableAccionCorrectora,
    decimal? CosteEconomico,
    decimal? HorasManoObra,
    string? Seguimiento);
