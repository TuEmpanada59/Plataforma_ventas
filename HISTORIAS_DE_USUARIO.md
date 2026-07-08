# Historias de Usuario — Plataforma de Ventas (Londoño Gómez)

**Épica:** Plataforma de Lanzamientos Inmobiliarios (nombre del proyecto en Azure DevOps)
**Versión del documento:** 1.0 · **Fecha:** 08/07/2026
**Convención de estados:** ✅ Implementada · 🔨 Parcial · ⏳ Pendiente

Estructura pensada para Azure DevOps: **Epic → Feature → User Story → Task**.
Las *Tasks* listadas en cada historia son el trabajo pendiente o de mejora que puede planificarse en sprints; las historias marcadas ✅ ya están desarrolladas y sus tasks son evolutivos.

---

## FEATURE 1 — Autenticación y control de acceso

### HU-101 · Inicio de sesión ✅
**Como** usuario registrado (administrador o vendedor),
**quiero** iniciar sesión con mi usuario y contraseña,
**para** acceder a las funciones que corresponden a mi rol.

**Criterios de aceptación:**
1. Dado un usuario y contraseña válidos, cuando inicio sesión, entonces soy redirigido al panel de mi rol (Dashboard si soy Administrador/SuperAdministrador; Inicio de vendedor si soy Vendedor).
2. Dado un usuario o contraseña incorrectos, cuando intento entrar, entonces veo el mensaje "Usuario o contraseña incorrectos" con los intentos restantes, sin revelar cuál campo falló.
3. La contraseña se verifica contra un hash BCrypt (factor 12); nunca se almacena ni se compara en texto plano.
4. Al autenticarme se descarta cualquier sesión previa (protección anti session-fixation).
5. Todos los intentos (exitosos y fallidos) quedan registrados en el log con usuario e IP.

**Tasks:**
- [ ] Agregar CAPTCHA o rate-limit por IP tras N intentos fallidos (mitigar DoS de cuenta).
- [ ] Pruebas automatizadas del flujo de login (unitarias + integración).

### HU-102 · Bloqueo por intentos fallidos (lockout) ✅
**Como** responsable de seguridad,
**quiero** que una cuenta se bloquee temporalmente tras varios intentos fallidos,
**para** impedir ataques de fuerza bruta sobre las contraseñas.

**Criterios de aceptación:**
1. Tras 5 intentos fallidos consecutivos, la cuenta queda bloqueada por 15 minutos.
2. Durante el bloqueo, incluso la contraseña correcta es rechazada con el mensaje "Cuenta bloqueada temporalmente…".
3. El bloqueo y el desbloqueo quedan auditados en el log con IP.
4. Restablecer la contraseña por correo elimina el bloqueo vigente.

**Tasks:**
- [ ] Panel para que el SuperAdministrador vea/libere bloqueos activos.

### HU-103 · Recuperación de contraseña por correo ✅
**Como** usuario que olvidó su contraseña,
**quiero** solicitar un enlace de restablecimiento a mi correo,
**para** recuperar el acceso sin intervención del administrador.

**Criterios de aceptación:**
1. La respuesta en pantalla es idéntica exista o no el correo (anti-enumeración).
2. El token es aleatorio de 256 bits; solo se almacena su hash SHA-256; expira en 15 minutos y es de un solo uso.
3. Máximo 5 solicitudes por IP cada 15 minutos (rate-limit).
4. La nueva contraseña exige mínimo 8 caracteres y confirmación.
5. Sin SMTP configurado, el enlace se registra en el log del servidor (solo para desarrollo).

**Tasks:**
- [ ] Plantilla HTML corporativa para el correo de recuperación.
- [ ] Política de complejidad de contraseña (mayúscula/minúscula/número/símbolo) configurable.

### HU-104 · Autorización por roles ✅
**Como** propietario del sistema,
**quiero** que cada pantalla y acción valide el rol del usuario en sesión,
**para** que nadie acceda a funciones que no le corresponden.

**Criterios de aceptación:**
1. Roles soportados: `SuperAdministrador`, `Administrador`, `Vendedor`.
2. Sin sesión activa, cualquier URL protegida redirige al login.
3. Un Vendedor que intente una URL de administrador es redirigido a su propio panel (y viceversa).
4. El SuperAdministrador accede a todo lo del Administrador.
5. Todos los POST llevan token antiforgery (CSRF).

**Tasks:**
- [ ] Matriz de permisos documentada por controlador/acción (anexo técnico).

### HU-105 · Gestión de sesión y expiración ✅
**Como** responsable de seguridad,
**quiero** sesiones con expiración corta y cookies endurecidas,
**para** reducir la ventana de secuestro de sesión.

**Criterios de aceptación:**
1. La sesión expira a los 20 minutos de inactividad (deslizante).
2. Cookies de sesión y autenticación: `HttpOnly`, `Secure`, `SameSite=Strict`.
3. Un banner avisa 2 minutos antes de expirar y permite renovar.
4. Cerrar sesión limpia la sesión y borra las cookies; el evento queda auditado.
5. Abrir una pestaña nueva conserva la misma sesión; cerrar sesión en una pestaña cierra las demás pestañas del navegador.

**Tasks:**
- [ ] Sincronizar también la expiración por inactividad entre pestañas.
- [ ] Configurar DataProtection persistente (Azure Blob + Key Vault) para que la sesión sobreviva reinicios/escalado.

---

## FEATURE 2 — Gestión de usuarios (Administración)

### HU-201 · Listado de usuarios ✅
**Como** administrador,
**quiero** ver todos los usuarios registrados con su rol y datos de contacto,
**para** administrar el equipo comercial.

**Criterios de aceptación:**
1. La pantalla ocupa el área completa y muestra todos los roles.
2. Existe un botón "Nuevo usuario" que abre el formulario de registro en un modal.

**Tasks:**
- [ ] Búsqueda y filtros por rol/nombre.
- [ ] Paginación cuando el número de usuarios crezca.

### HU-202 · Crear y editar usuarios ✅
**Como** administrador,
**quiero** crear y editar usuarios (nombre, apellido, documento, celular, correo, usuario, contraseña, rol),
**para** dar de alta asesores y otros administradores autorizados.

**Criterios de aceptación:**
1. La contraseña se guarda con BCrypt (12).
2. Un Administrador **no** puede crear ni promover usuarios al rol Administrador/SuperAdministrador (solo el SuperAdministrador puede); tampoco puede degradar a otro Administrador.
3. El administrador puede restablecer la contraseña de un usuario.

**Tasks:**
- [ ] Validación de unicidad de usuario/correo con mensaje claro.
- [ ] Desactivación (soft-delete) de usuarios en lugar de borrado físico.

### HU-203 · Asignación de proyecto a vendedores ✅
**Como** administrador,
**quiero** asignar el proyecto en el que trabajará cada vendedor,
**para** que solo vea y venda inventario de ese proyecto.

**Criterios de aceptación:**
1. La asignación se hace desde administración (el código de acceso fue eliminado).
2. Un vendedor sin proyecto ve el mensaje "Por favor, dile al Administrador que te asigne un proyecto" y tiene la navegación bloqueada en toda la app hasta tener proyecto.
3. Al asignarle proyecto, el vendedor recupera la navegación completa sin volver a iniciar sesión.

**Tasks:**
- [ ] Historial de asignaciones (auditoría de qué admin asignó qué proyecto y cuándo).

---

## FEATURE 3 — Carga de proyectos e inventario (Excel)

### HU-301 · Carga masiva desde Excel ✅
**Como** administrador,
**quiero** cargar un proyecto y su inventario de inmuebles desde un archivo Excel,
**para** montar un lanzamiento en minutos sin digitación manual.

**Criterios de aceptación:**
1. Solo se aceptan archivos `.xlsx` de máximo 10 MB.
2. Tipos de proyecto soportados: Apartamentos, Lotes, Suites, Salud (consultorios), Oficinas — cada uno con su columna de unidad (APTO/SUITE/CONSULTORIO/OFICINA).
3. Se leen columnas de torre, piso, tipo, metros y hasta 5 listas de precio (Lista1…Lista5).
4. Si el archivo no tiene datos, se rechaza con mensaje claro y no se crea nada.

**Tasks:**
- [ ] **Validación estricta** (plan aprobado): columna PROYECTO obligatoria y coincidente con el nombre; columna METROS obligatoria; al menos 1 fila válida (unidad + metros + precio > 0); envolver la carga en transacción (evita "proyectos fantasma").
- [ ] Vista previa del Excel antes de confirmar la carga.
- [ ] Reporte de filas rechazadas y motivo.

### HU-302 · Gestión de proyectos cargados ✅
**Como** administrador,
**quiero** ver los proyectos cargados con sus totales por estado y poder retirarlos,
**para** mantener el catálogo de lanzamientos al día.

**Criterios de aceptación:**
1. Cada proyecto muestra totales: inmuebles, disponibles, vendidos, reservados, en proceso.
2. Retirar un proyecto lo desactiva y desvincula a sus clientes/vendedores, sin borrar el histórico de ventas.

**Tasks:**
- [ ] Confirmación en dos pasos para retirar un proyecto con ventas activas.

---

## FEATURE 4 — Inventario y estados de inmuebles (Administración)

### HU-401 · Grilla de inmuebles por proyecto ✅
**Como** administrador,
**quiero** ver el mapa/grilla de inmuebles del proyecto activo agrupado por área y tipología,
**para** conocer el estado del inventario de un vistazo.

**Criterios de aceptación:**
1. Estados visibles: DISPONIBLE, EN PROCESO, RESERVADO, VENDIDO, con código de color.
2. Puedo filtrar por área (m²) y cambiar de proyecto activo desde la barra superior.
3. La lista activa (global y por área) se muestra con su precio correspondiente.

**Tasks:**
- [ ] Exportar la grilla actual a imagen/Excel.

### HU-402 · Cambios de estado sin condiciones de carrera ✅
**Como** propietario del negocio,
**quiero** que tomar/reservar/vender un inmueble sea una operación atómica,
**para** que dos personas no puedan quedarse con la misma unidad.

**Criterios de aceptación:**
1. Todo cambio de estado usa `UPDATE … WHERE Estado = <estado previo>` y verifica filas afectadas.
2. Si otro usuario ganó la carrera, veo "Este inmueble ya no está disponible" y no se hace ningún cambio.
3. Tras cada cambio de estado se emite el evento SignalR para todos los clientes conectados.

**Tasks:**
- [ ] Prueba de concurrencia automatizada (dos ventas simultáneas del mismo inmueble).

### HU-403 · Listas de precio con escalamiento automático ✅
**Como** administrador,
**quiero** que la lista de precios suba automáticamente cada N unidades vendidas (global o por área),
**para** ejecutar la estrategia de precios del lanzamiento sin intervención manual.

**Criterios de aceptación:**
1. Puedo configurar "apartamentos por lista" a nivel proyecto y por área (m²).
2. Al cumplirse el umbral, la lista sube (máximo Lista 5) solo si la lista destino tiene precios cargados.
3. El cambio de lista se difunde por SignalR y las vistas de vendedor/admin se actualizan sin recargar.
4. También puedo cambiar la lista o el precio de un área manualmente.

**Tasks:**
- [ ] Historial de cambios de lista (cuándo, quién/qué lo disparó).

---

## FEATURE 5 — Flujo de venta del vendedor (tiempo real)

### HU-501 · Panel de inicio del vendedor ✅
**Como** vendedor,
**quiero** ver al entrar mis indicadores (inventario del proyecto, disponibles, mis ventas, mis clientes),
**para** ubicarme rápido durante el lanzamiento.

**Tasks:**
- [ ] KPI adicional: mis reservas activas.

### HU-502 · Ver inmuebles disponibles y tomar unidad ✅
**Como** vendedor,
**quiero** ver los inmuebles del proyecto agrupados por área y tomar una unidad disponible,
**para** iniciar una venta con un cliente en sala.

**Criterios de aceptación:**
1. Solo veo el inventario de mi proyecto asignado; las reservas de otros vendedores aparecen bloqueadas con su nombre.
2. "Tomar" pasa la unidad a EN PROCESO a mi nombre de forma atómica y me lleva al formulario de venta.
3. Puedo cancelar el proceso y la unidad vuelve a DISPONIBLE.
4. Los cambios de otros vendedores se reflejan en mi pantalla en vivo (SignalR), incluidos los KPIs.

**Tasks:**
- [ ] Tiempo máximo de "EN PROCESO" con liberación automática (configurable).

### HU-503 · Reservar con precio bloqueado ✅
**Como** vendedor,
**quiero** reservar un inmueble congelando el precio de la lista vigente,
**para** garantizarle al cliente el valor pactado aunque la lista suba después.

**Criterios de aceptación:**
1. La reserva guarda `PrecioReserva` con el precio de la lista activa del área en ese momento.
2. Al confirmar la venta desde la reserva, se usa **siempre** el precio bloqueado (no editable).
3. Puedo liberar mis reservas; solo el dueño de la reserva (o un admin) puede liberarla/venderla.

**Tasks:**
- [ ] Vencimiento automático de reservas (n horas/días, configurable).

### HU-504 · Mis ventas y mis reservas ✅
**Como** vendedor,
**quiero** consultar mis ventas registradas y mis reservas activas,
**para** hacer seguimiento a mi gestión comercial.

**Tasks:**
- [ ] Exportar "mis ventas" a Excel desde el panel del vendedor.

---

## FEATURE 6 — Registro de ventas y cumplimiento

### HU-601 · Verificación SAGRILAFT previa al registro de cliente ✅
**Como** oficial de cumplimiento,
**quiero** que antes de habilitar el panel de cliente se confirme si el cliente ya fue consultado en SAGRILAFT,
**para** cumplir la normativa de prevención de lavado de activos.

**Criterios de aceptación:**
1. En los 4 flujos de venta (venta directa y desde reserva, en vendedor y en admin) aparece la pregunta "¿Ya fue consultado el cliente en SAGRILAFT?".
2. "Sí" habilita el panel de cliente y el botón de confirmar; "No" mantiene el panel bloqueado y muestra: "Por favor, consulte el cliente y recargue la página para realizar el nuevo registro."
3. El servidor rechaza la venta si la confirmación no llega (la regla no depende solo del navegador).

**Tasks:**
- [ ] Guardar la confirmación SAGRILAFT en la venta (auditoría: quién confirmó y cuándo).
- [ ] Integración futura con el proveedor de listas restrictivas (consulta en línea).

### HU-602 · Registrar venta con cliente nuevo o existente ✅
**Como** vendedor o administrador,
**quiero** registrar la venta seleccionando un cliente existente (buscando por cédula o nombre) o creando uno nuevo,
**para** cerrar la operación con los datos completos del comprador.

**Criterios de aceptación:**
1. Sin cliente válido, la venta no continúa; el mensaje aparece en la misma pantalla sin perder lo diligenciado.
2. El campo documento (CC/NIT) solo acepta números (validado en pantalla y saneado en servidor).
3. El destino de la venta se elige entre: Vivienda, Inversión para reventa, Inversión para arriendo, Cesión de derechos (lista blanca validada en servidor).
4. **El precio y la lista aplicada los calcula el servidor** (lista activa del área o precio de reserva bloqueado); cualquier valor manipulado desde el formulario se ignora.
5. El registro de la venta y el cambio a VENDIDO ocurren en una transacción atómica que verifica estado y propietario.

**Tasks:**
- [ ] Detección de cliente duplicado por documento al crear uno nuevo.
- [ ] Anulación de ventas con motivo y auditoría (flujo de administrador).

---

## FEATURE 7 — Gestión de clientes

### HU-701 · Listado y detalle de clientes ✅
**Como** administrador,
**quiero** listar los clientes con paginación y ver su detalle con historial de compras,
**para** conocer y atender a los compradores del proyecto.

**Criterios de aceptación:**
1. Listado paginado (25 por página) con búsqueda.
2. El detalle permite editar la información del cliente y muestra su historial de compras.

**Tasks:**
- [ ] **Privacidad:** limitar el listado de clientes que ve un vendedor a su proyecto o a sus propios clientes (Ley 1581 / habeas data).
- [ ] Exportar clientes a Excel.

---

## FEATURE 8 — Dashboard y monitoreo en tiempo real (Administración)

### HU-801 · Dashboard con KPIs y mapa en vivo ✅
**Como** administrador,
**quiero** un panel con KPIs del proyecto y el mapa de inmuebles actualizándose en tiempo real,
**para** dirigir el lanzamiento minuto a minuto.

**Criterios de aceptación:**
1. KPIs: total, disponibles, vendidos, reservados, en proceso, valor vendido, ventas de hoy.
2. Cuando un vendedor toma/reserva/vende, el dashboard se actualiza sin recargar (SignalR).
3. Puedo cambiar el proyecto activo desde la barra superior.

**Tasks:**
- [ ] Feed de actividad en vivo (última venta, quién, hace cuánto).

---

## FEATURE 9 — Reportes y exportaciones

### HU-901 · Informe del día en pantalla ✅
**Como** administrador,
**quiero** un informe del día con KPIs, progreso, destino de ventas, tipologías, mapa y detalle de ventas,
**para** revisar el desempeño del lanzamiento en cualquier momento.

**Tasks:**
- [ ] Filtro por rango de fechas (no solo "hoy").

### HU-902 · Exportación a Excel ✅
**Como** administrador,
**quiero** exportar el informe y el detalle de ventas a Excel (EPPlus),
**para** compartirlo con gerencia y llevar archivo.

**Tasks:**
- [ ] Plantilla Excel con logo y formato corporativo unificado.

### HU-903 · PDF técnico rediseñado (handoff PRIMAVELA) 🔨
**Como** gerencia,
**quiero** un PDF técnico de una sola hoja con módulos (KPIs con íconos, donut de avance, tipologías, detalle de ventas, asistencia y módulos colapsados),
**para** recibir un informe ejecutivo claro y estandarizado tras cada jornada.

**Criterios de aceptación:**
1. Página carta con header (título, GEN/DOC), franja KPI de 6 columnas, módulo 01 (donut + leyenda + barras por tipología), módulo 02 (tabla de ventas con subtotales por asesor + línea de destinos), módulo 03 (asistencia en 6 tarjetas), módulos 04–05 colapsados a una línea cuando su total es 0, y footer con paginación.
2. Todos los valores provienen de datos vivos; formato numérico es-CO.
3. Cualquier módulo sin datos se colapsa a una línea con la etiqueta COLAPSADO.

**Tasks:**
- [ ] Validar la generación tras el último fix de layout y ajustar detalles visuales contra el diseño (pendiente de prueba con datos reales).
- [ ] Quitar `QuestPDF.Settings.EnableDebugging` al estabilizar.
- [ ] Tipografía Barlow embebida (hoy usa Arial como fallback).

### HU-904 · Reporte de asesores ✅
**Como** administrador,
**quiero** un reporte por asesor (ventas, unidades y valores),
**para** medir el desempeño individual del equipo.

**Tasks:**
- [ ] Ranking histórico multi-proyecto.

---

## FEATURE 10 — Cuadro de asistencia del lanzamiento

### HU-1001 · Captura del cuadro de asistencia ✅
**Como** administrador,
**quiero** registrar por día del lanzamiento: familias, adultos, niños, mascotas, asistentes con cita, carros, motos, caminando y agendados (equipo/Lucía),
**para** medir la convocatoria del evento.

**Criterios de aceptación:**
1. Puedo agregar/quitar días, escribir observaciones libres y guardar el cuadro.
2. La vista previa calcula porcentajes en vivo (% con cita, cumplimiento, ventas vs familias).

**Tasks:**
- [ ] Captura desde móvil optimizada (registro en puerta).

### HU-1002 · Exportación del cuadro (Excel y PDF) ✅
**Como** administrador,
**quiero** exportar el cuadro de asistencia a Excel y verlo integrado al PDF técnico,
**para** anexarlo al informe oficial del lanzamiento.

**Tasks:**
- [ ] Consolidado de asistencia multi-lanzamiento (comparativo entre proyectos).

---

## FEATURE 11 — Seguridad técnica y cumplimiento (transversal)

### HU-1101 · Endurecimiento de aplicación ✅
**Como** responsable de seguridad,
**quiero** cabeceras y configuración endurecidas (CSP, X-Frame-Options DENY, nosniff, Referrer-Policy, Permissions-Policy, HSTS, HTTPS-only, TLS ≥ 1.2),
**para** reducir la superficie de ataque en producción.

**Tasks:**
- [ ] Eliminar `'unsafe-inline'` de `script-src` migrando el JS inline a archivos con nonce.
- [ ] Fijar `AllowedHosts` al dominio real de producción.

### HU-1102 · Protección de datos y secretos 🔨
**Como** responsable de seguridad,
**quiero** que ningún secreto viva en el repositorio y que los datos sensibles estén protegidos,
**para** cumplir buenas prácticas y la normativa de datos personales.

**Criterios de aceptación:**
1. La cadena de conexión se define por variable de entorno/Key Vault (no en `appsettings.json`). ✅
2. `appsettings.Development/Production.json` están en `.gitignore`. ✅
3. SQL 100% parametrizado; sin XSS conocido; documento del cliente saneado a solo dígitos. ✅

**Tasks:**
- [ ] Rotar la credencial SQL expuesta en el historial de git y purgar historial (`git filter-repo`).
- [ ] DataProtection persistente con Azure Blob + Key Vault.
- [ ] Auditoría periódica `dotnet list package --vulnerable` en CI.

### HU-1103 · Auditoría de QA continua ⏳
**Como** líder técnico,
**quiero** un proyecto de pruebas automatizadas (xUnit) ejecutado en CI,
**para** que cada cambio se valide antes de llegar a QA/Producción.

**Tasks:**
- [ ] Crear `Plataforma_ventas.Tests` con los casos ya diseñados (SoloDigitos, mapeo de listas, política de contraseña).
- [ ] Pruebas de integración del flujo de venta (tomar → vender, reservar → vender) con BD en contenedor.
- [ ] Incorporar `dotnet test` como gate del pipeline.

---

## FEATURE 12 — Infraestructura, despliegue y DevOps

### HU-1201 · Entornos QA y Producción idénticos ⏳
**Como** equipo de desarrollo,
**quiero** dos entornos aislados con la misma configuración (QA y Producción),
**para** probar cada versión en QA antes de liberarla.

**Criterios de aceptación:**
1. Recursos separados por entorno (grupos de recursos/App Service/BD propios).
2. La configuración sensible vive en el entorno (variables/Key Vault), no en el código.
3. QA nunca comparte base de datos con Producción.

**Tasks:**
- [ ] Crear los recursos de QA y Producción en Azure (App Service B1 + Azure SQL Serverless con auto-pausa, según decisión aprobada).
- [ ] Dominio y subdominio (`qa.dominio.com` / dominio raíz) con certificado administrado.

### HU-1202 · CI/CD por ramas con aprobación a Producción 🔨
**Como** equipo de desarrollo,
**quiero** que la rama `qa` despliegue automáticamente a QA y la rama `master` a Producción con aprobación manual,
**para** liberar con control y trazabilidad desde Azure DevOps.

**Criterios de aceptación:**
1. Pipeline YAML único que compila una vez y despliega según la rama de origen. ✅ (archivo `azure-pipelines.yml` listo)
2. El stage de Producción exige aprobación en el Environment "Production".
3. Los pull requests no despliegan.

**Tasks:**
- [ ] Conectar el repo (GitHub) a Azure Pipelines y crear los Environments QA/Production con aprobadores.
- [ ] Crear las Service Connections por entorno con permisos mínimos.
- [ ] Reemplazar los placeholders del YAML con los nombres reales de los App Service.

### HU-1203 · Monitoreo y respaldo en producción ⏳
**Como** responsable de operación,
**quiero** monitoreo de errores/desempeño y respaldos verificados,
**para** detectar incidentes durante los lanzamientos y poder recuperarnos.

**Tasks:**
- [ ] Application Insights + alertas (errores 5xx, latencia, caídas).
- [ ] Verificar la retención PITR de Azure SQL y documentar el procedimiento de restauración.
- [ ] Prueba de restauración de respaldo previa al primer lanzamiento en producción.

---

## Resumen para carga en Azure DevOps

| Feature | Historias | Estado general |
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

**Total: 12 Features · 33 Historias de Usuario · 45+ Tasks pendientes.**

> Sugerencia de carga en Azure DevOps: crear primero las 12 Features bajo la Épica; luego cada User Story con su descripción ("Como… quiero… para…") en el campo *Description* y los criterios de aceptación en *Acceptance Criteria*; finalmente las Tasks pendientes como hijos de cada historia. Las historias ✅ pueden crearse ya en estado *Closed/Done* para reflejar el trabajo realizado.
