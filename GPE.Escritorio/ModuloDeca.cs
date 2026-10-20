namespace GPE.Escritorio;

// Módulo DeCA: lista de documentos de control. PROVISIONAL: en construcción.
public class ModuloDeca : UserControl
{
    public ModuloDeca()
    {
        var (scroll, pila) = Componentes.Pagina();
        pila.Controls.Add(Componentes.Cabecera("DECA", "DeCA",
            "Documentos de control del transporte. En construcción: aquí irá la lista."));
        Controls.Add(scroll);
    }
}
