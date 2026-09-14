# Informe de práctica — contenido redactado

**Título:** Desarrollo de aplicación de nuevos lanzamientos para la venta de inmuebles
**Autor:** Sergio Lezcano Sucerquia
**Empresa:** Londoño Gómez S.A.S.
**Institución:** Institución Universitaria Pascual Bravo — Tecnología en Desarrollo de Software

> **Cómo usar este documento.** Sigue la misma estructura del informe de práctica que ya
> tienes armado. Cada apartado está redactado para reemplazar directamente el contenido que
> hoy corresponde al proyecto de BTW S.A.S. / Gdoc Clientes 360.
>
> Las secciones **1.1 Descripción de la empresa**, **1.1.1 Reseña histórica**, **1.1.2
> Misión**, **1.1.3 Visión** y **1.2 Descripción del área de trabajo** ya están adaptadas a
> Londoño Gómez en tu documento: **no las toques**. Lo que se reemplaza es la Introducción,
> las funciones de la práctica, el planteamiento, los objetivos, los resultados y las
> conclusiones.
>
> **Advertencia:** no se han medido resultados de negocio, porque el primer lanzamiento en
> producción todavía no ocurre. No agregar porcentajes de mejora, tiempos ahorrados ni
> cifras de venta. Donde se habla de beneficios, van en condicional.

---

## Introducción

Londoño Gómez S.A.S. es una empresa colombiana fundada en Medellín en la década de 1970,
dedicada al desarrollo y la comercialización de proyectos inmobiliarios en todo el país. Una
parte central de su operación comercial son los **lanzamientos**: eventos de uno o pocos días
en los que se pone a la venta el inventario completo de un proyecto y un equipo de asesores
atiende de manera simultánea a decenas de familias interesadas en una sala de ventas.

Hasta ahora, la gestión de estos eventos se apoyaba en hojas de cálculo compartidas que
contenían el inventario de unidades y sus listas de precios. Este esquema, adecuado para la
planeación previa, resulta frágil durante el evento mismo, cuando varios asesores consultan
y modifican la misma información al mismo tiempo. De esa fragilidad se derivan tres riesgos
concretos: la venta duplicada de una misma unidad, la cotización con una lista de precios
que ya fue superada, y la ausencia de información consolidada que permita a la dirección
tomar decisiones mientras el evento transcurre.

En el presente informe se documenta el procedimiento llevado a cabo para diseñar, desarrollar
e implementar una plataforma web que centraliza la gestión de los lanzamientos inmobiliarios
de la compañía. La solución controla el inventario en un único repositorio, garantiza la
integridad de los cambios de estado de cada unidad ante accesos concurrentes, administra las
listas de precios con sus reglas de escalamiento y entrega información consolidada en tiempo
real tanto a los asesores como a la administración.

El resultado final es una aplicación web desarrollada en ASP.NET Core 10 bajo el patrón
Modelo-Vista-Controlador, con SQL Server como motor de base de datos, SignalR para la
comunicación en tiempo real entre el servidor y los navegadores, y desplegada en Microsoft
Azure mediante un proceso de integración y despliegue continuos.

---

## 1.3 Funciones asignadas y plan de trabajo concertado con la empresa

> *(Reemplaza la lista de funciones del documento actual. Ajusta según lo que realmente
> hayas hecho; esta es la versión correspondiente al desarrollo de la plataforma.)*

En el cargo como desarrollador de software se tienen asignadas las siguientes funciones:

- Participar en el levantamiento de requerimientos con las áreas comercial y de sistemas,
  traduciendo las necesidades del negocio a historias de usuario.
- Diseñar el modelo de datos relacional y la arquitectura de la aplicación.
- Desarrollar la aplicación web completa, tanto la capa de presentación como la lógica de
  negocio y el acceso a datos.
- Implementar los controles de seguridad de la aplicación conforme a buenas prácticas.
- Desplegar y mantener la solución en el entorno de nube, incluyendo la configuración del
  proceso de integración continua.
- Documentar las historias de usuario y los cambios realizados sobre el sistema.

Durante el desarrollo de la práctica no se estableció un cronograma estrictamente definido,
dado que las actividades se asignaron de manera flexible según las necesidades del área de
Sistemas y los requerimientos planteados por los líderes del proyecto. El trabajo se
organizó en ciclos cortos de entrega, priorizando las funcionalidades indispensables para
operar un lanzamiento y sumando progresivamente las de apoyo y análisis.

---

## 2. Planteamiento de la propuesta de mejora

Londoño Gómez S.A.S. comercializa sus proyectos inmobiliarios mediante lanzamientos, eventos
comerciales de corta duración en los que se ofrece al público el inventario completo de un
proyecto. Durante estos eventos, un equipo de asesores atiende simultáneamente a las familias
interesadas, consulta la disponibilidad de las unidades, cotiza con la lista de precios
vigente, bloquea unidades mediante reservas y cierra las ventas.

Actualmente esta operación se soporta en hojas de cálculo compartidas que contienen el
inventario del proyecto y sus listas de precios. Este mecanismo presenta limitaciones
estructurales cuando varios asesores trabajan de forma concurrente. En primer lugar, no
existe un control que impida que dos asesores comprometan la misma unidad: al no actualizarse
la información de manera inmediata en todos los puestos, es posible que dos negocios avancen
en paralelo sobre el mismo inmueble y que la inconsistencia se detecte cuando ya se ha
formalizado un compromiso con ambos clientes. En segundo lugar, los proyectos manejan listas
de precios que se incrementan a medida que se coloca el inventario; con un archivo compartido
no existe garantía de que el asesor esté cotizando con la lista correcta, lo que puede
derivar en que la empresa deba honrar un valor inferior al que corresponde. En tercer lugar,
la dirección no dispone de información consolidada durante el evento, de modo que las
decisiones comerciales —como incrementar una lista o concentrar el esfuerzo en un área con
baja rotación— se toman sin datos oportunos.

El no atender esta situación implica mantener un riesgo operativo y financiero en el momento
de mayor exposición comercial de la compañía, además de una carga administrativa posterior
considerable para consolidar manualmente los resultados de cada evento.

De acuerdo con lo anterior, la propuesta de mejora se orienta a desarrollar e implementar una
plataforma web para la gestión de lanzamientos inmobiliarios, que centralice el inventario en
un único repositorio, garantice la integridad de la información ante accesos concurrentes,
administre las listas de precios con sus reglas de negocio y entregue información consolidada
en tiempo real. Esta plataforma generará impacto en dos frentes: para el equipo comercial,
porque elimina la incertidumbre sobre la disponibilidad y el precio de cada unidad; y para la
dirección, porque convierte el lanzamiento en un proceso medible, con trazabilidad de cada
operación y con informes generados por el propio sistema.

---

## 3. Objetivos

### 3.1 Objetivo general

Desarrollar e implementar una plataforma web para la gestión de lanzamientos inmobiliarios de
Londoño Gómez S.A.S., mediante el uso de ASP.NET Core 10 bajo el patrón Modelo-Vista-
Controlador, SQL Server como motor de base de datos y SignalR para la comunicación en tiempo
real; cumpliendo con el ciclo de vida del software y garantizando la integridad y la
trazabilidad de la información del inventario durante eventos comerciales de alta
concurrencia.

### 3.2 Objetivos específicos

- Generar las historias de usuario y los requerimientos de la plataforma, para establecer el
  alcance de la solución y traducir las necesidades del área comercial a un lenguaje técnico.
- Diagramar la base de datos y la arquitectura de la aplicación, de manera que las entidades
  del inventario, las ventas, los clientes y las listas de precios queden correctamente
  relacionadas y normalizadas.
- Desarrollar los módulos de la plataforma con base en las historias de usuario, los
  requerimientos y la diagramación, implementando los controles de concurrencia y de
  seguridad que exige la operación.
- Desplegar la solución en un entorno de nube con integración continua y validar su
  funcionamiento en un ambiente de pruebas equivalente al de producción.

---

## 4. Resultados

### 4.1 Historias de usuario y requerimientos de la plataforma

El proceso de desarrollo del software contempla la definición de las historias de usuario y
los requerimientos, con el propósito de establecer el alcance de la solución, comprender las
necesidades del área comercial y traducirlas a un lenguaje técnico que sirva de base para el
ciclo de desarrollo, como se muestra en la figura 4.

> **[Figura 4. Historias de usuario — Autenticación e inventario]**
> *Fuente: propia*

Esta definición se llevó a cabo mediante sesiones con los líderes del área comercial y de
sistemas, en las cuales se expuso la necesidad de eliminar el riesgo de comprometer dos veces
una misma unidad y de asegurar que el precio cotizado correspondiera siempre a la lista
vigente. A partir de esta interacción se definieron las historias de usuario presentadas en
las figuras 4 a 6, organizadas bajo la jerarquía *Épica → Funcionalidad → Historia de usuario
→ Tarea*.

> **[Figura 5. Historias de usuario — Flujo de venta y reservas]**
> *Fuente: propia*

El levantamiento arrojó **doce funcionalidades** que agrupan **treinta y cuatro historias de
usuario**, relacionadas a continuación:

| Funcionalidad | Alcance |
|---|---|
| 1. Autenticación y control de acceso | Inicio de sesión, bloqueo por intentos fallidos, recuperación de contraseña y autorización por rol |
| 2. Gestión de usuarios | Creación y edición de cuentas, y asignación de proyecto a los asesores |
| 3. Carga de proyectos e inventario | Carga masiva desde el archivo Excel del proyecto y administración de los proyectos cargados |
| 4. Inventario y estados de inmuebles | Grilla por proyecto, cambios de estado sin condiciones de carrera y listas de precios |
| 5. Flujo de venta del asesor | Consulta del inventario, toma de unidades y reservas con precio bloqueado |
| 6. Registro de ventas y cumplimiento | Verificación SAGRILAFT previa y registro de la venta con cliente nuevo o existente |
| 7. Gestión de clientes | Listado, detalle y catálogo de medios publicitarios |
| 8. Tablero de control y monitoreo | Indicadores del lanzamiento y mapa del inventario en tiempo real |
| 9. Informes y exportaciones | Informe en pantalla, exportaciones a Excel y PDF técnico |
| 10. Cuadro de asistencia del lanzamiento | Captura de familias atendidas por día y franja horaria |
| 11. Seguridad técnica y cumplimiento | Endurecimiento de la aplicación y protección de datos |
| 12. Infraestructura y despliegue | Entornos de pruebas y producción, e integración continua |

> **[Figura 6. Historias de usuario — Informes y cuadro de asistencia]**
> *Fuente: propia*

Cada historia de usuario se documentó con su enunciado en formato *Como… quiero… para…*, sus
criterios de aceptación y las tareas técnicas necesarias para construirla, y se desarrolló de
acuerdo con la prioridad asignada, atendiendo primero aquellas sin las cuales el lanzamiento
no podría operar. A partir de las historias se elaboraron los requerimientos funcionales, no
funcionales y del sistema.

Entre los requerimientos no funcionales se establecieron como críticos los siguientes: la
integridad del inventario ante accesos concurrentes, la respuesta inmediata de las pantallas
ante cambios de estado producidos por otros usuarios, la protección de la información
personal de los compradores y la disponibilidad de la aplicación durante la jornada del
evento.

Una vez formuladas las historias de usuario y documentados los requerimientos, se dio por
concluido el proceso de definición del alcance, dando paso a la fase de diagramación.

### 4.2 Diagramación de la plataforma

Esta etapa se centra en el diseño del software, tomando como base las historias de usuario y
los requerimientos definidos en la fase anterior. En primera instancia se elaboró el Modelo
Entidad-Relación (MER) sobre SQL Server, representado en la figura 7.

> **[Figura 7. Diagrama entidad-relación de la base de datos]**
> *Fuente: propia*

El modelo se estructura alrededor de la entidad **Proyectos**, que representa cada
lanzamiento, y de **Inmuebles**, que contiene cada unidad con su torre, etapa, piso, área,
tipología, sus cinco niveles de precio de lista, su estado y el asesor que la tiene tomada o
reservada. La entidad **ProyectoAreaListas** administra la lista de precios vigente por área
dentro de un mismo proyecto, dado que el negocio requiere que cada tipología escale de forma
independiente. El cierre comercial se registra en **Ventas**, que relaciona el inmueble, el
**Cliente** y el **Usuario** que realizó la operación, conservando la lista aplicada y el
precio pactado. Complementan el modelo las entidades de trazabilidad —**HistorialListas**,
**AjustesPrecio** y **AjustesPrecioDetalle**—, el catálogo de **MediosPublicitarios** y el
conjunto de tablas del cuadro de asistencia del evento.

Posteriormente se definió la arquitectura de la aplicación, organizada bajo el patrón
Modelo-Vista-Controlador. La capa de presentación se construye con vistas Razor apoyadas en
dos plantillas compartidas, una por cada rol; la capa de control se distribuye en diez
controladores, cada uno responsable de un área funcional; y el acceso a datos se resuelve con
ADO.NET mediante consultas parametrizadas. Se optó deliberadamente por no utilizar un mapeador
objeto-relacional, con el fin de mantener control explícito sobre las sentencias que
garantizan la integridad del inventario, según se explica en el apartado siguiente.

> **[Figura 8. Arquitectura de la aplicación]**
> *Fuente: propia*

Finalmente se elaboraron los prototipos de interfaz, en los cuales se definieron la paleta de
colores corporativa, la tipografía, la iconografía y la distribución de las pantallas. Se
diseñaron dos entornos diferenciados: el panel de administración, orientado al control del
inventario y al análisis, y el panel del asesor, concebido para operarse con rapidez durante
la atención al cliente.

> **[Figura 9. Prototipos de interfaz — panel de administración]**
> *Fuente: propia*

> **[Figura 10. Prototipos de interfaz — panel del asesor]**
> *Fuente: propia*

Una vez obtenida la aprobación de los líderes del proyecto sobre los diagramas y prototipos,
se dio inicio al proceso de desarrollo.

### 4.3 Desarrollo de la plataforma con base en las historias de usuario, los requerimientos y la diagramación

El desarrollo se llevó a cabo de forma iterativa, ejecutando las tareas según la priorización
de las historias de usuario, con el propósito de disponer en el menor tiempo posible de un
Producto Mínimo Viable capaz de operar un lanzamiento completo. Este enfoque permitió validar
tempranamente con el área comercial si las necesidades expresadas quedaban efectivamente
cubiertas.

La aplicación se desarrolló en lenguaje C# sobre ASP.NET Core 10, con vistas Razor para la
capa de presentación y SQL Server como motor de base de datos. Para la comunicación en tiempo
real se integró SignalR mediante un *hub* tipado, que notifica a todos los navegadores
conectados cada cambio de estado del inventario. La generación de informes se resolvió con
EPPlus para los archivos de Excel y QuestPDF para el informe técnico, y el envío de correos de
recuperación de contraseña con MailKit. El control de versiones se gestionó con Git, y el
despliegue hacia Microsoft Azure se automatizó mediante GitHub Actions, de modo que cada
cambio aprobado se publica en el ambiente de pruebas sin intervención manual.

**Integridad del inventario.** El requerimiento más exigente del proyecto fue impedir que dos
asesores comprometieran la misma unidad. La solución implementada consiste en resolver cada
cambio de estado con una única sentencia atómica que incluye el estado esperado dentro de su
condición, en lugar de consultar primero y actualizar después:

```sql
UPDATE Inmuebles
SET Estado = 'EN PROCESO', IdVendedorEnProceso = @uid
WHERE IdInmuebles = @id AND Estado = 'DISPONIBLE'
```

Si la sentencia afecta cero filas, significa que otro asesor tomó la unidad en ese mismo
instante; la operación se detiene y se informa al usuario. Este mecanismo traslada la garantía
de exclusión mutua al motor de base de datos, que es el único componente capaz de ofrecerla
de forma confiable, y hace que la doble venta sea imposible por diseño y no por convención
entre los asesores.

**Bloqueo del precio.** Al reservar una unidad, el precio vigente se congela en el registro
del inmueble. Si la lista de precios sube posteriormente, la venta se cierra con el valor que
se le prometió al cliente, sin que ello dependa de la memoria del asesor.

**Listas de precios.** Cada área de un proyecto administra su propia lista vigente y puede
operar en modo manual —el precio permanece fijo hasta que el administrador lo modifique— o en
modo automático, escalando un nivel cada determinada cantidad de unidades vendidas. Ambos
modos conviven porque el negocio los requiere simultáneamente dentro de un mismo proyecto.

> **[Figura 11. Pantalla de inicio de sesión]**
> *Fuente: propia*

> **[Figura 12. Carga del proyecto desde archivo Excel]**
> *Fuente: propia*

La carga del inventario se realiza a partir del mismo archivo de lista de precios que la
empresa ya utiliza, sin exigir cambios en su forma de trabajo. El sistema identifica las
columnas por su encabezado, admite distintos tipos de producto —apartamentos, suites, lotes,
consultorios y oficinas—, procesa todas las hojas del libro tratando cada una como una etapa
del proyecto, determina la torre de cada unidad a partir de su nombre comercial y valida la
totalidad del archivo antes de escribir en la base de datos, de manera que un error en una
hoja no deje el proyecto cargado a medias. Al finalizar, genera el código de acceso con el
que los asesores se vinculan al lanzamiento.

> **[Figura 13. Inventario por áreas con filtros de torre y etapa]**
> *Fuente: propia*

En la figura 13 se presenta la navegación del inventario, organizada por áreas y con filtros
por torre y por etapa. Cada tarjeta muestra el precio de la lista vigente para esa área, el
avance de colocación y las torres en las que la tipología está disponible.

> **[Figura 14. Registro de venta con verificación SAGRILAFT]**
> *Fuente: propia*

En la figura 14 se observa el registro de la venta. Antes de habilitar el panel de datos del
cliente, el sistema exige confirmar la verificación SAGRILAFT, en cumplimiento de la normativa
de prevención de lavado de activos. El precio y la lista aplicada se derivan en el servidor a
partir del estado del inmueble, nunca se toman del formulario enviado por el navegador, de
modo que no es posible alterarlos desde el cliente.

> **[Figura 15. Pantalla de reservas activas]**
> *Fuente: propia*

La figura 15 corresponde a la administración de reservas, donde se consultan las unidades con
precio bloqueado, se asigna o corrige el asesor responsable, se complementa la observación
registrada durante la atención y se descarga el informe de reservas.

> **[Figura 16. Tablero de control con indicadores y mapa en tiempo real]**
> *Fuente: propia*

> **[Figura 17. Informe del lanzamiento: mapa por torre y área]**
> *Fuente: propia*

Las figuras 16 y 17 muestran los componentes de monitoreo y análisis. El tablero de control
presenta los indicadores del lanzamiento y se actualiza automáticamente ante cualquier cambio
de estado, sin que el usuario deba recargar la página. El informe presenta el avance de
colocación por torre y, dentro de cada torre, el detalle por área, de manera que la dirección
identifique no solo cuánto inventario queda, sino de qué tipología es.

Para el cierre del evento, la plataforma genera informes exportables: el mapa del inventario y
el listado de ventas por asesor en Excel, el informe de reservas con su precio bloqueado y su
observación, el cuadro de asistencia del lanzamiento, y un informe técnico consolidado en PDF
con la imagen corporativa de la compañía.

En materia de seguridad se implementaron los siguientes controles: almacenamiento de
contraseñas mediante el algoritmo BCrypt con factor de coste 12; bloqueo temporal de la cuenta
tras cinco intentos fallidos; recuperación de contraseña mediante un token del que solo se
almacena su valor cifrado, de un solo uso y con vencimiento; consultas parametrizadas en la
totalidad de los accesos a la base de datos; token *antiforgery* en todas las operaciones de
escritura; cabeceras HTTP de seguridad; y sesiones con expiración corta y cookies endurecidas.

Adicionalmente, se estableció como criterio transversal que el sistema no elimine información:
una venta anulada conserva su registro con el motivo y el responsable, un ajuste de precios
guarda el valor anterior de cada unidad para poder revertirse, y los cambios de lista quedan
registrados en su historial. Esta decisión responde a la necesidad de auditoría de una
operación en la que cada registro tiene consecuencias contractuales.

Con las figuras anteriores se da por concluido el desarrollo de la propuesta de mejora,
obteniendo como resultado una plataforma desplegada en el ambiente de pruebas de Microsoft
Azure, funcional en la totalidad de las historias de usuario priorizadas y a la espera de su
puesta en operación en el primer lanzamiento en producción.

---

## 5. Conclusiones

El desarrollo de la plataforma permitió afianzar y ampliar los conocimientos en el ecosistema
de tecnologías de Microsoft, particularmente en ASP.NET Core bajo el patrón Modelo-Vista-
Controlador, en el acceso a datos mediante ADO.NET y en la comunicación en tiempo real con
SignalR. El haber prescindido de un mapeador objeto-relacional obligó a comprender con
detalle el comportamiento del motor de base de datos, lo que resultó determinante para
resolver el requerimiento central del proyecto.

El principal aprendizaje técnico del proceso fue el tratamiento de la concurrencia. Comprender
que la única garantía confiable de exclusión mutua la ofrece el propio motor de base de datos,
y que una verificación previa seguida de una escritura deja siempre una ventana de error,
cambió la manera de abordar cualquier operación crítica sobre datos compartidos. Esta
comprensión se tradujo en una regla de diseño aplicada de forma consistente en toda la
plataforma: los cambios de estado se resuelven con una sentencia condicionada y se verifica el
número de filas afectadas antes de continuar.

De igual forma, el proyecto permitió entender la diferencia entre construir software que
funciona y construir software que resiste el error humano. Decisiones como bloquear el precio
al momento de la reserva, conservar el valor anterior de cada ajuste para poder deshacerlo, o
impedir que el precio de una venta provenga del formulario enviado por el navegador, no
responden a requerimientos explícitos del cliente, sino al reconocimiento de que un sistema en
operación real será usado con prisa y bajo presión.

El trabajo directo con el área comercial resultó igualmente formativo. Traducir una necesidad
expresada en términos del negocio —"que no se vendan dos veces", "que no cotice con la lista
vieja"— a una decisión técnica concreta exige comprender el proceso antes de escribir código,
y fue la fuente de la mayoría de los ajustes realizados durante el desarrollo.

Finalmente, el proceso de práctica empresarial resultó altamente satisfactorio. Asumir la
responsabilidad completa de una aplicación, desde el levantamiento de requerimientos hasta el
despliegue en la nube, ofreció una visión integral del ciclo de vida del software y permitió
evaluar y fortalecer tanto las competencias técnicas como las habilidades de comunicación con
áreas no técnicas de la organización.

---

## Anexo A — Datos técnicos verificables

Para consultar al redactar, sin necesidad de volver al código:

| Dato | Valor |
|---|---|
| Framework | ASP.NET Core 10 (`net10.0`), C# |
| Patrón | Modelo-Vista-Controlador |
| Base de datos | SQL Server (Azure SQL en la nube) |
| Acceso a datos | ADO.NET, `Microsoft.Data.SqlClient` 6.1.4 |
| Tiempo real | SignalR, *hub* tipado |
| Contraseñas | BCrypt, factor de coste 12 |
| Excel | EPPlus 8.4.2 · **PDF:** QuestPDF 2026.2.4 · **Correo:** MailKit |
| Despliegue | Azure App Service + GitHub Actions |
| Controladores | 10 · **Vistas Razor:** 37 · **Servicios:** 3 |
| Volumen de código | ≈ 8.500 líneas de C# y ≈ 8.250 de Razor |
| Historial | 143 confirmaciones de cambios entre mayo y septiembre de 2026 |
| Roles | SuperAdministrador, Administrador, Vendedor |
| Estados de un inmueble | DISPONIBLE, EN PROCESO, RESERVADO, VENDIDO |
| Niveles de lista de precios | 5 por unidad |
| Pruebas unitarias | xUnit sobre reglas de negocio puras |

**Limitaciones que conviene reconocer en el informe**

- La verificación SAGRILAFT es **declarativa**: el sistema exige confirmar que la consulta se
  realizó, pero no consulta listas restrictivas. Su automatización requiere definición del
  área de cumplimiento.
- SignalR opera **en memoria de un único proceso**. Es suficiente para la escala prevista
  (veinte asesores), pero escalar a varias instancias exigiría un *backplane*.
- La plataforma está desplegada en **ambiente de pruebas**; el primer lanzamiento en
  producción aún no se ha realizado, por lo que no existen resultados de negocio medidos.

---

## Anexo B — Glosario

- **ADO.NET:** conjunto de clases de .NET que permite acceder a bases de datos mediante
  consultas explícitas, sin una capa de mapeo objeto-relacional.
- **ASP.NET Core:** framework libre y multiplataforma de Microsoft para construir
  aplicaciones y servicios web.
- **BCrypt:** algoritmo de hash diseñado específicamente para contraseñas, deliberadamente
  lento y con sal incorporada, lo que dificulta los ataques por fuerza bruta.
- **CI/CD:** integración y despliegue continuos; automatización de la compilación, prueba y
  publicación del software.
- **Condición de carrera:** error que se produce cuando dos operaciones simultáneas sobre el
  mismo dato generan un resultado incorrecto según el orden en que se ejecuten.
- **CSRF:** *Cross-Site Request Forgery*; ataque que induce al navegador de un usuario
  autenticado a ejecutar una acción no deseada. Se mitiga con un token por formulario.
- **Inyección SQL:** vulnerabilidad que permite alterar una consulta insertando código en los
  datos de entrada. Se evita con consultas parametrizadas.
- **MVC:** patrón Modelo-Vista-Controlador, que separa los datos, la presentación y la lógica
  de control de una aplicación.
- **Razor:** motor de vistas de ASP.NET Core que combina marcado HTML con código C#.
- **SAGRILAFT:** Sistema de Autocontrol y Gestión del Riesgo Integral de Lavado de Activos y
  Financiación del Terrorismo, exigido por la Superintendencia de Sociedades de Colombia.
- **SignalR:** biblioteca de ASP.NET Core que permite enviar información desde el servidor
  hacia el navegador en tiempo real.
- **SQL Server:** sistema gestor de bases de datos relacionales de Microsoft.
- **Transacción:** conjunto de operaciones sobre la base de datos que se confirman o se
  deshacen como una sola unidad.
