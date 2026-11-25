-- Aviso a calidad en el programa de escritorio cuando un trabajador marca una acción como hecha
-- desde la web (/acciones). Se ejecuta después de acciones_nc_avisos.sql y login.sql, y se puede
-- ejecutar más de una vez.
-- No lleva USE: se elige la base de datos al ejecutarlo, con -d. Con sqlcmd, usar -f 65001.
--   sqlcmd -S localhost -E -C -b -f 65001 -d miGPE -i BaseDatos\acciones_hechas_web.sql

-- Una fila por cada acción marcada hecha desde la web
IF OBJECT_ID('NC_ACCION_HECHA_WEB') IS NULL
    CREATE TABLE NC_ACCION_HECHA_WEB (
        id INT IDENTITY PRIMARY KEY,
        accion_id INT NOT NULL,
        persona NVARCHAR(150) NOT NULL,
        comentario NVARCHAR(MAX) NULL,
        hecha_en DATETIME2 NOT NULL CONSTRAINT DF_nc_accion_hecha_web_hecha DEFAULT SYSDATETIME(),
        CONSTRAINT FK_nc_accion_hecha_web_accion FOREIGN KEY (accion_id) REFERENCES NC_ACCION(id) ON DELETE CASCADE
    );
GO

-- Quién ha visto ya cada aviso (cada usuaria de calidad lo ve por su cuenta). Se apunta al pulsar
-- "Ver" o la ✕ del aviso; mientras no, le sigue saliendo cada vez que abre el programa.
IF OBJECT_ID('NC_ACCION_HECHA_VISTO') IS NULL
    CREATE TABLE NC_ACCION_HECHA_VISTO (
        aviso_id INT NOT NULL,
        usuario_id INT NOT NULL,
        visto_en DATETIME2 NOT NULL CONSTRAINT DF_nc_accion_hecha_visto_visto DEFAULT SYSDATETIME(),
        CONSTRAINT PK_nc_accion_hecha_visto PRIMARY KEY (aviso_id, usuario_id),
        CONSTRAINT FK_nc_accion_hecha_visto_aviso FOREIGN KEY (aviso_id) REFERENCES NC_ACCION_HECHA_WEB(id) ON DELETE CASCADE,
        CONSTRAINT FK_nc_accion_hecha_visto_usuario FOREIGN KEY (usuario_id) REFERENCES USUARIO(id)
    );
GO

-- Comprobación
SELECT name FROM sys.tables WHERE name IN (N'NC_ACCION_HECHA_WEB', N'NC_ACCION_HECHA_VISTO');
GO
