USE GPE;
GO

-- Rutina del supervisor de calidad (CHEQUEOS DIARIOS - PCC). Se ejecuta una vez, después de no_conformidades.sql.

-- Columnas de la hoja. seccion_calidad: la de CALIDAD_LISTA que se propone en la incidencia
-- (los nombres no coinciden: FLO 1 en la hoja, FLOWPACK en las incidencias).
CREATE TABLE RUTINA_SECCION (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(50) NOT NULL,
    planta NVARCHAR(10) NOT NULL,                 -- BAJA o SUPERIOR
    seccion_calidad NVARCHAR(150) NULL,
    orden INT NOT NULL,
    activo BIT NOT NULL CONSTRAINT DF_rutina_seccion_activo DEFAULT 1,
    CONSTRAINT UQ_rutina_seccion UNIQUE (nombre),
    CONSTRAINT CK_rutina_seccion_planta CHECK (planta IN ('BAJA', 'SUPERIOR'))
);

-- Filas de la hoja. Los numéricos (PESOS) llevan valor; mc_pcc_bpm se propone en la incidencia.
CREATE TABLE RUTINA_PUNTO (
    id INT IDENTITY(1,1) PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL,
    numerico BIT NOT NULL CONSTRAINT DF_rutina_punto_numerico DEFAULT 0,
    mc_pcc_bpm NVARCHAR(150) NULL,
    orden INT NOT NULL,
    activo BIT NOT NULL CONSTRAINT DF_rutina_punto_activo DEFAULT 1,
    CONSTRAINT UQ_rutina_punto UNIQUE (nombre)
);

-- Plantilla: qué puntos se revisan en cada sección (las casillas grises de la hoja no están)
CREATE TABLE RUTINA_APLICA (
    punto_id INT NOT NULL,
    seccion_id INT NOT NULL,
    CONSTRAINT PK_rutina_aplica PRIMARY KEY (punto_id, seccion_id),
    CONSTRAINT FK_rutina_aplica_punto FOREIGN KEY (punto_id) REFERENCES RUTINA_PUNTO(id),
    CONSTRAINT FK_rutina_aplica_seccion FOREIGN KEY (seccion_id) REFERENCES RUTINA_SECCION(id)
);

-- Una ronda por día, con las observaciones fijas del pie de la hoja (caja de herramientas y depósitos).
CREATE TABLE RUTINA (
    id INT IDENTITY(1,1) PRIMARY KEY,
    fecha DATE NOT NULL,
    caja_operario NVARCHAR(100) NULL,
    caja_estado NVARCHAR(100) NULL,
    depositos NVARCHAR(1000) NULL,
    creado_por INT NOT NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_rutina_creado DEFAULT SYSDATETIME(),
    -- Como en NO_CONFORMIDAD, para no pisar lo que otro haya guardado
    version ROWVERSION NOT NULL,
    CONSTRAINT UQ_rutina_fecha UNIQUE (fecha),
    CONSTRAINT FK_rutina_usuario FOREIGN KEY (creado_por) REFERENCES USUARIO(id)
);

-- La 1 es la ronda completa; las siguientes, los repasos (el segundo cuadrado de la hoja).
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

-- Casillas rellenadas. estado: OK, NO (no correcto), NA (no aplica hoy), MTO (avisado a mantenimiento).
CREATE TABLE RUTINA_RESULTADO (
    id INT IDENTITY(1,1) PRIMARY KEY,           -- para el historial, que guarda un id por fila
    rutina_id INT NOT NULL,
    revision INT NOT NULL CONSTRAINT DF_rutina_resultado_revision DEFAULT 1,
    punto_id INT NOT NULL,
    seccion_id INT NOT NULL,
    estado NVARCHAR(3) NULL,
    valor DECIMAL(10,2) NULL,
    comentario NVARCHAR(1000) NULL,
    no_conformidad_id INT NULL,                 -- incidencia abierta desde esta casilla
    CONSTRAINT UQ_rutina_resultado UNIQUE (rutina_id, revision, punto_id, seccion_id),
    CONSTRAINT CK_rutina_resultado_estado CHECK (estado IN ('OK', 'NO', 'NA', 'MTO')),
    CONSTRAINT FK_rutina_resultado_rutina FOREIGN KEY (rutina_id) REFERENCES RUTINA(id) ON DELETE CASCADE,
    -- Sin cascada: ya la hay desde RUTINA, y SQL Server no admite dos caminos
    CONSTRAINT FK_rutina_resultado_revision FOREIGN KEY (rutina_id, revision)
        REFERENCES RUTINA_REVISION(rutina_id, numero),
    CONSTRAINT FK_rutina_resultado_punto FOREIGN KEY (punto_id) REFERENCES RUTINA_PUNTO(id),
    CONSTRAINT FK_rutina_resultado_seccion FOREIGN KEY (seccion_id) REFERENCES RUTINA_SECCION(id),
    CONSTRAINT FK_rutina_resultado_nc FOREIGN KEY (no_conformidad_id)
        REFERENCES NO_CONFORMIDAD(id) ON DELETE SET NULL
);

-- Observaciones por sección del pie de la hoja
CREATE TABLE RUTINA_OBSERVACION (
    id INT IDENTITY(1,1) PRIMARY KEY,
    rutina_id INT NOT NULL,
    seccion_id INT NOT NULL,
    texto NVARCHAR(1000) NOT NULL,
    orden INT NOT NULL,
    CONSTRAINT FK_rutina_observacion_rutina FOREIGN KEY (rutina_id) REFERENCES RUTINA(id) ON DELETE CASCADE,
    CONSTRAINT FK_rutina_observacion_seccion FOREIGN KEY (seccion_id) REFERENCES RUTINA_SECCION(id)
);
GO

-- Historial: igual que los de NO_CONFORMIDAD (sin la versión en el JSON)
CREATE OR ALTER TRIGGER TR_RUTINA_HISTORIAL ON RUTINA
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
    SELECT NULL, @usuario, 'RUTINA', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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

-- Igual para las casillas (registro_id es el id de la casilla; el JSON lleva rutina_id)
CREATE OR ALTER TRIGGER TR_RUTINA_RESULTADO_HISTORIAL ON RUTINA_RESULTADO
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
    SELECT NULL, @usuario, 'RUTINA_RESULTADO', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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

-- Igual para las observaciones por sección (el JSON lleva rutina_id)
CREATE OR ALTER TRIGGER TR_RUTINA_OBSERVACION_HISTORIAL ON RUTINA_OBSERVACION
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
    SELECT NULL, @usuario, 'RUTINA_OBSERVACION', c.registro_id, c.accion, c.anteriores, c.nuevos, CURRENT_TRANSACTION_ID()
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

-- Igual para las revisiones del día (el JSON lleva rutina_id)
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

-- Plantilla inicial, de la hoja del 01/10. La sección de calidad es una propuesta pendiente de calidad.
INSERT INTO RUTINA_SECCION (nombre, planta, seccion_calidad, orden) VALUES
    (N'L1', 'BAJA', N'L1', 1),
    (N'L2', 'BAJA', N'L2', 2),
    (N'LR', 'BAJA', N'LROCAS', 3),
    (N'MC 1', 'BAJA', N'MC1-MC2-MC3', 4),
    (N'MC 2', 'BAJA', N'MC1-MC2-MC3', 5),
    (N'MC 3', 'BAJA', N'MC1-MC2-MC3', 6),
    (N'MC 4', 'BAJA', NULL, 7),
    (N'NAP 1', 'BAJA', N'NAP 1 SAPAL', 8),
    (N'NAP 2', 'BAJA', N'NAP 2', 9),
    (N'FLO 1 (SYN)', 'BAJA', N'FLOWPACK', 10),
    (N'EMB 1', 'BAJA', N'EMBOLSADORA', 11),
    (N'EMB 2', 'BAJA', N'EMBOLSADORA', 12),
    (N'RELL', 'BAJA', NULL, 13),
    (N'COMEDOR', 'BAJA', N'VESTUARIOS/COMEDOR', 14),
    (N'CONCHA', 'BAJA', NULL, 15),
    (N'DEPOSITOS', 'BAJA', NULL, 16),
    (N'ESC 1', 'BAJA', N'ESCARGOTS', 17),
    (N'ESC 2', 'BAJA', N'ESCARGOTS', 18),
    (N'FLO 3 (ROC)', 'BAJA', N'FLOWPACK', 19),
    (N'FLO 5 (MAYMA)', 'BAJA', N'FLOWPACK', 20),
    (N'L3', 'SUPERIOR', N'L3', 21),
    (N'L4', 'SUPERIOR', N'L4', 22),
    (N'FLO 6 (TSV)', 'SUPERIOR', N'TSV L3', 23),
    (N'COR 1', 'SUPERIOR', N'CORTADOS', 24),
    (N'MON 1', 'SUPERIOR', N'MONEDAS', 25),
    (N'EST 1', 'SUPERIOR', N'ESTUCHADORA', 26),
    (N'FLO 4 (SUPER)', 'SUPERIOR', N'SUPERMERIENDA', 27),
    (N'TERM 1', 'SUPERIOR', N'TERMOSELLADO', 28);

INSERT INTO RUTINA_PUNTO (nombre, numerico, mc_pcc_bpm, orden) VALUES
    (N'Temperaturas trabajo', 0, NULL, 1),
    (N'Parámetros trabajo', 0, NULL, 2),
    (N'Control de fórmula', 0, N'MC3 - Formulación/trazabilidad', 3),
    (N'Pesos autocontrol', 0, NULL, 4),
    (N'Distribución capas y decoración', 0, NULL, 5),
    (N'Mermas recuperables', 0, N'MC8 - Reprocesados', 6),
    (N'Organoléptico mezcla', 0, NULL, 7),
    (N'Orden y limpieza', 0, N'HIGIENE-BPM', 8),
    (N'BP', 0, N'HIGIENE-BPM', 9),
    (N'Envoltura', 0, NULL, 10),
    (N'Envase/Estuche', 0, NULL, 11),
    (N'Corte', 0, N'MC7 - Cuchilas cortadoras', 12),
    (N'Revisión autocontroles', 0, N'MC-P.CONTROL', 13),
    (N'PCC metales', 0, N'PCC 2 - Metales', 14),
    (N'PCC aditivos', 0, N'PCC 1 - Aditivos', 15),
    (N'Control peso neto. Estadísticas', 0, N'MC9 - Etiquetado y peso neto', 16),
    (N'Etiquetado embalaje', 0, NULL, 17),
    (N'Etiquetado legal', 0, N'MC9 - Etiquetado y peso neto', 18),
    (N'Estado del embalaje final', 0, NULL, 19),
    (N'Paletizado', 0, NULL, 20),
    (N'Micraje obrador rellenos', 0, NULL, 21),
    (N'Mirar registro', 0, NULL, 22),
    (N'Temperaturas dentro rango', 0, N'MC6 - Filtros AWEMA, atemperadoras, despositos', 23),
    (N'Revisar mangueras', 0, NULL, 24),
    (N'PESOS', 1, NULL, 25);

-- Una letra por sección en su orden (L1, L2, LR... TERM 1): X se revisa, . no.
-- COMEDOR, CONCHA y DEPOSITOS (14 a 16) están sombreadas enteras en la hoja: pendientes de confirmar.
INSERT INTO RUTINA_APLICA (punto_id, seccion_id)
SELECT p.id, s.id
FROM (VALUES
    (N'Temperaturas trabajo',              'XXX.........X.......XX......'),
    (N'Parámetros trabajo',                'XXX.......XXX.......XXX.....'),
    (N'Control de fórmula',                'XXXXXXXXXXXXX...XXXXXXXXXXXX'),
    (N'Pesos autocontrol',                 'XXX.................XX......'),
    (N'Distribución capas y decoración',   'XXX.................XXX.....'),
    (N'Mermas recuperables',               'XXX....XX...X......XXXXX.XX.'),
    (N'Organoléptico mezcla',              'XXX.........X......XXXX.....'),
    (N'Orden y limpieza',                  'XXXXXXXXXXXXXX.XXXXXXXXXXXXX'),
    (N'BP',                                'XXXXXXXXXXXXXX..XXXXXXXXXXXX'),
    (N'Envoltura',                         '...XXXXXXX......XXX....XX.X.'),
    (N'Envase/Estuche',                    '..........XX.......X..X..XXX'),
    (N'Corte',                             '....................X.......'),
    (N'Revisión autocontroles',            'XXXXXXXXXXXXX...XXXXXXXXXXXX'),
    (N'PCC metales',                       'XXX.......X.........XXX.....'),
    (N'PCC aditivos',                      'XXX.........................'),
    (N'Control peso neto. Estadísticas',   '..........XX.......X..X.....'),
    (N'Etiquetado embalaje',               '..XXXXXXXXXX....XXXXX.XXXXX.'),
    (N'Etiquetado legal',                  '..XXXXXXXXXX....XXXXX.XXXXX.'),
    (N'Estado del embalaje final',         '..XXXXXXXXXX....XXXXX.XXXXX.'),
    (N'Paletizado',                        'XXXXXXXXXXXX....XXXXXXXXXXX.'),
    (N'Micraje obrador rellenos',          '............X.X.............'),
    (N'Mirar registro',                    '..............X.............'),
    (N'Temperaturas dentro rango',         '............X..X............'),
    (N'Revisar mangueras',                 '...............X............'),
    (N'PESOS',                             'XXX.................XX......')
) AS h (punto, secciones)
JOIN RUTINA_PUNTO p ON p.nombre = h.punto
JOIN RUTINA_SECCION s ON SUBSTRING(h.secciones, s.orden, 1) = 'X';
GO
