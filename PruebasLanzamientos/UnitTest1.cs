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
    [InlineData("Uso propio", "Uso propio")]
    [InlineData("Inversión para reventa", "Inversión para reventa")]
    [InlineData("Inversión para arriendo", "Inversión para arriendo")]
    [InlineData("Cesión de derechos", "Cesión de derechos")]
    public void DestinoVenta_AceptaLosPermitidos(string entrada, string esperado)
        => Assert.Equal(esperado, Texto.DestinoVenta(entrada));

    [Theory]
    [InlineData("Lavado de activos")]   // valor arbitrario / malicioso
    [InlineData("uso propio")]          // no coincide (es sensible a mayúsculas)
    [InlineData("")]
    [InlineData(null)]
    public void DestinoVenta_RechazaLoNoPermitido_YUsaUsoPropio(string? entrada)
        => Assert.Equal("Uso propio", Texto.DestinoVenta(entrada));

    //TorreNormalizada: la torre sale de la columna TORRE o del nombre de la unidad
    [Theory]
    [InlineData("", "1204 T3", "T3")]      // la torre viene dentro del nombre comercial
    [InlineData("", "801T1", "T1")]        // sin espacio
    [InlineData("", "1502 t5", "T5")]      // minúscula
    [InlineData("T2", "1204 T3", "T2")]    // la columna TORRE manda sobre el nombre
    [InlineData("3", "1204", "T3")]        // columna TORRE con solo el número
    [InlineData("Torre 4", "1204", "T4")]
    [InlineData("", "1204", "")]           // proyecto de una sola torre: sin torre
    [InlineData("", "", "")]
    [InlineData(null, null, "")]
    public void TorreNormalizada_ResuelveLaTorre(string? torreExcel, string? unidad, string esperado)
        => Assert.Equal(esperado, Texto.TorreNormalizada(torreExcel, unidad));

    //AjustarPrecio: subir/bajar precios en bloque, redondeando al millar
    [Theory]
    [InlineData(400000000L, 3, true, 412000000L)]      // +3 %
    [InlineData(400000000L, -5, true, 380000000L)]     // -5 %
    [InlineData(412837451L, 0.5, true, 414902000L)]    // el resultado queda redondeado al millar
    [InlineData(400000000L, 5000000, false, 405000000L)]  // suma en pesos
    [InlineData(400000000L, -2000000, false, 398000000L)] // resta en pesos
    [InlineData(0L, 10, true, 0L)]                     // lista sin usar: no se inventa precio
    [InlineData(1000000L, -200, true, 0L)]             // nunca queda negativo
    // El valor entra como double porque un atributo no admite constantes decimal.
    public void AjustarPrecio_AplicaYRedondea(long precio, double valor, bool porcentaje, long esperado)
        => Assert.Equal(esperado, Listas.AjustarPrecio(precio, (decimal)valor, porcentaje));

    //ColumnaLista: lista blanca fija, nunca sale texto del usuario
    [Theory]
    [InlineData(1, "Lista1")]
    [InlineData(3, "Lista3")]
    [InlineData(5, "Lista5")]
    [InlineData(9, "Lista5")]
    [InlineData(0, "Lista5")]
    public void ColumnaLista_DevuelveLaColumnaEsperada(int n, string esperado)
        => Assert.Equal(esperado, Listas.ColumnaLista(n));

    //EstadoInmueble: el ESTADO del Excel se traduce a los cuatro estados de la plataforma
    [Theory]
    [InlineData("VENDIDO", "VENDIDO")]
    [InlineData("vendida", "VENDIDO")]
    [InlineData(" Vendido ", "VENDIDO")]
    [InlineData("RESERVADO", "RESERVADO")]
    [InlineData("reservada", "RESERVADO")]
    [InlineData("EN PROCESO", "EN PROCESO")]
    [InlineData("DISPONIBLE", "DISPONIBLE")]
    [InlineData("cualquier cosa", "DISPONIBLE")]   // un error de digitación no deja el inmueble bloqueado
    [InlineData("", "DISPONIBLE")]
    [InlineData(null, "DISPONIBLE")]
    public void EstadoInmueble_TraduceElEstadoDelExcel(string? entrada, string esperado)
        => Assert.Equal(esperado, Texto.EstadoInmueble(entrada));
}
