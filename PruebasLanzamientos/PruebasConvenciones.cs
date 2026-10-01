using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Plataforma_ventas;
using Plataforma_ventas.Filters;
using Xunit;

namespace PruebasLanzamientos;

/// <summary>
/// Pruebas de convención: no comprueban qué hace el código sino cómo está escrito.
///
/// Las reglas que verifican están en CLAUDE.md y hoy se cumplen todas. El valor no
/// está en descubrir un error —no hay ninguno— sino en que sigan cumpliéndose cuando
/// alguien agregue una pantalla a las once de la noche antes de un lanzamiento. Un
/// POST sin antiforgery o una consulta armada con un "+" no se notan al mirar la
/// pantalla: se notan cuando ya pasó algo.
///
/// Cada vez que una de estas pruebas falle, lo que hay que arreglar es el código
/// nuevo, no la prueba. Si de verdad hay una excepción legítima, se agrega a su lista
/// blanca escribiéndola a propósito, que es justamente lo que se quiere forzar.
/// </summary>
public class PruebasConvenciones
{
    // ── El material sobre el que se trabaja ──────────────────────────────────

    private static readonly Assembly Aplicacion = typeof(Roles).Assembly;

    private static IEnumerable<Type> Controladores =>
        Aplicacion.GetTypes().Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract);

    /// <summary>
    /// Las acciones de un controlador: métodos públicos propios que devuelven una
    /// respuesta. Se excluyen los heredados de Controller y los generados.
    /// </summary>
    private static IEnumerable<MethodInfo> AccionesDe(Type controlador) =>
        controlador.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                   .Where(m => !m.IsSpecialName)
                   .Where(m => DevuelveRespuesta(m.ReturnType));

    /// <summary>
    /// IActionResult, cualquiera de sus derivados (ViewResult, JsonResult…) y las
    /// versiones envueltas en Task. Se mira el tipo de verdad y no solo
    /// Task&lt;IActionResult&gt;: una acción que devuelva Task&lt;ViewResult&gt; es una acción
    /// igual, y dejarla fuera sería un agujero silencioso en estas pruebas.
    /// </summary>
    private static bool DevuelveRespuesta(Type tipo)
    {
        if (typeof(IActionResult).IsAssignableFrom(tipo)) return true;
        if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(Task<>))
            return typeof(IActionResult).IsAssignableFrom(tipo.GetGenericArguments()[0]);
        return false;
    }

    private static bool Tiene<T>(MethodInfo m) where T : Attribute =>
        m.GetCustomAttribute<T>() != null || m.DeclaringType!.GetCustomAttribute<T>() != null;

    private static string Nombre(MethodInfo m) => $"{m.DeclaringType!.Name}.{m.Name}";

    // ── 1) Antiforgery en todo POST ──────────────────────────────────────────

    /// <summary>
    /// Un POST sin token es un formulario que otro sitio puede enviar por el usuario
    /// mientras tiene la sesión abierta: reservar, vender o borrar en su nombre.
    /// </summary>
    [Fact]
    public void TodoPostLlevaAntiforgery()
    {
        var sinToken = Controladores
            .SelectMany(AccionesDe)
            .Where(m => Tiene<HttpPostAttribute>(m) && !Tiene<ValidateAntiForgeryTokenAttribute>(m))
            .Select(Nombre)
            .OrderBy(x => x)
            .ToList();

        Assert.True(sinToken.Count == 0,
            "Estas acciones POST no validan el token antiforgery:\n  " +
            string.Join("\n  ", sinToken));
    }

    // ── 2) Toda acción protegida, salvo las que son públicas a propósito ─────

    /// <summary>
    /// Las únicas acciones que pueden quedar sin rol son las que se usan ANTES de
    /// tener sesión, más la página de error. La lista es el punto de la prueba: una
    /// acción pública nueva obliga a escribirla aquí, es decir, a decidirlo.
    /// </summary>
    private static readonly HashSet<string> PublicasAProposito = new()
    {
        // Sin sesión todavía: son la puerta de entrada.
        "AccountController.Login",
        "AccountController.RecuperarPassword",
        "AccountController.RestablecerPassword",
        // Cerrar sesión no necesita rol: cualquiera que la tenga puede salir.
        "AccountController.Logout",
        // Páginas públicas y el manejador global de errores y 404.
        "HomeController.Index",
        "HomeController.Privacy",
        "HomeController.Error",
    };

    [Fact]
    public void TodaAccionExigeRol()
    {
        var desprotegidas = Controladores
            .SelectMany(AccionesDe)
            .Where(m => !Tiene<RolAutorizadoAttribute>(m))
            .Select(Nombre)
            .Where(n => !PublicasAProposito.Contains(n))
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        Assert.True(desprotegidas.Count == 0,
            "Estas acciones quedaron sin [RolAutorizado]. Si alguna debe ser pública,\n" +
            "agrégala a PublicasAProposito explicando por qué:\n  " +
            string.Join("\n  ", desprotegidas));
    }

    /// <summary>
    /// Que la lista blanca no se quede con nombres de acciones que ya no existen: una
    /// entrada muerta es permiso concedido a algo que nadie vuelve a revisar.
    /// </summary>
    [Fact]
    public void LaListaBlancaNoTieneSobrantes()
    {
        var reales = Controladores.SelectMany(AccionesDe).Select(Nombre).ToHashSet();
        var fantasmas = PublicasAProposito.Where(n => !reales.Contains(n)).OrderBy(x => x).ToList();

        Assert.True(fantasmas.Count == 0,
            "PublicasAProposito nombra acciones que ya no existen:\n  " +
            string.Join("\n  ", fantasmas));
    }

    // ── 3) Los roles escritos en los atributos existen ───────────────────────

    /// <summary>
    /// Hoy un error de digitación —"Direccion" sin tilde, "vendedor" en minúscula— no
    /// falla en ningún lado: simplemente nadie puede entrar, y se descubre en
    /// producción con la sala de ventas llena.
    /// </summary>
    [Fact]
    public void LosRolesDeLosAtributosExisten()
    {
        var validos = new[] { Roles.SuperAdministrador, Roles.Administrador, Roles.Direccion, Roles.Vendedor };

        var malos = new List<string>();
        foreach (var tipo in Controladores)
        {
            foreach (var attr in tipo.GetCustomAttributes<RolAutorizadoAttribute>())
                foreach (var rol in RolesDe(attr))
                    if (!validos.Contains(rol)) malos.Add($"{tipo.Name} (clase): \"{rol}\"");

            foreach (var m in AccionesDe(tipo))
                foreach (var attr in m.GetCustomAttributes<RolAutorizadoAttribute>())
                    foreach (var rol in RolesDe(attr))
                        if (!validos.Contains(rol)) malos.Add($"{Nombre(m)}: \"{rol}\"");
        }

        Assert.True(malos.Count == 0,
            "Roles que no existen en Roles.cs (nadie podrá entrar a estas pantallas):\n  " +
            string.Join("\n  ", malos.Distinct().OrderBy(x => x)));
    }

    /// <summary>
    /// El atributo guarda los roles en un campo privado; se leen por reflexión para no
    /// tener que abrirlos solo para esta prueba.
    /// </summary>
    private static string[] RolesDe(RolAutorizadoAttribute attr)
    {
        var campo = typeof(RolAutorizadoAttribute)
            .GetField("_roles", BindingFlags.NonPublic | BindingFlags.Instance);
        return campo?.GetValue(attr) as string[] ?? Array.Empty<string>();
    }
}
