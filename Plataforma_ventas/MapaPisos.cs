using System.Text.RegularExpressions;

namespace Plataforma_ventas;

/// <summary>
/// Arma el mapa de ventas por piso: la cuadrícula que el área comercial ya maneja en
/// Excel, con un renglón por piso y una columna por línea de apartamentos.
/// </summary>
public static class MapaPisos
{
    /// <summary>Una columna de la cuadrícula: la línea, su tipo y su área.</summary>
    public sealed record Linea(string Nombre, string Tipo, string Metros);

    /// <summary>Una celda: la unidad que ocupa un piso en una línea.</summary>
    public sealed record Celda(int Id, string Apto, string Estado, string Quien);

    /// <summary>Un renglón: el piso y sus celdas, en el mismo orden que las líneas.</summary>
    public sealed record Piso(string Nombre, List<Celda?> Celdas);

    /// <summary>Una cuadrícula: una torre (y etapa) completa.</summary>
    public sealed record Torre(string Nombre, string Etapa, List<Linea> Lineas, List<Piso> Pisos);

    /// <summary>
    /// La línea de un apartamento es lo que queda del nombre al quitar la torre y el
    /// piso: "27A" con piso 27 → "A"; "1204 T3" con piso 12 → "04". Sirve para las dos
    /// nomenclaturas que usa la empresa. Si no queda nada, se usa el nombre completo.
    /// </summary>
    public static string LineaDe(string? apto, string? piso)
    {
        var s = Regex.Replace(apto ?? "", @"(?<![A-Za-z])T\s*-?\s*\d{1,2}(?!\d)", "",
                              RegexOptions.IgnoreCase).Trim();
        var p = (piso ?? "").Trim();
        if (p.Length > 0 && s.StartsWith(p, StringComparison.OrdinalIgnoreCase) && s.Length > p.Length)
            s = s.Substring(p.Length).Trim(' ', '-', '_');
        return s.Length > 0 ? s : (apto ?? "").Trim();
    }

    /// <summary>Construye las cuadrículas a partir de la lista plana de inmuebles.</summary>
    public static List<Torre> Construir(IEnumerable<dynamic> inmuebles)
    {
        var resultado = new List<Torre>();

        var porTorre = inmuebles
            .GroupBy(i => ((string)i.Etapa, string.IsNullOrWhiteSpace((string)i.Torre) ? "" : (string)i.Torre))
            .OrderBy(g => g.Key.Item1, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.Item2, StringComparer.OrdinalIgnoreCase);

        foreach (var g in porTorre)
        {
            var unidades = g.Select(i => new
            {
                Id = (int)i.Id,
                Apto = (string)i.Apto,
                Tipo = ((string)i.Tipo ?? "").Trim().ToUpper(),
                Metros = (string)i.Metros,
                Piso = ((string)i.Piso ?? "").Trim(),
                Estado = (string)i.Estado,
                Linea = LineaDe((string)i.Apto, (string)i.Piso),
                Quien = (string)i.Estado == "EN PROCESO" ? (string)i.EnProcesoPor
                      : (string)i.Estado == "RESERVADO" ? (string)i.ReservadoPor : "",
            }).ToList();

            // Columnas: interiores primero, luego exteriores; dentro de cada grupo por
            // nombre. El área de la columna es la que más se repite en esa línea.
            var lineas = unidades
                .GroupBy(u => u.Linea)
                .Select(l => new Linea(
                    l.Key,
                    l.GroupBy(u => u.Tipo).OrderByDescending(t => t.Count()).First().Key,
                    l.GroupBy(u => u.Metros).OrderByDescending(m => m.Count()).First().Key))
                .OrderBy(l => l.Tipo == "EXT" ? 1 : 0)
                .ThenBy(l => l.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var indice = lineas.Select((l, i) => (l.Nombre, i)).ToDictionary(x => x.Nombre, x => x.i);

            // Renglones: pisos de arriba hacia abajo, como en el edificio.
            var pisos = unidades
                .GroupBy(u => u.Piso)
                .OrderByDescending(p => int.TryParse(p.Key, out int n) ? n : int.MinValue)
                .ThenByDescending(p => p.Key, StringComparer.OrdinalIgnoreCase)
                .Select(p =>
                {
                    var celdas = new List<Celda?>(new Celda?[lineas.Count]);
                    foreach (var u in p.OrderBy(u => u.Apto))
                        celdas[indice[u.Linea]] = new Celda(u.Id, u.Apto, u.Estado, u.Quien);
                    return new Piso(p.Key, celdas);
                })
                .ToList();

            resultado.Add(new Torre(g.Key.Item2, g.Key.Item1, lineas, pisos));
        }
        return resultado;
    }
}
