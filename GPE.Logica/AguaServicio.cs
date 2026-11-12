using GPE.Datos;
using System.Globalization;

namespace GPE.Logica;

// Control de agua de calidad (hoja AGUA, MT-RG09): cloro y organolépticos de cada punto revisado.
public class AguaServicio
{
    private readonly AguaRepositorio repositorio = new();
    private static readonly CultureInfo Es = new("es-ES");

    // Consultas

    public List<AguaToma> ObtenerTomas() => repositorio.ObtenerTomas();

    // Todos los controles del mes de ese día
    public List<ControlAguaConToma> ObtenerMes(DateTime mes)
    {
        var primero = new DateTime(mes.Year, mes.Month, 1);
        return repositorio.ObtenerControles(primero, primero.AddMonths(1).AddDays(-1));
    }

    // "Hoy: sin tomas" o "Hoy: 2 tomas", para la tarjeta del inicio
    public string ResumenHoy()
    {
        int controles = repositorio.ContarControles(DateTime.Today);
        return controles == 0 ? "Hoy: sin tomas" : $"Hoy: {controles} {(controles == 1 ? "toma" : "tomas")}";
    }

    // El cloro se sale del rango de la toma (las que no tienen rango nunca se salen)
    public static bool FueraDeRango(decimal? cloro, AguaToma toma) =>
        cloro is decimal c && (c < toma.CloroMin || c > toma.CloroMax);

    // Lo escrito en la columna Hora: "9:30", "09.30", "0930" o "9". null si no es una hora.
    public static bool LeerHora(string? texto, out TimeSpan? hora)
    {
        hora = null;
        string t = (texto ?? "").Trim().Replace('.', ':').Replace(',', ':').Replace('h', ':').TrimEnd(':');
        if (t == "") return true;
        if (!t.Contains(':') && t.All(char.IsDigit))
            t = t.Length <= 2 ? t + ":00" : t[..^2] + ":" + t[^2..];
        var partes = t.Split(':');
        if (partes.Length != 2 || !int.TryParse(partes[0], out int h) || !int.TryParse(partes[1], out int m)
            || h is < 0 or > 23 || m is < 0 or > 59)
            return false;
        hora = new TimeSpan(h, m, 0);
        return true;
    }

    public static string Hora(TimeSpan hora) => hora.ToString(@"hh\:mm");

    // "0,55 ppm"
    public static string Ppm(decimal cloro) => cloro.ToString("0.00", Es) + " ppm";

    // Validación

    public const int LargoComentario = 1000;
    public const decimal CloroMaximo = 9.99m;   // lo que cabe en AGUA_CONTROL.cloro

    // Quita espacios y deja en null lo vacío
    public static ControlAgua Normalizar(ControlAgua control) => control with
    {
        Fecha = control.Fecha.Date,
        Hora = control.Hora is TimeSpan h ? new TimeSpan(h.Hours, h.Minutes, 0) : null,   // sin segundos
        Comentario = string.IsNullOrWhiteSpace(control.Comentario) ? null : control.Comentario.Trim()
    };

    // Error de un control ya normalizado (null si está bien)
    public static string? Error(ControlAgua control)
    {
        if (control.TomaId == 0)
            return "Elige el punto de agua.";
        if (control.Cloro < 0)
            return "El cloro no puede ser negativo.";
        if (control.Cloro > CloroMaximo)
            return $"El cloro no puede pasar de {Ppm(CloroMaximo)}.";
        if (control.Comentario?.Length > LargoComentario)
            return $"El comentario no puede pasar de {LargoComentario} caracteres.";
        return null;
    }

    // Guardado

    private const string MensajeModificado =
        "Otra persona ha cambiado o borrado esta toma mientras la tenías abierta. " +
        "Para no borrar sus cambios, los tuyos no se han guardado.\n\n" +
        "Apunta lo que hayas escrito y vuelve a abrirla para ver lo último.";

    // Nueva (Id 0) o corregida. Devuelve la toma tal como se ha guardado (con su id).
    public ControlAgua Guardar(ControlAgua control)
    {
        if (Sesion.UsuarioActual is not Usuario usuario)
            throw new ReglaNegocioException("No hay ningún usuario elegido.");

        control = Normalizar(control);
        if (control.Fecha > DateTime.Today)
            throw new ReglaNegocioException("No se pueden apuntar tomas de un día que todavía no ha llegado.");
        if (Error(control) is string error)
            throw new ReglaNegocioException(error);

        if (control.Id == 0)
            return control with { Id = repositorio.Insertar(control, usuario.Id) };
        if (!repositorio.Actualizar(control))
            throw new ReglaNegocioException(MensajeModificado);
        return control;
    }

    // La toma que repite otra que no convenció: mismo punto y mismo día
    public static ControlAgua Repeticion(ControlAgua original, TimeSpan? hora) =>
        new(0, original.Fecha, original.TomaId, null, true, true, true, true, null, Hora: hora, RepiteId: original.Id);

    public void Borrar(ControlAgua control)
    {
        if (!repositorio.Borrar(control))
            throw new ReglaNegocioException(MensajeModificado);
    }
}
