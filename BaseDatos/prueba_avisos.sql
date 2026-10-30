/* Prueba de avisos: crea un usuario con el correo de abajo y le da los roles
   Administración, Transporte y Almacén en la fábrica 1. Después de crear_tablas.sql. */

USE GPE;
GO

DECLARE @correo NVARCHAR(150) = N'estelaretin10@gmail.com';
DECLARE @fabrica INT = 1;

IF NOT EXISTS (SELECT 1 FROM USUARIO WHERE email = @correo)
    INSERT INTO USUARIO (nombre, email) VALUES (N'Usuario de prueba', @correo);

DECLARE @usuario INT = (SELECT id FROM USUARIO WHERE email = @correo);

INSERT INTO USUARIO_ROL (usuario_id, rol_id, fabrica_id)
SELECT @usuario, r.id, @fabrica
FROM ROL r
WHERE r.nombre IN (N'Administración', N'Transporte', N'Almacén')
  AND NOT EXISTS (SELECT 1 FROM USUARIO_ROL ur
                  WHERE ur.usuario_id = @usuario AND ur.rol_id = r.id AND ur.fabrica_id = @fabrica);

-- Comprobación: qué avisos le llegarán a cada uno
SELECT e.fase, e.nombre AS paso, r.nombre AS rol, u.nombre, u.email
FROM ESTADO_DEVOLUCION e
JOIN ROL r ON r.id = e.rol_responsable_id
JOIN USUARIO_ROL ur ON ur.rol_id = r.id AND ur.fabrica_id = @fabrica
JOIN USUARIO u ON u.id = ur.usuario_id
ORDER BY e.fase;
GO
