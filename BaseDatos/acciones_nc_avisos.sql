USE GPE;
GO

-- Prioridad de las acciones y correos a sus responsables. Se ejecuta después de no_conformidades.sql
-- y se puede ejecutar más de una vez. Con sqlcmd, usar -I -f 65001 (por las tildes).

-- Correo de cada persona de la lista PERSONA (los responsables de las acciones).
-- Vacío: a esa persona no se le envía nada. Se rellena a mano, por ejemplo:
--   UPDATE CALIDAD_LISTA SET email = N'celia@empresa.com' WHERE lista = 'PERSONA' AND valor = N'Celia';
IF COL_LENGTH('CALIDAD_LISTA', 'email') IS NULL
    ALTER TABLE CALIDAD_LISTA ADD email NVARCHAR(150) NULL;
GO

-- Solo en las acciones inmediatas y correctoras; NULL = sin prioridad
IF COL_LENGTH('NC_ACCION', 'prioridad') IS NULL
    ALTER TABLE NC_ACCION ADD prioridad NVARCHAR(5) NULL
        CONSTRAINT CK_nc_accion_prioridad CHECK (prioridad IN (N'Alta', N'Media', N'Baja'));
GO

-- A qué responsable se le avisó ya de cada acción. Si se cambia el responsable, se avisa al nuevo.
-- email NULL: la persona no tenía correo y no se le envió nada (no se le enviará al ponérselo:
-- le llegará en el resumen diario).
IF OBJECT_ID('NC_ACCION_AVISO') IS NULL
BEGIN
    CREATE TABLE NC_ACCION_AVISO (
        accion_id INT PRIMARY KEY,
        responsable NVARCHAR(150) NOT NULL,
        email NVARCHAR(150) NULL,
        avisado_en DATETIME2 NOT NULL CONSTRAINT DF_nc_accion_aviso_avisado DEFAULT SYSDATETIME(),
        CONSTRAINT FK_nc_accion_aviso_accion FOREIGN KEY (accion_id) REFERENCES NC_ACCION(id) ON DELETE CASCADE
    );

    -- Las acciones que ya existían se dan por avisadas: si no, el día que se pongan los correos
    -- a cada persona le llegaría un aviso por cada acción antigua
    INSERT INTO NC_ACCION_AVISO (accion_id, responsable)
    SELECT id, responsable FROM NC_ACCION WHERE responsable IS NOT NULL;
END;
GO

-- El resumen diario de acciones se apunta en AVISO_RESUMEN con su propio tipo
-- (esa tabla no limita los tipos, así que no hay que cambiarla).
