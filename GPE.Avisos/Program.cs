using GPE.Logica;

// Tarea diaria sin abrir GPE (tarea programada, ver README): envía los avisos pendientes y los
// resúmenes diarios, y borra los PDF del DeCA de más de un año.
// Log en %LOCALAPPDATA%\GPE\avisos.log; si algo falla sale con código 1 para que la tarea reintente.
// Con --solo-deca solo borra los PDF (en el servidor de la web del DeCA, donde no hay correo configurado).

string carpetaLog = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GPE");
Directory.CreateDirectory(carpetaLog);
string rutaLog = Path.Combine(carpetaLog, "avisos.log");

void Log(string texto)
{
    string linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {texto}";
    Console.WriteLine(linea);
    File.AppendAllText(rutaLog, linea + Environment.NewLine);
}

// Cada parte va por separado: si falla el correo, los PDF antiguos se borran igual, y al revés
bool todoBien = true;
bool soloDeca = args.Contains("--solo-deca");

if (!soloDeca)
try
{
    AvisoServicio.EnviarPendientes();

    int enviados = AvisoServicio.EnviarResumenDiario();
    Log(enviados == 0
        ? "Resumen diario: nada que enviar (no hay devoluciones pendientes de revisar o ya se envió hoy)."
        : $"Resumen diario: {enviados} correo(s) enviado(s).");
}
catch (Exception ex)
{
    Log("ERROR: " + AvisoServicio.DescribirError(ex));
    todoBien = false;
}

// Acciones de calidad: las asignaciones que no salieron al guardar la ficha y el resumen de cada responsable
try
{
    int asignadas = AvisoServicio.EnviarAsignaciones();
    if (asignadas > 0) Log($"Acciones de calidad: {asignadas} correo(s) de acciones nuevas enviado(s).");

    int resumenes = AvisoServicio.EnviarResumenAcciones();
    Log(resumenes == 0
        ? "Acciones de calidad: ningún resumen que enviar (nadie con correo tiene acciones pendientes o ya se envió hoy)."
        : $"Acciones de calidad: {resumenes} resumen(es) enviado(s).");
}
catch (Exception ex)
{
    Log("ERROR en los avisos de acciones de calidad: " + AvisoServicio.DescribirError(ex));
    todoBien = false;
}

try
{
    var (web, carpeta) = DecaServicio.BorrarPdfsAntiguos();
    Log(web == 0 && carpeta == 0
        ? "DeCA: ningún PDF de más de un año."
        : $"DeCA: borrados {web} PDF de más de un año de la web y {carpeta} archivo(s) de la carpeta.");
}
catch (Exception ex)
{
    Log("ERROR al borrar los DeCA antiguos: " + ex.Message);
    todoBien = false;
}

return todoBien ? 0 : 1;
