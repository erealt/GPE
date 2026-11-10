USE GPE;
GO

-- Fotos de las incidencias, NC y acciones de mejora. Se ejecuta después de no_conformidades.sql
-- y se puede ejecutar más de una vez. Con sqlcmd, usar -I -f 65001 (por las tildes).
-- Como ADJUNTO en las devoluciones: el archivo se guarda en la propia base de datos.
IF OBJECT_ID('NC_FOTO') IS NULL
BEGIN
    CREATE TABLE NC_FOTO (
        id INT IDENTITY(1,1) PRIMARY KEY,
        no_conformidad_id INT NOT NULL,
        nombre_archivo NVARCHAR(260) NOT NULL,
        tamano_bytes INT NOT NULL,
        contenido VARBINARY(MAX) NOT NULL,
        usuario_id INT NOT NULL,
        subido_en DATETIME2 NOT NULL CONSTRAINT DF_nc_foto_subido DEFAULT SYSDATETIME(),
        CONSTRAINT FK_nc_foto_no_conformidad FOREIGN KEY (no_conformidad_id)
            REFERENCES NO_CONFORMIDAD(id) ON DELETE CASCADE,
        CONSTRAINT FK_nc_foto_usuario FOREIGN KEY (usuario_id) REFERENCES USUARIO(id)
    );
    CREATE INDEX IX_nc_foto_no_conformidad ON NC_FOTO (no_conformidad_id);
END;
GO

-- Historial: como TR_ADJUNTO_HISTORIAL, sin copiar el contenido del archivo
CREATE OR ALTER TRIGGER TR_NC_FOTO_HISTORIAL ON NC_FOTO
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM inserted) AND NOT EXISTS (SELECT 1 FROM deleted) RETURN;

    DECLARE @usuario INT = CAST(SESSION_CONTEXT(N'usuario_id') AS INT);
    IF @usuario IS NULL
        THROW 50001, 'No se ha indicado el usuario que realiza el cambio (sp_set_session_context).', 1;

    INSERT INTO HISTORIAL_CAMBIOS
        (devolucion_id, usuario_id, tabla_afectada, registro_id, accion, valores_anteriores, valores_nuevos, transaccion_id)
    SELECT NULL, @usuario, 'NC_FOTO', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
    FROM inserted i
    FULL OUTER JOIN deleted d ON d.id = i.id
    CROSS APPLY (SELECT
        COALESCE(i.id, d.id) AS registro_id,
        CASE WHEN i.id IS NULL THEN 'DELETE' WHEN d.id IS NULL THEN 'INSERT' ELSE 'UPDATE' END AS accion,
        CASE WHEN d.id IS NULL THEN NULL
             ELSE (SELECT d.id, d.no_conformidad_id, d.nombre_archivo, d.tamano_bytes
                   FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS anteriores,
        CASE WHEN i.id IS NULL THEN NULL
             ELSE (SELECT i.id, i.no_conformidad_id, i.nombre_archivo, i.tamano_bytes
                   FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS nuevos
    ) c
    WHERE c.accion <> 'UPDATE' OR c.anteriores <> c.nuevos;
END;
GO
