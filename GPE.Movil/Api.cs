using System.Net.Http.Json;

namespace GPE.Movil;

public class Aviso
{
    public int Id { get; set; }
    public string Maquina { get; set; } = "";
    public string? Motivo { get; set; }
    public string? Comentario { get; set; }
    public string? Maquinista { get; set; }
    public DateTime Avisado { get; set; }
    public DateTime? Llegada { get; set; }
    public string? Mecanico { get; set; }       // el que pulsó "He llegado"
    public DateTime? Resuelto { get; set; }
    public string? QueHaHecho { get; set; }
    public string? ResueltoPor { get; set; }    // el que escribió el informe
}

// Las llamadas a GPE.Api. La dirección y el nombre del mecánico se guardan en el móvil.
public static class Api
{
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static string Direccion
    {
        get => Preferences.Default.Get("api", "http://192.168.1.49:5080");
        set => Preferences.Default.Set("api", value.Trim().TrimEnd('/'));
    }

    public static string Mecanico
    {
        get => Preferences.Default.Get("mecanico", "");
        set => Preferences.Default.Set("mecanico", value.Trim());
    }

    private static string Url(string ruta) => $"{Direccion.TrimEnd('/')}/{ruta}";

    // null si no hay conexión
    public static async Task<List<Aviso>?> Avisos()
    {
        try { return await http.GetFromJsonAsync<List<Aviso>>(Url("avisos")); }
        catch (Exception) { return null; }
    }

    public static Task<bool> Llegada(int id) => Enviar($"avisos/{id}/llegada", new { mecanico = Mecanico });

    public static Task<bool> Resuelto(int id, string queHaHecho) =>
        Enviar($"avisos/{id}/resuelto", new { queHaHecho, mecanico = Mecanico });

    private static async Task<bool> Enviar(string ruta, object datos)
    {
        try { return (await http.PostAsJsonAsync(Url(ruta), datos)).IsSuccessStatusCode; }
        catch (Exception) { return false; }
    }
}
