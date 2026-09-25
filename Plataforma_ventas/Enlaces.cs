namespace Plataforma_ventas;

/// <summary>
/// Enlaces del material de un lanzamiento: la presentación, el brochure, los renders,
/// el formulario de separación. Hoy eso se reparte por WhatsApp y cada asesor termina
/// con una versión distinta; aquí el administrador lo publica una vez.
/// </summary>
public static class Enlaces
{
    /// <summary>
    /// Valida y normaliza una dirección antes de guardarla. Devuelve null si no sirve.
    ///
    /// Solo se aceptan http y https. Es la parte importante: un enlace pegado por un
    /// administrador termina renderizado en la pantalla de todos los asesores, y una
    /// dirección con esquema "javascript:" convierte esa pantalla en una ejecución de
    /// código ajeno. Lo mismo con "data:", que permite servir una página completa
    /// desde el propio enlace.
    ///
    /// Una dirección sin esquema ("www.ejemplo.com") se asume https, que es lo que la
    /// gente escribe y espera que funcione.
    /// </summary>
    public static string? NormalizarUrl(string? url)
    {
        var u = (url ?? "").Replace(' ', ' ').Trim();
        if (u.Length == 0) return null;
        // Un salto de línea o un retorno dentro de la dirección no es un descuido
        // inocente: es la forma clásica de colar una cabecera o partir un atributo.
        if (u.Any(c => c == '\n' || c == '\r' || c == '\t')) return null;
        if (u.Length > 1000) return null;

        if (!u.Contains("://", StringComparison.Ordinal))
            u = "https://" + u;

        if (!Uri.TryCreate(u, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
        if (string.IsNullOrEmpty(uri.Host)) return null;

        return uri.ToString();
    }

    /// <summary>
    /// El dominio, para mostrarlo bajo el título. El asesor ve a dónde lo lleva el
    /// enlace antes de tocarlo, sin tener que leer una dirección de doscientos
    /// caracteres con parámetros de seguimiento.
    /// </summary>
    public static string Dominio(string? url)
    {
        if (!Uri.TryCreate(url ?? "", UriKind.Absolute, out var uri)) return "";
        var h = uri.Host;
        return h.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? h.Substring(4) : h;
    }
}
