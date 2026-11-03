using GPE.Datos;
using GPE.Logica;

namespace GPE.Web;

// Lo que se escribe en el formulario del DeCA mientras se edita (los record de GPE.Datos no se
// pueden modificar campo a campo). Las comprobaciones son las mismas que en el escritorio.

public class ParteFormulario
{
    public string Nombre { get; set; } = "";
    public string Nif { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string CodigoPostal { get; set; } = "";
    public string Poblacion { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string Pais { get; set; } = DecaServicio.PaisPorDefecto;

    public ParteEnvio Leer() => new(Nombre, Nif, Direccion, CodigoPostal, Poblacion, Provincia, Pais, Telefono);

    public void Rellenar(ParteEnvio p)
    {
        Nombre = p.Nombre;
        Nif = p.Nif;
        Telefono = p.Telefono ?? "";
        Direccion = p.Direccion;
        CodigoPostal = p.CodigoPostal;
        Poblacion = p.Poblacion;
        Provincia = p.Provincia ?? "";
        Pais = p.Pais;
    }

    // Error de cada campo por su nombre (el de la propiedad), en el orden de la pantalla
    public IEnumerable<(string Campo, string? Error)> Comprobaciones() =>
    [
        (nameof(Nombre), DecaServicio.ErrorObligatorio(Nombre, "el nombre", 150)),
        (nameof(Nif), DecaServicio.ErrorNif(Nif, Pais)),
        (nameof(Telefono), DecaServicio.ErrorOpcional(Telefono, 30)),
        (nameof(Direccion), DecaServicio.ErrorObligatorio(Direccion, "la dirección", 200)),
        (nameof(CodigoPostal), DecaServicio.ErrorCodigoPostal(CodigoPostal, Pais)),
        (nameof(Poblacion), DecaServicio.ErrorObligatorio(Poblacion, "la población", 100)),
        (nameof(Provincia), DecaServicio.ErrorOpcional(Provincia, 100)),
        (nameof(Pais), DecaServicio.ErrorObligatorio(Pais, "el país", 60))
    ];
}

public class PaletFormulario
{
    public decimal? PesoKg { get; set; }
    public int? NumCajas { get; set; }
    public string? Error { get; set; }
}
