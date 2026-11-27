USE GPE;
GO

-- Almacenes en la rutina de calidad: hoja RG-AL-01 (registro verificación condiciones y limpieza de almacenes).
-- Va como una tercera "planta" con sus 4 columnas y sus puntos. Se ejecuta después de rutina_calidad.sql
-- y se puede ejecutar más de una vez.

-- La planta ALMACEN en las bases creadas antes
IF EXISTS (SELECT 1 FROM sys.check_constraints
           WHERE name = 'CK_rutina_seccion_planta' AND definition NOT LIKE '%ALMACEN%')
    ALTER TABLE RUTINA_SECCION DROP CONSTRAINT CK_rutina_seccion_planta;
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_rutina_seccion_planta')
    ALTER TABLE RUTINA_SECCION ADD CONSTRAINT CK_rutina_seccion_planta
        CHECK (planta IN ('BAJA', 'SUPERIOR', 'ALMACEN'));
GO

-- Columnas de la hoja, detrás de las máquinas
INSERT INTO RUTINA_SECCION (nombre, planta, seccion_calidad, orden)
SELECT h.nombre, 'ALMACEN', NULL, (SELECT ISNULL(MAX(orden), 0) FROM RUTINA_SECCION) + h.n
FROM (VALUES
    (1, N'P. SUPERIOR'),
    (2, N'TªC. AMBIENTE'),
    (3, N'MMPP'),
    (4, N'PRODUCTO FINAL')
) AS h (n, nombre)
WHERE NOT EXISTS (SELECT 1 FROM RUTINA_SECCION s WHERE s.nombre = h.nombre);

-- Filas de la hoja, detrás de los puntos de la rutina
INSERT INTO RUTINA_PUNTO (nombre, numerico, mc_pcc_bpm, orden)
SELECT h.nombre, 0, NULL, (SELECT ISNULL(MAX(orden), 0) FROM RUTINA_PUNTO) + h.n
FROM (VALUES
    (1, N'Se cumple el FIFO'),
    (2, N'Se cumple orden preestablecido PO-RG10 (Listado de ubicación de MMPP y auxiliares en almacén)'),
    (3, N'Identificación correcta producto'),
    (4, N'Mercancía NO CONFORME identificada y ubicada correctamente'),
    (5, N'Pallet NO CONFORME (rotos, astillados, etc.) fuera de las instalaciones'),
    (6, N'DEVOLUCIONES identificadas y ubicadas correctamente'),
    (7, N'MUESTRAS identificadas y ubicadas correctamente'),
    (8, N'Materiales de envuelta y envasado protegidos'),
    (9, N'Ropa térmica en perchas o puesta'),
    (10, N'Se cumplen las medidas de ALÉRGENOS'),
    (11, N'Se cumplen las medidas de SIN AZÚCAR'),
    (12, N'ENVASES íntegros y cerrados'),
    (13, N'DERRAMES mercancía'),
    (14, N'GRG con producto limpios'),
    (15, N'PLÁSTICOS Y OTROS MATERIALES procedentes del despaletizado de mercancías gestionados'),
    (16, N'Placas sucias para lavar retractiladas'),
    (17, N'Placas lavadas retractiladas'),
    (18, N'Limpieza de suelos conforme'),
    (19, N'Limpieza de rincones y rodapiés conforme'),
    (20, N'Limpieza de puertas conforme'),
    (21, N'Limpieza de estanterías conforme'),
    (22, N'Limpieza de muelle conforme')
) AS h (n, nombre)
WHERE NOT EXISTS (SELECT 1 FROM RUTINA_PUNTO p WHERE p.nombre = h.nombre);

-- Plantilla: cada punto de almacén en las 4 columnas, menos las casillas "NO APLICA" de la hoja
INSERT INTO RUTINA_APLICA (punto_id, seccion_id)
SELECT p.id, s.id
FROM RUTINA_PUNTO p
CROSS JOIN RUTINA_SECCION s
WHERE s.planta = 'ALMACEN'
  AND p.nombre IN (
        N'Se cumple el FIFO',
        N'Se cumple orden preestablecido PO-RG10 (Listado de ubicación de MMPP y auxiliares en almacén)',
        N'Identificación correcta producto',
        N'Mercancía NO CONFORME identificada y ubicada correctamente',
        N'Pallet NO CONFORME (rotos, astillados, etc.) fuera de las instalaciones',
        N'DEVOLUCIONES identificadas y ubicadas correctamente',
        N'MUESTRAS identificadas y ubicadas correctamente',
        N'Materiales de envuelta y envasado protegidos',
        N'Ropa térmica en perchas o puesta',
        N'Se cumplen las medidas de ALÉRGENOS',
        N'Se cumplen las medidas de SIN AZÚCAR',
        N'ENVASES íntegros y cerrados',
        N'DERRAMES mercancía',
        N'GRG con producto limpios',
        N'PLÁSTICOS Y OTROS MATERIALES procedentes del despaletizado de mercancías gestionados',
        N'Placas sucias para lavar retractiladas',
        N'Placas lavadas retractiladas',
        N'Limpieza de suelos conforme',
        N'Limpieza de rincones y rodapiés conforme',
        N'Limpieza de puertas conforme',
        N'Limpieza de estanterías conforme',
        N'Limpieza de muelle conforme')
  AND NOT EXISTS (SELECT 1 FROM (VALUES
        (N'Ropa térmica en perchas o puesta', N'P. SUPERIOR'),
        (N'Ropa térmica en perchas o puesta', N'TªC. AMBIENTE'),
        (N'Ropa térmica en perchas o puesta', N'MMPP'),
        (N'GRG con producto limpios',         N'P. SUPERIOR'),
        (N'GRG con producto limpios',         N'PRODUCTO FINAL'),
        (N'Limpieza de muelle conforme',      N'P. SUPERIOR'),
        (N'Limpieza de muelle conforme',      N'MMPP')
      ) AS na (punto, seccion) WHERE na.punto = p.nombre AND na.seccion = s.nombre)
  AND NOT EXISTS (SELECT 1 FROM RUTINA_APLICA a WHERE a.punto_id = p.id AND a.seccion_id = s.id);
GO
