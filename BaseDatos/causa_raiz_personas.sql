-- Causa raíz (26/11/2026): el grupo "Personal" pasa a llamarse "Personas" (nombre que dio calidad).
-- Cambia las no conformidades que ya lo tenían. Se puede ejecutar más de una vez.
-- Los subgrupos antiguos de Equipos y Procesos no se tocan: la ficha los sigue mostrando tal cual.
-- No lleva USE: se elige la base de datos al ejecutarlo, con -d. Con sqlcmd, usar -f 65001.
--   sqlcmd -S localhost -E -C -b -f 65001 -d miGPE -i BaseDatos\causa_raiz_personas.sql
-- En el servidor: instalar-servidor.ps1 -InstanciaSql MSSQLSERVER -Scripts causa_raiz_personas

-- El historial pide quién hace el cambio: el usuario 1 (administrador)
EXEC sp_set_session_context @key = N'usuario_id', @value = 1;

UPDATE NO_CONFORMIDAD SET causa_raiz = N'Personas' WHERE causa_raiz = N'Personal';
