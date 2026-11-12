USE GPE;
GO

-- Repetir una toma de agua: cuando el resultado no convence se toma otra vez el mismo punto.
-- repite_id es la toma que se repite (la que no convenció), que se queda guardada.
-- Solo para bases creadas antes con control_agua.sql. Se puede ejecutar más de una vez.

IF COL_LENGTH('AGUA_CONTROL', 'repite_id') IS NULL
    ALTER TABLE AGUA_CONTROL ADD repite_id INT NULL;
GO

IF OBJECT_ID('FK_agua_control_repite', 'F') IS NULL
    ALTER TABLE AGUA_CONTROL ADD CONSTRAINT FK_agua_control_repite FOREIGN KEY (repite_id) REFERENCES AGUA_CONTROL(id);
GO
