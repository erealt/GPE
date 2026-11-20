USE GPE;
GO

-- Plan de análisis de calidad (Excel PLAN DE ANALISIS 2026, hoja MICRO & FYQ). Una fila de la hoja = un análisis.
-- Se ejecuta después de no_conformidades.sql y se puede ejecutar más de una vez.
-- Con sqlcmd, usar -f 65001 (UTF-8, por las tildes).
-- Las listas de parámetro y detalle (hoja LISTAS) están en la aplicación (ListasAnalisis.cs).

IF OBJECT_ID('ANALISIS') IS NULL
    CREATE TABLE ANALISIS (
        id INT IDENTITY(1,1) PRIMARY KEY,
        tipo NVARCHAR(10) NOT NULL,                  -- PRODUCTO, MMPP o ENVUELTA
        descripcion NVARCHAR(300) NOT NULL,
        lote NVARCHAR(300) NULL,
        caducidad DATE NULL,                         -- si solo se sabe el mes ("abr-26"), el día 1
        analisis_totales NVARCHAR(500) NULL,
        parametro NVARCHAR(30) NULL,
        parametro_detalle NVARCHAR(100) NULL,
        fecha_analisis DATE NULL,
        resultado NVARCHAR(500) NULL,
        creado_por INT NOT NULL,
        creado_en DATETIME2 NOT NULL CONSTRAINT DF_analisis_creado DEFAULT SYSDATETIME(),
        -- Igual que en AGUA_CONTROL: al guardar se comprueba que nadie lo haya cambiado desde que se abrió
        version ROWVERSION NOT NULL,
        CONSTRAINT FK_analisis_usuario FOREIGN KEY (creado_por) REFERENCES USUARIO(id)
    );
GO

-- Los tipos de la columna TIPO del Excel (ENVUELTA = envases)
IF OBJECT_ID('CK_analisis_tipo') IS NOT NULL
    ALTER TABLE ANALISIS DROP CONSTRAINT CK_analisis_tipo;
ALTER TABLE ANALISIS ADD CONSTRAINT CK_analisis_tipo CHECK (tipo IN (N'PRODUCTO', N'MMPP', N'ENVUELTA'));
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_analisis_fecha')
    CREATE INDEX IX_analisis_fecha ON ANALISIS (fecha_analisis);
GO

-- Historial: igual que TR_AGUA_CONTROL_HISTORIAL
CREATE OR ALTER TRIGGER TR_ANALISIS_HISTORIAL ON ANALISIS
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
    SELECT NULL, @usuario, 'ANALISIS', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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
