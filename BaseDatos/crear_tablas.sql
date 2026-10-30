
USE GPE;
GO
 
-- Tablas base
 
CREATE TABLE FABRICA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL UNIQUE
);
 
-- LOTE aún no se usa: hoy DEVOLUCION_PRODUCTO.lote es texto libre.
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
    codigo NVARCHAR(20) NOT NULL CONSTRAINT UQ_cliente_codigo UNIQUE,
    nombre NVARCHAR(100) NOT NULL
);
 
CREATE TABLE PRODUCTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    codigo NVARCHAR(30) NOT NULL UNIQUE,
    nombre NVARCHAR(100) NOT NULL,
    formato NVARCHAR(50) NULL   -- presentación, p. ej. "Caja 12 × 500 g"
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
 
-- Devoluciones
 
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
    -- DEV-2026-001...; desde la 1000 lleva todas las cifras para no repetir números.
    numero AS ('DEV-' + CAST(YEAR(fecha_solicitud) AS NVARCHAR(4)) + '-'
               + CASE WHEN id < 1000 THEN RIGHT('000' + CAST(id AS NVARCHAR(10)), 3)
                      ELSE CAST(id AS NVARCHAR(10)) END) PERSISTED
               CONSTRAINT UQ_devolucion_numero UNIQUE,
    fabrica_id INT NOT NULL,
    cliente_id INT NOT NULL,
    estado_id INT NOT NULL,
    motivo NVARCHAR(50) NOT NULL,
    observaciones NVARCHAR(MAX) NULL,
    fecha_solicitud DATE NOT NULL,
    fecha_recogida DATE NULL,                    -- fecha de recogida prevista
    fecha_llegada DATE NULL,
    -- Revisión del almacén
    revisada_por NVARCHAR(100) NULL,             -- quién revisó la mercancía (puede no ser usuario de GPE)
    agencia NVARCHAR(100) NULL,                  -- agencia que trajo la mercancía
    observaciones_revision NVARCHAR(MAX) NULL,   -- observaciones generales del almacén
    motivo_reapertura NVARCHAR(500) NULL,        -- motivo de la última vez que se volvió a un paso anterior
    -- Abono y cierre (se rellenan juntos al registrar el abono)
    resultado_abono NVARCHAR(10) NULL,           -- TOTAL, PARCIAL o NINGUNO
    numero_abono NVARCHAR(30) NULL,              -- nº del abono / factura rectificativa
    fecha_abono DATE NULL,
    importe_abono DECIMAL(10,2) NULL,
    observaciones_abono NVARCHAR(MAX) NULL,
    fecha_cierre DATE NULL,
    CONSTRAINT CK_devolucion_resultado_abono CHECK (resultado_abono IN ('TOTAL', 'PARCIAL', 'NINGUNO')),
    CONSTRAINT CK_devolucion_importe_abono CHECK (importe_abono >= 0),
    CONSTRAINT FK_devolucion_fabrica FOREIGN KEY (fabrica_id) REFERENCES FABRICA(id),
    CONSTRAINT FK_devolucion_cliente FOREIGN KEY (cliente_id) REFERENCES CLIENTE(id),
    CONSTRAINT FK_devolucion_estado  FOREIGN KEY (estado_id)  REFERENCES ESTADO_DEVOLUCION(id)
);
 
-- Productos que el cliente dijo que iba a devolver (opcional; el lote es texto libre)
CREATE TABLE DEVOLUCION_PRODUCTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    devolucion_id INT NOT NULL,
    producto_id INT NOT NULL,
    lote NVARCHAR(30) NULL,
    cantidad_esperada INT NOT NULL CHECK (cantidad_esperada > 0),
    CONSTRAINT FK_devprod_devolucion FOREIGN KEY (devolucion_id)
        REFERENCES DEVOLUCION(id) ON DELETE CASCADE,
    CONSTRAINT FK_devprod_producto FOREIGN KEY (producto_id) REFERENCES PRODUCTO(id)
);

-- Revisión del almacén. Si parte de un producto está bien y parte no, van en dos líneas.
CREATE TABLE REVISION_LINEA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    devolucion_id INT NOT NULL,
    producto_id INT NOT NULL,
    lote NVARCHAR(30) NULL,
    caducidad DATE NULL,
    cantidad INT NOT NULL CONSTRAINT CK_revlinea_cantidad CHECK (cantidad > 0),
    apto BIT NOT NULL,   -- 1 = está bien para servir
    CONSTRAINT FK_revlinea_devolucion FOREIGN KEY (devolucion_id)
        REFERENCES DEVOLUCION(id) ON DELETE CASCADE,
    CONSTRAINT FK_revlinea_producto FOREIGN KEY (producto_id) REFERENCES PRODUCTO(id)
);

CREATE INDEX IX_revlinea_devolucion ON REVISION_LINEA (devolucion_id);
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
    fecha_recogida DATE NOT NULL,                -- fecha de recogida prevista
    direccion NVARCHAR(200) NOT NULL,
    telefono_contacto NVARCHAR(30) NULL,
    observaciones NVARCHAR(MAX) NULL,
    -- Cuándo se registró el transportista (se rellena sola al crear la recogida)
    creada_en DATETIME2 NOT NULL CONSTRAINT DF_recogida_creada DEFAULT SYSDATETIME(),
    CONSTRAINT FK_recogida_devolucion FOREIGN KEY (devolucion_id)
        REFERENCES DEVOLUCION(id) ON DELETE CASCADE,
    CONSTRAINT FK_recogida_transportista FOREIGN KEY (transportista_id)
        REFERENCES TRANSPORTISTA(id)
);

-- Archivos de una devolución: recibo de la agencia, fotos de la revisión y documento del abono
CREATE TABLE ADJUNTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    devolucion_id INT NOT NULL,
    tipo NVARCHAR(10) NOT NULL,
    nombre_archivo NVARCHAR(260) NOT NULL,
    tamano_bytes INT NOT NULL,
    contenido VARBINARY(MAX) NOT NULL,
    usuario_id INT NOT NULL,
    subido_en DATETIME2 NOT NULL CONSTRAINT DF_adjunto_subido DEFAULT SYSDATETIME(),
    CONSTRAINT CK_adjunto_tipo CHECK (tipo IN ('FOTO', 'ABONO', 'RECIBO')),
    CONSTRAINT FK_adjunto_devolucion FOREIGN KEY (devolucion_id)
        REFERENCES DEVOLUCION(id) ON DELETE CASCADE,
    CONSTRAINT FK_adjunto_usuario FOREIGN KEY (usuario_id) REFERENCES USUARIO(id)
);

CREATE INDEX IX_adjunto_devolucion ON ADJUNTO (devolucion_id, tipo);
GO

-- Avisos pendientes: se apuntan con el cambio y se envían después; si el correo falla, se reintenta.
CREATE TABLE AVISO_CORREO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    devolucion_id INT NOT NULL,
    tipo NVARCHAR(30) NOT NULL,
    destinatario NVARCHAR(150) NOT NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_aviso_creado DEFAULT SYSDATETIME(),
    enviado_en DATETIME2 NULL,
    intentos INT NOT NULL CONSTRAINT DF_aviso_intentos DEFAULT 0,
    ultimo_error NVARCHAR(1000) NULL,
    bloqueado_hasta DATETIME2 NULL,   -- mientras un ordenador lo está enviando
    CONSTRAINT CK_aviso_tipo CHECK (tipo IN ('NUEVA_DEVOLUCION', 'RECOGIDA_SOLICITADA', 'REVISION_FINALIZADA', 'ABONO_REGISTRADO')),
    CONSTRAINT FK_aviso_devolucion FOREIGN KEY (devolucion_id)
        REFERENCES DEVOLUCION(id) ON DELETE CASCADE
);

CREATE INDEX IX_aviso_pendiente ON AVISO_CORREO (enviado_en, id);

-- Resúmenes ya enviados, para no repetirlos si la tarea se ejecuta dos veces el mismo día
CREATE TABLE AVISO_RESUMEN (
    fecha DATE NOT NULL,
    tipo NVARCHAR(30) NOT NULL,
    destinatario NVARCHAR(150) NOT NULL,
    enviado_en DATETIME2 NOT NULL CONSTRAINT DF_resumen_enviado DEFAULT SYSDATETIME(),
    CONSTRAINT PK_aviso_resumen PRIMARY KEY (fecha, tipo, destinatario)
);
GO

-- Historial de cambios

-- Quién modificó qué y cuándo. Lo rellenan los triggers de abajo.
CREATE TABLE HISTORIAL_CAMBIOS (
    id INT IDENTITY(1,1) PRIMARY KEY,

    devolucion_id INT NULL,
    usuario_id INT NOT NULL,

    tabla_afectada NVARCHAR(100) NOT NULL,
    registro_id INT NOT NULL,
    accion NVARCHAR(20) NOT NULL,

    -- Los cambios de un mismo "Guardar" comparten id y salen juntos en el historial.
    transaccion_id BIGINT NULL,

    fecha DATETIME2 NOT NULL
        CONSTRAINT DF_historial_fecha DEFAULT SYSDATETIME(),

    valores_anteriores NVARCHAR(MAX) NULL,
    valores_nuevos NVARCHAR(MAX) NULL,

    CONSTRAINT FK_historial_usuario
        FOREIGN KEY (usuario_id) REFERENCES USUARIO(id),

    -- Sin FK a propósito: el historial se conserva aunque se borre la devolución.

    CONSTRAINT CK_historial_accion
        CHECK (accion IN ('INSERT', 'UPDATE', 'DELETE'))
);

CREATE INDEX IX_historial_devolucion ON HISTORIAL_CAMBIOS (devolucion_id, fecha);
GO

-- Para cambiar datos a mano desde SSMS, antes: EXEC sp_set_session_context @key = N'usuario_id', @value = <id>;
-- Los UPDATE que no cambian nada no se registran.

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

CREATE OR ALTER TRIGGER TR_REVISION_LINEA_HISTORIAL ON REVISION_LINEA
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
    SELECT c.devolucion_id, @usuario, 'REVISION_LINEA', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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

-- Sin "d.*", para no copiar el contenido del archivo al historial.
CREATE OR ALTER TRIGGER TR_ADJUNTO_HISTORIAL ON ADJUNTO
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
    SELECT c.devolucion_id, @usuario, 'ADJUNTO', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
    FROM inserted i
    FULL OUTER JOIN deleted d ON d.id = i.id
    CROSS APPLY (SELECT
        COALESCE(i.id, d.id) AS registro_id,
        COALESCE(i.devolucion_id, d.devolucion_id) AS devolucion_id,
        CASE WHEN i.id IS NULL THEN 'DELETE' WHEN d.id IS NULL THEN 'INSERT' ELSE 'UPDATE' END AS accion,
        CASE WHEN d.id IS NULL THEN NULL
             ELSE (SELECT d.id, d.devolucion_id, d.tipo, d.nombre_archivo, d.tamano_bytes
                   FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS anteriores,
        CASE WHEN i.id IS NULL THEN NULL
             ELSE (SELECT i.id, i.devolucion_id, i.tipo, i.nombre_archivo, i.tamano_bytes
                   FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES) END AS nuevos
    ) c
    WHERE c.accion <> 'UPDATE' OR c.anteriores <> c.nuevos;
END;
GO

-- Datos iniciales imprescindibles: fases, fábrica 1 y roles.

INSERT INTO FABRICA (nombre) VALUES (N'Fábrica Principal');

-- Calidad no interviene en las devoluciones, solo en no conformidades y rutina.
INSERT INTO ROL (nombre) VALUES (N'Administración'), (N'Transporte'), (N'Almacén'), (N'Calidad');

-- rol_responsable_id: quién hace el paso que lleva a esa fase (recibe los avisos por correo)
INSERT INTO ESTADO_DEVOLUCION (nombre, fase, rol_responsable_id) VALUES
    (N'Solicitud recibida',  1, (SELECT id FROM ROL WHERE nombre = N'Administración')),
    (N'Recogida solicitada', 2, (SELECT id FROM ROL WHERE nombre = N'Transporte')),
    (N'Mercancía recibida',  3, (SELECT id FROM ROL WHERE nombre = N'Almacén')),
    (N'Revisada almacén',    4, (SELECT id FROM ROL WHERE nombre = N'Almacén')),
    (N'Abono realizado',     5, (SELECT id FROM ROL WHERE nombre = N'Administración')),
    (N'Cierre',              6, (SELECT id FROM ROL WHERE nombre = N'Administración'));
GO
