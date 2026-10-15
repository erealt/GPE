
USE GPE;
GO
 
/* ============================================================
   1) TABLAS BASE
   ============================================================ */
 
CREATE TABLE FABRICA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL UNIQUE
);
 
CREATE TABLE ROL (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(50) NOT NULL UNIQUE
);
 
CREATE TABLE USUARIO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL,
    email NVARCHAR(150) NOT NULL UNIQUE
);
 
CREATE TABLE CLIENTE (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL
);
 
CREATE TABLE PRODUCTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    codigo NVARCHAR(30) NOT NULL UNIQUE,
    nombre NVARCHAR(100) NOT NULL
);
 
CREATE TABLE USUARIO_ROL (
    usuario_id INT NOT NULL,
    rol_id INT NOT NULL,
    fabrica_id INT NOT NULL,
    CONSTRAINT PK_usuario_rol PRIMARY KEY (usuario_id, rol_id, fabrica_id),
    CONSTRAINT FK_usuario_rol_usuario FOREIGN KEY (usuario_id) REFERENCES USUARIO(id),
    CONSTRAINT FK_usuario_rol_rol     FOREIGN KEY (rol_id)     REFERENCES ROL(id),
    CONSTRAINT FK_usuario_rol_fabrica FOREIGN KEY (fabrica_id) REFERENCES FABRICA(id)
);
 
CREATE TABLE LOTE (
    id INT IDENTITY(1,1) PRIMARY KEY,
    producto_id INT NOT NULL,
    fabrica_id INT NOT NULL,
    codigo NVARCHAR(20) NOT NULL,
    caducidad DATE NOT NULL,
    CONSTRAINT FK_lote_producto FOREIGN KEY (producto_id) REFERENCES PRODUCTO(id),
    CONSTRAINT FK_lote_fabrica  FOREIGN KEY (fabrica_id)  REFERENCES FABRICA(id),
    CONSTRAINT UQ_lote_producto_codigo UNIQUE (producto_id, codigo)
);
 
/* ============================================================
   2) DEVOLUCIONES
   ============================================================ */
 
-- Estados del proceso; "fase" (1-6) alimenta la barra de progreso
CREATE TABLE ESTADO_DEVOLUCION (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(60) NOT NULL UNIQUE,
    fase INT NOT NULL UNIQUE,
    rol_responsable_id INT NULL,
    CONSTRAINT FK_estado_rol FOREIGN KEY (rol_responsable_id) REFERENCES ROL(id)
);
 
-- Cabecera de la devolución
CREATE TABLE DEVOLUCION (
    id INT IDENTITY(1,1) PRIMARY KEY,
    -- Columna calculada: DEV-2026-001, DEV-2026-002...
    numero AS ('DEV-' + CAST(YEAR(fecha_solicitud) AS NVARCHAR(4)) + '-'
               + RIGHT('000' + CAST(id AS NVARCHAR(10)), 3)) PERSISTED,
    fabrica_id INT NOT NULL,
    cliente_id INT NOT NULL,
    estado_id INT NOT NULL,
    albaran NVARCHAR(30) NULL,
    motivo NVARCHAR(50) NOT NULL,
    observaciones NVARCHAR(MAX) NULL,
    fecha_solicitud DATE NOT NULL,
    fecha_recogida DATE NULL,
    fecha_llegada DATE NULL,
    CONSTRAINT FK_devolucion_fabrica FOREIGN KEY (fabrica_id) REFERENCES FABRICA(id),
    CONSTRAINT FK_devolucion_cliente FOREIGN KEY (cliente_id) REFERENCES CLIENTE(id),
    CONSTRAINT FK_devolucion_estado  FOREIGN KEY (estado_id)  REFERENCES ESTADO_DEVOLUCION(id)
);
 
-- Líneas de producto de cada devolución (el lote es texto libre)
CREATE TABLE DEVOLUCION_PRODUCTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    devolucion_id INT NOT NULL,
    producto_id INT NOT NULL,
    lote NVARCHAR(30) NULL,
    cantidad_esperada INT NOT NULL CHECK (cantidad_esperada > 0),
    cantidad_recibida INT NULL CHECK (cantidad_recibida >= 0),  -- la rellena el almacén
    CONSTRAINT FK_devprod_devolucion FOREIGN KEY (devolucion_id)
        REFERENCES DEVOLUCION(id) ON DELETE CASCADE,
    CONSTRAINT FK_devprod_producto FOREIGN KEY (producto_id) REFERENCES PRODUCTO(id)
);
GO

-- Transportistas que pueden hacer las recogidas
CREATE TABLE TRANSPORTISTA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL UNIQUE,
    telefono NVARCHAR(30) NULL,
    activo BIT NOT NULL CONSTRAINT DF_transportista_activo DEFAULT 1
);
 
-- Recogida acordada para una devolución
CREATE TABLE RECOGIDA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    devolucion_id INT NOT NULL,
    transportista_id INT NOT NULL,
    fecha_recogida DATE NOT NULL,
    direccion NVARCHAR(200) NOT NULL,
    telefono_contacto NVARCHAR(30) NULL,
    observaciones NVARCHAR(MAX) NULL,
    creada_en DATETIME2 NOT NULL CONSTRAINT DF_recogida_creada DEFAULT SYSDATETIME(),
    CONSTRAINT FK_recogida_devolucion FOREIGN KEY (devolucion_id)
        REFERENCES DEVOLUCION(id) ON DELETE CASCADE,
    CONSTRAINT FK_recogida_transportista FOREIGN KEY (transportista_id)
        REFERENCES TRANSPORTISTA(id)
);
GO

/* ============================================================
   3) HISTORIAL DE CAMBIOS
   ============================================================ */

-- Quién modificó qué y cuándo. Lo rellenan los triggers de abajo.
CREATE TABLE HISTORIAL_CAMBIOS (
    id INT IDENTITY(1,1) PRIMARY KEY,

    devolucion_id INT NULL,
    usuario_id INT NOT NULL,

    tabla_afectada NVARCHAR(100) NOT NULL,
    registro_id INT NOT NULL,
    accion NVARCHAR(20) NOT NULL,

    -- Los cambios de una misma transacción (un mismo "Guardar") comparten este id;
    -- la pantalla de historial los agrupa en una sola entrada.
    transaccion_id BIGINT NULL,

    fecha DATETIME2 NOT NULL
        CONSTRAINT DF_historial_fecha DEFAULT SYSDATETIME(),

    valores_anteriores NVARCHAR(MAX) NULL,
    valores_nuevos NVARCHAR(MAX) NULL,

    CONSTRAINT FK_historial_usuario
        FOREIGN KEY (usuario_id) REFERENCES USUARIO(id),

    -- devolucion_id no lleva FK a propósito: así se conserva el historial
    -- aunque la devolución se borre.

    CONSTRAINT CK_historial_accion
        CHECK (accion IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IX_historial_devolucion ON HISTORIAL_CAMBIOS (devolucion_id, fecha);
GO

/* La aplicación indica el usuario al abrir cada conexión con:
       EXEC sp_set_session_context @key = N'usuario_id', @value = <id>;
   Para modificar datos a mano desde SSMS hay que ejecutar esa misma línea antes.

   Los UPDATE que no cambian ningún valor no se registran. */

CREATE OR ALTER TRIGGER TR_DEVOLUCION_HISTORIAL ON DEVOLUCION
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
    SELECT c.registro_id, @usuario, 'DEVOLUCION', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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

CREATE OR ALTER TRIGGER TR_DEVOLUCION_PRODUCTO_HISTORIAL ON DEVOLUCION_PRODUCTO
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
    SELECT c.devolucion_id, @usuario, 'DEVOLUCION_PRODUCTO', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
    FROM inserted i
    FULL OUTER JOIN deleted d ON d.id = i.id
    CROSS APPLY (SELECT
        COALESCE(i.id, d.id) AS registro_id,
        COALESCE(i.devolucion_id, d.devolucion_id) AS devolucion_id,
        CASE WHEN i.id IS NULL THEN 'DELETE' WHEN d.id IS NULL THEN 'INSERT' ELSE 'UPDATE' END AS accion,
        CASE WHEN d.id IS NULL THEN NULL
             ELSE (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS anteriores,
        CASE WHEN i.id IS NULL THEN NULL
             ELSE (SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS nuevos
    ) c
    WHERE c.accion <> 'UPDATE' OR c.anteriores <> c.nuevos;
END;
GO

CREATE OR ALTER TRIGGER TR_RECOGIDA_HISTORIAL ON RECOGIDA
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
    SELECT c.devolucion_id, @usuario, 'RECOGIDA', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
    FROM inserted i
    FULL OUTER JOIN deleted d ON d.id = i.id
    CROSS APPLY (SELECT
        COALESCE(i.id, d.id) AS registro_id,
        COALESCE(i.devolucion_id, d.devolucion_id) AS devolucion_id,
        CASE WHEN i.id IS NULL THEN 'DELETE' WHEN d.id IS NULL THEN 'INSERT' ELSE 'UPDATE' END AS accion,
        CASE WHEN d.id IS NULL THEN NULL
             ELSE (SELECT d.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS anteriores,
        CASE WHEN i.id IS NULL THEN NULL
             ELSE (SELECT i.* FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS nuevos
    ) c
    WHERE c.accion <> 'UPDATE' OR c.anteriores <> c.nuevos;
END;
GO
