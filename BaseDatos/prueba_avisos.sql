/* Prueba de los avisos por correo: hace que el aviso de "nueva devolución"
   llegue al correo indicado abajo. Ejecutar después de migracion_avisos_correo.sql.

   - Crea el rol "Transporte" y lo pone como responsable del paso "Recogida solicitada".
   - Crea un usuario de prueba con ese correo (si no existe ya) y le da el rol
     Transporte en la fábrica 1 (la que usa la aplicación de momento). */

USE GPE;
GO

DECLARE @correo NVARCHAR(150) = N'estelaretin10@gmail.com';   
DECLARE @fabrica INT = 1;

IF NOT EXISTS (SELECT 1 FROM ROL WHERE nombre = N'Transporte')
    INSERT INTO ROL (nombre) VALUES (N'Transporte');

DECLARE @rol INT = (SELECT id FROM ROL WHERE nombre = N'Transporte');

-- Quien gestiona el transporte es el responsable de programar la recogida
UPDATE ESTADO_DEVOLUCION SET rol_responsable_id = @rol WHERE fase = 2;

IF NOT EXISTS (SELECT 1 FROM USUARIO WHERE email = @correo)
    INSERT INTO USUARIO (nombre, email) VALUES (N'Gestor transporte (prueba)', @correo);

DECLARE @usuario INT = (SELECT id FROM USUARIO WHERE email = @correo);

IF NOT EXISTS (SELECT 1 FROM USUARIO_ROL WHERE usuario_id = @usuario AND rol_id = @rol AND fabrica_id = @fabrica)
    INSERT INTO USUARIO_ROL (usuario_id, rol_id, fabrica_id) VALUES (@usuario, @rol, @fabrica);

-- Comprobación: a quién le llegará el aviso de una nueva devolución
SELECT u.nombre, u.email
FROM ESTADO_DEVOLUCION e
JOIN USUARIO_ROL ur ON ur.rol_id = e.rol_responsable_id AND ur.fabrica_id = @fabrica
JOIN USUARIO u ON u.id = ur.usuario_id
WHERE e.fase = 2;
GO

/* ---------- Aviso al almacén cuando se solicita la recogida ----------
   - Permite el nuevo tipo de aviso RECOGIDA_SOLICITADA (en una base de datos ya creada).
   - Crea el rol "Almacén", lo pone como responsable del paso "Mercancía recibida"
     y se lo da al mismo usuario de prueba. */

ALTER TABLE AVISO_CORREO DROP CONSTRAINT CK_aviso_tipo;
ALTER TABLE AVISO_CORREO ADD CONSTRAINT CK_aviso_tipo
    CHECK (tipo IN ('NUEVA_DEVOLUCION', 'RECOGIDA_SOLICITADA'));
GO

DECLARE @correo NVARCHAR(150) = N'estelaretin10@gmail.com';
DECLARE @fabrica INT = 1;

IF NOT EXISTS (SELECT 1 FROM ROL WHERE nombre = N'Almacén')
    INSERT INTO ROL (nombre) VALUES (N'Almacén');

DECLARE @rol INT = (SELECT id FROM ROL WHERE nombre = N'Almacén');

-- El almacén es quien recibe la mercancía
UPDATE ESTADO_DEVOLUCION SET rol_responsable_id = @rol WHERE fase = 3;

DECLARE @usuario INT = (SELECT id FROM USUARIO WHERE email = @correo);

IF NOT EXISTS (SELECT 1 FROM USUARIO_ROL WHERE usuario_id = @usuario AND rol_id = @rol AND fabrica_id = @fabrica)
    INSERT INTO USUARIO_ROL (usuario_id, rol_id, fabrica_id) VALUES (@usuario, @rol, @fabrica);

-- Comprobación: a quién le llegará el aviso al solicitar una recogida
SELECT u.nombre, u.email
FROM ESTADO_DEVOLUCION e
JOIN USUARIO_ROL ur ON ur.rol_id = e.rol_responsable_id AND ur.fabrica_id = @fabrica
JOIN USUARIO u ON u.id = ur.usuario_id
WHERE e.fase = 3;
GO

