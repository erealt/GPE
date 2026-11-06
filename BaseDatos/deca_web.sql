USE GPE;
GO

-- Tablas que necesita la web del DeCA y nada más. Las demás de GPE se instalarán cuando se junte
-- con el resto de la aplicación. Hay que ejecutarlo con sqlcmd -I, o desde instalar_base_datos.ps1.
-- Las definiciones son las de crear_tablas.sql: si se cambian allí, hay que cambiarlas aquí.

CREATE TABLE USUARIO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL,
    email NVARCHAR(150) NOT NULL UNIQUE
);

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
    -- Cargador contractual: quien contrata el transporte con el transportista (Orden FOM/2861/2012).
    -- Si es el remitente o el destinatario no se repiten sus datos; con OTRO van en cargador_*.
    cargador_tipo NVARCHAR(12) NOT NULL
        CONSTRAINT DF_deca_cargador_tipo DEFAULT N'REMITENTE'
        CONSTRAINT CK_deca_cargador_tipo CHECK (cargador_tipo IN (N'REMITENTE', N'DESTINATARIO', N'OTRO')),
    cargador_nombre NVARCHAR(150) NULL,
    cargador_nif NVARCHAR(20) NULL,
    cargador_direccion NVARCHAR(200) NULL,
    cargador_cp NVARCHAR(10) NULL,
    cargador_poblacion NVARCHAR(100) NULL,
    cargador_provincia NVARCHAR(100) NULL,
    cargador_pais NVARCHAR(60) NULL,
    cargador_telefono NVARCHAR(30) NULL,
    -- Transporte (el catálogo TRANSPORTISTA de las recogidas no tiene NIF, por eso es texto)
    fecha_carga DATE NOT NULL,
    transportista_nombre NVARCHAR(150) NOT NULL,   -- transportista efectivo
    transportista_nif NVARCHAR(20) NOT NULL,
    matricula NVARCHAR(20) NOT NULL,
    matricula_remolque NVARCHAR(20) NULL,   -- solo en conjuntos articulados
    -- Origen y destino, si no son las direcciones del remitente y del destinatario
    lugar_carga NVARCHAR(250) NULL,
    lugar_descarga NVARCHAR(250) NULL,
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
    CONSTRAINT FK_deca_modificado_por FOREIGN KEY (modificado_por)   REFERENCES USUARIO(id),
    CONSTRAINT CK_deca_cargador_otro CHECK (cargador_tipo <> N'OTRO' OR
        (cargador_nombre IS NOT NULL AND cargador_nif IS NOT NULL AND cargador_direccion IS NOT NULL
         AND cargador_cp IS NOT NULL AND cargador_poblacion IS NOT NULL AND cargador_pais IS NOT NULL))
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
