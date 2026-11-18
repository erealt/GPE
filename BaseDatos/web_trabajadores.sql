USE GPE;
GO

-- Web de los trabajadores: cada uno entra con su usuario y contraseña y ve las acciones de
-- NC/incidencias que tiene asignadas. Se ejecuta después de acciones_nc_avisos.sql y se puede
-- ejecutar más de una vez. Con sqlcmd, usar -I -f 65001 (por las tildes).

-- persona: su nombre en la lista PERSONA de CALIDAD_LISTA, que es lo que se pone de responsable
-- en las acciones. No son usuarios de GPE (tabla USUARIO): solo entran en esta web.
IF OBJECT_ID('TRABAJADOR_WEB') IS NULL
    CREATE TABLE TRABAJADOR_WEB (
        id INT IDENTITY(1,1) PRIMARY KEY,
        usuario NVARCHAR(50) NOT NULL CONSTRAINT UQ_trabajador_web_usuario UNIQUE,
        clave NVARCHAR(200) NOT NULL,             -- cifrada (PBKDF2), nunca la contraseña tal cual
        persona NVARCHAR(150) NOT NULL,
        activo BIT NOT NULL CONSTRAINT DF_trabajador_web_activo DEFAULT 1,
        creado_en DATETIME2 NOT NULL CONSTRAINT DF_trabajador_web_creado DEFAULT SYSDATETIME()
    );
GO

-- PRUEBA: usuario "estela", contraseña "Prueba1234", con las acciones de la persona "Estela".
-- Quitar cuando se decida cómo se dan de alta los trabajadores.
IF NOT EXISTS (SELECT 1 FROM TRABAJADOR_WEB WHERE usuario = N'estela')
    INSERT INTO TRABAJADOR_WEB (usuario, clave, persona)
    VALUES (N'estela', N'PBKDF2-SHA256$100000$CqTgTnEJmb9XJ0F2M7hHcA==$3I5R4JtE/3GD01zSHzFnn8+FbthA9OF+dmt/E/0wKnM=', N'Estela');
GO
