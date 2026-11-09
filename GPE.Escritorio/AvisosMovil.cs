using System.Net.Http.Json;
using Tramo = GPE.Escritorio.ModuloMaquina.Tramo;

namespace GPE.Escritorio;

// Une las paradas de la pantalla Máquina con la API que lee la aplicación del móvil.
// Si la API no está arrancada todo sigue funcionando como antes, solo en el escritorio.
static class AvisosMovil
{
    // GPE_API permite apuntar a la API de otro ordenador
    private static readonly HttpClient http = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("GPE_API") ?? "http://localhost:5080"),
        Timeout = TimeSpan.FromSeconds(3)
    };

    private static readonly List<(string Maquina, Tramo Parada)> avisados = new();
    private static readonly System.Windows.Forms.Timer reloj = new() { Interval = 3000 };
    private static bool sincronizando;

    // null mientras no se ha probado; luego, si la última llamada llegó a la API
    public static bool? Conectado { get; private set; }

    public static void Empezar()
    {
        if (reloj.Enabled) return;
        reloj.Tick += async (s, e) => await Sincronizar();
        reloj.Start();
    }

    private record AvisoApi(int Id, string? Motivo, string? Comentario, DateTime? Llegada, string? Mecanico,
        DateTime? Resuelto, string? QueHaHecho);

    // Se añade a la lista después de enviarlo: si no, la sincronización lo vería sin número mientras se envía
    // y lo mandaría otra vez (salía duplicado en el móvil)
    public static async Task Avisar(string maquina, Tramo parada)
    {
        var aviso = await Enviar("avisos", new { maquina, parada.Motivo, parada.Comentario, parada.Maquinista });
        if (aviso != null) parada.IdAviso = aviso.Id;
        avisados.Add((maquina, parada));
    }

    private static async Task<AvisoApi?> Enviar(string ruta, object datos)
    {
        try
        {
            var respuesta = await http.PostAsJsonAsync(ruta, datos);
            Conectado = true;
            return respuesta.IsSuccessStatusCode ? await respuesta.Content.ReadFromJsonAsync<AvisoApi>() : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Conectado = false;
            return null;
        }
    }

    // Trae lo que han pulsado en el móvil y manda lo que se ha pulsado aquí (llegada, resuelto porque la máquina
    // arrancó, el motivo o el comentario puestos después del aviso)
    private static async Task Sincronizar()
    {
        if (sincronizando || avisados.Count == 0) return;
        sincronizando = true;
        try
        {
            // Los que no llegaron a la API (estaba apagada) se mandan ahora
            foreach (var (maquina, parada) in avisados.Where(a => a.Parada.IdAviso == null).ToList())
                if (await Enviar("avisos", new { maquina, parada.Motivo, parada.Comentario, parada.Maquinista }) is AvisoApi nuevo)
                    parada.IdAviso = nuevo.Id;
                else return;

            List<AvisoApi>? lista;
            try
            {
                lista = await http.GetFromJsonAsync<List<AvisoApi>>("avisos");
                Conectado = true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                Conectado = false;
                return;
            }
            if (lista == null) return;

            foreach (var (_, parada) in avisados)
            {
                if (lista.FirstOrDefault(a => a.Id == parada.IdAviso) is not AvisoApi aviso) continue;
                if (parada.LlegaMecanico != null && aviso.Llegada == null) await Enviar($"avisos/{aviso.Id}/llegada", new { });
                if (parada.ResueltoMecanico != null && aviso.Resuelto == null) await Enviar($"avisos/{aviso.Id}/resuelto", new { });
                parada.LlegaMecanico ??= aviso.Llegada;
                parada.ResueltoMecanico ??= aviso.Resuelto;
                parada.Mecanico = aviso.Mecanico;
                parada.QueHaHecho = aviso.QueHaHecho;
                if (parada.Motivo != aviso.Motivo || parada.Comentario != aviso.Comentario)
                    await Enviar($"avisos/{aviso.Id}/datos", new { parada.Motivo, parada.Comentario });
            }
        }
        finally
        {
            sincronizando = false;
        }
    }
}
