using Plataforma_ventas;
using Xunit;

namespace PruebasLanzamientos;

/// <summary>
/// El área de la unidad.
///
/// Es lo único que quedó sin cubrir de lo que se agregó después de las primeras
/// pruebas: Enlaces, ProgresoProyecto y Listas.ListaDePrecio ya están en UnitTest1 y
/// no se repiten aquí. Una prueba duplicada no protege el doble, solo cuesta el doble
/// de mantener y hace creer que la cobertura es más ancha de lo que es.
///
/// El área merece las suyas porque es la llave con la que se agrupan las unidades y
/// con la que vive la lista de precios vigente. Cuando se parte en dos —la misma área
/// escrita "70,4" y "70,40"— cada mitad sube de lista por su cuenta y media torre se
/// sigue vendiendo al precio viejo.
/// </summary>
public class PruebasCalculos
{
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
}
