# Contexto de la aplicación — para redactar el informe de práctica

> Documento de insumo. Reúne, en un solo lugar, todo lo necesario para escribir las
> secciones **Objetivos**, **Resultados** y **Conclusiones** del informe de práctica
> sobre la *Plataforma de Lanzamientos Inmobiliarios* de Londoño Gómez S.A.S.
>
> **Regla importante para quien redacte:** todo lo que aparece aquí es verificable en el
> código o en la documentación del repositorio. **No inventar cifras de negocio**
> (ventas logradas, tiempos ahorrados, porcentajes de mejora) que no hayan sido medidas:
> el lanzamiento en producción todavía no ocurre. Donde se necesite hablar de beneficios,
> redactarlos como *resultados esperados*, no como resultados medidos.

---

## 1. Identificación del proyecto

| | |
|---|---|
| **Nombre** | Plataforma de Lanzamientos Inmobiliarios |
| **Empresa** | Londoño Gómez S.A.S. — Medellín, Colombia |
| **Practicante** | Sergio Lezcano Sucerquia |
| **Institución** | Institución Universitaria Pascual Bravo — Tecnología en Desarrollo de Software |
| **Periodo de desarrollo** | 26 de mayo de 2026 – septiembre de 2026 |
| **Estado** | Desplegado en ambiente de pruebas (Azure App Service); pendiente el lanzamiento en producción |

---

## 2. El problema que resuelve

Londoño Gómez realiza **lanzamientos inmobiliarios**: eventos comerciales de uno o pocos
días en los que se pone a la venta el inventario completo de un proyecto (un edificio o
conjunto) y un equipo de asesores atiende simultáneamente a decenas de familias en una
sala de ventas.

Antes de la plataforma, el inventario y los precios se manejaban en **hojas de cálculo
compartidas**. Ese esquema tiene tres fallas estructurales en un evento de alta
concurrencia:

1. **Doble venta.** Dos asesores pueden vender el mismo apartamento porque cada uno
   trabaja sobre su copia o sobre una hoja que no se refresca. El error se detecta cuando
   ya se firmaron dos separos.
2. **Precio desactualizado.** Los proyectos manejan *listas de precios* que suben a medida
   que se vende el inventario. En una hoja, el asesor puede cotizar con una lista vencida
   y la empresa pierde el diferencial o debe honrar un precio menor.
3. **Ausencia de información consolidada.** La gerencia no sabe, en el momento, cuánto se
   ha vendido, qué áreas están quedando rezagadas ni qué asesor está cerrando.

La propuesta de mejora consistió en construir una **aplicación web** que centralice el
inventario, controle los cambios de estado con garantías de concurrencia, gestione las
listas de precios y entregue información consolidada en tiempo real durante el evento.

---

## 3. Objetivos (redacción sugerida)

### Objetivo general

Desarrollar e implementar una plataforma web para la gestión de lanzamientos inmobiliarios
de Londoño Gómez S.A.S., utilizando ASP.NET Core 10 MVC para la aplicación, SQL Server como
motor de base de datos y SignalR para la comunicación en tiempo real, cumpliendo el ciclo
de vida del software y garantizando la integridad de la información del inventario durante
eventos de venta de alta concurrencia.

### Objetivos específicos

1. **Levantar los requerimientos** del proceso de lanzamiento mediante historias de usuario
   organizadas por épicas y funcionalidades, de modo que reflejen la operación real de la
   sala de ventas.
2. **Diseñar el modelo de datos y la arquitectura** de la aplicación, definiendo las
   entidades del inventario, las ventas, los clientes y las listas de precios, junto con
   las reglas que garantizan su integridad.
3. **Implementar los módulos** de carga de inventario, gestión de inmuebles, registro de
   ventas, administración de usuarios y generación de informes, aplicando controles de
   concurrencia y de seguridad.
4. **Desplegar la solución** en un entorno de nube con integración continua, y validar su
   funcionamiento en un ambiente de pruebas equivalente al de producción.

---

## 4. Alcance y roles

La plataforma distingue **tres roles**, cada uno con su propio panel:

| Rol | Qué puede hacer |
|---|---|
| **SuperAdministrador** | Todo lo del Administrador, más crear cuentas de Administrador |
| **Administrador** | Cargar proyectos, administrar inventario y precios, gestionar usuarios y clientes, registrar y anular ventas, generar informes |
| **Vendedor (asesor)** | Consultar el inventario del proyecto asignado, tomar unidades, reservar con precio bloqueado y registrar ventas. No accede a configuración ni a precios de lista |

El acceso del asesor a un proyecto se hace mediante un **código de acceso** que genera la
plataforma al cargar el proyecto.

---

## 5. Arquitectura y tecnologías

### Stack

| Capa | Tecnología |
|---|---|
| Aplicación | **ASP.NET Core 10 MVC** (`net10.0`), C# |
| Vistas | **Razor** (`.cshtml`) con layouts compartidos por rol |
| Base de datos | **SQL Server** (Azure SQL en la nube; LocalDB en desarrollo) |
| Acceso a datos | **ADO.NET directo** (`Microsoft.Data.SqlClient` 6.1.4), sin ORM |
| Tiempo real | **SignalR** con hub tipado |
| Autenticación | Sesión propia (sin ASP.NET Identity) + **BCrypt** (factor de coste 12) |
| Excel | **EPPlus** 8.4.2 (lectura de la carga e informes) |
| PDF | **QuestPDF** 2026.2.4 |
| Correo | **MailKit** (recuperación de contraseña por SMTP) |
| Despliegue | **Azure App Service** con **GitHub Actions** (CI/CD) |

### Decisión de arquitectura: ADO.NET en lugar de un ORM

Se optó por consultas parametrizadas escritas a mano en vez de Entity Framework. La razón
es el control explícito sobre las operaciones críticas: los cambios de estado de un
inmueble se resuelven con un único `UPDATE` condicionado, y ese comportamiento —descrito
en el punto 7— es más difícil de garantizar a través de la capa de abstracción de un ORM.
Toda consulta es parametrizada (`@parametro`), nunca concatenada, para evitar inyección SQL.

### Organización del código

```
Plataforma_ventas/
├── Controllers/      10 controladores (uno por área funcional)
├── Views/            37 vistas Razor + 2 layouts compartidos por rol
├── Services/         Correo, bloqueo de cuentas, auditoría
├── Hubs/             VentasHub — hub SignalR tipado
├── Filters/          RolAutorizadoAttribute — autorización por rol
├── Scripts/          Migraciones SQL idempotentes
└── wwwroot/css/      Hoja de estilos compartida
```

Aproximadamente **8.500 líneas de C#** y **8.250 líneas de Razor**, en **143 commits**.

---

## 6. Modelo de datos

Entidades principales:

| Tabla | Función |
|---|---|
| **Proyectos** | Un lanzamiento. Nombre, tipo de producto, código de acceso, lista de precios vigente |
| **Inmuebles** | Cada unidad: nombre, torre, etapa, piso, área, tipo, los 5 precios de lista, estado y quién la tiene |
| **ProyectoAreaListas** | Lista de precios vigente **por área** dentro de un proyecto, y su umbral de escalamiento |
| **Usuarios** | Cuentas con rol y proyecto asignado; contraseña en hash BCrypt |
| **Clientes** | Compradores, con el medio publicitario por el que llegaron |
| **Ventas** | Cierre: inmueble, cliente, asesor, lista aplicada, precio, destino, estado y origen |
| **MediosPublicitarios** | Catálogo editable de canales de captación (183 registros iniciales) |
| **AjustesPrecio** / **AjustesPrecioDetalle** | Cada ajuste de precios con el valor anterior de cada unidad, para poder revertirlo |
| **HistorialListas** | Trazabilidad de los cambios de lista de precios |
| **AsistenciaEvento / AsistenciaDia / AsistenciaTorre / AsistenciaFranja** | Cuadro de asistencia del evento: familias atendidas y resultados por día, torre y franja horaria |

**Estados de un inmueble:** `DISPONIBLE` → `EN PROCESO` → `VENDIDO`, con `RESERVADO` como
estado paralelo desde el que también se puede vender o liberar.

---

## 7. Reglas de negocio críticas

Estas son las decisiones de diseño que vale la pena destacar en el informe, porque
resuelven directamente los problemas planteados en el punto 2.

### 7.1 Cambios de estado sin condición de carrera

El problema de la doble venta se resuelve con **una sola sentencia atómica** que incluye el
estado esperado en la condición:

```sql
UPDATE Inmuebles
SET Estado = 'EN PROCESO', IdVendedorEnProceso = @uid
WHERE IdInmuebles = @id AND Estado = 'DISPONIBLE'
```

Si la sentencia afecta **cero filas**, significa que otro asesor ganó la carrera en ese
instante: la operación se detiene y se le informa. Nunca se consulta primero y se actualiza
después, porque entre la consulta y la escritura cabe otra transacción. Es el mecanismo que
hace imposible vender dos veces la misma unidad, incluso con veinte asesores simultáneos.

### 7.2 Precio bloqueado al reservar

Al reservar, el precio vigente se **congela** en la unidad (`PrecioReserva`). Si la lista
sube después, la venta se cierra con el precio que se le prometió al cliente. Es una
garantía comercial implementada en el dato, no en un acuerdo verbal.

### 7.3 Listas de precios por área, manuales y automáticas

Cada área (agrupación por metros cuadrados) tiene su propia lista vigente. Puede operar en
dos modos:

- **Manual:** la lista se mantiene fija hasta que el administrador la cambie.
- **Automática:** sube un nivel cada *N* unidades vendidas de esa área.

Ambos modos conviven porque el negocio los necesita: hay áreas cuyo precio se mantiene
durante todo el lanzamiento y otras que escalan con la demanda.

### 7.4 Nada se borra

Una venta anulada se marca como `ANULADA` con motivo, fecha y responsable, y el inmueble
vuelve a estar disponible; el registro se conserva. Un ajuste de precios guarda el valor
anterior de cada unidad, de modo que puede revertirse. Un medio publicitario eliminado del
catálogo no desaparece de los clientes que ya lo tenían. El criterio transversal es que
**el sistema no reescribe la historia**.

---

## 8. Módulos de la aplicación

### 8.1 Autenticación y control de acceso
Inicio de sesión con BCrypt; **bloqueo de cuenta** tras 5 intentos fallidos durante 15
minutos; **recuperación de contraseña** por correo con token de 256 bits del que solo se
almacena su hash SHA-256, de un solo uso y con vencimiento a 15 minutos; autorización por
rol mediante un atributo propio; token antiforgery en todas las operaciones de escritura.

### 8.2 Carga de proyectos desde Excel
El administrador sube el archivo de lista de precios del proyecto. El sistema:
- detecta las columnas por su encabezado y admite distintos tipos de producto
  (apartamentos, suites, lotes, consultorios, oficinas);
- **lee todas las hojas del libro**, tratando cada una como una etapa del proyecto;
- detecta cuáles de las columnas de lista traen precio y las mapea a las cinco listas;
- deduce la **torre** de cada unidad, de una columna propia o del nombre comercial
  (por ejemplo `1204 T3`);
- valida todas las hojas **antes** de escribir en la base, para no dejar el proyecto a medias;
- genera el **código de acceso** con el que los asesores se vinculan al proyecto.

### 8.3 Inventario
Navegación por **área → unidades**, con filtros por torre y etapa. Muestra el precio de la
lista vigente, el estado de cada unidad y quién la tiene tomada o reservada. Permite editar
el precio de una lista para toda un área, **con opción de deshacer**, y configurar el
escalamiento automático.

### 8.4 Flujo de venta del asesor
Tomar una unidad disponible (pasa a *en proceso*), reservarla con precio bloqueado y
observación, o registrar la venta. Antes de habilitar el panel de datos del cliente, el
sistema exige confirmar la **verificación SAGRILAFT** (normativa antilavado). El precio y
la lista aplicada se derivan **en el servidor**, nunca se toman del formulario.

### 8.5 Reservas
Pantalla dedicada para el administrador: ver todas las reservas activas con su precio
bloqueado, **asignar o corregir el asesor** al que corresponde la reserva, editar la
observación y descargar el **informe de reservas en Excel**.

### 8.6 Dashboard y tiempo real
Panel con indicadores del lanzamiento y mapa del inventario. Mediante SignalR, cualquier
cambio de estado se propaga **inmediatamente** a todas las pantallas conectadas, sin
recargar. Es lo que sustituye al "avísale a todos que el 1204 se vendió".

### 8.7 Informes
- **En pantalla:** indicadores, mapa por torre y área, ventas por asesor, tipologías,
  medios publicitarios y análisis de horas pico.
- **Excel:** mapa del inventario, ventas por asesor, listado de ventas, reservas y cuadro
  de asistencia.
- **PDF:** informe técnico del lanzamiento con la imagen corporativa.

### 8.8 Cuadro de asistencia
Captura de las familias atendidas por día y franja horaria, con sus resultados por torre,
para cruzar **afluencia contra ventas** y saber en qué momentos del evento se concreta.

---

## 9. Seguridad

| Control | Implementación |
|---|---|
| Contraseñas | BCrypt, factor de coste 12 (sal aleatoria por contraseña) |
| Fuerza bruta | Bloqueo de 5 intentos / 15 minutos |
| Recuperación de clave | Token de 256 bits, se almacena solo su hash SHA-256, un solo uso, expira en 15 min, límite de 5 solicitudes por IP cada 15 min y respuesta anti-enumeración |
| Inyección SQL | Consultas parametrizadas en el 100 % de los accesos |
| CSRF | Token antiforgery en todas las operaciones POST |
| Cabeceras HTTP | CSP estricta, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy`, `Permissions-Policy` |
| Cookies | `HttpOnly` y `SameSite=Strict`; sesión de 20 minutos |
| Errores | Páginas propias de error; sin trazas de excepción hacia el usuario |
| Auditoría | Inicios de sesión, intentos fallidos, bloqueos y recuperaciones quedan en el registro con usuario e IP |

---

## 10. Metodología y despliegue

- **Marco de trabajo:** desarrollo iterativo guiado por historias de usuario, organizadas en
  la jerarquía *Épica → Feature → Historia de usuario → Task* (Azure DevOps).
- **Control de versiones:** Git, con rama de desarrollo y despliegue automático.
- **CI/CD:** GitHub Actions compila y publica en Azure App Service en cada *push*.
- **Migraciones:** scripts SQL **idempotentes** —pueden ejecutarse varias veces sin
  duplicar ni perder datos—, de modo que la base de pruebas y la de producción se
  actualizan con el mismo archivo.
- **Pruebas:** proyecto de pruebas unitarias (xUnit) sobre las reglas de negocio puras:
  normalización de precios y documentos, resolución de torre, cálculo de ajustes de precio,
  validación de destinos y nombres de unidad.

---

## 11. Resultados obtenidos (verificables)

Esto es lo que **existe y funciona**, y puede afirmarse sin riesgo en el informe:

1. **Plataforma web completa y desplegada** en Azure App Service, operativa en ambiente de
   pruebas, con tres roles diferenciados y sus paneles.
2. **Inventario centralizado** con control de concurrencia que hace imposible la doble
   venta, por diseño y no por convención.
3. **Carga automatizada del proyecto** desde el archivo Excel que la empresa ya usa, sin
   pedirle a nadie que cambie su forma de trabajar. Incluye proyectos multi-etapa y
   distintos tipos de producto.
4. **Gestión de listas de precios** por área, manual y automática, con historial y con
   posibilidad de deshacer tanto una edición puntual como un ajuste masivo.
5. **Tiempo real** en los paneles de administración y de asesores mediante SignalR.
6. **Informes** en pantalla, Excel y PDF, incluyendo mapa del inventario por torre y área,
   ventas por asesor, reservas y cuadro de asistencia.
7. **Controles de seguridad** listados en el punto 9, incluidos el bloqueo por fuerza bruta
   y el flujo de recuperación de contraseña conforme a prácticas estándar.
8. **Trazabilidad**: ninguna operación destructiva borra información; las anulaciones, los
   ajustes de precio y los cambios de lista quedan registrados con su responsable.
9. **Despliegue continuo** funcionando: cada cambio aprobado llega al ambiente de pruebas
   de forma automática.

---

## 12. Resultados esperados (impacto, aún no medido)

Redactar esta sección **en futuro o en condicional**. Son los efectos que la plataforma
busca producir en el primer lanzamiento real (20 asesores, ~200 unidades):

- **Eliminación de la doble venta**, que en el esquema de hojas de cálculo era un riesgo
  permanente y de costo alto: implica deshacer un negocio ya cerrado con un cliente.
- **Cobro del precio correcto**: al derivarse la lista en el servidor y bloquearse el precio
  al reservar, desaparece la diferencia entre lo cotizado y lo que corresponde.
- **Visibilidad inmediata para la gerencia** durante el evento, en lugar de esperar a la
  consolidación manual del cierre del día.
- **Reducción del trabajo administrativo posterior**, al quedar los informes generados
  desde el mismo sistema en lugar de armarse a mano.
- **Trazabilidad para auditoría y cumplimiento**, incluida la verificación SAGRILAFT previa
  al registro del cliente.
- **Medición de la efectividad comercial** por asesor, por área, por torre, por franja
  horaria y por medio publicitario, base para decidir dónde invertir en el siguiente
  lanzamiento.

---

## 13. Limitaciones y trabajo futuro (para Conclusiones)

Reconocer límites da credibilidad al informe:

- La verificación **SAGRILAFT es declarativa**: el sistema exige confirmar que la consulta
  se hizo, pero no consulta las listas restrictivas. Integrarlo requiere definición del área
  de cumplimiento.
- **SignalR opera en memoria de un solo proceso.** Con la escala prevista es suficiente,
  pero escalar a varias instancias exigiría un *backplane* (Azure SignalR Service o Redis).
- El ambiente de pruebas corre en un **plan gratuito** de Azure, con limitaciones de
  disponibilidad que no aplicarían en producción.
- La plataforma **no se ha usado todavía en un lanzamiento real**, por lo que los beneficios
  del punto 12 son proyecciones, no mediciones.

---

## 14. Glosario (para la sección Glosario del informe)

- **ADO.NET:** conjunto de clases de .NET para acceder a bases de datos mediante consultas
  explícitas, sin una capa de mapeo objeto-relacional.
- **ASP.NET Core:** framework libre y multiplataforma de Microsoft para construir
  aplicaciones y servicios web.
- **BCrypt:** algoritmo de hash diseñado para contraseñas, deliberadamente lento y con sal
  incorporada, lo que dificulta los ataques por fuerza bruta.
- **CSRF (Cross-Site Request Forgery):** ataque que induce al navegador de un usuario
  autenticado a ejecutar una acción no deseada; se mitiga con un token por formulario.
- **CI/CD:** integración y despliegue continuos; automatización de la compilación, prueba y
  publicación del software.
- **Condición de carrera:** error que ocurre cuando dos operaciones simultáneas sobre el
  mismo dato producen un resultado incorrecto según el orden en que se ejecuten.
- **Inyección SQL:** vulnerabilidad que permite alterar una consulta insertando código en
  los datos de entrada; se evita con consultas parametrizadas.
- **MVC (Modelo-Vista-Controlador):** patrón que separa los datos, la presentación y la
  lógica de control de una aplicación.
- **Razor:** motor de vistas de ASP.NET Core que combina HTML con C#.
- **SAGRILAFT:** Sistema de Autocontrol y Gestión del Riesgo Integral de Lavado de Activos y
  Financiación del Terrorismo, exigido por la Superintendencia de Sociedades de Colombia.
- **SignalR:** biblioteca de ASP.NET Core que permite enviar información del servidor al
  navegador en tiempo real, sin que el cliente tenga que preguntar.
- **SQL Server:** sistema gestor de bases de datos relacionales de Microsoft.
- **Transacción:** conjunto de operaciones sobre la base de datos que se confirman o se
  deshacen como una sola unidad.

---

## 15. Figuras y tablas sugeridas

Para que el informe tenga el mismo tratamiento gráfico del documento original:

**Figuras** (capturas de la aplicación)
1. Pantalla de inicio de sesión
2. Carga de proyecto desde Excel
3. Inventario por áreas, con filtros de torre y etapa
4. Listado de unidades de un área con sus estados
5. Registro de venta con la verificación SAGRILAFT
6. Pantalla de reservas con asignación de asesor
7. Dashboard con indicadores y mapa en tiempo real
8. Informe en pantalla: mapa por torre y área
9. Panel del asesor
10. Informe PDF generado
11. Diagrama entidad-relación de la base de datos
12. Historias de usuario (captura de Azure DevOps)

**Tablas**
1. Cronograma de actividades
2. Roles y permisos
3. Tecnologías utilizadas
4. Controles de seguridad implementados

---

## 16. Advertencias para la redacción

- No afirmar cifras de ventas, tiempos ahorrados ni porcentajes de mejora: **no se han
  medido**.
- No describir la verificación SAGRILAFT como una consulta automática a listas
  restrictivas; es una confirmación declarativa.
- El proyecto está **desplegado en pruebas**, no en producción. Decir "en producción" sería
  inexacto.
- La empresa es **Londoño Gómez S.A.S.** El documento base que sirvió de plantilla
  corresponde a otra empresa (BTW S.A.S.) y a otro proyecto (Gdoc Clientes 360): esos
  contenidos deben reemplazarse por completo en la introducción, el planteamiento, los
  objetivos y los resultados.
