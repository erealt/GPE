namespace GPE.Logica;

// Regla incumplida; el mensaje se enseña tal cual al usuario.
public class ReglaNegocioException : Exception
{
    public ReglaNegocioException(string mensaje) : base(mensaje) { }
}
