using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.SqlClient;

string connectionString="Server=localhost;Database=GPE;Trusted_Connection=True;TrustServerCertificate=True;";
try
{
    using(SqlConnection connection = new SqlConnection(connectionString))
    {
        connection.Open();
        Console.WriteLine("Conectado "+ connection.Database);

    }
}
catch ( Exception ex)
{
    Console.WriteLine(ex.Message);

}

