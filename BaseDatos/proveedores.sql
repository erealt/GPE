USE GPE;
GO

-- Evaluación de proveedores y fraude (Excel CP-RG05, hoja PRODUCTOS). Se ejecuta una vez, después de crear_tablas.sql.
-- Con sqlcmd, usar -f 65001 (UTF-8, por las tildes).
-- En el Excel cada fila es un proveedor con uno de sus productos: aquí el proveedor va una vez y sus productos aparte.
-- Las notas (A/B/C, muestreo, riesgos y actuación) no se guardan: las calcula ProveedorServicio como el Excel.

-- Columnas A-N de la hoja, más HISTORIAL y P. SUBCONTRATADO (riesgo del proveedor para cometer fraude).
CREATE TABLE PROVEEDOR (
    id INT IDENTITY(1,1) PRIMARY KEY,
    numero INT NULL,                              -- Nº del Excel
    nombre NVARCHAR(200) NOT NULL,
    fecha_alta DATE NULL,
    baja BIT NOT NULL CONSTRAINT DF_proveedor_baja DEFAULT 0,
    tipo NVARCHAR(30) NULL,                       -- MMPP, PROD. ACABADO, CONT. DIRECTO, CONT. INDIRECTO, DISTRIBUIDOR
    fecha_revision DATE NULL,
    impacto TINYINT NULL,
    estatus TINYINT NULL,
    reclamaciones TINYINT NULL,
    -- Texto: en el Excel se mezclan fechas y explicaciones ("BRC: 12/04/27", "NA", "No se realiza por confianza...")
    certificados NVARCHAR(500) NULL,
    ensayo_trazabilidad NVARCHAR(500) NULL,
    auditoria NVARCHAR(500) NULL,
    rfa NVARCHAR(500) NULL,
    -- RELACIÓN: en el Excel es el estatus, salvo en los proveedores donde se escribió otro valor a mano.
    -- NULL = igual que el estatus.
    relacion TINYINT NULL,
    historial TINYINT NULL,
    subcontratado TINYINT NULL,
    -- Al pasar el Excel: los datos que eran distintos según la fila, para que calidad decida cuál es el bueno
    revisar NVARCHAR(MAX) NULL,
    creado_por INT NOT NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_proveedor_creado DEFAULT SYSDATETIME(),
    version ROWVERSION NOT NULL,
    CONSTRAINT CK_proveedor_impacto CHECK (impacto BETWEEN 1 AND 3),
    CONSTRAINT CK_proveedor_estatus CHECK (estatus BETWEEN 0 AND 3),
    CONSTRAINT CK_proveedor_reclamaciones CHECK (reclamaciones BETWEEN 0 AND 2),
    CONSTRAINT CK_proveedor_relacion CHECK (relacion BETWEEN 0 AND 3),
    CONSTRAINT CK_proveedor_historial CHECK (historial BETWEEN 0 AND 3),
    CONSTRAINT CK_proveedor_subcontratado CHECK (subcontratado BETWEEN 0 AND 3),
    CONSTRAINT FK_proveedor_usuario FOREIGN KEY (creado_por) REFERENCES USUARIO(id)
);

-- Columnas O-AY de la hoja, sin las calculadas. Se llama así porque PRODUCTO ya es el de las devoluciones.
CREATE TABLE PROVEEDOR_PRODUCTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    proveedor_id INT NOT NULL,
    codigo NVARCHAR(30) NULL,                     -- sin UNIQUE: el mismo código puede venir de dos proveedores
    descripcion NVARCHAR(300) NULL,
    tipo_producto NVARCHAR(30) NULL,              -- MMPP, M. C. DIRECTO, M. C. INDIRECTO, PRODUCTO FINAL
    fecha_alta DATE NULL,
    baja BIT NOT NULL CONSTRAINT DF_proveedor_producto_baja DEFAULT 0,
    alergenos NVARCHAR(300) NULL,
    posibles_alergenos NVARCHAR(300) NULL,
    fecha_revision DATE NULL,
    -- Texto: año, fecha o "BOP 01/22"
    ficha_tecnica NVARCHAR(50) NULL,
    analisis_quimicos NVARCHAR(500) NULL,         -- o declaración de conformidad
    analisis_micro NVARCHAR(500) NULL,            -- o ensayo de migración
    analisis_alergenos NVARCHAR(500) NULL,        -- o formaldehído
    eurochoc_fecha DATE NULL,
    eurochoc_alcance NVARCHAR(500) NULL,
    eurochoc_resultado NVARCHAR(1000) NULL,
    -- IMPACTO DEL PRODUCTO: en el Excel es el del proveedor, salvo en los productos donde se escribió otro a mano.
    -- NULL = igual que el del proveedor.
    impacto TINYINT NULL,
    control_recepcion TINYINT NULL,
    reclamaciones TINYINT NULL,
    parametro NVARCHAR(1000) NULL,
    -- Vulnerabilidad al fraude: probabilidad de ocurrencia (P.) y de detección (D.)
    p_historial TINYINT NULL,
    p_motivacion TINYINT NULL,
    p_facilidad TINYINT NULL,
    d_origen TINYINT NULL,
    d_cadena TINYINT NULL,
    d_medidas TINYINT NULL,
    medidas_preventivas NVARCHAR(1000) NULL,
    -- Lo que en el Excel no cabía en su columna ("1 UE Y CHINA" en D. ORIGEN, fechas mal escritas...)
    observaciones NVARCHAR(MAX) NULL,
    creado_por INT NOT NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_proveedor_producto_creado DEFAULT SYSDATETIME(),
    version ROWVERSION NOT NULL,
    CONSTRAINT CK_proveedor_producto_impacto CHECK (impacto BETWEEN 1 AND 3),
    CONSTRAINT CK_proveedor_producto_control CHECK (control_recepcion BETWEEN 1 AND 3),
    CONSTRAINT CK_proveedor_producto_reclamaciones CHECK (reclamaciones BETWEEN 0 AND 2),
    CONSTRAINT CK_proveedor_producto_fraude CHECK (
        p_historial BETWEEN 0 AND 3 AND p_motivacion BETWEEN 0 AND 3 AND p_facilidad BETWEEN 0 AND 3
        AND d_origen BETWEEN 0 AND 3 AND d_cadena BETWEEN 0 AND 3 AND d_medidas BETWEEN 0 AND 3),
    CONSTRAINT FK_proveedor_producto_proveedor FOREIGN KEY (proveedor_id) REFERENCES PROVEEDOR(id),
    CONSTRAINT FK_proveedor_producto_usuario FOREIGN KEY (creado_por) REFERENCES USUARIO(id)
);
CREATE INDEX IX_proveedor_producto_proveedor ON PROVEEDOR_PRODUCTO (proveedor_id);
GO

-- Historial: igual que TR_AGUA_CONTROL_HISTORIAL
CREATE OR ALTER TRIGGER TR_PROVEEDOR_HISTORIAL ON PROVEEDOR
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
    SELECT NULL, @usuario, 'PROVEEDOR', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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

CREATE OR ALTER TRIGGER TR_PROVEEDOR_PRODUCTO_HISTORIAL ON PROVEEDOR_PRODUCTO
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
    SELECT NULL, @usuario, 'PROVEEDOR_PRODUCTO', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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
