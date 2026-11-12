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

// Número = Prefijo + siguiente de la serie ("" → 1, 2...; "AC-2026-" → AC-2026-1, AC-2026-2...)
public record SerieNumero(string TipoRegistro, string Prefijo);

// Fila de la hoja INCIDENCIAS (Id = 0: nuevo). Tipo es solo de NC; McPccBpm, Parametro y Operario, de incidencias.
// Version: la ROWVERSION leída, para no pisar lo que otro haya guardado.
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
    decimal? CosteEconomico,
    decimal? HorasManoObra,
    byte[]? Version = null);

// Quién tiene abierta una ficha (NC_BLOQUEO). MismoUsuario: el usuario actual, en otro GPE.
public record BloqueoNc(string Usuario, string Equipo, DateTime Desde, bool MismoUsuario);

// Columna NC_ACCION.clase: en qué tabla de la ficha va la acción
public static class ClaseAccion
{
    public const string Inmediata = "INMEDIATA";
    public const string Correctora = "CORRECTORA";
    // Comprobaciones antes del cierre: cada una con su fecha. En ellas Hecha no se usa.
    public const string Seguimiento = "SEGUIMIENTO";
}

// Columna NC_ACCION.prioridad (solo inmediatas y correctoras; null = sin prioridad)
public static class PrioridadAccion
{
    public const string Alta = "Alta";
    public const string Media = "Media";
    public const string Baja = "Baja";

    public static readonly string[] Todas = { Alta, Media, Baja };

    // Para ordenar: primero las altas, al final las que no tienen
    public static int Orden(string? prioridad) => prioridad switch
    {
        Alta => 0,
        Media => 1,
        Baja => 2,
        _ => 3
    };
}

// Acción inmediata, correctora o seguimiento (NC_ACCION). Id = 0 si es nueva.
public record AccionNc(
    int Id,
    string Clase,
    string Accion,
    DateTime? Fecha,
    string? Responsable,
    string? Observaciones,
    bool Hecha,
    string? Prioridad = null);

// Acción de calidad para avisar a su responsable por correo (al asignarla y en el resumen diario)
public record AccionPendienteAviso(
    int AccionId,
    int NoConformidadId,
    string Destinatario,
    string Responsable,
    string? NumeroNc,
    string TipoRegistro,
    string Problema,
    string Clase,
    string Accion,
    DateTime? Fecha,
    string? Prioridad,
    // Clasificación de la incidencia (vacía en las no conformidades)
    string? McPccBpm = null,
    string? Parametro = null,
    string? Operario = null);
