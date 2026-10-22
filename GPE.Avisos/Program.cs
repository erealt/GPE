using GPE.Logica;

// Avisos por correo sin abrir GPE. Lo ejecuta cada mañana una tarea programada de Windows
// en un ordenador que esté encendido (ver README, "Avisos por correo").
//   1. Envía los avisos que se quedaron pendientes en AVISO_CORREO.
//   2. Envía el resumen diario al almacén con las devoluciones pendientes de revisar.
// Usa la misma configuración que GPE (variables GPE_SMTP_* y GPE_CONEXION).
// Lo que pasa se apunta en %LOCALAPPDATA%\GPE\avisos.log. Si falla, termina con código 1
// para que la tarea programada lo reintente.

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
        : $"Resumen diario: enviado a {enviados} persona(s).");
    return 0;
}
catch (Exception ex)
{
    Log("ERROR: " + AvisoServicio.DescribirError(ex));
    return 1;
}
