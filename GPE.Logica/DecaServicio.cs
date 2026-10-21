using GPE.Datos;

namespace GPE.Logica;

public class DecaServicio
{
    private readonly DecaRepositorio repositorio = new();

    // Más que esto en un palet es casi seguro un error al teclear
    public const decimal PesoMaximoPalet = 2000;

    // La fecha de carga puede ir hasta un mes hacia atrás o hacia delante
    public const int DiasMargenFechaCarga = 30;

    public const string PaisPorDefecto = "España";

    // ---------- Consultas ----------

    public List<DecaFila> ObtenerDecas() => repositorio.ObtenerDecas();

    public DecaGuardado? ObtenerDeca(int decaId) => repositorio.ObtenerDeca(decaId);

    public List<ParteEnvio> ObtenerPartesUsadas() => repositorio.ObtenerPartesUsadas();

    public List<TransportistaDeca> ObtenerTransportistasUsados() => repositorio.ObtenerTransportistasUsados();

    // ---------- Operaciones ----------

    // Devuelve el id del DeCA creado
    public int CrearDeca(DatosDeca datos)
    {
        datos = Normalizar(datos);
        Validar(datos);
        return repositorio.InsertarDeca(datos, UsuarioActual());
    }

    public void ModificarDeca(int decaId, DatosDeca datos)
    {
        datos = Normalizar(datos);

        // La fecha solo se comprueba si se ha cambiado: así se puede corregir un DeCA antiguo
        DateTime? fechaGuardada = repositorio.ObtenerDeca(decaId)?.Datos.FechaCarga;
        Validar(datos, comprobarFecha: datos.FechaCarga.Date != fechaGuardada?.Date);

        if (!repositorio.ActualizarDeca(decaId, datos, UsuarioActual()))
            throw new ReglaNegocioException("Este DeCA ya no existe. Cierra la pantalla y vuelve a abrirla.");
    }

    private static int UsuarioActual() =>
        Sesion.UsuarioActual?.Id ?? throw new ReglaNegocioException("No hay ningún usuario seleccionado.");

    // ---------- Comprobaciones de cada campo ----------
    // Devuelven el error o null si está bien. Las usa la pantalla para poner el error
    // debajo de cada campo, y Validar para volver a comprobarlo todo al guardar.

    public static bool EsEspana(string pais) =>
        pais.Trim().ToUpperInvariant() is "ESPAÑA" or "ESPANA" or "SPAIN" or "ES";

    // En España se comprueba el carácter de control; en otros países cada uno tiene su formato
    public static string? ErrorNif(string nif, string pais)
    {
        nif = Nif.Normalizar(nif);
        if (nif.Length == 0) return "Indica el NIF.";
        if (nif.Length > 20) return "El NIF no puede superar los 20 caracteres.";
        if (EsEspana(pais) && !Nif.EsValido(nif)) return "El NIF no es correcto: revisa que esté bien escrito.";
        return null;
    }

    // El transportista no tiene país: si su NIF tiene forma de español se comprueba el
    // control; si no, se da por bueno (puede ser extranjero)
    public static string? ErrorNifTransportista(string nif)
    {
        nif = Nif.Normalizar(nif);
        if (nif.Length == 0) return "Indica el NIF del transportista.";
        if (nif.Length > 20) return "El NIF no puede superar los 20 caracteres.";
        if (Nif.PareceEspanol(nif) && !Nif.EsValido(nif)) return "El NIF no es correcto: revisa que esté bien escrito.";
        return null;
    }

    public static string? ErrorCodigoPostal(string cp, string pais)
    {
        cp = cp.Trim();
        if (cp.Length == 0) return "Indica el código postal.";
        if (cp.Length > 10) return "El código postal no puede superar los 10 caracteres.";
        if (EsEspana(pais) && (cp.Length != 5 || !cp.All(char.IsAsciiDigit)))
            return "En España el código postal tiene 5 cifras.";
        return null;
    }

    public static string? ErrorFechaCarga(DateTime fecha)
    {
        if (fecha.Date < DateTime.Today.AddDays(-DiasMargenFechaCarga))
            return $"La fecha de carga es de hace más de {DiasMargenFechaCarga} días. Revísala.";
        if (fecha.Date > DateTime.Today.AddDays(DiasMargenFechaCarga))
            return $"La fecha de carga es dentro de más de {DiasMargenFechaCarga} días. Revísala.";
        return null;
    }

    // Un palet de la tabla (null si la celda está vacía o no es un número)
    public static string? ErrorPalet(decimal? pesoKg, int? numCajas)
    {
        if (pesoKg == null) return "Indica el peso del palet.";
        if (pesoKg <= 0) return "El peso tiene que ser mayor que 0.";
        if (pesoKg > PesoMaximoPalet) return $"Un palet no puede pesar más de {PesoMaximoPalet:N0} kg.";
        if (decimal.Round(pesoKg.Value, 2) != pesoKg) return "El peso admite como mucho 2 decimales.";
        if (numCajas == null) return "Indica el número de cajas.";
        if (numCajas <= 0) return "El número de cajas tiene que ser mayor que 0.";
        return null;
    }

    // Texto obligatorio con su longitud máxima en la base de datos
    public static string? ErrorObligatorio(string valor, string nombre, int maximo)
    {
        valor = valor.Trim();
        if (valor.Length == 0) return $"Indica {nombre}.";
        if (valor.Length > maximo) return $"No puede superar los {maximo} caracteres.";
        return null;
    }

    public static string? ErrorOpcional(string? valor, int maximo) =>
        valor != null && valor.Trim().Length > maximo ? $"No puede superar los {maximo} caracteres." : null;

    // ---------- Validación completa ----------

    // Quita espacios sobrantes, pone NIF y matrícula en mayúsculas y los vacíos opcionales a null
    public static DatosDeca Normalizar(DatosDeca d) => d with
    {
        Remitente = Normalizar(d.Remitente),
        Destinatario = Normalizar(d.Destinatario),
        TransportistaNombre = d.TransportistaNombre.Trim(),
        TransportistaNif = Nif.Normalizar(d.TransportistaNif),
        Matricula = string.Concat(d.Matricula.Where(char.IsLetterOrDigit)).ToUpperInvariant(),
        Mercancia = d.Mercancia.Trim(),
        Observaciones = Opcional(d.Observaciones)
    };

    private static ParteEnvio Normalizar(ParteEnvio p) => new(
        Nombre: p.Nombre.Trim(),
        Nif: Nif.Normalizar(p.Nif),
        Direccion: p.Direccion.Trim(),
        CodigoPostal: p.CodigoPostal.Trim(),
        Poblacion: p.Poblacion.Trim(),
        Provincia: Opcional(p.Provincia),
        Pais: string.IsNullOrWhiteSpace(p.Pais) ? PaisPorDefecto : p.Pais.Trim(),
        Telefono: Opcional(p.Telefono));

    private static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    // Lanza ReglaNegocioException con el primer error que encuentre
    private static void Validar(DatosDeca d, bool comprobarFecha = true)
    {
        Comprobar(ErroresParte(d.Remitente, "del remitente"));
        Comprobar(ErroresParte(d.Destinatario, "del destinatario"));

        Comprobar(
            comprobarFecha ? ErrorFechaCarga(d.FechaCarga) : null,
            ErrorObligatorio(d.TransportistaNombre, "el transportista", 150),
            ErrorNifTransportista(d.TransportistaNif),
            ErrorObligatorio(d.Matricula, "la matrícula", 20),
            ErrorObligatorio(d.Mercancia, "la mercancía", 250));

        if (d.Palets.Count == 0)
            throw new ReglaNegocioException("Añade al menos un palet.");
        for (int i = 0; i < d.Palets.Count; i++)
            if (ErrorPalet(d.Palets[i].PesoKg, d.Palets[i].NumCajas) is string error)
                throw new ReglaNegocioException($"Palet {i + 1}: {error}");
    }

    // Los mensajes llevan "del remitente" / "del destinatario" para saber de cuál es
    private static string?[] ErroresParte(ParteEnvio p, string deQuien) =>
    [
        ErrorObligatorio(p.Nombre, "el nombre " + deQuien, 150),
        Contexto(ErrorNif(p.Nif, p.Pais), deQuien),
        ErrorObligatorio(p.Direccion, "la dirección " + deQuien, 200),
        Contexto(ErrorCodigoPostal(p.CodigoPostal, p.Pais), deQuien),
        ErrorObligatorio(p.Poblacion, "la población " + deQuien, 100),
        Contexto(ErrorOpcional(p.Provincia, 100), deQuien),
        ErrorObligatorio(p.Pais, "el país " + deQuien, 60),
        Contexto(ErrorOpcional(p.Telefono, 30), deQuien)
    ];

    private static string? Contexto(string? error, string deQuien) =>
        error == null ? null : $"{error.TrimEnd('.')} ({deQuien.Replace("del ", "")}).";

    private static void Comprobar(params string?[] errores)
    {
        if (errores.FirstOrDefault(e => e != null) is string error)
            throw new ReglaNegocioException(error);
    }
}
