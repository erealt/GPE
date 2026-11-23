USE GPE;
GO

-- RELACIÓN, HISTORIAL y P. SUBCONTRATADO (riesgo del proveedor para cometer fraude) pasan a ser de cada producto,
-- como el resto del riesgo de fraude: en el Excel están en cada fila. En el proveedor solo quedan impacto, estatus,
-- reclamaciones (y la evaluación, que sale de ellos).
-- Cada producto se queda con lo que tenía su proveedor, así las notas no cambian.
-- Se ejecuta después de proveedores.sql y proveedor_relacion_impacto.sql, y se puede ejecutar más de una vez.
-- Con sqlcmd, usar -I -f 65001 (por las tildes).
-- El historial (TR_PROVEEDOR_HISTORIAL y TR_PROVEEDOR_PRODUCTO_HISTORIAL) copia la fila entera: no hay que cambiarlo.
IF COL_LENGTH('PROVEEDOR_PRODUCTO', 'relacion') IS NULL
    ALTER TABLE PROVEEDOR_PRODUCTO ADD
        relacion TINYINT NULL CONSTRAINT CK_proveedor_producto_relacion CHECK (relacion BETWEEN 0 AND 3),
        historial TINYINT NULL CONSTRAINT CK_proveedor_producto_historial CHECK (historial BETWEEN 0 AND 3),
        subcontratado TINYINT NULL CONSTRAINT CK_proveedor_producto_subcontratado CHECK (subcontratado BETWEEN 0 AND 3);
GO

-- Copiar lo del proveedor a sus productos y quitarlo del proveedor (solo si todavía está)
IF COL_LENGTH('PROVEEDOR', 'relacion') IS NOT NULL
BEGIN
    DECLARE @usuario INT = (SELECT TOP 1 id FROM USUARIO ORDER BY id);
    EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;   -- lo piden los triggers de historial

    EXEC(N'
    UPDATE x SET relacion = COALESCE(p.relacion, p.estatus), historial = p.historial, subcontratado = p.subcontratado
    FROM PROVEEDOR_PRODUCTO x
    JOIN PROVEEDOR p ON p.id = x.proveedor_id;');

    ALTER TABLE PROVEEDOR DROP CONSTRAINT CK_proveedor_relacion, CK_proveedor_historial, CK_proveedor_subcontratado;
    ALTER TABLE PROVEEDOR DROP COLUMN relacion, historial, subcontratado;
END;
GO
