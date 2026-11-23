USE GPE;
GO

-- Las NC eligen el proveedor de la lista de proveedores (PROVEEDOR, pantalla Proveedores) en vez de escribirlo.
-- Se ejecuta después de proveedores.sql y no_conformidades.sql, y se puede ejecutar más de una vez.
-- Con sqlcmd, usar -I -f 65001 (por las tildes).
--
-- proveedor (texto) se queda como estaba: es el nombre escrito a mano en las NC antiguas, que todavía no están
-- enlazadas. Cuando una NC tiene proveedor_id, la aplicación enseña el nombre de la lista.
-- El historial (TR_NO_CONFORMIDAD_HISTORIAL) copia la fila entera, así que recoge la columna nueva sin cambiarlo.
IF COL_LENGTH('NO_CONFORMIDAD', 'proveedor_id') IS NULL
    ALTER TABLE NO_CONFORMIDAD ADD proveedor_id INT NULL
        CONSTRAINT FK_no_conformidad_proveedor REFERENCES PROVEEDOR(id);
GO

-- Para contar las NC de cada proveedor desde su ficha
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_no_conformidad_proveedor')
    CREATE INDEX IX_no_conformidad_proveedor ON NO_CONFORMIDAD (proveedor_id);
GO
