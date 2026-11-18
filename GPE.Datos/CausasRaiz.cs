namespace GPE.Datos;

// Grupos de la hoja CAUSA RAIZ del Excel MT-RG09 (en el Excel son imágenes en inglés:
// People, Equipment, Economics, Processes, Food). La causa raíz de un registro es uno de ellos.
// Lista fija: si calidad cambia la hoja, se cambia aquí.
public static class CausasRaiz
{
    public static readonly string[] Todas = { "Personal", "Equipos", "Economía", "Procesos", "Alimentos" };
}
