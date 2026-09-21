# Scripts de base de datos

## Montar una base nueva (producción)

Los scripts de esta carpeta **no crean la base completa**. Solo traen lo que se fue
agregando después del arranque. Las tablas centrales (`Usuarios`, `Proyectos`,
`Inmuebles`, `Ventas`, `Clientes`, `ProyectoAreaListas`) se crearon a mano en su
momento y viven dentro de la base, no en el repositorio.

Para montar una base nueva, en este orden:

| Paso | Qué se ejecuta | De dónde sale |
|---|---|---|
| 1 | Esquema base | Generado desde la base de pruebas con **solo esquema**, sin datos |
| 2 | `PanelAdmin.sql` | Este repositorio. Completo; es idempotente |
| 3 | `Asistencias.sql` | Este repositorio |
| 4 | `VerificarBase.sql` | Este repositorio. Solo consulta, confirma que no falte nada |

El paso 4 no modifica nada: lista tablas, columnas y datos mínimos, y marca con `>>>`
todo lo que falte. Si sale limpio, la base está lista.

## No copiar los datos de pruebas

Restaurar un respaldo de la base de pruebas en producción arrastra usuarios de prueba
con contraseñas conocidas, clientes inventados, ventas que aparecerán en los reportes
del primer lanzamiento y proyectos viejos con sus códigos de acceso todavía válidos.
Limpiar eso después es más trabajo y más riesgoso que empezar en limpio.

La única excepción es el primer administrador: su contraseña se guarda con BCrypt y no
se puede escribir a mano, así que esa fila se copia desde la otra base.

## Los archivos

- **`PanelAdmin.sql`** — migración incremental, dividida en secciones numeradas. Cada
  sección revisa si su cambio ya está aplicado, así que se puede volver a ejecutar
  completo sin romper nada. Aquí también se siembran los medios publicitarios.
- **`Asistencias.sql`** — tablas del cuadro de asistencia del lanzamiento. **Ojo:**
  este sí borra y recrea sus tablas, así que no se ejecuta sobre una base con cuadros
  de asistencia ya diligenciados.
- **`VerificarBase.sql`** — diagnóstico. No modifica nada.
