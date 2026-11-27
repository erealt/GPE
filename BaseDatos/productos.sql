-- Productos de las devoluciones: los artículos de la aplicación de producción (tabla Articulos de su base), 27/11/2026.
-- Quita los que no están en esta lista (los de prueba PRD-00x) y añade o actualiza el resto por código.
-- Si algún producto que se va a quitar ya lo usa una devolución, no se borra y se avisa.
-- Se puede ejecutar más de una vez. No lleva USE: se elige la base de datos al ejecutarlo, con -d.
--   sqlcmd -S localhost -E -C -b -f 65001 -d miGPE -i BaseDatos\productos.sql

SET NOCOUNT ON;

DECLARE @productos TABLE (codigo NVARCHAR(30) NOT NULL PRIMARY KEY, nombre NVARCHAR(100) NOT NULL);
INSERT INTO @productos (codigo, nombre) VALUES
    (N'ALU13', N'ALUMINIO 13 g, COMO FLECO PERO EN ALUMINIO'),
    (N'BAR1', N'Barritas 10x2''6x13''5 Dubai'),
    (N'BARR', N'BARRITAS'),
    (N'BIMB', N'BIMBOM'),
    (N'BRET', N'Bretzels'),
    (N'CALA', N'CALAVERAS'),
    (N'CARA', N'ESCARGOT-CARACOLES'),
    (N'CH16', N'CHOCOLATINAS 16 g MERIENDA'),
    (N'CH25', N'CHOCOLATINAS 25 g Quely'),
    (N'CHEJE', N'Chocolatina ejército 25 gramos'),
    (N'CHUP', N'CHUPA CHUPS LOLLIPOP'),
    (N'CMEL', N'CARAMELO'),
    (N'CORA', N'CORAZÓN'),
    (N'CORT1', N'CORTADOS 10 g Eurochoc y CCH CON Vitola'),
    (N'CORT2', N'CORTADOS 12 g CON Vitola'),
    (N'CORT3', N'Cortados SIN vitola 10 g'),
    (N'CORT4', N'Cortados SIN vitola 12 g'),
    (N'CRE13', N'CREMA 13 g'),
    (N'CRE5', N'CREMA 5 g'),
    (N'EMOT', N'EMOTIBON'),
    (N'ESFE', N'ESFERAS'),
    (N'ESFE-2', N'Esferas sin acoplar'),
    (N'EST125', N'Estuches 125 g'),
    (N'EST155', N'ESTUCHES 155 gramos'),
    (N'EST250', N'Estuches 250 g'),
    (N'EST92', N'Estuches 92 g'),
    (N'EXPR', N'EXPRESIONI'),
    (N'FIG-2', N'Figuritas 8''33 gramos (placas 300 g)'),
    (N'FIG8', N'FIGURITAS 8 g'),
    (N'FLE10', N'Fleco 10 gramos'),
    (N'FLE13', N'FLECOS 13 g'),
    (N'FLE14', N'Fleco 13''88 gramos (placa de 1 Kg)'),
    (N'FLLI', N'Fleco licor 13 g'),
    (N'FLTU', N'FLECOS TURRÓN'),
    (N'FRMA', N'Frutos de mar'),
    (N'GOMI', N'Gominolas'),
    (N'HUE2', N'Huevos con molde único'),
    (N'HUEV', N'HUEVOS'),
    (N'LAD4', N'LADYBUG 4 gramos'),
    (N'LAD5', N'LADYBUG 5 gramos'),
    (N'LENG', N'LENGUAS GATO-solo placas'),
    (N'METE', N'Meteoritos'),
    (N'MFRU', N'Mix fruit'),
    (N'MICU', N'Minicups 4g'),
    (N'MINI', N'MINIBOMBÓN 5 g'),
    (N'MONE', N'MONEDAS'),
    (N'MTTO', N'Mantenimiento, limpieza'),
    (N'NAPC', N'NAPOLITANA CUADRADA'),
    (N'NAV4', N'Navideñas 4 gramos'),
    (N'NAV5', N'Navideñas 5 gramos'),
    (N'NR1', N'NAPOLITANAS REDONDAS 3''2 g'),
    (N'NR2', N'NAPOLITANAS REDONDAS 5 g'),
    (N'OSO4', N'OSO PANDA 4 gramos'),
    (N'OSO5', N'OSO PANDA 5 gramos'),
    (N'POR1', N'PORCIONES 4''5x2''6x1''4 LACASITOS y CONGUITOS'),
    (N'POR2', N'PORCIONES 5x2''6x1''2'),
    (N'PRUE', N'Pruebas-muestras'),
    (N'REGA', N'Regaliz'),
    (N'RELL', N'Rellenos'),
    (N'ROCA', N'ROCAS'),
    (N'ROND', N'RONDOS-CHUPACHUPS'),
    (N'SOLI', N'Solidificar, pasar de líquido a sólido'),
    (N'STIC', N'STICKS'),
    (N'SUME', N'SÚPER MERIENDA 16 g'),
    (N'T100', N'Tableta 100 gramos'),
    (N'T100D', N'Tableta 100 gramos Dubai'),
    (N'T150', N'Tableta 150 gramos'),
    (N'T200', N'Tableta 200 gramos'),
    (N'T250', N'Tableta 250 gramos'),
    (N'T300', N'Tableta 300 g'),
    (N'TA40', N'Tableta 40 gramos'),
    (N'TA80', N'Tableta 80 gramos'),
    (N'TA90', N'Tableta 90 gramos'),
    (N'TAFR', N'Tableta frutos rojos 40 gramos'),
    (N'TAMO', N'Tableta montaña'),
    (N'TATR', N'Tabletas trufadas straciatela Picó'),
    (N'THIN', N'Thins'),
    (N'TXXL', N'Tableta XXL 110 gramos');

BEGIN TRANSACTION;

-- Los que sobran y nadie usa, fuera
DELETE p FROM PRODUCTO p
WHERE NOT EXISTS (SELECT 1 FROM @productos n WHERE n.codigo = p.codigo)
  AND NOT EXISTS (SELECT 1 FROM DEVOLUCION_PRODUCTO d WHERE d.producto_id = p.id)
  AND NOT EXISTS (SELECT 1 FROM REVISION_LINEA r WHERE r.producto_id = p.id)
  AND NOT EXISTS (SELECT 1 FROM LOTE l WHERE l.producto_id = p.id);

UPDATE p SET nombre = n.nombre
FROM PRODUCTO p JOIN @productos n ON n.codigo = p.codigo
WHERE p.nombre <> n.nombre;

INSERT INTO PRODUCTO (codigo, nombre)
SELECT n.codigo, n.nombre FROM @productos n
WHERE NOT EXISTS (SELECT 1 FROM PRODUCTO p WHERE p.codigo = n.codigo);

COMMIT;

-- Los que no se han podido quitar porque ya están en alguna devolución
SELECT p.codigo, p.nombre AS [no se ha borrado: lo usa una devolución]
FROM PRODUCTO p
WHERE NOT EXISTS (SELECT 1 FROM @productos n WHERE n.codigo = p.codigo);

SELECT COUNT(*) AS productos FROM PRODUCTO;
