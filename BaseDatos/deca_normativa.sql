USE GPE;
GO

-- Datos que pide la normativa del DeCA electrónico (Orden FOM/2861/2012, art. 6) y que faltaban:
-- cargador contractual, matrícula del remolque y lugares de carga y descarga.
-- Solo para bases creadas antes con crear_tablas.sql. Se puede ejecutar más de una vez.

IF COL_LENGTH('DECA', 'cargador_tipo') IS NULL
    ALTER TABLE DECA ADD
        -- Los DeCA que ya existían quedan con el remitente como cargador contractual: revisarlos
        cargador_tipo NVARCHAR(12) NOT NULL
            CONSTRAINT DF_deca_cargador_tipo DEFAULT N'REMITENTE'
            CONSTRAINT CK_deca_cargador_tipo CHECK (cargador_tipo IN (N'REMITENTE', N'DESTINATARIO', N'OTRO')),
        cargador_nombre NVARCHAR(150) NULL,
        cargador_nif NVARCHAR(20) NULL,
        cargador_direccion NVARCHAR(200) NULL,
        cargador_cp NVARCHAR(10) NULL,
        cargador_poblacion NVARCHAR(100) NULL,
        cargador_provincia NVARCHAR(100) NULL,
        cargador_pais NVARCHAR(60) NULL,
        cargador_telefono NVARCHAR(30) NULL;
GO

IF COL_LENGTH('DECA', 'matricula_remolque') IS NULL
    ALTER TABLE DECA ADD
        matricula_remolque NVARCHAR(20) NULL,
        lugar_carga NVARCHAR(250) NULL,
        lugar_descarga NVARCHAR(250) NULL;
GO

IF OBJECT_ID('CK_deca_cargador_otro', 'C') IS NULL
    ALTER TABLE DECA ADD CONSTRAINT CK_deca_cargador_otro CHECK (cargador_tipo <> N'OTRO' OR
        (cargador_nombre IS NOT NULL AND cargador_nif IS NOT NULL AND cargador_direccion IS NOT NULL
         AND cargador_cp IS NOT NULL AND cargador_poblacion IS NOT NULL AND cargador_pais IS NOT NULL));
GO
