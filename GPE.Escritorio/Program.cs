namespace GPE.Escritorio;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        // Red de seguridad: un error que ninguna pantalla haya previsto se enseña
        // en un mensaje en vez de cerrar la aplicación de golpe
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => MostrarErrorInesperado(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex) MostrarErrorInesperado(ex);
        };

        // Sin usuario no se puede registrar el historial, así que no se entra
        using (var seleccion = new FormSeleccionUsuario())
        {
            if (seleccion.ShowDialog() != DialogResult.OK) return;
        }

        Application.Run(new FormPrincipal());
    }

    private static void MostrarErrorInesperado(Exception ex) =>
        MessageBox.Show(
            "Ha ocurrido un error inesperado. Si se repite, avisa con este mensaje:\n\n" + ex.Message,
            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
}