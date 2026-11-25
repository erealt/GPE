-- Usuarios de verdad (25/11/2026), en la fábrica 1:
--   admin          contraseña admin                  todos los roles (lo ve todo)
--   celia          contraseña celia@calidad          Calidad
--   alba           contraseña alba@calidad           Calidad
--   marcoantonio   contraseña marcoAntonio@almacen   Almacén
-- Se quitan los de prueba estela, calidad y almacen. Quedan transporte y trabajador (Prueba1234).
-- Los correos son provisionales (llegan a estelaretin10@gmail.com) hasta tener los de cada uno.
--
-- Se ejecuta después de login.sql y rol_trabajador.sql y se puede ejecutar más de una vez
-- (cada vez vuelve a poner estas contraseñas). No lleva USE: la base se elige con -d.
--   sqlcmd -S localhost -E -C -b -f 65001 -d miGPE -i BaseDatos\usuarios.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @fabrica INT = 1;

DECLARE @usuarios TABLE (usuario NVARCHAR(50), nombre NVARCHAR(100), email NVARCHAR(150), rol NVARCHAR(50), clave NVARCHAR(200));
INSERT INTO @usuarios (usuario, nombre, email, rol, clave) VALUES
    (N'admin',        N'Administrador', N'estelaretin10@gmail.com',              NULL,         -- NULL: todos los roles
     N'PBKDF2-SHA256$100000$0Tagh/HN0OeIUQgBeNrn2Q==$2MWgtJsNoz7ldhnVnA9paG+GlLG2cOOV7JOlSIpqUvY='),
    (N'celia',        N'Celia',         N'estelaretin10+celia@gmail.com',        N'Calidad',
     N'PBKDF2-SHA256$100000$GEoVTlu6aGXNikK5S0DeRQ==$hOGm757ZbiaUa7xwgTnrghW82xgydg/iP8kPOvfFaxM='),
    (N'alba',         N'Alba',          N'estelaretin10+alba@gmail.com',         N'Calidad',
     N'PBKDF2-SHA256$100000$SkGvaBlBFflPd2X7Rm0hkw==$9RBwzcHjKvKpsIbxUoYKhaV1J8lC0bez2FOPxcGGsdc='),
    (N'marcoantonio', N'Marco Antonio', N'estelaretin10+marcoantonio@gmail.com', N'Almacén',
     N'PBKDF2-SHA256$100000$3iHd6o+tLlr+GVLD79OEYQ==$QM4MJsJQmD4++U2JNI8kJrL4LzLiPDc1nr4v7cZxvCc=');

DECLARE @quitar TABLE (usuario NVARCHAR(50));
INSERT INTO @quitar VALUES (N'estela'), (N'calidad'), (N'almacen');

-- Si algún rol no coincide con la tabla ROL (pasa si el archivo se lee sin UTF-8), no se toca nada
IF EXISTS (SELECT 1 FROM @usuarios u WHERE u.rol IS NOT NULL AND NOT EXISTS (SELECT 1 FROM ROL r WHERE r.nombre = u.rol))
BEGIN
    RAISERROR(N'Algún rol de la lista no existe en la tabla ROL. Si usas sqlcmd, ejecútalo con -f 65001.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

-- 1. Los que se quitan: sin roles (no entran ni reciben avisos). Si no han hecho nada se borran;
--    si figuran en algo (una NC, el historial...), se quedan desactivados para no perder quién lo hizo.
DELETE ur
FROM USUARIO_ROL ur
JOIN USUARIO u ON u.id = ur.usuario_id
JOIN @quitar q ON q.usuario = u.usuario;

UPDATE u SET activo = 0
FROM USUARIO u
JOIN @quitar q ON q.usuario = u.usuario;

-- Todas las columnas que apuntan a USUARIO (menos USUARIO_ROL), unidas en una consulta
DECLARE @referencias NVARCHAR(MAX) = N'';
SELECT @referencias = @referencias + CASE WHEN @referencias = N'' THEN N'' ELSE N' UNION ' END
         + N'SELECT ' + QUOTENAME(COL_NAME(fc.parent_object_id, fc.parent_column_id))
         + N' FROM ' + QUOTENAME(OBJECT_SCHEMA_NAME(fc.parent_object_id)) + N'.' + QUOTENAME(OBJECT_NAME(fc.parent_object_id))
FROM sys.foreign_key_columns fc
WHERE fc.referenced_object_id = OBJECT_ID(N'USUARIO')
  AND fc.parent_object_id <> OBJECT_ID(N'USUARIO_ROL');

EXEC (N'DELETE u FROM USUARIO u
       WHERE u.activo = 0
         AND u.usuario IN (N''estela'', N''calidad'', N''almacen'')
         AND u.id NOT IN (SELECT r.id FROM (' + @referencias + N') AS r(id) WHERE r.id IS NOT NULL);');

-- 2. Los nuevos se crean; a todos se les pone nombre, contraseña y se activan
INSERT INTO USUARIO (nombre, email, usuario, clave)
SELECT u.nombre, u.email, u.usuario, u.clave
FROM @usuarios u
WHERE NOT EXISTS (SELECT 1 FROM USUARIO us WHERE us.usuario = u.usuario);

UPDATE us SET nombre = u.nombre, clave = u.clave, activo = 1
FROM USUARIO us
JOIN @usuarios u ON u.usuario = us.usuario;

-- 3. Roles: admin todos; los demás, solo el suyo
DELETE ur
FROM USUARIO_ROL ur
JOIN USUARIO us ON us.id = ur.usuario_id
JOIN @usuarios u ON u.usuario = us.usuario
JOIN ROL r ON r.id = ur.rol_id
WHERE ur.fabrica_id = @fabrica
  AND u.rol IS NOT NULL AND r.nombre <> u.rol;

INSERT INTO USUARIO_ROL (usuario_id, rol_id, fabrica_id)
SELECT us.id, r.id, @fabrica
FROM @usuarios u
JOIN USUARIO us ON us.usuario = u.usuario
JOIN ROL r ON u.rol IS NULL OR r.nombre = u.rol
WHERE NOT EXISTS (SELECT 1 FROM USUARIO_ROL ur
                  WHERE ur.usuario_id = us.id AND ur.rol_id = r.id AND ur.fabrica_id = @fabrica);

COMMIT;

-- Comprobación
SELECT us.id, us.usuario, us.nombre, us.activo, r.nombre AS rol
FROM USUARIO us
LEFT JOIN USUARIO_ROL ur ON ur.usuario_id = us.id AND ur.fabrica_id = @fabrica
LEFT JOIN ROL r ON r.id = ur.rol_id
ORDER BY us.activo DESC, us.usuario, r.nombre;
GO
