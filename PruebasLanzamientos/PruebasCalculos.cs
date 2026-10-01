using Plataforma_ventas;
using Xunit;

namespace PruebasLanzamientos;

/// <summary>
/// Lógica que se agregó después de las primeras pruebas y quedó sin cubrir. El orden
/// no es casual: primero lo que cuesta plata o datos si falla.
/// </summary>
public class PruebasCalculos
{
    // ── Enlaces del lanzamiento ──────────────────────────────────────────────
    //
    // El administrador publica enlaces que todos los asesores abren. Que solo se
    // acepte http y https no es una formalidad: un "javascript:" guardado ahí se
    // ejecuta en la sesión de quien le dé clic.

    [Theory]
    [InlineData("https://londonogomez.com/brochure.pdf")]
    [InlineData("http://londonogomez.com")]
    [InlineData("www.londonogomez.com")]            // sin esquema: se asume https
    public void NormalizarUrl_AceptaLoNavegable(string entrada)
    {
        var r = Enlaces.NormalizarUrl(entrada);
        Assert.NotNull(r);
        Assert.True(r!.StartsWith("http://") || r.StartsWith("https://"));
    }

    [Theory]
    [InlineData("javascript:alert(document.cookie)")]  // se ejecutaría en la sesión del asesor
    [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
    [InlineData("file:///C:/Windows/System32")]
    [InlineData("ftp://servidor/archivo.pdf")]
    [InlineData("https://ejemplo.com/a\nSet-Cookie: x=1")]  // salto de línea: cabecera colada
    [InlineData("")]
    [InlineData(null)]
    public void NormalizarUrl_RechazaLoDemas(string? entrada)
        => Assert.Null(Enlaces.NormalizarUrl(entrada));

    // ── Área de la unidad ────────────────────────────────────────────────────
    //
    // El área es la llave con la que se agrupan las unidades y con la que vive la
    // lista de precios. Dos formas de escribirla parten el área en dos y cada mitad
    // sube de lista por su cuenta.

    [Theory]
    [InlineData("70,40", "70,4")]
    [InlineData("70.4", "70,4")]     // el Excel pudo guardarse con punto decimal
    [InlineData("70,4", "70,4")]
    [InlineData("70,00", "70")]      // sin decimales que aporten, y sin coma suelta
    [InlineData("70", "70")]
    [InlineData(" 55,58 ", "55,58")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void AreaNormalizada_UnificaLasFormasDeEscribirla(string? entrada, string esperado)
        => Assert.Equal(esperado, Texto.AreaNormalizada(entrada));

    [Fact]
    public void AreaNormalizada_NoPierdeLoQueNoEsNumero()
    {
        // Antes que inventar un valor o devolver vacío, se conserva: un área rara en
        // el Excel es un dato a corregir, no uno a borrar sin que nadie se entere.
        Assert.Equal("Local esquinero", Texto.AreaNormalizada("  Local esquinero  "));
    }

    [Fact]
    public void AreaNumerica_OrdenaPorValorYNoAlfabeticamente()
    {
        var areas = new[] { "140,96", "98,17", "55,58" }
            .OrderBy(Inventario.AreaNumerica)
            .ToArray();

        // Alfabéticamente "140,96" iría primero, que no es como nadie lee un cuadro.
        Assert.Equal(new[] { "55,58", "98,17", "140,96" }, areas);
    }

    // ── Con qué lista se bloqueó una reserva ─────────────────────────────────
    //
    // No hay columna que lo guarde: se deduce comparando el precio bloqueado contra
    // las listas de la unidad.

    [Theory]
    [InlineData(400_000_000L, 1)]
    [InlineData(430_000_000L, 3)]
    public void ListaDePrecio_EncuentraLaListaDelPrecioBloqueado(long precio, int esperada)
        => Assert.Equal(esperada, Listas.ListaDePrecio(
            precio, "400000000", "415000000", "430000000", "445000000", "460000000"));

    [Theory]
    [InlineData(427_500_000L)]   // se ajustaron los precios después de reservar
    [InlineData(0L)]             // sin precio bloqueado no hay nada que deducir
    public void ListaDePrecio_PrefiereNoDecirNadaAntesQueEquivocarse(long precio)
        => Assert.Equal(0, Listas.ListaDePrecio(
            precio, "400000000", "415000000", "430000000", "445000000", "460000000"));

    // ── Avance del lanzamiento ───────────────────────────────────────────────
    //
    // La cifra con la que se mide al equipo. Mezclarla con lo que ya venía
    // comprometido en el archivo infla el resultado del evento.

    [Fact]
    public void Progreso_SeparaElEventoDeLoQueYaVenia()
    {
        //              Total  TotalLanz  VendLanz VendPrev  ResLanz ResPrev  Disp
        var p = new ProgresoProyecto(100, 70, 10, 20, 5, 5, 60);

        Assert.Equal(30, p.Vendidas);              // 10 del evento + 20 del archivo
        Assert.Equal(10, p.Reservadas);
        Assert.Equal(25, p.Previas);               // lo que no cuenta como resultado
        Assert.Equal(15, p.ColocadoLanzamiento);   // lo que sí logró el equipo
        Assert.True(p.HaySeparacion);

        // 15 de 70 que salieron a vender esa noche, no 15 de 100.
        Assert.Equal(21.43, Math.Round(p.PctAvanceLanzamiento, 2));
    }

    [Fact]
    public void Progreso_UnProyectoQueEstrenaNoMencionaLaSeparacion()
    {
        var p = new ProgresoProyecto(100, 100, 12, 0, 3, 0, 85);

        Assert.False(p.HaySeparacion);   // no hay dos cosas que distinguir
        Assert.Equal(0, p.Previas);
        Assert.Equal(15, p.ColocadoLanzamiento);
    }

    [Fact]
    public void Progreso_ProyectoVacioNoDivideEntreCero()
    {
        var p = new ProgresoProyecto(0, 0, 0, 0, 0, 0, 0);

        Assert.Equal(0, p.PctAvanceLanzamiento);
        Assert.Equal(0, p.PctDeTotal(0));
    }

    [Fact]
    public void Ancho_UsaPuntoDecimalParaElAtributoStyle()
    {
        // Con coma, el navegador descarta la regla y la barra se ve vacía. Esto tiene
        // que seguir siendo independiente de la configuración regional del servidor.
        Assert.Equal("21.43", ProgresoProyecto.Ancho(21.4285));
    }
}
