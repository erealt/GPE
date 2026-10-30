USE GPE;
GO

-- Control de agua (hoja AGUA, MT-RG09). Se ejecuta una vez, después de no_conformidades.sql.
-- Con sqlcmd, usar -f 65001 (UTF-8, por las tildes).

-- cloro_min / cloro_max en ppm; en NULL no se comprueba (falta que calidad dé el rango).
CREATE TABLE AGUA_TOMA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL,
    cloro_min DECIMAL(4,2) NULL,
    cloro_max DECIMAL(4,2) NULL,
    orden INT NOT NULL,
    activo BIT NOT NULL CONSTRAINT DF_agua_toma_activo DEFAULT 1,
    CONSTRAINT UQ_agua_toma UNIQUE (nombre),
    CONSTRAINT CK_agua_toma_rango CHECK (cloro_min IS NULL OR cloro_max IS NULL OR cloro_min <= cloro_max)
);

-- Una fila de la hoja; un punto puede repetirse el mismo día. Organolépticos: 1 = Ok, 0 = no correcto.
CREATE TABLE AGUA_CONTROL (
    id INT IDENTITY(1,1) PRIMARY KEY,
    fecha DATE NOT NULL,
    toma_id INT NOT NULL,
    cloro DECIMAL(4,2) NULL,
    sabor_ok BIT NOT NULL CONSTRAINT DF_agua_control_sabor DEFAULT 1,
    olor_ok BIT NOT NULL CONSTRAINT DF_agua_control_olor DEFAULT 1,
    color_ok BIT NOT NULL CONSTRAINT DF_agua_control_color DEFAULT 1,
    turbidez_ok BIT NOT NULL CONSTRAINT DF_agua_control_turbidez DEFAULT 1,
    comentario NVARCHAR(1000) NULL,
    creado_por INT NOT NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_agua_control_creado DEFAULT SYSDATETIME(),
    -- Igual que en RUTINA: al guardar se comprueba que nadie lo haya cambiado desde que se abrió
    version ROWVERSION NOT NULL,
    CONSTRAINT CK_agua_control_cloro CHECK (cloro >= 0 AND cloro < 10),
    CONSTRAINT FK_agua_control_toma FOREIGN KEY (toma_id) REFERENCES AGUA_TOMA(id),
    CONSTRAINT FK_agua_control_usuario FOREIGN KEY (creado_por) REFERENCES USUARIO(id)
);
CREATE INDEX IX_agua_control_fecha ON AGUA_CONTROL (fecha);
GO

-- Historial: igual que TR_RUTINA_HISTORIAL (sin la versión en el JSON)
CREATE OR ALTER TRIGGER TR_AGUA_CONTROL_HISTORIAL ON AGUA_CONTROL
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
    SELECT NULL, @usuario, 'AGUA_CONTROL', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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

-- Los puntos de agua que salen en la hoja, sin rango de cloro hasta que calidad lo confirme
INSERT INTO AGUA_TOMA (nombre, orden) VALUES
    (N'PILA OBRADOR', 1),
    (N'PILA ZONA ENVASADO', 2),
    (N'FUENTE ZONA ENVASADO', 3),
    (N'LAVABO LROCAS', 4),
    (N'PILA OBRADOR LINEA 3', 5),
    (N'FUENTE LINEA 3', 6),
    (N'LAVABO VESTUARIO MASCULINO PLANTA BAJA', 7),
    (N'LAVABO VESTUARIO FEMENINO PLANTA BAJA', 8),
    (N'LAVABO VESTUARIO MASCULINO PLANTA ARRIBA', 9),
    (N'LAVABO VESTUARIO FEMENINO PLANTA ARRIBA', 10),
    (N'COMEDOR', 11);
GO
