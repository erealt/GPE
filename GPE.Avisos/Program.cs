using GPE.Logica;

// Envía los avisos pendientes y los resúmenes diarios sin abrir GPE (tarea programada, ver README).
// Log en %LOCALAPPDATA%\GPE\avisos.log; si falla sale con código 1 para que la tarea reintente.

string carpetaLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GPE");
Directory.CreateDirectory(carpetaLog);
string rutaLog = Path.Combine(carpetaLog, "avisos.log");

void Log(string texto)
{
    string linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {texto}";
    Console.WriteLine(linea);
    File.AppendAllText(rutaLog, linea + Environment.NewLine);
}

try
{
    AvisoServicio.EnviarPendientes();

    int enviados = AvisoServicio.EnviarResumenDiario();
    Log(enviados == 0
        ? "Resumen diario: nada que enviar (no hay devoluciones pendientes de revisar o ya se envió hoy)."
        : $"Resumen diario: {enviados} correo(s) enviado(s).");
    return 0;
}
catch (Exception ex)
{
    Log("ERROR: " + AvisoServicio.DescribirError(ex));
    return 1;
}
