-- Borra TODAS las devoluciones y lo que cuelga de ellas, para empezar limpio.
-- No se puede deshacer: hacer antes una copia de seguridad de la base si se quiere guardar algo.
-- Se queda: los estados de devolución, clientes, productos, transportistas y usuarios.
-- Sin USE: se ejecuta sobre la base que se indique con -d.
--   sqlcmd -S localhost -E -C -I -f 65001 -d miGPE -i .\limpiar_devoluciones.sql

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Los triggers de historial necesitan saber quién hace el cambio (1 = Administrador)
EXEC sp_set_session_context @key = N'usuario_id', @value = 1;

BEGIN TRANSACTION;

-- Resúmenes diarios de devoluciones (abonos y revisiones pendientes)
DELETE FROM AVISO_RESUMEN;

-- Al borrar la devolución se borran también sus productos, recogidas, revisiones,
-- adjuntos y avisos por correo (ON DELETE CASCADE)
DELETE FROM DEVOLUCION;

-- El historial de las devoluciones (incluido el de este borrado)
DELETE FROM HISTORIAL_CAMBIOS
WHERE devolucion_id IS NOT NULL
   OR tabla_afectada IN ('DEVOLUCION', 'DEVOLUCION_PRODUCTO', 'REVISION_LINEA', 'RECOGIDA', 'ADJUNTO');

-- Que la próxima devolución vuelva a ser la 1
DBCC CHECKIDENT ('DEVOLUCION', RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('DEVOLUCION_PRODUCTO', RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('RECOGIDA', RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('REVISION_LINEA', RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('ADJUNTO', RESEED, 0) WITH NO_INFOMSGS;
DBCC CHECKIDENT ('AVISO_CORREO', RESEED, 0) WITH NO_INFOMSGS;

COMMIT;

SELECT
    (SELECT COUNT(*) FROM DEVOLUCION) AS devoluciones,
    (SELECT COUNT(*) FROM DEVOLUCION_PRODUCTO) AS productos,
    (SELECT COUNT(*) FROM RECOGIDA) AS recogidas,
    (SELECT COUNT(*) FROM REVISION_LINEA) AS revisiones,
    (SELECT COUNT(*) FROM ADJUNTO) AS adjuntos,
    (SELECT COUNT(*) FROM AVISO_CORREO) AS avisos;
