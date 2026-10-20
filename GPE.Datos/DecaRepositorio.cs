using Microsoft.Data.SqlClient;

namespace GPE.Datos;

public class DecaRepositorio
{
    // Columnas de remitente y destinatario, en el orden en que las lee LeerParte
    private const string ColumnasParte =
        "{0}_nombre, {0}_nif, {0}_direccion, {0}_cp, {0}_poblacion, {0}_provincia, {0}_pais, {0}_telefono";

    // ---------- Lectura ----------

    public List<DecaFila> ObtenerDecas()
    {
        const string sql = @"
            SELECT d.id, d.numero, d.fecha_carga, d.remitente_nombre, d.destinatario_nombre,
                   d.transportista_nombre,
                   COUNT(p.id), COALESCE(SUM(p.num_cajas), 0), COALESCE(SUM(p.peso_kg), 0),
                   CASE WHEN d.pdf_generado_en IS NULL THEN 0 ELSE 1 END
            FROM DECA d
            LEFT JOIN DECA_PALET p ON p.deca_id = d.id
            GROUP BY d.id, d.numero, d.fecha_carga, d.remitente_nombre, d.destinatario_nombre,
                     d.transportista_nombre, d.pdf_generado_en
            ORDER BY d.fecha_carga DESC, d.id DESC;";

        var lista = new List<DecaFila>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            lista.Add(new DecaFila(
                Id: lector.GetInt32(0),
                Numero: lector.GetString(1),
                FechaCarga: lector.GetDateTime(2),
                Remitente: lector.GetString(3),
                Destinatario: lector.GetString(4),
                Transportista: lector.GetString(5),
                Palets: lector.GetInt32(6),
                Cajas: lector.GetInt32(7),
                PesoKg: lector.GetDecimal(8),
                PdfAlDia: lector.GetInt32(9) == 1));
        }

        return lista;
    }

    // Un DeCA con sus palets, o null si ya no existe
    public DecaGuardado? ObtenerDeca(int decaId)
    {
        string sql = $@"
            SELECT d.id, d.numero, d.codigo_publico,
                   {string.Format(ColumnasParte, "d.remitente")},
                   {string.Format(ColumnasParte, "d.destinatario")},
                   d.fecha_carga, d.transportista_nombre, d.transportista_nif, d.matricula,
                   d.mercancia, d.observaciones, d.archivo_pdf, d.pdf_generado_en,
                   uc.nombre, d.creado_en, um.nombre, d.modificado_en
            FROM DECA d
            JOIN USUARIO uc ON uc.id = d.creado_por
            LEFT JOIN USUARIO um ON um.id = d.modificado_por
            WHERE d.id = @deca;

            SELECT peso_kg, num_cajas FROM DECA_PALET WHERE deca_id = @deca ORDER BY orden;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@deca", decaId);

        using var lector = comando.ExecuteReader();
        if (!lector.Read()) return null;

        int id = lector.GetInt32(0);
        string numero = lector.GetString(1);
        Guid codigo = lector.GetGuid(2);
        var remitente = LeerParte(lector, 3);
        var destinatario = LeerParte(lector, 11);
        DateTime fechaCarga = lector.GetDateTime(19);
        string transportista = lector.GetString(20);
        string nif = lector.GetString(21);
        string matricula = lector.GetString(22);
        string mercancia = lector.GetString(23);
        string? observaciones = TextoONull(lector, 24);
        string? archivoPdf = TextoONull(lector, 25);
        DateTime? pdfGeneradoEn = lector.IsDBNull(26) ? null : lector.GetDateTime(26);
        string creadoPor = lector.GetString(27);
        DateTime creadoEn = lector.GetDateTime(28);
        string? modificadoPor = TextoONull(lector, 29);
        DateTime? modificadoEn = lector.IsDBNull(30) ? null : lector.GetDateTime(30);

        var palets = new List<PaletDeca>();
        lector.NextResult();
        while (lector.Read())
            palets.Add(new PaletDeca(lector.GetDecimal(0), lector.GetInt32(1)));

        var datos = new DatosDeca(remitente, destinatario, fechaCarga, transportista, nif,
            matricula, mercancia, observaciones, palets);

        return new DecaGuardado(id, numero, codigo, datos, archivoPdf, pdfGeneradoEn,
            creadoPor, creadoEn, modificadoPor, modificadoEn);
    }

    // Remitentes y destinatarios ya usados (cualquiera de los dos papeles), el más reciente primero
    public List<ParteEnvio> ObtenerPartesUsadas()
    {
        string sql = $@"
            SELECT nombre, nif, direccion, cp, poblacion, provincia, pais, telefono
            FROM (
                SELECT {string.Format(ColumnasParte, "remitente")}, COALESCE(modificado_en, creado_en)
                FROM DECA
                UNION ALL
                SELECT {string.Format(ColumnasParte, "destinatario")}, COALESCE(modificado_en, creado_en)
                FROM DECA
            ) p (nombre, nif, direccion, cp, poblacion, provincia, pais, telefono, fecha)
            GROUP BY nombre, nif, direccion, cp, poblacion, provincia, pais, telefono
            ORDER BY MAX(fecha) DESC;";

        var partes = new List<ParteEnvio>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            partes.Add(LeerParte(lector, 0));

        return partes;
    }

    // Transportistas ya usados (nombre + NIF) con la matrícula de su último DeCA
    public List<TransportistaDeca> ObtenerTransportistasUsados()
    {
        const string sql = @"
            SELECT transportista_nombre, transportista_nif, matricula
            FROM (
                SELECT transportista_nombre, transportista_nif, matricula,
                       ROW_NUMBER() OVER (PARTITION BY transportista_nombre, transportista_nif
                                          ORDER BY COALESCE(modificado_en, creado_en) DESC, id DESC) AS n,
                       COALESCE(modificado_en, creado_en) AS fecha
                FROM DECA
            ) t
            WHERE n = 1
            ORDER BY fecha DESC;";

        var transportistas = new List<TransportistaDeca>();

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);

        using var lector = comando.ExecuteReader();
        while (lector.Read())
            transportistas.Add(new TransportistaDeca(lector.GetString(0), lector.GetString(1), lector.GetString(2)));

        return transportistas;
    }

    // ---------- Escritura ----------

    public int InsertarDeca(DatosDeca datos, int usuarioId)
    {
        string sql = $@"
            INSERT INTO DECA
                ({string.Format(ColumnasParte, "remitente")},
                 {string.Format(ColumnasParte, "destinatario")},
                 fecha_carga, transportista_nombre, transportista_nif, matricula, mercancia,
                 observaciones, creado_por)
            OUTPUT INSERTED.id
            VALUES
                (@remitente_nombre, @remitente_nif, @remitente_direccion, @remitente_cp,
                 @remitente_poblacion, @remitente_provincia, @remitente_pais, @remitente_telefono,
                 @destinatario_nombre, @destinatario_nif, @destinatario_direccion, @destinatario_cp,
                 @destinatario_poblacion, @destinatario_provincia, @destinatario_pais, @destinatario_telefono,
                 @fecha, @transportista, @nif, @matricula, @mercancia, @observaciones, @usuario);";

        using var conexion = Conexion.Abrir();

        // Si algo falla antes del Commit, la transacción se deshace sola al salir del método
        using var transaccion = conexion.BeginTransaction();

        int id;
        using (var comando = new SqlCommand(sql, conexion, transaccion))
        {
            AnadirParametros(comando, datos);
            comando.Parameters.AddWithValue("@usuario", usuarioId);
            id = (int)comando.ExecuteScalar()!;
        }

        InsertarPalets(conexion, transaccion, id, datos.Palets);

        transaccion.Commit();
        return id;
    }

    // Guarda los cambios y sustituye los palets. El PDF deja de estar al día (pdf_generado_en
    // a NULL) pero se conserva su nombre, para que el QR ya impreso siga sirviendo al regenerarlo.
    // Devuelve false si el DeCA ya no existe.
    public bool ActualizarDeca(int decaId, DatosDeca datos, int usuarioId)
    {
        const string sql = @"
            UPDATE DECA SET
                remitente_nombre = @remitente_nombre, remitente_nif = @remitente_nif,
                remitente_direccion = @remitente_direccion, remitente_cp = @remitente_cp,
                remitente_poblacion = @remitente_poblacion, remitente_provincia = @remitente_provincia,
                remitente_pais = @remitente_pais, remitente_telefono = @remitente_telefono,
                destinatario_nombre = @destinatario_nombre, destinatario_nif = @destinatario_nif,
                destinatario_direccion = @destinatario_direccion, destinatario_cp = @destinatario_cp,
                destinatario_poblacion = @destinatario_poblacion, destinatario_provincia = @destinatario_provincia,
                destinatario_pais = @destinatario_pais, destinatario_telefono = @destinatario_telefono,
                fecha_carga = @fecha, transportista_nombre = @transportista, transportista_nif = @nif,
                matricula = @matricula, mercancia = @mercancia, observaciones = @observaciones,
                pdf_generado_en = NULL,
                modificado_por = @usuario, modificado_en = SYSDATETIME()
            WHERE id = @deca;

            DELETE FROM DECA_PALET WHERE deca_id = @deca;";

        using var conexion = Conexion.Abrir();
        using var transaccion = conexion.BeginTransaction();

        using (var comando = new SqlCommand(sql, conexion, transaccion))
        {
            AnadirParametros(comando, datos);
            comando.Parameters.AddWithValue("@usuario", usuarioId);
            comando.Parameters.AddWithValue("@deca", decaId);

            // Filas del UPDATE + del DELETE: si es 0, el DeCA no existe (sin él tampoco hay palets)
            if (comando.ExecuteNonQuery() == 0) return false;
        }

        InsertarPalets(conexion, transaccion, decaId, datos.Palets);

        transaccion.Commit();
        return true;
    }

    // Apunta que el PDF se ha generado y publicado con este nombre de archivo
    public void MarcarPdfGenerado(int decaId, string archivoPdf)
    {
        const string sql = @"
            UPDATE DECA SET archivo_pdf = @archivo, pdf_generado_en = SYSDATETIME()
            WHERE id = @deca;";

        using var conexion = Conexion.Abrir();
        using var comando = new SqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("@archivo", archivoPdf);
        comando.Parameters.AddWithValue("@deca", decaId);
        comando.ExecuteNonQuery();
    }

    // ---------- Ayudas ----------

    private static void InsertarPalets(SqlConnection conexion, SqlTransaction transaccion,
        int decaId, List<PaletDeca> palets)
    {
        const string sql = @"
            INSERT INTO DECA_PALET (deca_id, orden, peso_kg, num_cajas)
            VALUES (@deca, @orden, @peso, @cajas);";

        for (int i = 0; i < palets.Count; i++)
        {
            using var comando = new SqlCommand(sql, conexion, transaccion);
            comando.Parameters.AddWithValue("@deca", decaId);
            comando.Parameters.AddWithValue("@orden", i + 1);
            comando.Parameters.AddWithValue("@peso", palets[i].PesoKg);
            comando.Parameters.AddWithValue("@cajas", palets[i].NumCajas);
            comando.ExecuteNonQuery();
        }
    }

    // Parámetros comunes de InsertarDeca y ActualizarDeca (todo menos usuario e id)
    private static void AnadirParametros(SqlCommand comando, DatosDeca d)
    {
        AnadirParte(comando, "remitente", d.Remitente);
        AnadirParte(comando, "destinatario", d.Destinatario);
        comando.Parameters.AddWithValue("@fecha", d.FechaCarga.Date);
        comando.Parameters.AddWithValue("@transportista", d.TransportistaNombre);
        comando.Parameters.AddWithValue("@nif", d.TransportistaNif);
        comando.Parameters.AddWithValue("@matricula", d.Matricula);
        comando.Parameters.AddWithValue("@mercancia", d.Mercancia);
        comando.Parameters.AddWithValue("@observaciones", (object?)d.Observaciones ?? DBNull.Value);
    }

    private static void AnadirParte(SqlCommand comando, string prefijo, ParteEnvio p)
    {
        comando.Parameters.AddWithValue($"@{prefijo}_nombre", p.Nombre);
        comando.Parameters.AddWithValue($"@{prefijo}_nif", p.Nif);
        comando.Parameters.AddWithValue($"@{prefijo}_direccion", p.Direccion);
        comando.Parameters.AddWithValue($"@{prefijo}_cp", p.CodigoPostal);
        comando.Parameters.AddWithValue($"@{prefijo}_poblacion", p.Poblacion);
        comando.Parameters.AddWithValue($"@{prefijo}_provincia", (object?)p.Provincia ?? DBNull.Value);
        comando.Parameters.AddWithValue($"@{prefijo}_pais", p.Pais);
        comando.Parameters.AddWithValue($"@{prefijo}_telefono", (object?)p.Telefono ?? DBNull.Value);
    }

    // Lee las 8 columnas de ColumnasParte a partir de la posición "inicio"
    private static ParteEnvio LeerParte(SqlDataReader lector, int inicio) => new(
        Nombre: lector.GetString(inicio),
        Nif: lector.GetString(inicio + 1),
        Direccion: lector.GetString(inicio + 2),
        CodigoPostal: lector.GetString(inicio + 3),
        Poblacion: lector.GetString(inicio + 4),
        Provincia: TextoONull(lector, inicio + 5),
        Pais: lector.GetString(inicio + 6),
        Telefono: TextoONull(lector, inicio + 7));

    private static string? TextoONull(SqlDataReader lector, int columna) =>
        lector.IsDBNull(columna) ? null : lector.GetString(columna);
}
