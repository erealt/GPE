
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