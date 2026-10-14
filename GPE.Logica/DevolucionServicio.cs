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

    public List<EntradaHistorial> ObtenerHistorial(int devolucionId) =>
        repositorio.ObtenerHistorial(devolucionId)
            .Select(HistorialFormateador.Formatear)
            .ToList();

    // ---------- Operaciones ----------

    public int CrearDevolucion(DatosNuevaDevolucion datos)
    {
        Validar(datos);
        return repositorio.InsertarDevolucion(datos);
    }

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