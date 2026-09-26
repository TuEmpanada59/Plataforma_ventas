namespace Plataforma_ventas;

/// <summary>
/// El avance de un proyecto separando lo que logró este lanzamiento de lo que ya venía
/// comprometido cuando el evento empezó.
///
/// Es la diferencia entre "el proyecto va en 40 vendidas" y "esta noche vendimos 12".
/// Mezclarlas le atribuye al equipo un resultado que no hizo, y lo contrario también:
/// esconde el mérito de un lanzamiento bueno sobre un proyecto ya avanzado.
///
/// Vive aparte de las vistas porque lo usan el informe del administrador y el perfil de
/// dirección: el mismo dato tiene que dar el mismo número en las dos pantallas.
/// </summary>
/// <param name="Total">Unidades del proyecto.</param>
/// <param name="TotalLanzamiento">Las que salieron a vender en este evento.</param>
public sealed record ProgresoProyecto(
    int Total,
    int TotalLanzamiento,
    int VendidasLanzamiento,
    int VendidasPrevias,
    int ReservadasLanzamiento,
    int ReservadasPrevias,
    int Disponibles)
{
    public int Vendidas   => VendidasLanzamiento + VendidasPrevias;
    public int Reservadas => ReservadasLanzamiento + ReservadasPrevias;

    /// <summary>Lo que ya estaba comprometido antes de que el evento empezara.</summary>
    public int Previas => VendidasPrevias + ReservadasPrevias;

    /// <summary>Lo que este lanzamiento colocó: vendido más reservado durante el evento.</summary>
    public int ColocadoLanzamiento => VendidasLanzamiento + ReservadasLanzamiento;

    /// <summary>
    /// Si tiene sentido distinguir. En un proyecto que estrena, todo es del lanzamiento
    /// y mostrar la separación solo agrega ruido.
    /// </summary>
    public bool HaySeparacion => Previas > 0;

    /// <summary>Porcentaje sobre el total del proyecto, para el ancho de las barras.</summary>
    public double PctDeTotal(int cantidad) => Total > 0 ? (double)cantidad * 100 / Total : 0;

    /// <summary>
    /// Avance del evento: lo colocado sobre lo que salió a vender. Es el número que
    /// mide al equipo, y el que se desvirtúa si se calcula sobre el proyecto entero.
    /// </summary>
    public double PctAvanceLanzamiento
        => TotalLanzamiento > 0 ? (double)ColocadoLanzamiento * 100 / TotalLanzamiento : 0;

    /// <summary>Ancho para el atributo style, con punto decimal.</summary>
    public static string Ancho(double porcentaje)
        => porcentaje.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
}
