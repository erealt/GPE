namespace GPE.Logica;

// La lanzan todos los servicios cuando algo no cumple una regla; el mensaje
// está pensado para enseñárselo tal cual al usuario.
public class ReglaNegocioException : Exception
{
    public ReglaNegocioException(string mensaje) : base(mensaje) { }
}
