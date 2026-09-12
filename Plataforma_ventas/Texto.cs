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

    /// <summary>
    /// Destinos válidos de una venta. Es pública porque los formularios que editan una
    /// venta ofrecen exactamente estas opciones: si la vista tuviera su propia lista,
    /// una opción nueva aquí no llegaría a la pantalla.
    /// </summary>
    public static readonly string[] DestinosPermitidos =
        { "Uso propio", "Inversión para reventa", "Inversión para arriendo", "Cesión de derechos" };

    /// <summary>
    /// Valida el destino de una venta contra la lista blanca. Si no coincide,
    /// devuelve "Uso propio" (valor por defecto), evitando datos arbitrarios.
    /// </summary>
    public static string DestinoVenta(string? destino)
        => System.Array.IndexOf(DestinosPermitidos, destino) >= 0 ? destino! : "Uso propio";

    /// <summary>
    /// Cómo se llama una unidad del proyecto en los mensajes. Lleva el género porque en
    /// español no basta con cambiar el sustantivo: "la suite liberada" contra "el
    /// apartamento liberado". Sin esto los avisos quedan diciendo "apartamento" en un
    /// proyecto de suites, que es justo lo que el asesor le repite al cliente.
    /// </summary>
    /// <param name="Singular">"suite", "apartamento"…</param>
    /// <param name="Plural">"suites", "apartamentos"…</param>
    /// <param name="Femenina">true en suite y oficina.</param>
    public readonly record struct Unidad(string Singular, string Plural, bool Femenina)
    {
        /// <summary>"la" / "el".</summary>
        public string Articulo => Femenina ? "la" : "el";

        /// <summary>"Esta" / "Este".</summary>
        public string Demostrativo => Femenina ? "Esta" : "Este";

        /// <summary>Terminación de los participios: reservad**a** / reservad**o**.</summary>
        public string Fin => Femenina ? "a" : "o";

        /// <summary>El singular con la primera letra en mayúscula, para empezar frase.</summary>
        public string Titulo => char.ToUpper(Singular[0]) + Singular.Substring(1);
    }

    /// <summary>
    /// Devuelve cómo se nombra la unidad según el tipo de proyecto. Un tipo desconocido
    /// cae en apartamento, que es el caso más común.
    /// </summary>
    public static Unidad UnidadDe(string? tipoProyecto) => (tipoProyecto ?? "").ToUpper() switch
    {
        "SUITES"   => new Unidad("suite", "suites", true),
        "LOTES"    => new Unidad("lote", "lotes", false),
        "SALUD"    => new Unidad("consultorio", "consultorios", false),
        "OFICINAS" => new Unidad("oficina", "oficinas", true),
        _          => new Unidad("apartamento", "apartamentos", false),
    };

    /// <summary>
    /// Traduce el ESTADO que viene en el Excel a uno de los cuatro estados que maneja la
    /// plataforma. Lo que no se reconozca queda DISPONIBLE: un error de digitación no debe
    /// dejar un inmueble en un estado sobre el que nadie puede actuar.
    /// </summary>
    public static string EstadoInmueble(string? estadoExcel)
    {
        var e = (estadoExcel ?? "").Trim().ToUpper();
        if (e.StartsWith("VEND")) return "VENDIDO";
        if (e.StartsWith("RESER")) return "RESERVADO";
        if (e.StartsWith("EN PROCESO") || e.StartsWith("PROCESO")) return "EN PROCESO";
        return "DISPONIBLE";
    }

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
