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

public class NoConformidadServicio
{
    private readonly NoConformidadRepositorio repositorio = new();

    // ---------- Consultas ----------

    public List<NoConformidad> ObtenerRegistros() => repositorio.ObtenerRegistros();

    public NoConformidad? ObtenerRegistro(int id) => repositorio.ObtenerRegistro(id);

    public List<AccionNc> ObtenerAcciones(int id) => repositorio.ObtenerAcciones(id);

    public Dictionary<string, List<OpcionLista>> ObtenerListas() => repositorio.ObtenerListas();

    public List<string> ObtenerClientes() => repositorio.ObtenerClientes();

    public List<string> ObtenerProveedores() => repositorio.ObtenerProveedores();

    // ---------- Numeración ----------

    // Secciones de las NC de auditoría, que llevan su propia numeración
    private const string AuditoriaExterna = "A. EXTERNA";
    private const string AuditoriaInterna = "A. INTERNA";
    private const string AuditoriaCliente = "A. CLIENTE";

    // Origen A = auditoría: en una NC, la sección solo puede ser una de las de auditoría
    public static readonly string[] SeccionesAuditoria = { AuditoriaInterna, AuditoriaExterna, AuditoriaCliente };

    public static bool SoloSeccionesAuditoria(string tipoRegistro, string? origen) =>
        tipoRegistro == TipoRegistro.NoConformidad && origen == "A";

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

    // Un registro conserva su número mientras siga en la misma serie. Si no tiene
    // o cambia de serie (de incidencia a NC, a una auditoría...), toca uno nuevo.
    private static bool Renumerar(NoConformidad? anterior, SerieNumero? serie) =>
        serie != null && (anterior?.Numero == null || Serie(anterior) != serie);

    // Número que se enseña en la ficha antes de guardar (null: no lleva número).
    // El definitivo se calcula al guardar, por si otro usuario guarda antes.
    public string? NumeroPrevisto(NoConformidad nc, NoConformidad? anterior)
    {
        var serie = Serie(Normalizar(nc));
        if (serie == null) return null;
        return Renumerar(anterior, serie) ? repositorio.SiguienteNumero(serie) : anterior!.Numero;
    }

    // Con fecha fin está cerrada; si no, se mira la fecha prevista. En el Excel una
    // abierta sin fecha prevista salía "fuera de plazo"; aquí se distingue como "sin plazo".
    public static string Estado(DateTime? fechaPrevista, DateTime? fechaFin)
    {
        if (fechaFin != null) return EstadoRegistro.Cerrada;
        if (fechaPrevista == null) return EstadoRegistro.SinPlazo;
        return DateTime.Today <= fechaPrevista.Value.Date ? EstadoRegistro.EnPlazo : EstadoRegistro.FueraPlazo;
    }

    public static string Estado(NoConformidad nc) => Estado(nc.FechaPrevista, nc.FechaFin);

    public static bool Abierta(NoConformidad nc) => nc.FechaFin == null;

    // Días que han pasado desde la fecha prevista (solo si está fuera de plazo)
    public static int? DiasFueraPlazo(NoConformidad nc) =>
        Estado(nc) == EstadoRegistro.FueraPlazo ? (DateTime.Today - nc.FechaPrevista!.Value.Date).Days : null;

    // ---------- Validación ----------

    // Quita espacios, deja en null lo vacío y borra los campos que no son de este tipo
    // (Tipo es solo de las NC; MC/PCC/BPM, parámetro y operario, solo de las incidencias)
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
            AnalisisCausaRaiz = Limpio(nc.AnalisisCausaRaiz),
            Seguimiento = Limpio(nc.Seguimiento)
        };
    }

    // Todos los errores de un registro ya normalizado. El campo es el nombre de la
    // propiedad (nameof), para que la pantalla ponga cada mensaje debajo de su control.
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
        else if (SoloSeccionesAuditoria(nc.TipoRegistro, nc.Origen) && !SeccionesAuditoria.Contains(nc.Seccion))
            Error(nameof(nc.Seccion), "En una NC de auditoría, la sección es A. INTERNA, A. EXTERNA o A. CLIENTE.");
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

    // Error de una acción ya normalizada (null si está bien)
    public static string? ErrorAccion(AccionNc accion, DateTime fechaDetectado)
    {
        if (accion.Accion == "")
            return "Describe la acción.";
        if (accion.Fecha < fechaDetectado.Date)
            return "La fecha no puede ser anterior a la detección.";
        return null;
    }

    // ---------- Guardado ----------

    private const string MensajeBorrado = "Este registro ya no existe: puede que otro usuario lo haya borrado.";

    // Crea el registro (Id = 0) o guarda los cambios, con sus acciones. Devuelve su id.
    // El número no lo pone la pantalla: se calcula aquí según su serie.
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
            throw new ReglaNegocioException(MensajeBorrado);
        return nc.Id;
    }
}
