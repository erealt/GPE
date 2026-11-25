-- Responsables de las acciones de NC (lista PERSONA), 25/11/2026.
-- Deja activas solo estas personas, en este orden; las demás se desactivan (no se borran: las
-- acciones antiguas siguen mostrando su responsable). Se puede ejecutar más de una vez.
-- No lleva USE: se elige la base de datos al ejecutarlo, con -d. Con sqlcmd, usar -f 65001.
--   sqlcmd -S localhost -E -C -b -f 65001 -d miGPE -i BaseDatos\personas_responsables.sql
--
-- Para que alguien vea sus acciones en la web, su USUARIO.nombre tiene que ser igual que aquí.

SET NOCOUNT ON;

DECLARE @personas TABLE (valor NVARCHAR(150) NOT NULL, orden INT NOT NULL);
INSERT INTO @personas (valor, orden) VALUES
    (N'Ana López', 1),
    (N'Lidia Martínez', 2),
    (N'Marco Antonio', 3),
    (N'Antonio Raúl', 4),
    (N'Cati Cantos', 5),
    (N'José', 6),
    (N'Diego', 7),
    (N'Esteban', 8),
    (N'Pepelu', 9);

BEGIN TRANSACTION;

-- Las que ya estaban: se activan y se ponen en su orden
UPDATE l SET activo = 1, orden = p.orden
FROM CALIDAD_LISTA l
JOIN @personas p ON p.valor = l.valor
WHERE l.lista = N'PERSONA';

-- Las nuevas
INSERT INTO CALIDAD_LISTA (lista, valor, orden, activo)
SELECT N'PERSONA', p.valor, p.orden, 1
FROM @personas p
WHERE NOT EXISTS (SELECT 1 FROM CALIDAD_LISTA l WHERE l.lista = N'PERSONA' AND l.valor = p.valor);

-- El resto deja de salir en el desplegable
UPDATE CALIDAD_LISTA SET activo = 0
WHERE lista = N'PERSONA' AND valor NOT IN (SELECT valor FROM @personas);

COMMIT;

-- Comprobación
SELECT valor, orden FROM CALIDAD_LISTA WHERE lista = N'PERSONA' AND activo = 1 ORDER BY orden;
GO
