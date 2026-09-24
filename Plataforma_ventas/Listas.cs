using System.Linq;

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

    /// <summary>
    /// Aplica un ajuste a un precio de lista. <paramref name="porcentaje"/> distingue
    /// entre sumar pesos y aplicar un porcentaje; <paramref name="valor"/> puede ser
    /// negativo para bajar el precio.
    ///
    /// El resultado se redondea al millar más cercano: un porcentaje deja precios como
    /// $412.837.451 que nadie publica así, y redondear al aplicar (y no al mostrar)
    /// evita que el precio que ve el asesor y el que se guarda sean distintos.
    /// Nunca devuelve un valor negativo. Un precio en 0 (lista sin usar) se deja igual:
    /// no es un precio, es la ausencia de uno.
    /// </summary>
    public static long AjustarPrecio(long precio, decimal valor, bool porcentaje)
    {
        if (precio <= 0) return precio;

        decimal ajustado = porcentaje
            ? precio + precio * valor / 100m
            : precio + valor;

        if (ajustado <= 0) return 0;

        // Redondeo al millar, al alza en el punto medio (500 → 1.000).
        long redondeado = (long)decimal.Round(ajustado / 1000m, 0, MidpointRounding.AwayFromZero) * 1000;
        return redondeado < 0 ? 0 : redondeado;
    }

    /// <summary>
    /// Lee la columna "VENDIDO EN LISTA" del Excel: con qué lista de precios se cerró
    /// una unidad que llega vendida o reservada. Se conserva el precio con el que se
    /// negoció y no el de la lista vigente, porque al escriturar se le cobraría al
    /// cliente un valor que nunca aceptó.
    ///
    /// Cualquier cosa que no sea un número del 1 al 5 cae en la Lista 1, que es como
    /// venían los archivos antes de que existiera esta columna.
    /// </summary>
    public static int ListaNegociada(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return 1;
        var digitos = new string(valor.Where(char.IsDigit).ToArray());
        return int.TryParse(digitos, out int n) && n >= 1 && n <= 5 ? n : 1;
    }
}
