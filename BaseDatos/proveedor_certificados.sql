USE GPE;
GO

-- Certificados de cada proveedor (BRC, IFS, ISO 9001...) con su fecha de caducidad, para avisar antes de que caduquen.
-- Se ejecuta después de proveedores.sql y se puede ejecutar más de una vez.
-- Con sqlcmd, usar -I -f 65001 (por las tildes).
--
-- PROVEEDOR.certificados (texto) se queda como estaba: es lo escrito a mano ("BRC: 12/04/27", "Pendiente de
-- recertificación"...). La aplicación avisa solo con los de esta tabla.
IF OBJECT_ID('PROVEEDOR_CERTIFICADO') IS NULL
BEGIN
    CREATE TABLE PROVEEDOR_CERTIFICADO (
        id INT IDENTITY(1,1) PRIMARY KEY,
        proveedor_id INT NOT NULL,
        nombre NVARCHAR(100) NOT NULL,
        caduca DATE NOT NULL,
        creado_por INT NOT NULL,
        creado_en DATETIME2 NOT NULL CONSTRAINT DF_proveedor_certificado_creado DEFAULT SYSDATETIME(),
        version ROWVERSION NOT NULL,
        CONSTRAINT FK_proveedor_certificado_proveedor FOREIGN KEY (proveedor_id) REFERENCES PROVEEDOR(id),
        CONSTRAINT FK_proveedor_certificado_usuario FOREIGN KEY (creado_por) REFERENCES USUARIO(id)
    );
    CREATE INDEX IX_proveedor_certificado_proveedor ON PROVEEDOR_CERTIFICADO (proveedor_id);
    CREATE INDEX IX_proveedor_certificado_caduca ON PROVEEDOR_CERTIFICADO (caduca);
END;
GO

-- Historial: igual que TR_PROVEEDOR_HISTORIAL
CREATE OR ALTER TRIGGER TR_PROVEEDOR_CERTIFICADO_HISTORIAL ON PROVEEDOR_CERTIFICADO
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
    SELECT NULL, @usuario, 'PROVEEDOR_CERTIFICADO', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
    FROM inserted i
    FULL OUTER JOIN deleted d ON d.id = i.id
    CROSS APPLY (SELECT
        COALESCE(i.id, d.id) AS registro_id,
        CASE WHEN i.id IS NULL THEN 'DELETE' WHEN d.id IS NULL THEN 'INSERT' ELSE 'UPDATE' END AS accion,
        CASE WHEN d.id IS NULL THEN NULL
             ELSE JSON_MODIFY((SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES), '$.version', NULL) END AS anteriores,
        CASE WHEN i.id IS NULL THEN NULL
             ELSE JSON_MODIFY((SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES), '$.version', NULL) END AS nuevos
    ) c
    WHERE c.accion <> 'UPDATE' OR c.anteriores <> c.nuevos;
END;
GO
