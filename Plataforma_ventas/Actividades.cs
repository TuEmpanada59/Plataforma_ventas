namespace Plataforma_ventas;

/// <summary>
/// La actividad comercial con la que sale un proyecto: no es lo mismo estrenar un
/// proyecto que volver a activar uno que ya tiene mitad del inventario vendido.
/// De esto depende qué cuenta como resultado del evento y qué es historia previa.
/// </summary>
public static class Actividades
{
    /// <summary>Estreno: todo el inventario sale por primera vez.</summary>
    public const string ProyectoNuevo = "PROYECTO_NUEVO";

    /// <summary>Se vuelve a salir a vender un inventario que ya existía.</summary>
    public const string Activacion = "ACTIVACION";

    /// <summary>Se lanza una etapa nueva de un proyecto que ya tiene historia.</summary>
    public const string NuevaEtapa = "NUEVA_ETAPA";

    /// <summary>Las tres actividades, en el orden en que se muestran en la carga.</summary>
    public static readonly string[] Permitidas = { ProyectoNuevo, Activacion, NuevaEtapa };

    /// <summary>
    /// Valida contra la lista blanca. Cualquier cosa que no reconozca cae en proyecto
    /// nuevo, que es el caso donde no hay historia y ninguna cifra queda mal contada.
    /// </summary>
    public static string Normalizar(string? actividad)
        => System.Array.IndexOf(Permitidas, (actividad ?? "").Trim().ToUpper()) >= 0
           ? (actividad ?? "").Trim().ToUpper()
           : ProyectoNuevo;

    /// <summary>Cómo se nombra la actividad en pantalla.</summary>
    public static string Titulo(string? actividad) => Normalizar(actividad) switch
    {
        Activacion => "Activación",
        NuevaEtapa => "Lanzamiento de nueva etapa",
        _          => "Lanzamiento de proyecto nuevo",
    };

    /// <summary>
    /// Si la fila del Excel pertenece a la etapa que se está lanzando. Sin etapa
    /// indicada entran todas: es el caso de un archivo de una sola hoja.
    /// </summary>
    public static bool EtapaEnLanzamiento(string? etapaFila, string? etapaLanzada)
    {
        var e = (etapaLanzada ?? "").Trim();
        if (e.Length == 0) return true;
        return string.Equals((etapaFila ?? "").Trim(), e, System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Decide si una unidad del archivo cuenta como parte del lanzamiento. La regla es
    /// una sola: sale al evento lo que llega disponible, porque lo que ya viene vendido
    /// o reservado estaba comprometido antes y no lo logró nadie esa noche. En un
    /// lanzamiento de etapa se exige además que sea la etapa que se está lanzando, para
    /// que lo que sobró de etapas anteriores no infle la meta del evento.
    /// </summary>
    /// <param name="actividad">Actividad ya normalizada.</param>
    /// <param name="estadoFila">Estado traducido del Excel.</param>
    /// <param name="etapaFila">Etapa de la fila (el nombre de la hoja).</param>
    /// <param name="etapaLanzada">Etapa que se está lanzando; vacía significa todas.</param>
    public static bool EntraAlLanzamiento(string? actividad, string? estadoFila,
                                          string? etapaFila, string? etapaLanzada)
    {
        if ((estadoFila ?? "") != "DISPONIBLE") return false;
        return Normalizar(actividad) != NuevaEtapa || EtapaEnLanzamiento(etapaFila, etapaLanzada);
    }
}
