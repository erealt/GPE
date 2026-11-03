USE GPE;
GO

-- Subsección de la ficha de incidencias y NC: las secciones que se quitaron con secciones_calidad.sql
-- (L1, FLOWPACK, ESCARGOTS...). Ejecutar después de ese script. Se puede ejecutar más de una vez.

IF COL_LENGTH('NO_CONFORMIDAD', 'subseccion') IS NULL
    ALTER TABLE NO_CONFORMIDAD ADD subseccion NVARCHAR(150) NULL;
GO

ALTER TABLE CALIDAD_LISTA DROP CONSTRAINT CK_calidad_lista;
ALTER TABLE CALIDAD_LISTA ADD CONSTRAINT CK_calidad_lista CHECK (lista IN ('ORIGEN', 'SECCION', 'SUBSECCION', 'TIPO',
    'PERSONA', 'MC_PCC_BPM', 'PARAMETRO', 'OPERARIO', 'CAPITULO_NORMA'));
GO

INSERT INTO CALIDAD_LISTA (lista, valor, orden)
SELECT N'SUBSECCION', s.valor, s.orden
FROM CALIDAD_LISTA s
WHERE s.lista = N'SECCION' AND s.activo = 0
  AND NOT EXISTS (SELECT 1 FROM CALIDAD_LISTA l WHERE l.lista = N'SUBSECCION' AND l.valor = s.valor);
GO
