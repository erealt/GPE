USE GPE;
GO

-- Secciones de la ficha de incidencias y NC: solo quedan las de la fábrica más las de auditoría.
-- Las demás se desactivan, no se borran: los registros antiguos conservan su sección.
-- Se puede ejecutar más de una vez.

DECLARE @secciones TABLE (valor NVARCHAR(150) PRIMARY KEY, orden INT);
INSERT INTO @secciones (valor, orden) VALUES
    (N'OBRADOR', 1),
    (N'ENVOLTURA', 2),
    (N'EMBOLSADORA', 3),
    (N'ALMACEN', 4),
    (N'TUNEL LAVADO', 5),
    (N'GENERAL', 6),
    (N'INSTALACIONES', 7),
    (N'TRANSPORTE', 8),
    (N'TALLER', 9),
    -- Con origen auditoría solo se puede elegir una de estas
    (N'A. INTERNA', 10),
    (N'A. EXTERNA', 11),
    (N'A. CLIENTE', 12);

INSERT INTO CALIDAD_LISTA (lista, valor, orden)
SELECT N'SECCION', s.valor, s.orden
FROM @secciones s
WHERE NOT EXISTS (SELECT 1 FROM CALIDAD_LISTA l WHERE l.lista = N'SECCION' AND l.valor = s.valor);

UPDATE l
SET activo = CASE WHEN s.valor IS NULL THEN 0 ELSE 1 END,
    orden = COALESCE(s.orden, l.orden)
FROM CALIDAD_LISTA l
LEFT JOIN @secciones s ON s.valor = l.valor
WHERE l.lista = N'SECCION';
GO
