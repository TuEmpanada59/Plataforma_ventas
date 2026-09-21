# Scripts de base de datos

## Montar una base nueva (producción)

Cuatro archivos, en este orden:

| Paso | Qué se ejecuta | Qué hace |
|---|---|---|
| 1 | `EsquemaBase.sql` | Crea las tablas, las claves y los valores por defecto |
| 2 | `PanelAdmin.sql` | Columnas y tablas agregadas después, los índices y la lista de medios |
| 3 | `Asistencias.sql` | Tablas del cuadro de asistencia |
| 4 | `VerificarBase.sql` | Solo consulta: confirma que no falte nada |

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

- **`EsquemaBase.sql`** — esquema completo, generado desde la base de pruebas con el
  asistente de SSMS. **No trae los índices no agrupados**, porque el asistente los
  omite; los crea la sección 16 de `PanelAdmin.sql`. Está en UTF-8 con BOM: si se
  vuelve a guardar en otra codificación, la columna `Usuarios.Contraseña` puede quedar
  con el nombre corrupto y el inicio de sesión falla sin explicación.
- **`PanelAdmin.sql`** — migración incremental, dividida en secciones numeradas. Cada
  sección revisa si su cambio ya está aplicado, así que se puede volver a ejecutar
  completo sin romper nada. Aquí también se siembran los medios publicitarios.
- **`Asistencias.sql`** — tablas del cuadro de asistencia del lanzamiento. **Ojo:**
  este sí borra y recrea sus tablas, así que no se ejecuta sobre una base con cuadros
  de asistencia ya diligenciados.
- **`VerificarBase.sql`** — diagnóstico. No modifica nada.
