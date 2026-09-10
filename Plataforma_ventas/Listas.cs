namespace Plataforma_ventas;

/// <summary>
/// Reglas de negocio de las listas de precio, independientes de la base de datos
/// y de la interfaz, para poder validarlas con pruebas unitarias.
/// </summary>
public static class Listas
{
    /// <summary>
    /// Devuelve el nombre de la columna de precio para el número de lista dado
    /// (1 → "Lista1" … 5 → "Lista5"). Cualquier valor fuera de 1..5 cae en "Lista5".
    /// Es una lista blanca fija: el resultado nunca proviene de entrada del usuario,
    /// por lo que es seguro usarlo como nombre de columna en una consulta.
    /// </summary>
    public static string ColumnaLista(int numLista) => numLista switch
    {
        1 => "Lista1",
        2 => "Lista2",
        3 => "Lista3",
        4 => "Lista4",
        _ => "Lista5"
    };
}
