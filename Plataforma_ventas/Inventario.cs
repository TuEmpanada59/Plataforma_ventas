namespace Plataforma_ventas;

/// <summary>
/// El inventario agrupado por área, que es como el área comercial lo piensa: no
/// "cuántas unidades quedan" sino "cuántas de 98 metros quedan". Una lista corrida de
/// setenta unidades obliga a contar a ojo para responder eso.
///
/// Lo usan el informe del administrador y el perfil de dirección, así que el cálculo
/// vive aquí y no dentro de una vista.
/// </summary>
public static class Inventario
{
    /// <summary>Una unidad, con lo mínimo para dibujarla y agruparla.</summary>
    public sealed record Unidad(
        string Apto, string Torre, string Etapa, string Piso, string Tipo,
        string Metros, string Estado, int Lista, long Precio);

    /// <summary>Un área con sus cifras y sus unidades.</summary>
    public sealed record Area(
        string Metros, string Tipo, int Lista,
        int Total, int Vendidas, int Reservadas, int Disponibles, long Valor,
        List<Unidad> Unidades);

    /// <summary>
    /// El estado que se muestra. Una unidad "en proceso" la tiene un asesor en la mano
    /// pero todavía no está comprometida, así que se presenta como disponible, igual
    /// que en el mapa y en el informe.
    /// </summary>
    public static string EstadoVisible(string? estado)
        => estado == "VENDIDO" || estado == "RESERVADO" ? estado! : "DISPONIBLE";

    /// <summary>
    /// Ordena las áreas por su valor y no por texto: alfabéticamente "140.96" iría
    /// antes que "98.17", que no es como nadie lee un cuadro de áreas.
    /// </summary>
    public static double AreaNumerica(string? metros)
    {
        double.TryParse((metros ?? "").Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double d);
        return d;
    }

    /// <summary>Agrupa las unidades por área, ya ordenadas para mostrar.</summary>
    public static List<Area> Agrupar(IEnumerable<Unidad> unidades)
        => unidades
            .GroupBy(u => u.Metros)
            .Select(g => new Area(
                Metros: g.Key,
                // El tipo y la lista son los mismos para toda el área; se toma el
                // primero que venga informado.
                Tipo: g.Select(x => x.Tipo).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "",
                Lista: g.Select(x => x.Lista).FirstOrDefault(),
                Total: g.Count(),
                Vendidas: g.Count(x => x.Estado == "VENDIDO"),
                Reservadas: g.Count(x => x.Estado == "RESERVADO"),
                Disponibles: g.Count(x => EstadoVisible(x.Estado) == "DISPONIBLE"),
                Valor: g.Sum(x => x.Precio),
                Unidades: g.OrderBy(x => x.Torre, StringComparer.OrdinalIgnoreCase)
                           .ThenBy(x => PisoNumerico(x.Piso))
                           .ThenBy(x => x.Apto, StringComparer.OrdinalIgnoreCase)
                           .ToList()))
            .OrderBy(a => AreaNumerica(a.Metros))
            .ToList();

    private static int PisoNumerico(string? piso)
        => int.TryParse((piso ?? "").Trim(), out int n) ? n : int.MaxValue;
}
