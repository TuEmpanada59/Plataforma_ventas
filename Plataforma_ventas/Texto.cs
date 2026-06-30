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
}
