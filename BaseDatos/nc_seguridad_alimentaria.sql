USE GPE;
GO

-- Si una NC de un proveedor afecta a la seguridad alimentaria (SA) o no (NSA). Con esto la aplicación saca la nota
-- de reclamaciones del proveedor (0 a 3), como la tabla de calidad:
--   0 NC → 0 · 1 NSA → 1 · 2-3 NSA → 2 · más de 3 NSA, o 1 o más SA → 3   (NC de los últimos 12 meses)
-- Se ejecuta después de nc_proveedor.sql y se puede ejecutar más de una vez. Con sqlcmd, usar -I -f 65001.
--
-- NULL = sin indicar (NC antiguas, incidencias y mejoras): cuenta como que no afecta.
-- PROVEEDOR.reclamaciones deja de usarse: era la nota escrita a mano (0 a 2). La aplicación ya no la lee ni la guarda.
-- El historial (TR_NO_CONFORMIDAD_HISTORIAL) copia la fila entera, así que recoge la columna nueva sin cambiarlo.
IF COL_LENGTH('NO_CONFORMIDAD', 'seguridad_alimentaria') IS NULL
    ALTER TABLE NO_CONFORMIDAD ADD seguridad_alimentaria BIT NULL;
GO
