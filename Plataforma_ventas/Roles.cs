namespace Plataforma_ventas;

/// <summary>
/// Los roles de la plataforma, en un solo sitio. Antes vivían como cadenas sueltas
/// repartidas por los controladores y las vistas, y ya vimos lo que cuesta: una cuenta
/// creada a mano con el rol escrito "superadmin" no podía entrar a ninguna pantalla.
/// </summary>
public static class Roles
{
    public const string SuperAdministrador = "SuperAdministrador";
    public const string Administrador      = "Administrador";
    /// <summary>Dirección comercial: consulta el proyecto que se le asignó y nada más.</summary>
    public const string Direccion          = "Direccion";
    public const string Vendedor           = "Vendedor";

    /// <summary>
    /// Roles que solo pueden consultar. Se define aquí, y no repartido por cada
    /// pantalla, porque de esta lista depende que una función nueva nazca bloqueada
    /// para ellos en vez de quedar abierta hasta que alguien se acuerde.
    /// </summary>
    public static bool EsSoloLectura(string? rol) => rol == Direccion;

    /// <summary>Cómo se nombra el rol en pantalla.</summary>
    public static string Titulo(string? rol) => rol switch
    {
        SuperAdministrador => "Super Admin",
        Administrador      => "Administrador",
        Direccion          => "Dirección",
        Vendedor           => "Vendedor",
        _                  => rol ?? "",
    };

    /// <summary>Roles que un usuario con este rol puede crear o asignar a otros.</summary>
    /// <remarks>
    /// La regla es que nadie reparte más de lo que tiene. Un administrador puede crear
    /// cuentas de dirección porque son estrictamente menos capaces que la suya: solo
    /// leen, y solo un proyecto. Lo que no puede es crear otro administrador.
    /// </remarks>
    public static string[] PuedeCrear(string? rol) => rol switch
    {
        SuperAdministrador => new[] { Administrador, Direccion, Vendedor },
        Administrador      => new[] { Direccion, Vendedor },
        _                  => System.Array.Empty<string>(),
    };
}
