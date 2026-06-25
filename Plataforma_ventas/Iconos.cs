using Microsoft.AspNetCore.Html;

namespace Plataforma_ventas;

/// <summary>
/// Íconos SVG "duotono" (relleno tenue del color + trazo) en el mismo estilo que
/// los íconos del sidebar (wwwroot/Images/icons/*.svg). Permiten reponer íconos
/// en KPIs, cabeceras y estados vacíos sin recurrir a emojis.
/// Uso en vistas: @Iconos.Edificio("#0076E3")  ·  @Iconos.Llave("#E08600", 22)
/// </summary>
public static class Iconos
{
    private static HtmlString Svg(string fill, string stroke, string color, int size, double fillOpacity)
        => new HtmlString(
            $"<svg width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" style=\"color:{color};display:block\" aria-hidden=\"true\">" +
            $"<g fill=\"currentColor\" fill-opacity=\"{fillOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture)}\" stroke=\"none\">{fill}</g>" +
            $"<g fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{stroke}</g></svg>");

    // Edificio / proyecto (barras) — total de inmuebles
    public static HtmlString Edificio(string color = "#0076E3", int size = 19, double fo = 0.22) => Svg(
        "<rect x='4' y='4' width='4.6' height='16' rx='1.4'/><rect x='15.4' y='4' width='4.6' height='13' rx='1.4'/>",
        "<rect x='4' y='4' width='4.6' height='16' rx='1.4'/><rect x='9.7' y='4' width='4.6' height='10' rx='1.4'/><rect x='15.4' y='4' width='4.6' height='13' rx='1.4'/>",
        color, size, fo);

    // Casa — inicio / vivienda
    public static HtmlString Casa(string color = "#0076E3", int size = 19, double fo = 0.22) => Svg(
        "<path d='M4 11 12 4l8 7v8a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1Z'/>",
        "<path d='M3 11.5 12 4.5l9 7'/><path d='M5 10.5V19a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-8.5'/><path d='M10 20v-5h4v5'/>",
        color, size, fo);

    // Check en círculo — disponible / vendido / activo
    public static HtmlString Check(string color = "#1EA851", int size = 19, double fo = 0.22) => Svg(
        "<circle cx='12' cy='12' r='9'/>",
        "<circle cx='12' cy='12' r='9'/><path d='m8 12 2.5 2.5L16 9'/>",
        color, size, fo);

    // Dólar — ventas / dinero
    public static HtmlString Dolar(string color = "#0076E3", int size = 19, double fo = 0.22) => Svg(
        "<circle cx='12' cy='12' r='9'/>",
        "<circle cx='12' cy='12' r='9'/><path d='M14.7 9.2c-.5-1-1.6-1.4-2.7-1.4-1.4 0-2.5.7-2.5 1.9 0 2.8 5.4 1.4 5.4 4.4 0 1.3-1.3 2.1-2.9 2.1-1.3 0-2.5-.5-3-1.6'/><path d='M12 6.2v11.6'/>",
        color, size, fo);

    // Persona — cliente / vendedor
    public static HtmlString Persona(string color = "#5A5AC8", int size = 19, double fo = 0.22) => Svg(
        "<circle cx='12' cy='8' r='3.6'/>",
        "<circle cx='12' cy='8' r='3.6'/><path d='M5.5 20c0-3.7 2.9-6.1 6.5-6.1s6.5 2.4 6.5 6.1'/>",
        color, size, fo);

    // Candado — reservado / seguro
    public static HtmlString Candado(string color = "#E63946", int size = 19, double fo = 0.22) => Svg(
        "<rect x='5' y='10.5' width='14' height='10' rx='2'/>",
        "<rect x='5' y='10.5' width='14' height='10' rx='2'/><path d='M8 10.5V7.5a4 4 0 0 1 8 0v3'/>",
        color, size, fo);

    // Llave — reserva / entrega
    public static HtmlString Llave(string color = "#E08600", int size = 19, double fo = 0.22) => Svg(
        "<circle cx='8' cy='9' r='4.5'/>",
        "<circle cx='8' cy='9' r='4.5'/><path d='m11 12 8 8M16 18l1.8-1.8M18 20l1.8-1.8'/>",
        color, size, fo);

    // Reloj — en proceso / pendiente
    public static HtmlString Reloj(string color = "#E08600", int size = 19, double fo = 0.22) => Svg(
        "<circle cx='12' cy='12' r='9'/>",
        "<circle cx='12' cy='12' r='9'/><path d='M12 7.5v5l3.5 2'/>",
        color, size, fo);

    // Gráfico — reportes / progreso
    public static HtmlString Grafico(string color = "#0076E3", int size = 19, double fo = 0.22) => Svg(
        "<path d='M4 20h16V4H4Z'/>",
        "<path d='M4 4v16h16'/><path d='m7 14 3-4 3 2 5-7'/>",
        color, size, fo);

    // Calendario — informe del día / fecha
    public static HtmlString Calendario(string color = "#0076E3", int size = 19, double fo = 0.22) => Svg(
        "<rect x='4' y='5.5' width='16' height='15' rx='2'/>",
        "<rect x='4' y='5.5' width='16' height='15' rx='2'/><path d='M4 9.5h16M8 3.5v4M16 3.5v4'/>",
        color, size, fo);

    // Etiqueta — lista de precios
    public static HtmlString Etiqueta(string color = "#0076E3", int size = 19, double fo = 0.22) => Svg(
        "<path d='M4 4h7l9 9-7 7-9-9Z'/>",
        "<path d='M4 4v7l9 9 7-7-9-9Z'/><circle cx='8.5' cy='8.5' r='1.4'/>",
        color, size, fo);

    // Metros / plano — área
    public static HtmlString Plano(string color = "#5A5AC8", int size = 19, double fo = 0.22) => Svg(
        "<rect x='4' y='4' width='16' height='16' rx='2'/>",
        "<rect x='4' y='4' width='16' height='16' rx='2'/><path d='M4 10h6M10 4v6M14 20v-6h6'/>",
        color, size, fo);

    // Mapa / ubicación — destino / zona
    public static HtmlString Mapa(string color = "#1EA851", int size = 19, double fo = 0.22) => Svg(
        "<path d='M12 21s7-6.5 7-11a7 7 0 1 0-14 0c0 4.5 7 11 7 11Z'/>",
        "<path d='M12 21s7-6.5 7-11a7 7 0 1 0-14 0c0 4.5 7 11 7 11Z'/><circle cx='12' cy='10' r='2.5'/>",
        color, size, fo);

    // Lista / portapapeles — listado / detalle
    public static HtmlString Lista(string color = "#0076E3", int size = 19, double fo = 0.22) => Svg(
        "<rect x='5' y='4' width='14' height='17' rx='2'/>",
        "<rect x='5' y='4' width='14' height='17' rx='2'/><path d='M9 4V3h6v1M9 10h6M9 14h6M9 18h4'/>",
        color, size, fo);

    // Documento — informe técnico / archivo
    public static HtmlString Documento(string color = "#0076E3", int size = 19, double fo = 0.22) => Svg(
        "<path d='M6 3h8l4 4v14H6Z'/>",
        "<path d='M6 3h8l4 4v14H6Z'/><path d='M14 3v4h4M9 13h6M9 17h6'/>",
        color, size, fo);
}
