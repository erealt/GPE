using GPE.Datos;

namespace GPE.Logica;

// Web de los trabajadores: ver sus acciones pendientes y marcarlas hechas (entran con LoginServicio)
public class TrabajadorWebServicio
{
    private readonly TrabajadorWebRepositorio repositorio = new();

    public List<AccionTrabajador> ObtenerPendientes(string persona) => repositorio.ObtenerPendientes(persona);

    public void MarcarHecha(int accionId, string persona, string? comentario)
    {
        comentario = comentario?.Trim();
        string nota = $"Hecha por {persona} desde la web el {DateTime.Now:dd/MM/yyyy HH:mm}"
            + (string.IsNullOrEmpty(comentario) ? "." : $": {comentario}");

        if (!repositorio.MarcarHecha(accionId, persona, nota))
            throw new ReglaNegocioException(
                "Esta acción ya no está pendiente: puede que calidad la haya cambiado o cerrado. Se ha actualizado la lista.");
    }
}
