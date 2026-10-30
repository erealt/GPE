using GPE.Datos;

namespace GPE.Logica;

// Estado de un registro. No se guarda: se calcula como la columna "Estado" del Excel.
public static class EstadoRegistro
{
    public const string EnPlazo = "En plazo";
    public const string FueraPlazo = "Fuera de plazo";
    public const string SinPlazo = "Sin plazo";
    public const string Cerrada = "Cerrada";

    public static readonly string[] Todos = { EnPlazo, FueraPlazo, SinPlazo, Cerrada };
}

// Fases de un registro: no se guardan, salen de lo rellenado y se completan en cualquier orden.
public static class FaseCalidad
{
    public const string Registro = "Registro";
    public const string AccionInmediata = "Acción inmediata";
    public const string Analisis = "Análisis";
    public const string Correccion = "Corrección";
    public const string Acciones = "Acciones";   // las de una acción de mejora
    public const string Cierre = "Cierre";
}

// Opcional: la fase se puede rellenar, pero este tipo de registro no la necesita
public record EstadoFase(string Fase, bool Hecha, bool Opcional = false);

public class NoConformidadServicio
{
    private readonly NoConformidadRepositorio repositorio = new();

    // Consultas

    public List<NoConformidad> ObtenerRegistros() => repositorio.ObtenerRegistros();

    public NoConformidad? ObtenerRegistro(int id) => repositorio.ObtenerRegistro(id);

    public List<AccionNc> ObtenerAcciones(int id) => repositorio.ObtenerAcciones(id);

    public Dictionary<string, List<OpcionLista>> ObtenerListas() => repositorio.ObtenerListas();

    public List<string> ObtenerClientes() => repositorio.ObtenerClientes();

    public List<string> ObtenerProveedores() => repositorio.ObtenerProveedores();

    // Numeración

    // Secciones de las NC de auditoría, que llevan su propia numeración
    private const string AuditoriaExterna = "A. EXTERNA";
    private const string AuditoriaInterna = "A. INTERNA";
    private const string AuditoriaCliente = "A. CLIENTE";

    // Origen A = auditoría: la sección solo puede ser una de las de auditoría
    public static readonly string[] SeccionesAuditoria = { AuditoriaInterna, AuditoriaExterna, AuditoriaCliente };

    public static bool SoloSeccionesAuditoria(string? origen) => origen == "A";

    // Serie en la que se numera un registro, como en el Excel:
    //   incidencias          1, 2, 3...
    //   NC                   558, 559... (la serie sigue, no empieza cada año)
    //   NC auditoría externa AC-2026-1, AC-2026-2... (empieza cada año)
    //   NC auditoría interna AI-20/04/26-1, AI-20/04/26-2... (empieza en cada auditoría)
    //   acciones de mejora   sin número
    public static SerieNumero? Serie(NoConformidad nc)
    {
        DateTime fecha = nc.FechaDetectado;
        return nc.TipoRegistro switch
        {
            TipoRegistro.Incidencia => new SerieNumero(TipoRegistro.Incidencia, ""),
            TipoRegistro.NoConformidad => nc.Seccion switch
            {
                AuditoriaExterna => new SerieNumero(TipoRegistro.NoConformidad, $"AC-{fecha:yyyy}-"),
                AuditoriaInterna => new SerieNumero(TipoRegistro.NoConformidad, $"AI-{fecha:dd'/'MM'/'yy}-"),
                _ => new SerieNumero(TipoRegistro.NoConformidad, "")
            },
            _ => null
        };
    }

    // Conserva su número mientras siga en la misma serie; si no tiene o cambia de serie, toca uno nuevo.
    private static bool Renumerar(NoConformidad? anterior, SerieNumero? serie) =>
        serie != null && (anterior?.Numero == null || Serie(anterior) != serie);

    // Número que se enseña antes de guardar (null: no lleva). El definitivo se calcula al guardar.
    public string? NumeroPrevisto(NoConformidad nc, NoConformidad? anterior)
    {
        var serie = Serie(Normalizar(nc));
        if (serie == null) return null;
        return Renumerar(anterior, serie) ? repositorio.SiguienteNumero(serie) : anterior!.Numero;
    }

    // Cerrada si tiene fecha fin; si no, según la prevista (sin ella es "sin plazo", no "fuera de plazo").
    public static string Estado(DateTime? fechaPrevista, DateTime? fechaFin)
    {
        if (fechaFin != null) return EstadoRegistro.Cerrada;
        if (fechaPrevista == null) return EstadoRegistro.SinPlazo;
        return DateTime.Today <= fechaPrevista.Value.Date ? EstadoRegistro.EnPlazo : EstadoRegistro.FueraPlazo;
    }

    public static string Estado(NoConformidad nc) => Estado(nc.FechaPrevista, nc.FechaFin);

    public static bool Abierta(NoConformidad nc) => nc.FechaFin == null;

    // Para el inicio: cuántos registros hay abiertos y cuántos de ellos fuera de plazo
    public (int Abiertas, int FueraPlazo) ContarAbiertas()
    {
        var plazos = repositorio.ObtenerPlazosAbiertos();
        return (plazos.Count, plazos.Count(fecha => Estado(fecha, null) == EstadoRegistro.FueraPlazo));
    }

    // Días que han pasado desde la fecha prevista (solo si está fuera de plazo)
    public static int? DiasFueraPlazo(NoConformidad nc) =>
        Estado(nc) == EstadoRegistro.FueraPlazo ? (DateTime.Today - nc.FechaPrevista!.Value.Date).Days : null;

    // Fases

    // Todos los tipos tienen las cinco fases, pero solo la NC las necesita todas: a la incidencia
    // le basta la acción inmediata, y a la acción de mejora, sus acciones.
    public static List<EstadoFase> Fases(NoConformidad nc, IEnumerable<AccionNc> acciones)
    {
        var lista = acciones.ToList();
        var correctoras = lista.Where(a => a.Clase == ClaseAccion.Correctora).ToList();
        bool esIncidencia = nc.TipoRegistro == TipoRegistro.Incidencia;
        bool esMejora = nc.TipoRegistro == TipoRegistro.Mejora;

        return new()
        {
            new(FaseCalidad.Registro, !string.IsNullOrWhiteSpace(nc.Problema)),
            new(FaseCalidad.AccionInmediata, lista.Any(a => a.Clase == ClaseAccion.Inmediata), esMejora),
            new(FaseCalidad.Analisis, !string.IsNullOrWhiteSpace(nc.DescripcionCausa)
                || !string.IsNullOrWhiteSpace(nc.AnalisisCausaRaiz), esIncidencia || esMejora),
            new(esMejora ? FaseCalidad.Acciones : FaseCalidad.Correccion,
                correctoras.Count > 0 && correctoras.All(a => a.Hecha), esIncidencia),
            new(FaseCalidad.Cierre, nc.FechaFin != null)
        };
    }

    // Validación

    // Quita espacios, deja en null lo vacío y borra los campos que no son de este tipo
    public static NoConformidad Normalizar(NoConformidad nc)
    {
        static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        bool esNc = nc.TipoRegistro == TipoRegistro.NoConformidad;
        bool esIncidencia = nc.TipoRegistro == TipoRegistro.Incidencia;

        return nc with
        {
            Numero = Limpio(nc.Numero),
            Origen = Limpio(nc.Origen),
            Seccion = Limpio(nc.Seccion),
            Tipo = esNc ? Limpio(nc.Tipo) : null,
            Problema = nc.Problema.Trim(),
            DescripcionCausa = Limpio(nc.DescripcionCausa),
            Responsable = Limpio(nc.Responsable),
            Cliente = Limpio(nc.Cliente),
            Proveedor = Limpio(nc.Proveedor),
            McPccBpm = esIncidencia ? Limpio(nc.McPccBpm) : null,
            Parametro = esIncidencia ? Limpio(nc.Parametro) : null,
            Operario = esIncidencia ? Limpio(nc.Operario) : null,
            CapituloNorma = Limpio(nc.CapituloNorma),
            AnalisisCausaRaiz = Limpio(nc.AnalisisCausaRaiz)
        };
    }

    // Errores de un registro ya normalizado; el campo es el nameof de la propiedad, para ponerlo bajo su control.
    public static List<(string Campo, string Mensaje)> Errores(NoConformidad nc)
    {
        var errores = new List<(string, string)>();
        void Error(string campo, string mensaje) => errores.Add((campo, mensaje));

        bool esNc = nc.TipoRegistro == TipoRegistro.NoConformidad;

        if (!TipoRegistro.Todos.Contains(nc.TipoRegistro))
            Error(nameof(nc.TipoRegistro), "Elige si es una no conformidad, una incidencia o una acción de mejora.");

        if (nc.FechaDetectado.Date > DateTime.Today)
            Error(nameof(nc.FechaDetectado), "La fecha de detección no puede ser futura.");

        if (esNc && nc.Origen == null)
            Error(nameof(nc.Origen), "Elige el origen de la no conformidad.");
        if (nc.Seccion == null)
            Error(nameof(nc.Seccion), "Elige la sección.");
        else if (SoloSeccionesAuditoria(nc.Origen) && !SeccionesAuditoria.Contains(nc.Seccion))
            Error(nameof(nc.Seccion), "Con origen auditoría, la sección es A. INTERNA, A. EXTERNA o A. CLIENTE.");
        if (esNc && nc.Tipo == null)
            Error(nameof(nc.Tipo), "Elige el tipo de no conformidad.");

        // Origen R = reclamación de cliente, P = proveedor
        if (nc.Origen == "R" && nc.Cliente == null)
            Error(nameof(nc.Cliente), "En una reclamación de cliente, indica el cliente.");
        if (nc.Origen == "P" && nc.Proveedor == null)
            Error(nameof(nc.Proveedor), "Si el origen es un proveedor, indica cuál.");

        if (nc.Problema == "")
            Error(nameof(nc.Problema), "Describe el problema detectado.");

        if (nc.FechaPrevista?.Date < nc.FechaDetectado.Date)
            Error(nameof(nc.FechaPrevista), "La fecha prevista no puede ser anterior a la detección.");
        if (nc.FechaFin?.Date < nc.FechaDetectado.Date)
            Error(nameof(nc.FechaFin), "La fecha fin no puede ser anterior a la detección.");
        else if (nc.FechaFin?.Date > DateTime.Today)
            Error(nameof(nc.FechaFin), "La fecha fin no puede ser futura.");

        if (nc.CosteEconomico < 0)
            Error(nameof(nc.CosteEconomico), "El coste no puede ser negativo.");
        if (nc.HorasManoObra < 0)
            Error(nameof(nc.HorasManoObra), "Las horas no pueden ser negativas.");

        return errores;
    }

    public static AccionNc NormalizarAccion(AccionNc accion)
    {
        static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        return accion with
        {
            Accion = accion.Accion.Trim(),
            Fecha = accion.Fecha?.Date,
            Responsable = Limpio(accion.Responsable),
            Observaciones = Limpio(accion.Observaciones)
        };
    }

    // Error de una acción ya normalizada (null si está bien). Un seguimiento nuevo necesita fecha;
    // los antiguos (nc_seguimientos.sql) pueden no tenerla.
    public static string? ErrorAccion(AccionNc accion, DateTime fechaDetectado)
    {
        bool esSeguimiento = accion.Clase == ClaseAccion.Seguimiento;
        if (accion.Accion == "")
            return esSeguimiento ? "Describe lo que se ha comprobado." : "Describe la acción.";
        if (esSeguimiento && accion.Fecha == null && accion.Id == 0)
            return "Pon la fecha de la comprobación.";
        if (accion.Fecha < fechaDetectado.Date)
            return "La fecha no puede ser anterior a la detección.";
        return null;
    }

    // Guardado

    private const string MensajeBorrado = "Este registro ya no existe: puede que otro usuario lo haya borrado.";
    private const string MensajeModificado =
        "Otro usuario ha guardado cambios en este registro mientras lo tenías abierto. " +
        "Para no borrar sus cambios, los tuyos no se han guardado.\n\n" +
        "Copia lo que hayas escrito, cierra la ficha y vuelve a abrirla para ver lo último.";

    // Crea o guarda el registro con sus acciones y devuelve su id. El número se calcula aquí.
    public int Guardar(NoConformidad nc, IEnumerable<AccionNc> acciones)
    {
        nc = Normalizar(nc);
        var lista = acciones.Select(NormalizarAccion).ToList();

        var errores = Errores(nc);
        if (errores.Count > 0)
            throw new ReglaNegocioException(errores[0].Mensaje);
        foreach (var accion in lista)
            if (ErrorAccion(accion, nc.FechaDetectado) is string error)
                throw new ReglaNegocioException(error);

        var serie = Serie(nc);

        if (nc.Id == 0)
        {
            if (Sesion.UsuarioActual is not Usuario usuario)
                throw new ReglaNegocioException("No hay ningún usuario elegido.");
            return repositorio.Insertar(nc with { Numero = null }, lista, serie, usuario.Id);
        }

        // Se mira el número guardado, no el que trae la pantalla
        var anterior = repositorio.ObtenerRegistro(nc.Id)
            ?? throw new ReglaNegocioException(MensajeBorrado);
        nc = nc with { Numero = serie == null ? null : anterior.Numero };

        if (!repositorio.Actualizar(nc, lista, Renumerar(anterior, serie) ? serie : null))
            throw new ReglaNegocioException(
                repositorio.ObtenerRegistro(nc.Id) == null ? MensajeBorrado : MensajeModificado);
        return nc.Id;
    }

    // Fichas abiertas

    // La pantalla renueva el bloqueo cada minuto; si pasan estos segundos sin renovarlo, caduca.
    public const int SegundosRenovarBloqueo = 60;
    private const int CaducidadBloqueo = 180;

    // Identifica este GPE abierto: el mismo usuario puede tener GPE en dos equipos
    private static readonly Guid EsteGpe = Guid.NewGuid();

    // Bloquea la ficha para este GPE (o lo renueva). null si se puede editar; si no, quién la tiene.
    public BloqueoNc? Bloquear(int noConformidadId)
    {
        if (Sesion.UsuarioActual is not Usuario usuario)
            throw new ReglaNegocioException("No hay ningún usuario elegido.");
        return repositorio.Bloquear(noConformidadId, usuario.Id, EsteGpe, Environment.MachineName, CaducidadBloqueo);
    }

    public void Desbloquear(int noConformidadId) => repositorio.Desbloquear(noConformidadId, EsteGpe);

    // "María tiene abierta esta ficha desde las 10:42 en PC-CALIDAD."
    public static string DescribirBloqueo(BloqueoNc bloqueo)
    {
        string quien = bloqueo.MismoUsuario ? "Tú mismo tienes" : bloqueo.Usuario + " tiene";
        string cuando = bloqueo.Desde.Date == DateTime.Today
            ? $"desde las {bloqueo.Desde:HH:mm}"
            : $"desde el {bloqueo.Desde:dd/MM/yyyy} a las {bloqueo.Desde:HH:mm}";
        return $"{quien} abierta esta ficha {cuando} en el equipo {bloqueo.Equipo}.";
    }
}
