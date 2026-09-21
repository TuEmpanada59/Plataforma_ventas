# Scripts de base de datos

## Montar una base nueva (producción)

Un solo archivo: **`BaseCompleta.sql`**. Se ejecuta sobre una base vacía y deja el
esquema, las migraciones, los índices y el catálogo de medios, y al final informa qué
quedó pendiente. Es la unión de `EsquemaBase.sql`, `PanelAdmin.sql` y
`VerificarBase.sql`, que también se pueden correr por separado en ese mismo orden.

**`Asistencias.sql` no va en el montaje de una base nueva.** Ese script borra y recrea
sus tablas, y `EsquemaBase.sql` ya las trae. Peor: al intentar borrar `AsistenciaDia`
choca con la clave foránea de `AsistenciaFranja` y falla. Solo sirve para una base
antigua que todavía no tenga esas tablas.

Después de ejecutarlo falta una sola cosa: la fila del usuario administrador.

## No copiar los datos de pruebas

Restaurar un respaldo de la base de pruebas en producción arrastra usuarios de prueba
con contraseñas conocidas, clientes inventados, ventas que aparecerán en los reportes
del primer lanzamiento y proyectos viejos con sus códigos de acceso todavía válidos.
Limpiar eso después es más trabajo y más riesgoso que empezar en limpio.

La única excepción es el primer administrador: su contraseña se guarda con BCrypt y no
se puede escribir a mano, así que esa fila se copia desde la otra base.

## Los archivos

- **`BaseCompleta.sql`** — todo lo anterior en un solo archivo, en el orden correcto.
  Es el que se usa para montar una base nueva.
- **`EsquemaBase.sql`** — esquema completo, generado desde la base de pruebas con el
  asistente de SSMS. **No trae los índices no agrupados**, porque el asistente los
  omite; los crea la sección 16 de `PanelAdmin.sql`. Está en UTF-8 con BOM: si se
  vuelve a guardar en otra codificación, la columna `Usuarios.Contraseña` puede quedar
  con el nombre corrupto y el inicio de sesión falla sin explicación.
- **`PanelAdmin.sql`** — migración incremental, dividida en secciones numeradas. Cada
  sección revisa si su cambio ya está aplicado, así que se puede volver a ejecutar
  completo sin romper nada. Aquí también se siembran los medios publicitarios.
- **`Asistencias.sql`** — tablas del cuadro de asistencia, para bases antiguas que no
  las tengan. **Ojo:** borra y recrea sus tablas. No se ejecuta sobre una base montada
  con `EsquemaBase.sql`, ni sobre una que ya tenga cuadros diligenciados.
- **`VerificarBase.sql`** — diagnóstico. No modifica nada.
