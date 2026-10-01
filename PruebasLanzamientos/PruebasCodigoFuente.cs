using System.Text.RegularExpressions;
using Xunit;

namespace PruebasLanzamientos;

/// <summary>
/// Reglas que se verifican leyendo el código fuente, no el ensamblado: hablan de cómo
/// está escrita una llamada, y eso ya no existe después de compilar.
///
/// Son dos, las dos de CLAUDE.md, y las dos se cumplen hoy al cien por ciento. Están
/// aquí para que sigan cumpliéndose.
/// </summary>
public class PruebasCodigoFuente
{
    // ── Dónde está el código ─────────────────────────────────────────────────

    /// <summary>
    /// Las pruebas corren desde bin/, así que se sube hasta encontrar la carpeta del
    /// proyecto. Si no aparece, la prueba falla en vez de pasar en falso: una prueba
    /// que se salta sola cuando no encuentra qué revisar no protege nada.
    /// </summary>
    private static string CarpetaDelProyecto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidata = Path.Combine(dir.FullName, "Plataforma_ventas");
            if (Directory.Exists(Path.Combine(candidata, "Controllers"))) return candidata;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "No se encontró la carpeta Plataforma_ventas subiendo desde " + AppContext.BaseDirectory);
    }

    private static IEnumerable<(string Archivo, int Linea, string Texto)> LineasDeCodigo()
    {
        var raiz = CarpetaDelProyecto();
        foreach (var archivo in Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")) continue;

            var relativo = Path.GetRelativePath(raiz, archivo);
            var lineas = File.ReadAllLines(archivo);
            for (int i = 0; i < lineas.Length; i++)
            {
                var texto = lineas[i].Trim();
                // Los comentarios describen el código, no lo ejecutan. Varias de las
                // explicaciones de este repositorio nombran justamente lo que está
                // prohibido, y marcarlas sería ruido.
                if (texto.StartsWith("//") || texto.StartsWith("*") || texto.StartsWith("///")) continue;
                yield return (relativo, i + 1, texto);
            }
        }
    }

    // ── 1) Nada de acceso síncrono a la base ─────────────────────────────────

    /// <summary>
    /// Una sola llamada síncrona bloquea un hilo mientras SQL Server responde. Con
    /// treinta asesores conectados durante un lanzamiento, esos hilos son justo lo
    /// que no sobra.
    /// </summary>
    [Fact]
    public void NoHayAccesoSincronoALaBase()
    {
        // Paréntesis vacíos a propósito: Stream.Read(buffer, ...) no tiene nada que
        // ver con un DataReader y no debe marcarse.
        var sincronas = new Regex(@"\.(ExecuteReader|ExecuteNonQuery|ExecuteScalar|Open|Read)\(\)");

        var hallazgos = LineasDeCodigo()
            .Where(l => sincronas.IsMatch(l.Texto))
            .Select(l => $"{l.Archivo}:{l.Linea}  {l.Texto}")
            .ToList();

        Assert.True(hallazgos.Count == 0,
            "Acceso síncrono a la base (usa la versión Async con await):\n  " +
            string.Join("\n  ", hallazgos));
    }

    // ── 2) Nada de SQL armado pegando variables ──────────────────────────────

    /// <summary>
    /// Esta regla es más estrecha de lo que parece, y conviene saberlo: prohíbe pegar
    /// una variable a una consulta con "+", que es la forma clásica de abrir una
    /// inyección SQL. NO prohíbe la interpolación, porque el proyecto la usa de forma
    /// legítima —las guardas de esquema que agregan una columna solo si existe, y el
    /// nombre de columna que devuelve Listas.ColumnaLista, que sale de una lista
    /// cerrada—. Distinguir una interpolación segura de una peligrosa no se puede
    /// hacer con una expresión regular: eso lo hace un análisis que siga el dato desde
    /// el formulario, como CodeQL.
    /// </summary>
    [Fact]
    public void NoSeArmaSqlPegandoVariables()
    {
        // Una cadena que contiene SQL y, pegado con +, algo que no es otra cadena.
        var sql = new Regex(@"""[^""]*\b(SELECT|INSERT|UPDATE|DELETE|FROM|WHERE|VALUES|ORDER\s+BY|SET)\b",
                            RegexOptions.IgnoreCase);
        var pegado = new Regex(@"""\s*\+\s*[A-Za-z_]\w*|[A-Za-z_]\w*\s*\+\s*""");

        var hallazgos = LineasDeCodigo()
            .Where(l => sql.IsMatch(l.Texto) && pegado.IsMatch(l.Texto))
            .Select(l => $"{l.Archivo}:{l.Linea}  {l.Texto}")
            .ToList();

        Assert.True(hallazgos.Count == 0,
            "SQL armado concatenando variables. Usa un parámetro (@nombre + AddWithValue):\n  " +
            string.Join("\n  ", hallazgos));
    }
}
