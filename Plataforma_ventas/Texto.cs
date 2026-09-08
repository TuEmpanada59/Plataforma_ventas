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
}
