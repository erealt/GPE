USE GPE;
GO

-- Varias revisiones por día en la rutina. Solo para bases creadas antes con rutina_calidad.sql.
-- Se puede ejecutar más de una vez; lo ya guardado pasa a ser la revisión 1.

-- Usuario que figura en el historial por las revisiones 1 que se crean aquí
EXEC sp_set_session_context @key = N'usuario_id', @value = 1;
GO

IF OBJECT_ID('RUTINA_REVISION') IS NULL
    CREATE TABLE RUTINA_REVISION (
        id INT IDENTITY(1,1) PRIMARY KEY,
        rutina_id INT NOT NULL,
        numero INT NOT NULL,
        hora TIME(0) NOT NULL,
        creado_por INT NOT NULL,
        CONSTRAINT UQ_rutina_revision UNIQUE (rutina_id, numero),
        CONSTRAINT CK_rutina_revision_numero CHECK (numero >= 1),
        CONSTRAINT FK_rutina_revision_rutina FOREIGN KEY (rutina_id) REFERENCES RUTINA(id) ON DELETE CASCADE,
        CONSTRAINT FK_rutina_revision_usuario FOREIGN KEY (creado_por) REFERENCES USUARIO(id)
    );
GO

CREATE OR ALTER TRIGGER TR_RUTINA_REVISION_HISTORIAL ON RUTINA_REVISION
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
    SELECT NULL, @usuario, 'RUTINA_REVISION', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
    FROM inserted i
    FULL OUTER JOIN deleted d ON d.id = i.id
    CROSS APPLY (SELECT
        COALESCE(i.id, d.id) AS registro_id,
        CASE WHEN i.id IS NULL THEN 'DELETE' WHEN d.id IS NULL THEN 'INSERT' ELSE 'UPDATE' END AS accion,
        CASE WHEN d.id IS NULL THEN NULL
             ELSE (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS anteriores,
        CASE WHEN i.id IS NULL THEN NULL
             ELSE (SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS nuevos
    ) c
    WHERE c.accion <> 'UPDATE' OR c.anteriores <> c.nuevos;
END;
GO

IF COL_LENGTH('RUTINA_RESULTADO', 'revision') IS NULL
    ALTER TABLE RUTINA_RESULTADO
        ADD revision INT NOT NULL CONSTRAINT DF_rutina_resultado_revision DEFAULT 1;
GO

-- Las rondas ya guardadas tienen una sola revisión, empezada cuando se creó la ronda
INSERT INTO RUTINA_REVISION (rutina_id, numero, hora, creado_por)
SELECT r.id, 1, CAST(r.creado_en AS TIME(0)), r.creado_por
FROM RUTINA r
WHERE NOT EXISTS (SELECT 1 FROM RUTINA_REVISION v WHERE v.rutina_id = r.id AND v.numero = 1);
GO

-- La misma casilla puede estar una vez en cada revisión
IF NOT EXISTS (SELECT 1 FROM sys.index_columns ic
               JOIN sys.indexes ix ON ix.object_id = ic.object_id AND ix.index_id = ic.index_id
               WHERE ix.name = 'UQ_rutina_resultado' AND COL_NAME(ic.object_id, ic.column_id) = 'revision')
BEGIN
    ALTER TABLE RUTINA_RESULTADO DROP CONSTRAINT UQ_rutina_resultado;
    ALTER TABLE RUTINA_RESULTADO ADD CONSTRAINT UQ_rutina_resultado UNIQUE (rutina_id, revision, punto_id, seccion_id);
END;
GO

IF OBJECT_ID('FK_rutina_resultado_revision') IS NULL
    ALTER TABLE RUTINA_RESULTADO ADD CONSTRAINT FK_rutina_resultado_revision
        FOREIGN KEY (rutina_id, revision) REFERENCES RUTINA_REVISION(rutina_id, numero);
GO
