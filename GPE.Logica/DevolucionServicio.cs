using GPE.Datos;

namespace GPE.Logica;

public class ReglaNegocioException : Exception
{
    public ReglaNegocioException(string mensaje) : base(mensaje) { }
}

public class DevolucionServicio
{
    private readonly DevolucionRepositorio repositorio = new();

    // ---------- Consultas ----------

    public List<DevolucionFila> ObtenerDevoluciones() => repositorio.ObtenerDevoluciones();

    public List<string> ObtenerEstados() => repositorio.ObtenerEstados();

    public List<Cliente> ObtenerClientes() => repositorio.ObtenerClientes();

    public List<Producto> ObtenerProductos() => repositorio.ObtenerProductos();

    public List<Usuario> ObtenerUsuarios() => repositorio.ObtenerUsuarios();

    public List<Transportista> ObtenerTransportistas() => repositorio.ObtenerTransportistas();

    public RecogidaGuardada? ObtenerRecogida(int devolucionId) => repositorio.ObtenerRecogida(devolucionId);

    public List<EntradaHistorial> ObtenerHistorial(int devolucionId)
    {
        var nombres = repositorio.ObtenerNombresPorCampo();
        return HistorialFormateador.Agrupar(repositorio.ObtenerHistorial(devolucionId), nombres);
    }

    // ---------- Operaciones ----------

    public int CrearDevolucion(DatosNuevaDevolucion datos)
    {
        Validar(datos);
        return repositorio.InsertarDevolucion(datos);
    }

    public void GuardarRecogida(DatosRecogida datos, DateTime fechaSolicitud)
    {
        ValidarRecogida(datos, fechaSolicitud);
        repositorio.GuardarRecogida(datos);
    }

    private static void ValidarRecogida(DatosRecogida datos, DateTime fechaSolicitud)
    {
        if (string.IsNullOrWhiteSpace(datos.Direccion))
            throw new ReglaNegocioException("La dirección de recogida es obligatoria.");

        if (datos.Direccion.Length > 200)
            throw new ReglaNegocioException("La dirección no puede superar los 200 caracteres.");

        if (datos.FechaRecogida.Date < fechaSolicitud.Date)
            throw new ReglaNegocioException("La fecha de recogida no puede ser anterior a la de solicitud.");

        if (datos.TelefonoContacto != null && !TelefonoValido(datos.TelefonoContacto))
            throw new ReglaNegocioException("El teléfono de contacto no es válido.");
    }

    // Admite dígitos, espacios, guiones y "+" inicial, con al menos 9 dígitos
    public static bool TelefonoValido(string telefono) =>
        telefono.Length <= 30
        && telefono.Count(char.IsDigit) >= 9
        && telefono.Select((c, i) => char.IsDigit(c) || c == ' ' || c == '-' || (c == '+' && i == 0)).All(ok => ok);

    private static void Validar(DatosNuevaDevolucion datos)
    {
        if (string.IsNullOrWhiteSpace(datos.Motivo))
            throw new ReglaNegocioException("El motivo de la devolución es obligatorio.");

        if (datos.Lineas.Count == 0)
            throw new ReglaNegocioException("Añade al menos un producto a la devolución.");

        if (datos.Lineas.Any(l => l.Cantidad <= 0))
            throw new ReglaNegocioException("Todas las cantidades deben ser mayores que 0.");

        if (datos.FechaSolicitud.Date > DateTime.Today)
            throw new ReglaNegocioException("La fecha de solicitud no puede ser posterior a hoy.");
    }
}