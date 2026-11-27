using Microsoft.Data.SqlClient;

namespace GPE.Datos;

// Proveedor de TenDB: el Código es nuestro Nº
public record ProveedorTenDB(int Codigo, string Nombre, DateTime? FechaAlta);

// Producto de TenDB: el Código es el código de PROVEEDOR_PRODUCTO
public record ProductoTenDB(string Codigo, string Descripcion)
{
    public override string ToString() => $"{Codigo}  ·  {Descripcion}";
}

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

    private static SqlConnection Abrir()
    {
        if (CadenaConexion == null)
            throw new InvalidOperationException("Falta la conexión a TenDB (archivo tendb.txt junto al programa).");
        var conexion = new SqlConnection(CadenaConexion);
        conexion.Open();
        return conexion;
    }

    // Productos de un proveedor (por nuestro Nº, que es su Código): tblProductos.Proveedor es el IdNumero de
    // tblProveedores. Los productos de baja tienen Proveedor = 0, así que no salen.
    public List<ProductoTenDB> ProductosDeProveedor(int numero)
    {
        const string sql = @"
            SELECT LTRIM(RTRIM(pr.Codigo)), LTRIM(RTRIM(ISNULL(pr.Descripcion, '')))
            FROM dbo.tblProductos pr
            JOIN dbo.tblProveedores v ON v.IdEmpresa = pr.IdEmpresa AND v.IdNumero = pr.Proveedor
            WHERE pr.IdEmpresa = @empresa AND TRY_CAST(v.Codigo AS INT) = @numero
            ORDER BY pr.Codigo;";

        using var conexion = Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@empresa", EmpresaEurochoc);
        comando.Parameters.AddWithValue("@numero", numero);
        using var lector = comando.ExecuteReader();

        var productos = new List<ProductoTenDB>();
        while (lector.Read())
            productos.Add(new ProductoTenDB(lector.GetString(0), lector.GetString(1)));
        return productos;
    }

    // Proveedores de dbo.tblProveedores de Eurochoc, por orden alfabético
    public List<ProveedorTenDB> Proveedores()
    {
        const string sql = @"
            SELECT Codigo, LTRIM(RTRIM(Nombre)), FechaAlta
            FROM dbo.tblProveedores
            WHERE IdEmpresa = @empresa AND Nombre IS NOT NULL AND LTRIM(RTRIM(Nombre)) <> ''
            ORDER BY 2;";

        using var conexion = Abrir();
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
