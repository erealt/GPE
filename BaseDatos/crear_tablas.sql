
USE GPE;
GO
 
/* ============================================================
   1) TABLAS BASE
   ============================================================ */
 
CREATE TABLE FABRICA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL UNIQUE
);
 
-- ROL, USUARIO_ROL y LOTE todavía no los usa la aplicación: están preparadas para
-- cuando haya login con permisos por rol y fábrica, y para elegir el lote de una
-- lista en vez de escribirlo (hoy DEVOLUCION_PRODUCTO.lote es texto libre).
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
    -- Columna calculada: DEV-2026-001, DEV-2026-002... A partir de la 1000 se usan
    -- todas las cifras (DEV-2026-1000) para que no se repitan números.
    numero AS ('DEV-' + CAST(YEAR(fecha_solicitud) AS NVARCHAR(4)) + '-'
               + CASE WHEN id < 1000 THEN RIGHT('000' + CAST(id AS NVARCHAR(10)), 3)
                      ELSE CAST(id AS NVARCHAR(10)) END) PERSISTED
               CONSTRAINT UQ_devolucion_numero UNIQUE,
    fabrica_id INT NOT NULL,
    cliente_id INT NOT NULL,
    estado_id INT NOT NULL,
    albaran NVARCHAR(30) NULL,
    motivo NVARCHAR(50) NOT NULL,
    observaciones NVARCHAR(MAX) NULL,
    fecha_solicitud DATE NOT NULL,
    fecha_recogida DATE NULL,
    fecha_llegada DATE NULL,
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
 
-- Líneas de producto de cada devolución (el lote es texto libre)
CREATE TABLE DEVOLUCION_PRODUCTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    devolucion_id INT NOT NULL,
    producto_id INT NOT NULL,
    lote NVARCHAR(30) NULL,
    cantidad_esperada INT NOT NULL CHECK (cantidad_esperada > 0),
    cantidad_recibida INT NULL CHECK (cantidad_recibida >= 0),  -- la rellena el almacén
    cantidad_apta INT NULL,             -- de lo recibido, lo que se puede volver a vender
    observaciones NVARCHAR(500) NULL,   -- por qué no es apto, daños, etc.
    CONSTRAINT CK_devprod_apta CHECK (cantidad_apta IS NULL
        OR (cantidad_apta >= 0 AND cantidad_recibida IS NOT NULL AND cantidad_apta <= cantidad_recibida)),
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

-- Archivos de una devolución: fotos de la revisión y documento del abono
CREATE TABLE ADJUNTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    devolucion_id INT NOT NULL,
    tipo NVARCHAR(10) NOT NULL,
    nombre_archivo NVARCHAR(260) NOT NULL,
    tamano_bytes INT NOT NULL,
    contenido VARBINARY(MAX) NOT NULL,
    usuario_id INT NOT NULL,
    subido_en DATETIME2 NOT NULL CONSTRAINT DF_adjunto_subido DEFAULT SYSDATETIME(),
    CONSTRAINT CK_adjunto_tipo CHECK (tipo IN ('FOTO', 'ABONO')),
    CONSTRAINT FK_adjunto_devolucion FOREIGN KEY (devolucion_id)
        REFERENCES DEVOLUCION(id) ON DELETE CASCADE,
    CONSTRAINT FK_adjunto_usuario FOREIGN KEY (usuario_id) REFERENCES USUARIO(id)
);

CREATE INDEX IX_adjunto_devolucion ON ADJUNTO (devolucion_id, tipo);
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

-- A diferencia de los otros, no usa "d.*": así el contenido del archivo
-- (columna contenido) no se copia al historial.
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

/* ============================================================
   4) DeCA (documento de control del transporte)
   ============================================================
   Módulo aparte de las devoluciones. Si la base de datos ya existe,
   basta con ejecutar esta sección (si tiene la tabla DECA del primer
   intento, antes deca_quitar_tabla_antigua.sql). */

-- Un documento por envío: remitente, destinatario, transporte y mercancía; el peso y
-- las cajas van por palet en DECA_PALET. Remitente, destinatario y transportista se
-- escriben a mano; la aplicación sugiere los ya usados en otros DeCA (no hay catálogo aparte).
CREATE TABLE DECA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    -- Columna calculada: DECA-2026-001, DECA-2026-002... (como el número de devolución)
    numero AS ('DECA-' + CAST(YEAR(fecha_carga) AS NVARCHAR(4)) + '-'
               + CASE WHEN id < 1000 THEN RIGHT('000' + CAST(id AS NVARCHAR(10)), 3)
                      ELSE CAST(id AS NVARCHAR(10)) END) PERSISTED
               CONSTRAINT UQ_deca_numero UNIQUE,
    -- Parte aleatoria del nombre del PDF publicado, para que no se pueda adivinar
    -- la dirección de otros documentos probando números
    codigo_publico UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_deca_codigo_publico DEFAULT NEWID()
        CONSTRAINT UQ_deca_codigo_publico UNIQUE,
    -- Remitente
    remitente_nombre NVARCHAR(150) NOT NULL,
    remitente_nif NVARCHAR(20) NOT NULL,
    remitente_direccion NVARCHAR(200) NOT NULL,
    remitente_cp NVARCHAR(10) NOT NULL,
    remitente_poblacion NVARCHAR(100) NOT NULL,
    remitente_provincia NVARCHAR(100) NULL,
    remitente_pais NVARCHAR(60) NOT NULL CONSTRAINT DF_deca_remitente_pais DEFAULT N'España',
    remitente_telefono NVARCHAR(30) NULL,
    -- Destinatario
    destinatario_nombre NVARCHAR(150) NOT NULL,
    destinatario_nif NVARCHAR(20) NOT NULL,
    destinatario_direccion NVARCHAR(200) NOT NULL,
    destinatario_cp NVARCHAR(10) NOT NULL,
    destinatario_poblacion NVARCHAR(100) NOT NULL,
    destinatario_provincia NVARCHAR(100) NULL,
    destinatario_pais NVARCHAR(60) NOT NULL CONSTRAINT DF_deca_destinatario_pais DEFAULT N'España',
    destinatario_telefono NVARCHAR(30) NULL,
    -- Transporte (el catálogo TRANSPORTISTA de las recogidas no tiene NIF, por eso es texto)
    fecha_carga DATE NOT NULL,
    transportista_nombre NVARCHAR(150) NOT NULL,
    transportista_nif NVARCHAR(20) NOT NULL,
    matricula NVARCHAR(20) NOT NULL,
    mercancia NVARCHAR(250) NOT NULL,   -- naturaleza de la mercancía
    observaciones NVARCHAR(MAX) NULL,
    -- PDF publicado (nombre del archivo en la carpeta de publicación); NULL si aún no se ha generado
    archivo_pdf NVARCHAR(260) NULL,
    pdf_generado_en DATETIME2 NULL,
    -- Quién lo creó y quién lo cambió por última vez
    creado_por INT NOT NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_deca_creado DEFAULT SYSDATETIME(),
    modificado_por INT NULL,
    modificado_en DATETIME2 NULL,
    CONSTRAINT FK_deca_creado_por     FOREIGN KEY (creado_por)       REFERENCES USUARIO(id),
    CONSTRAINT FK_deca_modificado_por FOREIGN KEY (modificado_por)   REFERENCES USUARIO(id)
);

-- Palets de la carga: peso y cajas de cada uno. "orden" es el nº de palet (1, 2, 3...)
CREATE TABLE DECA_PALET (
    id INT IDENTITY(1,1) PRIMARY KEY,
    deca_id INT NOT NULL,
    orden INT NOT NULL,
    peso_kg DECIMAL(10,2) NOT NULL CONSTRAINT CK_deca_palet_peso CHECK (peso_kg > 0),
    num_cajas INT NOT NULL CONSTRAINT CK_deca_palet_cajas CHECK (num_cajas > 0),
    CONSTRAINT FK_deca_palet_deca FOREIGN KEY (deca_id) REFERENCES DECA(id) ON DELETE CASCADE,
    CONSTRAINT UQ_deca_palet_orden UNIQUE (deca_id, orden)
);
GO
