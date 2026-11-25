USE GPE;
GO

-- Login del programa de escritorio: cada USUARIO entra con su nombre de usuario y su contraseña.
-- Se ejecuta después de crear_tablas.sql y permisos_prueba.sql y se puede ejecutar más de una vez.
-- Con sqlcmd, usar -C -f 65001 (por las tildes).

-- usuario y clave se pueden dejar vacíos: quien no los tenga no puede entrar en el escritorio
-- (por ejemplo, el usuario que solo usa la web del DeCA).
IF COL_LENGTH('USUARIO', 'usuario') IS NULL
    ALTER TABLE USUARIO ADD usuario NVARCHAR(50) NULL;
GO
IF COL_LENGTH('USUARIO', 'clave') IS NULL
    ALTER TABLE USUARIO ADD clave NVARCHAR(200) NULL;     -- cifrada (PBKDF2), nunca la contraseña tal cual
GO
IF COL_LENGTH('USUARIO', 'activo') IS NULL
    ALTER TABLE USUARIO ADD activo BIT NOT NULL CONSTRAINT DF_usuario_activo DEFAULT 1;
GO

-- Un índice único filtrado obligaría a todos los scripts que tocan USUARIO a ejecutarse con
-- QUOTED_IDENTIFIER ON (sqlcmd sin -I fallaría), así que se quita si se llegó a crear
-- y el usuario repetido lo impide este trigger.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_usuario_usuario')
    DROP INDEX UQ_usuario_usuario ON USUARIO;
GO

-- No puede haber dos con el mismo usuario (los que no tienen usuario pueden ser varios)
CREATE OR ALTER TRIGGER TR_usuario_usuario_unico ON USUARIO
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(usuario) AND EXISTS (
        SELECT 1 FROM USUARIO u
        JOIN (SELECT DISTINCT usuario FROM inserted WHERE usuario IS NOT NULL) i ON i.usuario = u.usuario
        GROUP BY u.usuario HAVING COUNT(*) > 1)
    BEGIN
        RAISERROR(N'Ya hay otro usuario con ese nombre de usuario.', 16, 1);
        ROLLBACK TRANSACTION;
    END
END;
GO

-- PRUEBA: los usuarios de permisos_prueba.sql, con la contraseña "Prueba1234".
-- Solo se pone a quien todavía no tiene usuario. La contraseña de verdad de admin y los usuarios
-- de calidad y almacén los pone usuarios.sql.
DECLARE @clave NVARCHAR(200) = N'PBKDF2-SHA256$100000$CqTgTnEJmb9XJ0F2M7hHcA==$3I5R4JtE/3GD01zSHzFnn8+FbthA9OF+dmt/E/0wKnM=';

UPDATE u SET usuario = p.usuario, clave = @clave
FROM USUARIO u
JOIN (VALUES
    (N'estelaretin10@gmail.com',            N'admin'),
    (N'estelaretin10+transporte@gmail.com', N'transporte')
) AS p(email, usuario) ON p.email = u.email
WHERE u.usuario IS NULL
  AND NOT EXISTS (SELECT 1 FROM USUARIO o WHERE o.usuario = p.usuario);
GO
