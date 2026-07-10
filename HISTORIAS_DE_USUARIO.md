# Historias de Usuario — Plataforma de Ventas (Londoño Gómez)

**Épica:** Plataforma de Lanzamientos Inmobiliarios (nombre del proyecto en Azure DevOps)
**Versión del documento:** 2.0 · **Fecha:** 09/07/2026
**Convención de estados:** ✅ Implementada · 🔨 Parcial · ⏳ Pendiente
**Convención de tasks:** `[x]` completada · `[ ]` pendiente

Estructura para Azure DevOps: **Epic → Feature → User Story → Task**.
Cada Feature incluye una **descripción**; cada Historia de Usuario tiene **al menos una Task** (aunque el trabajo ya esté hecho, queda registrada y marcada como completada, para reflejar el esfuerzo y cerrarla en el tablero).

---

## FEATURE 1 — Autenticación y control de acceso

**Descripción:** Gestiona la identidad y el acceso a la plataforma. Cubre el inicio de sesión seguro con protección contra fuerza bruta, la recuperación de contraseña por correo, la autorización basada en roles (SuperAdministrador, Administrador, Vendedor) y el manejo endurecido de la sesión y sus cookies. Es la puerta de entrada del sistema y la base sobre la que se apoya toda la seguridad funcional.

### HU-101 · Inicio de sesión ✅
**Como** usuario registrado (administrador o vendedor),
**quiero** iniciar sesión con mi usuario y contraseña,
**para** acceder a las funciones que corresponden a mi rol.

**Criterios de aceptación:**
1. Con credenciales válidas soy redirigido al panel de mi rol.
2. Con credenciales inválidas veo "Usuario o contraseña incorrectos" con los intentos restantes, sin revelar cuál campo falló.
3. La contraseña se verifica contra un hash BCrypt (factor 12); nunca en texto plano.
4. Al autenticarme se descarta cualquier sesión previa (anti session-fixation).
5. Todos los intentos quedan registrados con usuario e IP.

**Tasks:**
- [x] Implementar login con verificación BCrypt y auditoría de intentos.
- [ ] Agregar CAPTCHA o rate-limit por IP tras N intentos fallidos.
- [ ] Pruebas automatizadas del flujo de login (unitarias + integración).

### HU-102 · Bloqueo por intentos fallidos (lockout) ✅
**Como** responsable de seguridad,
**quiero** que una cuenta se bloquee temporalmente tras varios intentos fallidos,
**para** impedir ataques de fuerza bruta.

**Criterios de aceptación:**
1. Tras 5 intentos fallidos, la cuenta se bloquea 15 minutos.
2. Durante el bloqueo, incluso la contraseña correcta es rechazada.
3. Bloqueo y desbloqueo quedan auditados con IP.
4. Restablecer la contraseña elimina el bloqueo vigente.

**Tasks:**
- [x] Implementar contador de intentos y bloqueo temporal (IMemoryCache).
- [ ] Panel para que el SuperAdministrador vea y libere bloqueos activos.

### HU-103 · Recuperación de contraseña por correo ✅
**Como** usuario que olvidó su contraseña,
**quiero** solicitar un enlace de restablecimiento a mi correo,
**para** recuperar el acceso sin intervención del administrador.

**Criterios de aceptación:**
1. Respuesta idéntica exista o no el correo (anti-enumeración).
2. Token de 256 bits; solo se guarda su hash SHA-256; expira en 15 min y es de un solo uso.
3. Máximo 5 solicitudes por IP cada 15 minutos.
4. La nueva contraseña exige mínimo 8 caracteres y confirmación.

**Tasks:**
- [x] Implementar flujo de token hasheado de un solo uso con rate-limit.
- [ ] Plantilla HTML corporativa para el correo de recuperación.
- [ ] Política de complejidad de contraseña configurable.

### HU-104 · Autorización por roles ✅
**Como** propietario del sistema,
**quiero** que cada pantalla y acción valide el rol del usuario en sesión,
**para** que nadie acceda a funciones que no le corresponden.

**Criterios de aceptación:**
1. Roles: SuperAdministrador, Administrador, Vendedor.
2. Sin sesión, cualquier URL protegida redirige al login.
3. Un rol que entra a una URL ajena es redirigido a su propio panel.
4. El SuperAdministrador accede a todo lo del Administrador.
5. Todos los POST llevan token antiforgery (CSRF).

**Tasks:**
- [x] Implementar filtro de autorización por rol basado en sesión.
- [ ] Documentar la matriz de permisos por controlador/acción (anexo técnico).

### HU-105 · Gestión de sesión y sincronización entre pestañas ✅
**Como** responsable de seguridad,
**quiero** sesiones con expiración corta, cookies endurecidas y comportamiento consistente entre pestañas,
**para** reducir la ventana de secuestro de sesión.

**Criterios de aceptación:**
1. La sesión expira a los 20 minutos de inactividad (deslizante).
2. Cookies de sesión y auth: HttpOnly, Secure, SameSite=Strict.
3. Un banner avisa 2 minutos antes de expirar y permite renovar.
4. Abrir una pestaña nueva conserva la misma sesión; cerrar sesión en una pestaña cierra las demás.
5. Cerrar sesión limpia la sesión y borra cookies; el evento queda auditado.

**Tasks:**
- [x] Endurecer cookies (Secure) y reducir la sesión a 20 min.
- [x] Sincronizar el cierre de sesión entre pestañas del navegador.
- [ ] Sincronizar también la expiración por inactividad entre pestañas.
- [ ] Configurar DataProtection persistente (Azure Blob + Key Vault).

---

## FEATURE 2 — Gestión de usuarios

**Descripción:** Administración del equipo de trabajo de la plataforma. Permite a los administradores listar, crear, editar y restablecer contraseñas de usuarios, con reglas que impiden que un Administrador cree o modifique a otros administradores, y asignar a cada vendedor el proyecto en el que operará. Es el módulo que define quién existe en el sistema y con qué alcance.

### HU-201 · Listado de usuarios ✅
**Como** administrador,
**quiero** ver todos los usuarios registrados con su rol y datos de contacto,
**para** administrar el equipo comercial.

**Criterios de aceptación:**
1. La pantalla ocupa el área completa y muestra todos los roles.
2. Hay un botón "Nuevo usuario" que abre el formulario en un modal.

**Tasks:**
- [x] Rediseñar la vista de usuarios (tabla completa + modal de registro).
- [ ] Búsqueda y filtros por rol/nombre.
- [ ] Paginación cuando crezca el número de usuarios.

### HU-202 · Crear y editar usuarios ✅
**Como** administrador,
**quiero** crear y editar usuarios (nombre, apellido, documento, celular, correo, usuario, contraseña, rol),
**para** dar de alta asesores y otros usuarios autorizados.

**Criterios de aceptación:**
1. La contraseña se guarda con BCrypt (12).
2. Un Administrador no puede crear/promover a rol Administrador ni degradar a otro Administrador (solo el SuperAdministrador).
3. El administrador puede restablecer la contraseña de un usuario.

**Tasks:**
- [x] Implementar CRUD de usuarios con reglas de rol y reset de contraseña.
- [ ] Validación de unicidad de usuario/correo con mensaje claro.
- [ ] Desactivación (soft-delete) en lugar de borrado físico.

### HU-203 · Asignación de proyecto a vendedores ✅
**Como** administrador,
**quiero** asignar el proyecto en el que trabajará cada vendedor,
**para** que solo vea y venda inventario de ese proyecto.

**Criterios de aceptación:**
1. La asignación se hace desde administración (el código de acceso fue eliminado).
2. Un vendedor sin proyecto ve "Por favor, dile al Administrador que te asigne un proyecto" y tiene bloqueada la navegación hasta tenerlo.
3. Al asignarle proyecto recupera la navegación sin volver a iniciar sesión.

**Tasks:**
- [x] Quitar el flujo de código y bloquear la navegación del vendedor sin proyecto.
- [ ] Historial de asignaciones (qué admin asignó qué proyecto y cuándo).

---

## FEATURE 3 — Carga de proyectos e inventario (Excel)

**Descripción:** Punto de entrada del inventario al sistema. Permite a los administradores crear un proyecto e importar masivamente sus inmuebles desde un archivo Excel (con validación de tipo y tamaño), soportando distintos tipos de producto (apartamentos, lotes, suites, salud, oficinas) y hasta cinco listas de precio por unidad. También administra los proyectos ya cargados y su retiro sin afectar el histórico de ventas.

### HU-301 · Carga masiva desde Excel ✅
**Como** administrador,
**quiero** cargar un proyecto y su inventario desde un archivo Excel,
**para** montar un lanzamiento en minutos sin digitación manual.

**Criterios de aceptación:**
1. Solo se aceptan archivos .xlsx de máximo 10 MB.
2. Tipos: Apartamentos, Lotes, Suites, Salud, Oficinas, cada uno con su columna de unidad.
3. Se leen torre, piso, tipo, metros y hasta 5 listas de precio.
4. Si el archivo no tiene datos, se rechaza y no se crea nada.

**Tasks:**
- [x] Implementar la importación desde Excel con validación de tipo/tamaño.
- [ ] Validación estricta: columna PROYECTO y METROS obligatorias, al menos 1 fila válida (unidad+metros+precio>0), carga en transacción.
- [ ] Vista previa del Excel antes de confirmar.
- [ ] Reporte de filas rechazadas y su motivo.

### HU-302 · Gestión de proyectos cargados ✅
**Como** administrador,
**quiero** ver los proyectos cargados con sus totales por estado y poder retirarlos,
**para** mantener el catálogo de lanzamientos al día.

**Criterios de aceptación:**
1. Cada proyecto muestra totales (inmuebles, disponibles, vendidos, reservados, en proceso).
2. Retirar un proyecto lo desactiva y desvincula clientes/vendedores, sin borrar el histórico de ventas.

**Tasks:**
- [x] Implementar listado de proyectos con totales y retiro lógico.
- [ ] Confirmación en dos pasos para retirar un proyecto con ventas activas.

---

## FEATURE 4 — Inventario y estados de inmuebles

**Descripción:** Corazón del control de inventario en tiempo real. Muestra la grilla de inmuebles del proyecto agrupada por área y tipología, y garantiza que los cambios de estado (disponible → en proceso → reservado → vendido) sean atómicos para evitar que dos personas tomen la misma unidad. Incluye el motor de listas de precio con escalamiento automático (global y por área) difundido por SignalR.

### HU-401 · Grilla de inmuebles por proyecto ✅
**Como** administrador,
**quiero** ver la grilla de inmuebles del proyecto agrupada por área y tipología,
**para** conocer el estado del inventario de un vistazo.

**Criterios de aceptación:**
1. Estados con color: DISPONIBLE, EN PROCESO, RESERVADO, VENDIDO.
2. Puedo filtrar por área (m²) y cambiar de proyecto activo.
3. La lista activa (global y por área) se muestra con su precio.

**Tasks:**
- [x] Implementar la grilla con agrupación, filtros y actualización en vivo.
- [ ] Exportar la grilla actual a imagen/Excel.

### HU-402 · Cambios de estado sin condiciones de carrera ✅
**Como** propietario del negocio,
**quiero** que tomar/reservar/vender un inmueble sea una operación atómica,
**para** que dos personas no puedan quedarse con la misma unidad.

**Criterios de aceptación:**
1. Cada cambio usa `UPDATE … WHERE Estado = <estado previo>` y verifica filas afectadas.
2. Si otro ganó la carrera, veo "Este inmueble ya no está disponible" y no se hace ningún cambio.
3. Tras cada cambio se emite el evento SignalR a todos los clientes.

**Tasks:**
- [x] Implementar cambios de estado atómicos + broadcast SignalR (incluido el cierre de venta transaccional).
- [ ] Prueba de concurrencia automatizada (dos ventas simultáneas del mismo inmueble).

### HU-403 · Listas de precio con escalamiento automático ✅
**Como** administrador,
**quiero** que la lista de precios suba automáticamente cada N unidades vendidas,
**para** ejecutar la estrategia de precios del lanzamiento sin intervención manual.

**Criterios de aceptación:**
1. Configurable "apartamentos por lista" a nivel proyecto y por área.
2. Al cumplirse el umbral, la lista sube (máx. Lista 5) solo si la lista destino tiene precios.
3. El cambio se difunde por SignalR y las vistas se actualizan sin recargar.
4. También puedo cambiar lista/precio de un área manualmente.

**Tasks:**
- [x] Implementar escalamiento automático global y por área con broadcast.
- [ ] Historial de cambios de lista (cuándo y qué lo disparó).

---

## FEATURE 5 — Flujo de venta del vendedor (tiempo real)

**Descripción:** Experiencia principal del asesor durante el lanzamiento. Le muestra sus indicadores, el inventario disponible de su proyecto y le permite tomar una unidad, reservarla congelando el precio de la lista vigente, y consultar sus ventas y reservas — todo actualizándose en vivo a medida que otros asesores actúan. Está diseñado para operar bajo alta concurrencia el día del evento.

### HU-501 · Panel de inicio del vendedor ✅
**Como** vendedor,
**quiero** ver al entrar mis indicadores (inventario, disponibles, mis ventas, mis clientes),
**para** ubicarme rápido durante el lanzamiento.

**Criterios de aceptación:**
1. Los KPIs corresponden a mi proyecto asignado y a mi gestión.
2. La vista es responsiva y no se descuadra según resolución/zoom.

**Tasks:**
- [x] Implementar el panel de KPIs del vendedor (responsivo y con íconos).
- [ ] KPI adicional: mis reservas activas.

### HU-502 · Ver inmuebles disponibles y tomar unidad ✅
**Como** vendedor,
**quiero** ver los inmuebles del proyecto y tomar una unidad disponible,
**para** iniciar una venta con un cliente en sala.

**Criterios de aceptación:**
1. Solo veo mi proyecto; las reservas de otros aparecen bloqueadas con su nombre.
2. "Tomar" pasa la unidad a EN PROCESO a mi nombre de forma atómica.
3. Puedo cancelar y la unidad vuelve a DISPONIBLE.
4. Los cambios de otros se reflejan en vivo (SignalR), incluidos los KPIs.

**Tasks:**
- [x] Implementar la grilla del vendedor con "tomar" atómico y tiempo real.
- [ ] Tiempo máximo de "EN PROCESO" con liberación automática (configurable).

### HU-503 · Reservar con precio bloqueado ✅
**Como** vendedor,
**quiero** reservar un inmueble congelando el precio de la lista vigente,
**para** garantizarle al cliente el valor pactado aunque la lista suba.

**Criterios de aceptación:**
1. La reserva guarda el precio de la lista activa del área en ese momento.
2. Al confirmar la venta desde la reserva se usa siempre el precio bloqueado.
3. Solo el dueño de la reserva (o un admin) puede liberarla/venderla.

**Tasks:**
- [x] Implementar reserva con precio bloqueado y verificación de propietario.
- [ ] Vencimiento automático de reservas (configurable).

### HU-504 · Mis ventas y mis reservas ✅
**Como** vendedor,
**quiero** consultar mis ventas registradas y mis reservas activas,
**para** hacer seguimiento a mi gestión.

**Criterios de aceptación:**
1. Veo el historial de mis ventas con precio, destino y fecha.
2. Veo mis reservas vigentes y puedo continuar la venta o liberarlas.

**Tasks:**
- [x] Implementar las vistas "Mis ventas" y "Mis reservas".
- [ ] Exportar "mis ventas" a Excel desde el panel del vendedor.

---

## FEATURE 6 — Registro de ventas y cumplimiento

**Descripción:** Cierre formal de la operación comercial con controles de negocio y normativos. Antes de registrar el cliente exige la verificación SAGRILAFT (prevención de lavado de activos), permite seleccionar cliente existente o crear uno nuevo con documento validado, y asegura la integridad financiera derivando el precio y la lista en el servidor dentro de una transacción atómica. Aplica tanto al vendedor como al administrador.

### HU-601 · Verificación SAGRILAFT previa al registro de cliente ✅
**Como** oficial de cumplimiento,
**quiero** que antes de habilitar el panel de cliente se confirme si el cliente ya fue consultado en SAGRILAFT,
**para** cumplir la normativa de prevención de lavado de activos.

**Criterios de aceptación:**
1. En los 4 flujos de venta aparece "¿Ya fue consultado el cliente en SAGRILAFT?".
2. "Sí" habilita el panel y el botón; "No" lo mantiene bloqueado con el mensaje "Por favor, consulte el cliente y recargue la página para realizar el nuevo registro."
3. El servidor rechaza la venta si la confirmación no llega.

**Tasks:**
- [x] Implementar el gate SAGRILAFT (cliente y servidor) en los 4 flujos.
- [ ] Guardar la confirmación SAGRILAFT en la venta (quién y cuándo).
- [ ] Integración con el proveedor de listas restrictivas (consulta en línea).

### HU-602 · Registrar venta con cliente nuevo o existente ✅
**Como** vendedor o administrador,
**quiero** registrar la venta seleccionando un cliente existente o creando uno nuevo,
**para** cerrar la operación con los datos completos del comprador.

**Criterios de aceptación:**
1. Sin cliente válido la venta no continúa; el mensaje aparece en la misma pantalla.
2. El documento (CC/NIT) solo acepta números (validado en pantalla y en servidor).
3. El destino se elige de una lista blanca validada en servidor.
4. El precio y la lista los calcula el servidor; los valores del formulario se ignoran.
5. El registro y el cambio a VENDIDO ocurren en una transacción atómica que verifica estado y propietario.

**Tasks:**
- [x] Implementar el cierre de venta con precio en servidor, destino validado y transacción.
- [ ] Detección de cliente duplicado por documento al crear uno nuevo.
- [ ] Anulación de ventas con motivo y auditoría.

---

## FEATURE 7 — Gestión de clientes

**Descripción:** Administración de la información de los compradores. Permite listar clientes con paginación y búsqueda, y ver el detalle de cada uno con su historial de compras y edición de datos. Incluye consideraciones de privacidad y protección de datos personales (Ley 1581) sobre qué clientes puede ver cada rol.

### HU-701 · Listado y detalle de clientes ✅
**Como** administrador,
**quiero** listar los clientes con paginación y ver su detalle con historial de compras,
**para** conocer y atender a los compradores del proyecto.

**Criterios de aceptación:**
1. Listado paginado (25 por página) con búsqueda.
2. El detalle permite editar la información y muestra el historial de compras.

**Tasks:**
- [x] Implementar el listado paginado y el detalle de cliente con historial.
- [ ] Limitar los clientes que ve un vendedor a su proyecto/propios (Ley 1581).
- [ ] Exportar clientes a Excel.

---

## FEATURE 8 — Dashboard y monitoreo en tiempo real

**Descripción:** Centro de mando del administrador durante el lanzamiento. Reúne los KPIs del proyecto y el mapa de inmuebles que se actualiza en vivo a medida que el equipo vende, reserva o toma unidades, permitiendo dirigir el evento minuto a minuto y cambiar el proyecto activo desde la barra superior.

### HU-801 · Dashboard con KPIs y mapa en vivo ✅
**Como** administrador,
**quiero** un panel con KPIs y el mapa de inmuebles actualizándose en tiempo real,
**para** dirigir el lanzamiento minuto a minuto.

**Criterios de aceptación:**
1. KPIs: total, disponibles, vendidos, reservados, en proceso, valor vendido, ventas de hoy.
2. Cuando un vendedor actúa, el dashboard se actualiza sin recargar (SignalR).
3. Puedo cambiar el proyecto activo desde la barra superior.

**Tasks:**
- [x] Implementar el dashboard con KPIs y mapa en tiempo real.
- [ ] Feed de actividad en vivo (última venta, quién, hace cuánto).

---

## FEATURE 9 — Reportes y exportaciones

**Descripción:** Generación de la información oficial del lanzamiento para la operación y la gerencia. Incluye el informe del día en pantalla, exportaciones a Excel, el reporte por asesor y el PDF técnico ejecutivo de una sola hoja (rediseñado según el handoff de diseño) que consolida estado del proyecto, ventas y asistencia.

### HU-901 · Informe del día en pantalla ✅
**Como** administrador,
**quiero** un informe del día con KPIs, progreso, destino de ventas, tipologías, mapa y detalle,
**para** revisar el desempeño del lanzamiento en cualquier momento.

**Criterios de aceptación:**
1. Muestra KPIs, progreso por estado y detalle de ventas del proyecto activo.
2. Los valores provienen de datos vivos.

**Tasks:**
- [x] Implementar el informe del día en pantalla.
- [ ] Filtro por rango de fechas (no solo "hoy").

### HU-902 · Exportación a Excel ✅
**Como** administrador,
**quiero** exportar el informe y el detalle de ventas a Excel,
**para** compartirlo con gerencia y llevar archivo.

**Criterios de aceptación:**
1. Se exporta el detalle de ventas y los indicadores a un archivo .xlsx.

**Tasks:**
- [x] Implementar la exportación a Excel (EPPlus).
- [ ] Plantilla Excel con logo y formato corporativo unificado.

### HU-903 · PDF técnico rediseñado 🔨
**Como** gerencia,
**quiero** un PDF técnico de una sola hoja con módulos (KPIs con íconos, donut de avance, tipologías, detalle de ventas, asistencia y módulos colapsados),
**para** recibir un informe ejecutivo claro y estandarizado.

**Criterios de aceptación:**
1. Página carta con header, franja KPI de 6 columnas, módulo de estado (donut + leyenda + barras), tabla de ventas con subtotales y destinos, módulo de asistencia y módulos colapsados, y footer con paginación.
2. Valores en vivo; formato numérico es-CO.
3. Los módulos sin datos se colapsan a una línea.

**Tasks:**
- [x] Rediseñar la composición del PDF técnico con QuestPDF según el handoff.
- [ ] Validar la generación tras el fix de layout y ajustar detalles visuales con datos reales.
- [ ] Quitar `QuestPDF.Settings.EnableDebugging` al estabilizar.
- [ ] Embeber la tipografía Barlow (hoy usa Arial como fallback).

### HU-904 · Reporte de asesores ✅
**Como** administrador,
**quiero** un reporte por asesor (ventas, unidades y valores),
**para** medir el desempeño individual del equipo.

**Criterios de aceptación:**
1. Muestra por asesor sus ventas, unidades y valor total.

**Tasks:**
- [x] Implementar el reporte de asesores.
- [ ] Ranking histórico multi-proyecto.

---

## FEATURE 10 — Cuadro de asistencia del lanzamiento

**Descripción:** Registro y medición de la convocatoria del evento. Permite capturar por día del lanzamiento los datos de asistencia (familias, adultos, niños, mascotas, asistentes con cita, vehículos, agendados) con cálculos de porcentajes en vivo, y exportarlos a Excel e integrarlos al PDF técnico como parte del informe oficial.

### HU-1001 · Captura del cuadro de asistencia ✅
**Como** administrador,
**quiero** registrar por día: familias, adultos, niños, mascotas, asistentes con cita, carros, motos, caminando y agendados,
**para** medir la convocatoria del evento.

**Criterios de aceptación:**
1. Puedo agregar/quitar días, escribir observaciones y guardar el cuadro.
2. La vista previa calcula porcentajes en vivo.

**Tasks:**
- [x] Implementar la captura del cuadro de asistencia con cálculo en vivo.
- [ ] Captura optimizada para móvil (registro en puerta).

### HU-1002 · Exportación del cuadro (Excel y PDF) ✅
**Como** administrador,
**quiero** exportar el cuadro de asistencia a Excel y verlo integrado al PDF técnico,
**para** anexarlo al informe oficial del lanzamiento.

**Criterios de aceptación:**
1. Se exporta el cuadro a Excel con totales y porcentajes.
2. El resumen de asistencia aparece en el PDF técnico.

**Tasks:**
- [x] Implementar la exportación del cuadro y su módulo en el PDF.
- [ ] Consolidado de asistencia multi-lanzamiento (comparativo).

---

## FEATURE 11 — Seguridad técnica y cumplimiento (transversal)

**Descripción:** Conjunto de controles técnicos que protegen la aplicación y los datos de forma transversal: cabeceras de seguridad y HTTPS, manejo de secretos fuera del repositorio, consultas parametrizadas, saneamiento de entradas y auditoría continua de calidad. Sostiene el cumplimiento normativo y la postura de seguridad de cara a producción.

### HU-1101 · Endurecimiento de aplicación ✅
**Como** responsable de seguridad,
**quiero** cabeceras y configuración endurecidas (CSP, X-Frame-Options DENY, nosniff, Referrer-Policy, Permissions-Policy, HSTS, HTTPS-only, TLS ≥ 1.2),
**para** reducir la superficie de ataque en producción.

**Criterios de aceptación:**
1. Las respuestas incluyen las cabeceras de seguridad y se fuerza HTTPS.
2. TLS mínimo 1.2 en las conexiones.

**Tasks:**
- [x] Configurar cabeceras de seguridad, HSTS y HTTPS-only.
- [ ] Eliminar `'unsafe-inline'` de `script-src` migrando el JS a archivos con nonce.
- [ ] Fijar `AllowedHosts` al dominio real de producción.

### HU-1102 · Protección de datos y secretos 🔨
**Como** responsable de seguridad,
**quiero** que ningún secreto viva en el repositorio y que los datos sensibles estén protegidos,
**para** cumplir buenas prácticas y la normativa de datos personales.

**Criterios de aceptación:**
1. La cadena de conexión se define por variable de entorno/Key Vault, no en `appsettings.json`.
2. `appsettings.Development/Production.json` están en `.gitignore`.
3. SQL 100% parametrizado; documento del cliente saneado a solo dígitos.

**Tasks:**
- [x] Sacar el secreto de `appsettings.json`, sanear documento y parametrizar SQL.
- [ ] Rotar la credencial SQL expuesta en el historial de git y purgar historial.
- [ ] DataProtection persistente con Azure Blob + Key Vault.
- [ ] Auditoría `dotnet list package --vulnerable` en CI.

### HU-1103 · Auditoría de QA continua ⏳
**Como** líder técnico,
**quiero** un proyecto de pruebas automatizadas (xUnit) ejecutado en CI,
**para** que cada cambio se valide antes de llegar a QA/Producción.

**Criterios de aceptación:**
1. Existe un proyecto de pruebas que corre en el pipeline.
2. `dotnet test` es un gate obligatorio antes de desplegar.

**Tasks:**
- [x] Realizar la auditoría inicial de QA/seguridad y documentar hallazgos (INFORME_QA_SEGURIDAD.md).
- [ ] Crear `Plataforma_ventas.Tests` con los casos ya diseñados.
- [ ] Pruebas de integración del flujo de venta con BD en contenedor.
- [ ] Incorporar `dotnet test` como gate del pipeline.

---

## FEATURE 12 — Infraestructura, despliegue y DevOps

**Descripción:** Todo lo relacionado con llevar y mantener la aplicación en la nube. Define los dos entornos idénticos y aislados (QA y Producción) en Azure, el pipeline de CI/CD por ramas con aprobación manual a Producción desde Azure DevOps, y el monitoreo y respaldo necesarios para operar con confianza durante los lanzamientos.

### HU-1201 · Entornos QA y Producción idénticos ⏳
**Como** equipo de desarrollo,
**quiero** dos entornos aislados con la misma configuración,
**para** probar cada versión en QA antes de liberarla.

**Criterios de aceptación:**
1. Recursos separados por entorno (App Service + BD propios).
2. La configuración sensible vive en el entorno (variables/Key Vault).
3. QA nunca comparte base de datos con Producción.

**Tasks:**
- [x] Definir la arquitectura objetivo (App Service B2 + Azure SQL Serverless) y su costeo.
- [ ] Crear los recursos de QA y Producción en Azure.
- [ ] Dominio y subdominio con certificado administrado.

### HU-1202 · CI/CD por ramas con aprobación a Producción 🔨
**Como** equipo de desarrollo,
**quiero** que la rama `qa` despliegue a QA y `master` a Producción con aprobación manual,
**para** liberar con control y trazabilidad desde Azure DevOps.

**Criterios de aceptación:**
1. Pipeline YAML único que compila una vez y despliega según la rama.
2. El stage de Producción exige aprobación en el Environment "Production".
3. Los pull requests no despliegan.

**Tasks:**
- [x] Crear el `azure-pipelines.yml` con despliegue por rama y aprobación.
- [ ] Conectar el repo a Azure Pipelines y crear los Environments con aprobadores.
- [ ] Crear Service Connections por entorno con permisos mínimos.
- [ ] Reemplazar los placeholders del YAML con los nombres reales de los App Service.

### HU-1203 · Monitoreo y respaldo en producción ⏳
**Como** responsable de operación,
**quiero** monitoreo de errores/desempeño y respaldos verificados,
**para** detectar incidentes durante los lanzamientos y poder recuperarnos.

**Criterios de aceptación:**
1. Hay alertas ante errores 5xx, latencia alta o caídas.
2. El procedimiento de restauración está documentado y probado.

**Tasks:**
- [ ] Configurar Application Insights + alertas.
- [ ] Verificar la retención PITR de Azure SQL y documentar la restauración.
- [ ] Prueba de restauración de respaldo previa al primer lanzamiento.

---

## Resumen para carga en Azure DevOps

| Feature | Historias | Estado |
|---|---|---|
| 1. Autenticación y control de acceso | HU-101…HU-105 | ✅ |
| 2. Gestión de usuarios | HU-201…HU-203 | ✅ |
| 3. Carga de proyectos (Excel) | HU-301…HU-302 | ✅ (validación estricta pendiente) |
| 4. Inventario y estados de inmuebles | HU-401…HU-403 | ✅ |
| 5. Flujo de venta del vendedor | HU-501…HU-504 | ✅ |
| 6. Registro de ventas y cumplimiento | HU-601…HU-602 | ✅ |
| 7. Gestión de clientes | HU-701 | ✅ (privacidad pendiente) |
| 8. Dashboard en tiempo real | HU-801 | ✅ |
| 9. Reportes y exportaciones | HU-901…HU-904 | 🔨 (PDF técnico en ajuste) |
| 10. Cuadro de asistencia | HU-1001…HU-1002 | ✅ |
| 11. Seguridad técnica | HU-1101…HU-1103 | 🔨 |
| 12. Infraestructura y DevOps | HU-1201…HU-1203 | ⏳ |

**Total: 12 Features (todas con descripción) · 33 Historias de Usuario (todas con al menos una Task) · 90+ Tasks.**

> **Carga sugerida en Azure DevOps:** crear las 12 Features bajo la Épica (usando la *Descripción* de cada una); dentro de cada Feature, las User Stories con su narrativa "Como… quiero… para…" en *Description* y los criterios en *Acceptance Criteria*; y bajo cada Story sus Tasks. Las tasks `[x]` pueden crearse en estado *Done* para reflejar el trabajo ya realizado; las `[ ]` quedan en *To Do* para planificar.
