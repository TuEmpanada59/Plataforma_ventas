using Xunit;
using Plataforma_ventas;

namespace PruebasLanzamientos;

public class UnitTest1
{
    //SoloDigitos: el documento (CC/NIT) solo debe quedar en números
    [Theory]
    [InlineData("12.345.678", "12345678")]
    [InlineData("CC 1007243645", "1007243645")]
    [InlineData("900.123.456-7", "9001234567")]
    [InlineData("  52 340 111 ", "52340111")]
    [InlineData("abc", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SoloDigitos_DejaSoloNumeros(string? entrada, string esperado)
        => Assert.Equal(esperado, Texto.SoloDigitos(entrada));

    //ParsearPrecio: convierte el texto del precio a entero (pesos)
    [Theory]
    [InlineData("$564.400.000", 564400000L)]
    [InlineData("1.682.600.000", 1682600000L)]
    [InlineData("350000000", 350000000L)]
    [InlineData("$ 1.000.000 ", 1000000L)]
    [InlineData("—", 0L)]       
    [InlineData("abc", 0L)]
    [InlineData("", 0L)]
    [InlineData(null, 0L)]
    public void ParsearPrecio_ConvierteCorrectamente(string? entrada, long esperado)
        => Assert.Equal(esperado, Texto.ParsearPrecio(entrada));

    //DestinoVenta: solo se aceptan destinos de la lista blanca
    [Theory]
    [InlineData("Vivienda", "Vivienda")]
    [InlineData("Inversión para reventa", "Inversión para reventa")]
    [InlineData("Inversión para arriendo", "Inversión para arriendo")]
    [InlineData("Cesión de derechos", "Cesión de derechos")]
    public void DestinoVenta_AceptaLosPermitidos(string entrada, string esperado)
        => Assert.Equal(esperado, Texto.DestinoVenta(entrada));

    [Theory]
    [InlineData("Lavado de activos")]   // valor arbitrario / malicioso
    [InlineData("vivienda")]            // no coincide (es sensible a mayúsculas)
    [InlineData("")]
    [InlineData(null)]
    public void DestinoVenta_RechazaLoNoPermitido_YUsaVivienda(string? entrada)
        => Assert.Equal("Vivienda", Texto.DestinoVenta(entrada));
}