USE GPE;
GO

/*
   Control de agua: permite apuntar el mismo punto más de una vez en el mismo día.
   Solo para bases de datos creadas antes con control_agua.sql, que ya lo trae.
   Se puede ejecutar más de una vez.
*/

IF OBJECT_ID('UQ_agua_control', 'UQ') IS NOT NULL
    ALTER TABLE AGUA_CONTROL DROP CONSTRAINT UQ_agua_control;
GO

-- La pantalla busca los controles por día; antes lo cubría la restricción
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_agua_control_fecha' AND object_id = OBJECT_ID('AGUA_CONTROL'))
    CREATE INDEX IX_agua_control_fecha ON AGUA_CONTROL (fecha);
GO
