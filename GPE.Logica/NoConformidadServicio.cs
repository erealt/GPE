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

    public Dictionary<string, List<OpcionLista>> ObtenerListas() => repositorio.ObtenerListas();

    public List<string> ObtenerClientes() => repositorio.ObtenerClientes();

    public List<string> ObtenerProveedores() => repositorio.ObtenerProveedores();

    public string SiguienteNumero() => repositorio.SiguienteNumero().ToString();

    public bool ExisteNumero(string numero, int idExcluido) => repositorio.ExisteNumero(numero, idExcluido);

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
            AccionInmediata = Limpio(nc.AccionInmediata),
            Responsable = Limpio(nc.Responsable),
            Cliente = Limpio(nc.Cliente),
            Proveedor = Limpio(nc.Proveedor),
            McPccBpm = esIncidencia ? Limpio(nc.McPccBpm) : null,
            Parametro = esIncidencia ? Limpio(nc.Parametro) : null,
            Operario = esIncidencia ? Limpio(nc.Operario) : null,
            CapituloNorma = Limpio(nc.CapituloNorma),
            AnalisisCausaRaiz = Limpio(nc.AnalisisCausaRaiz),
            AccionCorrectora = Limpio(nc.AccionCorrectora),
            ResponsableAccionCorrectora = Limpio(nc.ResponsableAccionCorrectora),
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

        if (esNc && nc.Numero == null)
            Error(nameof(nc.Numero), "Indica el nº de la no conformidad.");
        else if (nc.Numero?.Length > 30)
            Error(nameof(nc.Numero), "El nº no puede superar los 30 caracteres.");

        if (esNc && nc.Origen == null)
            Error(nameof(nc.Origen), "Elige el origen de la no conformidad.");
        if (nc.Seccion == null)
            Error(nameof(nc.Seccion), "Elige la sección.");
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

    // ---------- Guardado ----------

    // Crea el registro (Id = 0) o guarda los cambios. Devuelve su id.
    public int Guardar(NoConformidad nc)
    {
        nc = Normalizar(nc);

        var errores = Errores(nc);
        if (errores.Count > 0)
            throw new ReglaNegocioException(errores[0].Mensaje);

        if (nc.Id == 0)
        {
            if (Sesion.UsuarioActual is not Usuario usuario)
                throw new ReglaNegocioException("No hay ningún usuario elegido.");
            return repositorio.Insertar(nc, usuario.Id);
        }

        if (!repositorio.Actualizar(nc))
            throw new ReglaNegocioException("Este registro ya no existe: puede que otro usuario lo haya borrado.");
        return nc.Id;
    }
}
