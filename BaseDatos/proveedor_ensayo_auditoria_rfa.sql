USE GPE;
GO

-- ENSAYO TRAZABILIDAD y AUDITORÍA pasan de texto a fecha, con una casilla "No aplica" y su motivo
-- ("No se realiza por confianza en el proveedor...", "Elaboramos su producto con su propia materia prima").
-- RFA pasa a ser solo el año.
-- Se ejecuta después de proveedores.sql y se puede ejecutar más de una vez.
-- Con sqlcmd, usar -I -f 65001 (por las tildes).
--
-- Lo que ya había escrito se pasa así: una fecha (dd/MM/yyyy) va a la fecha; "NA" o "NO APLICA" marca "No aplica"
-- sin motivo; cualquier otro texto marca "No aplica" con ese texto de motivo. En RFA, el año si es un número.
-- El historial (TR_PROVEEDOR_HISTORIAL) copia la fila entera, así que recoge las columnas nuevas sin cambiarlo.
IF COL_LENGTH('PROVEEDOR', 'ensayo_fecha') IS NULL
    ALTER TABLE PROVEEDOR ADD
        ensayo_fecha DATE NULL,
        ensayo_no_aplica BIT NOT NULL CONSTRAINT DF_proveedor_ensayo_no_aplica DEFAULT 0,
        ensayo_motivo NVARCHAR(500) NULL,
        auditoria_fecha DATE NULL,
        auditoria_no_aplica BIT NOT NULL CONSTRAINT DF_proveedor_auditoria_no_aplica DEFAULT 0,
        auditoria_motivo NVARCHAR(500) NULL,
        rfa_anio SMALLINT NULL;
GO

-- Pasar lo escrito y quitar las columnas de texto (solo si todavía están)
IF COL_LENGTH('PROVEEDOR', 'ensayo_trazabilidad') IS NOT NULL
BEGIN
    DECLARE @usuario INT = (SELECT TOP 1 id FROM USUARIO ORDER BY id);
    EXEC sp_set_session_context @key = N'usuario_id', @value = @usuario;   -- lo pide el trigger de historial

    EXEC(N'
    UPDATE PROVEEDOR SET
        ensayo_fecha = TRY_CONVERT(DATE, LTRIM(RTRIM(ensayo_trazabilidad)), 103),
        ensayo_no_aplica = CASE WHEN ensayo_trazabilidad IS NOT NULL
                                 AND TRY_CONVERT(DATE, LTRIM(RTRIM(ensayo_trazabilidad)), 103) IS NULL THEN 1 ELSE 0 END,
        ensayo_motivo = CASE WHEN TRY_CONVERT(DATE, LTRIM(RTRIM(ensayo_trazabilidad)), 103) IS NULL
                              AND UPPER(LTRIM(RTRIM(ensayo_trazabilidad))) NOT IN (''NA'', ''N/A'', ''NO APLICA'')
                             THEN LTRIM(RTRIM(ensayo_trazabilidad)) END,
        auditoria_fecha = TRY_CONVERT(DATE, LTRIM(RTRIM(auditoria)), 103),
        auditoria_no_aplica = CASE WHEN auditoria IS NOT NULL
                                    AND TRY_CONVERT(DATE, LTRIM(RTRIM(auditoria)), 103) IS NULL THEN 1 ELSE 0 END,
        auditoria_motivo = CASE WHEN TRY_CONVERT(DATE, LTRIM(RTRIM(auditoria)), 103) IS NULL
                                 AND UPPER(LTRIM(RTRIM(auditoria))) NOT IN (''NA'', ''N/A'', ''NO APLICA'')
                                THEN LTRIM(RTRIM(auditoria)) END,
        rfa_anio = CASE WHEN TRY_CAST(LTRIM(RTRIM(rfa)) AS SMALLINT) BETWEEN 1990 AND 2100
                        THEN TRY_CAST(LTRIM(RTRIM(rfa)) AS SMALLINT) END
    WHERE ensayo_trazabilidad IS NOT NULL OR auditoria IS NOT NULL OR rfa IS NOT NULL;');

    ALTER TABLE PROVEEDOR DROP COLUMN ensayo_trazabilidad, auditoria, rfa;
END;
GO

-- Con fecha no puede estar "No aplica", y el motivo solo va con "No aplica"
IF OBJECT_ID('CK_proveedor_ensayo') IS NULL
    ALTER TABLE PROVEEDOR ADD
        CONSTRAINT CK_proveedor_ensayo CHECK (
            (ensayo_no_aplica = 0 OR ensayo_fecha IS NULL) AND (ensayo_no_aplica = 1 OR ensayo_motivo IS NULL)),
        CONSTRAINT CK_proveedor_auditoria CHECK (
            (auditoria_no_aplica = 0 OR auditoria_fecha IS NULL) AND (auditoria_no_aplica = 1 OR auditoria_motivo IS NULL)),
        CONSTRAINT CK_proveedor_rfa CHECK (rfa_anio BETWEEN 1990 AND 2100);
GO
