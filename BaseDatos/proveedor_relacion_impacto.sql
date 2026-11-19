USE GPE;
GO

-- RELACIÓN del proveedor e IMPACTO DEL PRODUCTO dejan de tener "Igual que el estatus" / "Igual que el proveedor":
-- cada uno tiene su propio número. Los que estaban vacíos (= igual que...) se rellenan con el valor que usaban,
-- así las notas no cambian. Se ejecuta después de proveedores.sql y se puede ejecutar más de una vez.
-- Con sqlcmd, usar -I -f 65001 (por las tildes).
-- La relación ya no está en PROVEEDOR después de proveedor_riesgo_por_producto.sql: entonces solo se hace el impacto.
DECLARE @usuario INT = (SELECT TOP 1 id FROM USUARIO ORDER BY id);
EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;   -- lo piden los triggers de historial

IF COL_LENGTH('PROVEEDOR', 'relacion') IS NOT NULL
    EXEC(N'UPDATE PROVEEDOR SET relacion = estatus WHERE relacion IS NULL AND estatus IS NOT NULL;');

UPDATE x SET impacto = p.impacto
FROM PROVEEDOR_PRODUCTO x
JOIN PROVEEDOR p ON p.id = x.proveedor_id
WHERE x.impacto IS NULL AND p.impacto IS NOT NULL;
GO
