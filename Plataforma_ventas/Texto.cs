namespace Plataforma_ventas;

/// <summary>
/// Utilidades de saneamiento de texto para datos de entrada.
/// </summary>
public static class Texto
{
    /// <summary>
    /// Devuelve únicamente los dígitos de la cadena (para CC / NIT).
    /// Null o vacío devuelven cadena vacía.
    /// </summary>
    public static string SoloDigitos(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return "";
        var sb = new System.Text.StringBuilder(valor.Length);
        foreach (var c in valor)
            if (c >= '0' && c <= '9') sb.Append(c);
        return sb.ToString();
    }

    /// <summary>
    /// Convierte un precio almacenado como texto (con $, puntos, comas o espacios)
    /// en un entero largo. Devuelve 0 si no es parseable. Fuente única de verdad
    /// para el precio, evitando confiar en valores enviados por el cliente.
    /// </summary>
    public static long ParsearPrecio(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return 0;
        var limpio = raw.Replace("$", "").Replace(".", "").Replace(",", "").Replace(" ", "").Trim();
        return long.TryParse(limpio, out long v) ? v : 0;
    }

    private static readonly string[] DestinosPermitidos =
        { "Uso propio", "Inversión para reventa", "Inversión para arriendo", "Cesión de derechos" };

    /// <summary>
    /// Valida el destino de una venta contra la lista blanca. Si no coincide,
    /// devuelve "Uso propio" (valor por defecto), evitando datos arbitrarios.
    /// </summary>
    public static string DestinoVenta(string? destino)
        => System.Array.IndexOf(DestinosPermitidos, destino) >= 0 ? destino! : "Uso propio";

    /// <summary>
    /// Medios por los que un cliente se entera del proyecto. Es una lista cerrada
    /// a propósito: si fuera texto libre no se podrían agrupar los resultados para
    /// saber qué canal trae compradores. Editar aquí para cambiar las opciones.
    /// </summary>
    public static readonly string[] MediosPermitidos =
    {
        "Redes sociales", "Valla publicitaria", "Referido", "Feria o evento",
        "Página web", "Portales inmobiliarios", "Sala de ventas", "Otro"
    };

    /// <summary>
    /// Valida el medio publicitario contra la lista blanca. Cadena vacía si no se
    /// informó: es un dato opcional y no debe inventarse un valor por defecto que
    /// después distorsione las estadísticas del canal.
    /// </summary>
    public static string MedioPublicitario(string? medio)
        => System.Array.IndexOf(MediosPermitidos, medio) >= 0 ? medio! : "";

    /// <summary>
    /// Resuelve la torre de un inmueble: la columna TORRE cuando el archivo la trae,
    /// y si no, la marca "T1".."T5" que viene dentro del nombre comercial de la unidad
    /// (por ejemplo "1204 T3"). Siempre devuelve "T&lt;n&gt;" para que agrupar y filtrar
    /// por torre no dependa de cómo se haya escrito en el Excel. Cadena vacía si el
    /// proyecto es de una sola torre y no informa ninguna.
    /// </summary>
    /// <param name="torreExcel">Valor crudo de la columna TORRE; puede venir vacío.</param>
    /// <param name="nombreUnidad">Nombre de la unidad, que puede llevar la torre dentro.</param>
    public static string TorreNormalizada(string? torreExcel, string? nombreUnidad)
    {
        var fuente = !string.IsNullOrWhiteSpace(torreExcel) ? torreExcel : nombreUnidad ?? "";

        // Primero la palabra completa ("Torre 4", "TORRE-4").
        var m = System.Text.RegularExpressions.Regex.Match(
            fuente, @"TORRE\s*-?\s*(\d{1,2})(?!\d)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Y si no, la marca corta pegada al número de la unidad ("1204 T3", "801T1").
        // La T no puede venir precedida de otra letra: así "SUITE", "APTO" o "PENT"
        // no se confunden con una torre.
        if (!m.Success)
            m = System.Text.RegularExpressions.Regex.Match(
                fuente, @"(?<![A-Za-z])T\s*-?\s*(\d{1,2})(?!\d)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (m.Success) return "T" + m.Groups[1].Value;

        // Una columna TORRE que solo trae el número ("3") también es una torre válida.
        var soloNumero = (torreExcel ?? "").Trim();
        if (soloNumero.Length > 0 && soloNumero.Length <= 2 && int.TryParse(soloNumero, out _))
            return "T" + soloNumero;

        return soloNumero;
    }
}
