-- Subgrupo de la causa raíz (25/11/2026): dentro de su grupo (Personas, Equipos...), una de las
-- frases de la hoja CAUSA RAIZ del Excel. La lista está en la aplicación (CausasRaiz.cs).
-- Se ejecuta después de causa_raiz.sql y se puede ejecutar más de una vez.
-- No lleva USE: se elige la base de datos al ejecutarlo, con -d. Con sqlcmd, usar -f 65001.
--   sqlcmd -S localhost -E -C -b -f 65001 -d miGPE -i BaseDatos\causa_raiz_subgrupo.sql
-- En el servidor: instalar-servidor.ps1 -InstanciaSql MSSQLSERVER -Scripts causa_raiz_subgrupo

IF COL_LENGTH('NO_CONFORMIDAD', 'causa_raiz_subgrupo') IS NULL
    ALTER TABLE NO_CONFORMIDAD ADD causa_raiz_subgrupo NVARCHAR(200) NULL;
