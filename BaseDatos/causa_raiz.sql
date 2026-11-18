USE GPE;
GO

-- Fase 3 de la ficha (análisis de la causa) como en el Excel MT-RG09 desde noviembre de 2026:
-- los 5 porqués y la causa raíz (uno de los grupos de la hoja CAUSA RAIZ).
-- Se ejecuta después de no_conformidades.sql y se puede ejecutar más de una vez.
-- descripcion_causa y analisis_causa_raiz se quedan: guardan lo que se escribió antes.

IF COL_LENGTH('NO_CONFORMIDAD', 'porque_pregunta_1') IS NULL
    ALTER TABLE NO_CONFORMIDAD ADD
        porque_pregunta_1 NVARCHAR(1000) NULL, porque_respuesta_1 NVARCHAR(1000) NULL,
        porque_pregunta_2 NVARCHAR(1000) NULL, porque_respuesta_2 NVARCHAR(1000) NULL,
        porque_pregunta_3 NVARCHAR(1000) NULL, porque_respuesta_3 NVARCHAR(1000) NULL,
        porque_pregunta_4 NVARCHAR(1000) NULL, porque_respuesta_4 NVARCHAR(1000) NULL,
        porque_pregunta_5 NVARCHAR(1000) NULL, porque_respuesta_5 NVARCHAR(1000) NULL;
GO

-- Uno de los grupos de la hoja CAUSA RAIZ (Personal, Equipos...). La lista está en la aplicación (CausasRaiz.cs).
IF COL_LENGTH('NO_CONFORMIDAD', 'causa_raiz') IS NULL
    ALTER TABLE NO_CONFORMIDAD ADD causa_raiz NVARCHAR(30) NULL;
GO
