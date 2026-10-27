/* Prueba de permisos por rol. Ejecutar después de crear_tablas.sql (que crea los roles).
   Con sqlcmd hay que ejecutarlo con -f 65001 (el archivo está en UTF-8, por las tildes).

   Roles (cada uno ve en la lista las devoluciones que esperan algo de él):
   - Administración: da de alta las devoluciones y registra el abono.
                     Ve las recién creadas y las que esperan el abono.
   - Transporte:     programa la recogida. Ve las recién creadas.
   - Almacén:        marca la llegada de la mercancía y la revisa.
                     Ve las que tienen la recogida programada y las recibidas sin revisar.
   - Calidad:        no ve las devoluciones, solo las no conformidades y la rutina de calidad.

   Usuarios de la prueba, uno por rol (en la fábrica 1, la que usa la aplicación de momento).
   Los cuatro correos llegan al mismo buzón: Gmail entrega "nombre+loquesea@gmail.com" en "nombre@gmail.com".
   - Administrador: el usuario que antes se llamaba Estela
   - Almacén:       el que antes era Usuario Prueba
   - Transporte:    nuevo
   - Calidad:       nuevo

   Se puede ejecutar varias veces: deja a cada usuario exactamente con su rol. */

USE GPE;
GO

DECLARE @fabrica INT = 1;

DECLARE @usuarios TABLE (email_antiguo NVARCHAR(150) NULL, email NVARCHAR(150), nombre NVARCHAR(100), rol NVARCHAR(50));
INSERT INTO @usuarios (email_antiguo, email, nombre, rol) VALUES
    (NULL,                N'estelaretin10@gmail.com',            N'Administrador', N'Administración'),
    (N'prueba@gpe.local', N'estelaretin10+almacen@gmail.com',    N'Almacén',       N'Almacén'),
    (NULL,                N'estelaretin10+transporte@gmail.com', N'Transporte',    N'Transporte'),
    (NULL,                N'estelaretin10+calidad@gmail.com',    N'Calidad',       N'Calidad');

-- El rol Calidad se añadió después: en las bases de datos anteriores no existe todavía
IF NOT EXISTS (SELECT 1 FROM ROL WHERE nombre = N'Calidad')
    INSERT INTO ROL (nombre) VALUES (N'Calidad');

-- Si algún rol no coincide con la tabla ROL (pasa si el archivo se lee sin UTF-8), no se toca nada
IF EXISTS (SELECT 1 FROM @usuarios u WHERE NOT EXISTS (SELECT 1 FROM ROL r WHERE r.nombre = u.rol))
BEGIN
    RAISERROR(N'Algún rol de la lista no existe en la tabla ROL. Si usas sqlcmd, ejecútalo con -f 65001.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

-- Usuarios que cambian de correo (solo la primera vez que se ejecuta)
UPDATE us
SET email = u.email
FROM USUARIO us
JOIN @usuarios u ON u.email_antiguo = us.email
WHERE NOT EXISTS (SELECT 1 FROM USUARIO otro WHERE otro.email = u.email);

-- Nombres nuevos, y los usuarios que no existan se crean
UPDATE us
SET nombre = u.nombre
FROM USUARIO us
JOIN @usuarios u ON u.email = us.email;

INSERT INTO USUARIO (nombre, email)
SELECT u.nombre, u.email
FROM @usuarios u
WHERE NOT EXISTS (SELECT 1 FROM USUARIO us WHERE us.email = u.email);

-- Cada uno, solo con su rol
DELETE ur
FROM USUARIO_ROL ur
JOIN USUARIO us ON us.id = ur.usuario_id
JOIN @usuarios u ON u.email = us.email
JOIN ROL r ON r.id = ur.rol_id
WHERE ur.fabrica_id = @fabrica
  AND r.nombre <> u.rol;

INSERT INTO USUARIO_ROL (usuario_id, rol_id, fabrica_id)
SELECT us.id, r.id, @fabrica
FROM @usuarios u
JOIN USUARIO us ON us.email = u.email
JOIN ROL r ON r.nombre = u.rol
WHERE NOT EXISTS (SELECT 1 FROM USUARIO_ROL ur
                  WHERE ur.usuario_id = us.id AND ur.rol_id = r.id AND ur.fabrica_id = @fabrica);

COMMIT;

-- Comprobación
SELECT us.nombre AS usuario, us.email, r.nombre AS rol
FROM USUARIO us
LEFT JOIN USUARIO_ROL ur ON ur.usuario_id = us.id AND ur.fabrica_id = @fabrica
LEFT JOIN ROL r ON r.id = ur.rol_id
ORDER BY us.nombre, r.nombre;
GO
