using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Proveedor de TenDB: el Código es nuestro Nº
public record ProveedorTenDB(int Codigo, string Nombre, DateTime? FechaAlta);

// Base de datos TenDB de la empresa, solo para leer (usuario de lectura). Los proveedores se dan de alta y se
// cambian allí. La conexión, por orden: la variable de entorno TENDB_CONEXION o el archivo tendb.txt junto al
// programa. No se guarda en el proyecto porque lleva la contraseña.
public class TenDBRepositorio
{
    // En TenDB hay varias empresas; la de Eurochoc es la 7
    private const int EmpresaEurochoc = 7;

    private static readonly string? CadenaConexion =
        Environment.GetEnvironmentVariable("TENDB_CONEXION") ?? LeerArchivo();

    private static string? LeerArchivo()
    {
        string ruta = Path.Combine(AppContext.BaseDirectory, "tendb.txt");
        if (!File.Exists(ruta)) return null;
        string texto = File.ReadAllText(ruta).Trim();
        return texto == "" ? null : texto;
    }

    // Proveedores de dbo.tblProveedores de Eurochoc, por orden alfabético
    public List<ProveedorTenDB> Proveedores()
    {
        if (CadenaConexion == null)
            throw new InvalidOperationException("Falta la conexión a TenDB (archivo tendb.txt junto al programa).");

        const string sql = @"
            SELECT Codigo, LTRIM(RTRIM(Nombre)), FechaAlta
            FROM dbo.tblProveedores
            WHERE IdEmpresa = @empresa AND Nombre IS NOT NULL AND LTRIM(RTRIM(Nombre)) <> ''
            ORDER BY 2;";

        using var conexion = new SqlConnection(CadenaConexion);
        conexion.Open();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@empresa", EmpresaEurochoc);
        using var lector = comando.ExecuteReader();

        var proveedores = new List<ProveedorTenDB>();
        while (lector.Read())
        {
            // Los códigos son números con ceros delante ("00005"); uno que no lo sea no puede ser un Nº
            if (!int.TryParse(lector.GetString(0), out int codigo)) continue;
            proveedores.Add(new ProveedorTenDB(codigo, lector.GetString(1),
                lector.IsDBNull(2) ? null : lector.GetDateTime(2).Date));
        }
        return proveedores;
    }
}
