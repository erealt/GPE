-- Rol "Trabajador": los que solo entran en la web de los trabajadores (/acciones) para ver y marcar
-- sus acciones de NC/incidencias. No entran en el programa de escritorio.
-- Se ejecuta después de login.sql y se puede ejecutar más de una vez.
-- No lleva USE: se elige la base de datos al ejecutarlo, con -d. Con sqlcmd, usar -f 65001.
--   sqlcmd -S localhost -E -C -b -f 65001 -d miGPE -i BaseDatos\rol_trabajador.sql

IF NOT EXISTS (SELECT 1 FROM ROL WHERE nombre = N'Trabajador')
    INSERT INTO ROL (nombre) VALUES (N'Trabajador');
GO

-- PRUEBA: usuario "trabajador" (contraseña "Prueba1234") con solo ese rol, en la fábrica 1.
-- Quitar cuando se den de alta los trabajadores de verdad.
IF NOT EXISTS (SELECT 1 FROM USUARIO WHERE usuario = N'trabajador')
    INSERT INTO USUARIO (nombre, email, usuario, clave)
    VALUES (N'Trabajador', N'estelaretin10+trabajador@gmail.com', N'trabajador',
            N'PBKDF2-SHA256$100000$CqTgTnEJmb9XJ0F2M7hHcA==$3I5R4JtE/3GD01zSHzFnn8+FbthA9OF+dmt/E/0wKnM=');

INSERT INTO USUARIO_ROL (usuario_id, rol_id, fabrica_id)
SELECT u.id, r.id, 1
FROM USUARIO u
JOIN ROL r ON r.nombre = N'Trabajador'
WHERE u.usuario = N'trabajador'
  AND NOT EXISTS (SELECT 1 FROM USUARIO_ROL ur WHERE ur.usuario_id = u.id AND ur.rol_id = r.id AND ur.fabrica_id = 1);
GO

-- Comprobación
SELECT u.usuario, u.nombre, r.nombre AS rol
FROM USUARIO u
JOIN USUARIO_ROL ur ON ur.usuario_id = u.id
JOIN ROL r ON r.id = ur.rol_id
WHERE r.nombre = N'Trabajador';
GO
