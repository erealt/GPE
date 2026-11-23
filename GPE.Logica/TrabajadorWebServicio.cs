using GPE.Datos;

namespace GPE.Logica;

// Web de los trabajadores: ver sus acciones pendientes y marcarlas hechas (entran con LoginServicio)
public class TrabajadorWebServicio
{
    private readonly TrabajadorWebRepositorio repositorio = new();

    // La pestaña "Hechas" enseña las del último mes
    public const int DiasHechas = 30;

    public List<AccionTrabajador> ObtenerPendientes(string persona) => repositorio.ObtenerPendientes(persona);

    public List<AccionHecha> ObtenerHechas(string persona) => repositorio.ObtenerHechas(persona, DiasHechas);

    public (string NombreArchivo, byte[] Contenido)? ObtenerFoto(int fotoId, string persona) =>
        repositorio.ObtenerFoto(fotoId, persona);

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
