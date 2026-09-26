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

    //UnidadDe: cómo se nombra la unidad en los mensajes, con su género
    [Theory]
    [InlineData("SUITES", "suite", true)]
    [InlineData("suites", "suite", true)]        // el tipo llega en minúscula desde la sesión
    [InlineData("OFICINAS", "oficina", true)]
    [InlineData("LOTES", "lote", false)]
    [InlineData("SALUD", "consultorio", false)]
    [InlineData("APARTAMENTOS", "apartamento", false)]
    [InlineData("", "apartamento", false)]
    [InlineData(null, "apartamento", false)]
    public void UnidadDe_NombraLaUnidadSegunElProyecto(string? tipo, string singular, bool femenina)
    {
        var u = Texto.UnidadDe(tipo);
        Assert.Equal(singular, u.Singular);
        Assert.Equal(femenina, u.Femenina);
    }

    //El género tiene que llegar a la frase: "suite liberada" y no "suite liberado"
    [Fact]
    public void Unidad_ConcuerdaEnGenero()
    {
        var suite = Texto.UnidadDe("SUITES");
        Assert.Equal("Suite liberada", $"{suite.Titulo} liberad{suite.Fin}");
        Assert.Equal("la suite", $"{suite.Articulo} {suite.Singular}");
        Assert.Equal("Esta", suite.Demostrativo);

        var apto = Texto.UnidadDe("APARTAMENTOS");
        Assert.Equal("Apartamento liberado", $"{apto.Titulo} liberad{apto.Fin}");
        Assert.Equal("el apartamento", $"{apto.Articulo} {apto.Singular}");
        Assert.Equal("Este", apto.Demostrativo);
    }

    //LineaDe: la línea del apartamento sale del nombre quitando piso y torre
    [Theory]
    [InlineData("27A", "27", "A")]          // nomenclatura letra
    [InlineData("6E", "6", "E")]
    [InlineData("1204 T3", "12", "04")]     // nomenclatura número + torre
    [InlineData("101 T1", "1", "01")]
    [InlineData("801T1", "8", "01")]
    [InlineData("305", "3", "05")]          // sin torre
    [InlineData("Local 2", "", "Local 2")]  // sin piso: se usa el nombre completo
    [InlineData("", "", "")]
    public void LineaDe_ExtraeLaLinea(string apto, string piso, string esperado)
        => Assert.Equal(esperado, MapaPisos.LineaDe(apto, piso));

    //Actividades.Normalizar: lista blanca; lo desconocido cae en proyecto nuevo
    [Theory]
    [InlineData("ACTIVACION", "ACTIVACION")]
    [InlineData("activacion", "ACTIVACION")]
    [InlineData("NUEVA_ETAPA", "NUEVA_ETAPA")]
    [InlineData("PROYECTO_NUEVO", "PROYECTO_NUEVO")]
    [InlineData("cualquier cosa", "PROYECTO_NUEVO")]
    [InlineData("", "PROYECTO_NUEVO")]
    [InlineData(null, "PROYECTO_NUEVO")]
    public void Normalizar_ValidaLaActividad(string? entrada, string esperado)
        => Assert.Equal(esperado, Actividades.Normalizar(entrada));

    //EntraAlLanzamiento: sale al evento lo que llega DISPONIBLE en el Excel
    [Theory]
    // Estreno: todo lo disponible cuenta, lo comprometido no
    [InlineData("PROYECTO_NUEVO", "DISPONIBLE", "", "", true)]
    [InlineData("PROYECTO_NUEVO", "VENDIDO",   "", "", false)]
    // Activación: el archivo trae ventas viejas y esas son historia
    [InlineData("ACTIVACION", "DISPONIBLE", "", "", true)]
    [InlineData("ACTIVACION", "VENDIDO",    "", "", false)]
    [InlineData("ACTIVACION", "RESERVADO",  "", "", false)]
    // Etapa nueva: además de disponible, tiene que ser la etapa que se lanza
    [InlineData("NUEVA_ETAPA", "DISPONIBLE", "Etapa 2", "Etapa 2", true)]
    [InlineData("NUEVA_ETAPA", "DISPONIBLE", "etapa 2", "Etapa 2", true)]   // sin distinguir mayúsculas
    [InlineData("NUEVA_ETAPA", "DISPONIBLE", "Etapa 1", "Etapa 2", false)]  // sobrante de la etapa anterior
    [InlineData("NUEVA_ETAPA", "VENDIDO",    "Etapa 2", "Etapa 2", false)]
    [InlineData("NUEVA_ETAPA", "DISPONIBLE", "Etapa 1", "", true)]          // sin etapa indicada entran todas
    public void EntraAlLanzamiento_SeparaElEventoDeLaHistoria(
        string actividad, string estado, string etapaFila, string etapaLanzada, bool esperado)
        => Assert.Equal(esperado, Actividades.EntraAlLanzamiento(actividad, estado, etapaFila, etapaLanzada));

    //El título es el que ve el área comercial en pantalla
    [Fact]
    public void Titulo_NombraLaActividad()
    {
        Assert.Equal("Activación", Actividades.Titulo("ACTIVACION"));
        Assert.Equal("Lanzamiento de nueva etapa", Actividades.Titulo("NUEVA_ETAPA"));
        Assert.Equal("Lanzamiento de proyecto nuevo", Actividades.Titulo("otra cosa"));
    }

    //LimpiarTexto: el espacio duro de los pegados deja usuarios imposibles de escribir
    [Theory]
    [InlineData("OscarGiraldo  ", "OscarGiraldo")]
    [InlineData("  AnaCastano  ", "AnaCastano")]
    [InlineData("Juan   Pablo", "Juan Pablo")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void LimpiarTexto_QuitaEspaciosInvisibles(string? entrada, string esperado)
        => Assert.Equal(esperado, ImportacionUsuarios.LimpiarTexto(entrada));

    //PartirNombre: convención colombiana, dos apellidos al final
    [Theory]
    [InlineData("ANA MARIA CASTANO PELAEZ", "Ana Maria", "Castano Pelaez")]
    [InlineData("JUAN SALVADOR ALVAREZ MUÑOZ", "Juan Salvador", "Alvarez Muñoz")]
    [InlineData("JULIANA MAYA DIAZ", "Juliana", "Maya Diaz")]
    [InlineData("SARA FARBEROFF", "Sara", "Farberoff")]
    [InlineData("MADONNA", "Madonna", "")]
    [InlineData("", "", "")]
    public void PartirNombre_SeparaNombreYApellidos(string completo, string nombre, string apellido)
    {
        var (n, a) = ImportacionUsuarios.PartirNombre(completo);
        Assert.Equal(nombre, n);
        Assert.Equal(apellido, a);
    }

    //NormalizarRol: lista blanca. Un rol mal escrito crea una cuenta que no puede entrar
    //a ninguna pantalla, que es justo lo que pasó con "superadmin" escrito a mano.
    [Theory]
    [InlineData("Vendedor", "Vendedor")]
    [InlineData("vendedor", "Vendedor")]
    [InlineData("ASESOR", "Vendedor")]
    [InlineData("", "Vendedor")]
    [InlineData(null, "Vendedor")]
    [InlineData("Administrador", "Administrador")]
    [InlineData("admin", "Administrador")]
    [InlineData("superadmin", "")]           // no se crea desde el Excel
    [InlineData("SuperAdministrador", "")]
    [InlineData("jefe", "")]
    public void NormalizarRol_SoloAceptaLosDosRoles(string? entrada, string esperado)
        => Assert.Equal(esperado, ImportacionUsuarios.NormalizarRol(entrada));

    //MotivoDeRechazo: null significa que la fila se puede crear
    [Fact]
    public void MotivoDeRechazo_AceptaUnaFilaCompleta()
        => Assert.Null(ImportacionUsuarios.MotivoDeRechazo("Ana", "AnaCastano", "Vendedor",
                                                           "ana@ejemplo.com", ""));

    [Theory]
    [InlineData("", "AnaC", "Vendedor", "", "")]                      // sin nombre
    [InlineData("Ana", "", "Vendedor", "", "")]                       // sin usuario
    [InlineData("Ana", "Ana Castano", "Vendedor", "", "")]            // usuario con espacio
    [InlineData("Ana", "AnaC", "", "", "")]                           // rol no reconocido
    [InlineData("Ana", "AnaC", "Vendedor", "correo-sin-arroba", "")]  // correo inválido
    [InlineData("Ana", "AnaC", "Vendedor", "", "corta1")]             // contraseña muy corta
    public void MotivoDeRechazo_RechazaLoQueNoSirve(string nombre, string usuario, string rol,
                                                    string correo, string password)
        => Assert.NotNull(ImportacionUsuarios.MotivoDeRechazo(nombre, usuario, rol, correo, password));

    //GenerarPassword: distinta cada vez y sin caracteres que se confundan al dictarla
    [Fact]
    public void GenerarPassword_EsDistintaYLegible()
    {
        var claves = Enumerable.Range(0, 50).Select(_ => ImportacionUsuarios.GenerarPassword()).ToList();
        Assert.Equal(50, claves.Distinct().Count());
        foreach (var c in claves)
        {
            Assert.True(c.Length >= 10);
            Assert.DoesNotContain('O', c);
            Assert.DoesNotContain('0', c);
            Assert.DoesNotContain('l', c);
            Assert.DoesNotContain('1', c);
        }
    }

    //ListaNegociada: con qué lista se cerró lo que llega vendido o reservado en el Excel.
    //Si se toma la lista equivocada, al cliente se le cobra un precio que nunca aceptó.
    [Theory]
    [InlineData("3", 3)]
    [InlineData("1", 1)]
    [InlineData("5", 5)]
    [InlineData(" 4 ", 4)]
    [InlineData("Lista 2", 2)]     // por si alguien escribe la palabra
    [InlineData("", 1)]            // sin dato: la Lista 1, como los archivos antiguos
    [InlineData(null, 1)]
    [InlineData("0", 1)]           // fuera de rango
    [InlineData("9", 1)]
    [InlineData("abc", 1)]
    public void ListaNegociada_LeeLaColumnaDelExcel(string? entrada, int esperado)
        => Assert.Equal(esperado, Listas.ListaNegociada(entrada));

    //NormalizarUrl: un enlace que publica el admin se renderiza en la pantalla de todos
    //los asesores. Solo http y https; lo demás se rechaza.
    [Theory]
    [InlineData("https://ejemplo.com/pres.pdf")]
    [InlineData("http://ejemplo.com")]
    [InlineData("www.ejemplo.com")]              // sin esquema: se asume https
    [InlineData("  https://ejemplo.com  ")]
    public void NormalizarUrl_AceptaDireccionesWeb(string entrada)
        => Assert.NotNull(Enlaces.NormalizarUrl(entrada));

    [Theory]
    [InlineData("javascript:alert(1)")]          // ejecución de código en la pantalla ajena
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("file:///C:/secretos.txt")]
    [InlineData("https://ejemplo.com\nHost: otro")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizarUrl_RechazaLoPeligroso(string? entrada)
        => Assert.Null(Enlaces.NormalizarUrl(entrada));

    [Fact]
    public void NormalizarUrl_AgregaElEsquemaQueFalta()
        => Assert.StartsWith("https://", Enlaces.NormalizarUrl("ejemplo.com/brochure")!);

    //Dominio: lo que ve el asesor debajo del título, sin la dirección completa
    [Theory]
    [InlineData("https://www.ejemplo.com/a/b?c=1", "ejemplo.com")]
    [InlineData("https://drive.google.com/file/x", "drive.google.com")]
    [InlineData("no-es-una-url", "")]
    public void Dominio_MuestraADondeLleva(string url, string esperado)
        => Assert.Equal(esperado, Enlaces.Dominio(url));

    //Solo lectura: de esta respuesta depende que el rol de dirección no pueda escribir
    [Theory]
    [InlineData("Direccion", true)]
    [InlineData("Administrador", false)]
    [InlineData("SuperAdministrador", false)]
    [InlineData("Vendedor", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsSoloLectura_SoloDireccion(string? rol, bool esperado)
        => Assert.Equal(esperado, Roles.EsSoloLectura(rol));

    //PuedeCrear: nadie reparte más de lo que tiene
    [Fact]
    public void PuedeCrear_NadieEscalaPrivilegios()
    {
        // El superadministrador es el único que crea administradores.
        Assert.Contains("Administrador", Roles.PuedeCrear("SuperAdministrador"));
        Assert.DoesNotContain("Administrador", Roles.PuedeCrear("Administrador"));

        // Un administrador sí puede crear cuentas menos capaces que la suya.
        Assert.Contains("Direccion", Roles.PuedeCrear("Administrador"));
        Assert.Contains("Vendedor", Roles.PuedeCrear("Administrador"));

        // Nadie puede crear un superadministrador desde la plataforma.
        Assert.DoesNotContain("SuperAdministrador", Roles.PuedeCrear("SuperAdministrador"));

        // Los roles sin mando no crean a nadie.
        Assert.Empty(Roles.PuedeCrear("Direccion"));
        Assert.Empty(Roles.PuedeCrear("Vendedor"));
    }

    [Fact]
    public void Titulo_NombraElRolEnPantalla()
    {
        Assert.Equal("Dirección", Roles.Titulo("Direccion"));
        Assert.Equal("Super Admin", Roles.Titulo("SuperAdministrador"));
    }
}
