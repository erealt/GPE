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

        // Sin usuario no se puede registrar el historial, así que no se entra
        using (var seleccion = new FormSeleccionUsuario())
        {
            if (seleccion.ShowDialog() != DialogResult.OK) return;
        }

        Application.Run(new FormPrincipal());
    }    
}