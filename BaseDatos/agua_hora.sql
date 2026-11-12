USE GPE;
GO

-- Hora de cada control de agua, para distinguir varias tomas del mismo punto en un día.
-- En NULL en los controles antiguos (el Excel no la tenía). Se puede ejecutar más de una vez.

IF COL_LENGTH('AGUA_CONTROL', 'hora') IS NULL
    ALTER TABLE AGUA_CONTROL ADD hora TIME(0) NULL;
GO
