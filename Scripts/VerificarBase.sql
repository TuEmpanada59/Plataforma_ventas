-- ============================================================================
-- VERIFICACIÓN DE LA BASE DE DATOS
--
-- Se ejecuta sobre una base recién creada (o sobre una existente) y dice qué le
-- falta para que la plataforma funcione. No modifica nada: solo consulta.
--
-- Sirve para el paso previo a un lanzamiento: si aquí sale algo en FALTA, la
-- aplicación va a fallar en producción con la sala de ventas llena.
--
-- Orden correcto de montaje de una base nueva:
--   1) Script de esquema generado desde la base de pruebas (solo esquema)
--   2) Scripts/PanelAdmin.sql   (completo, es idempotente)
--   3) Scripts/Asistencias.sql
--   4) Este archivo, para confirmar
-- ============================================================================

SET NOCOUNT ON;

PRINT '=== TABLAS ===';

;WITH Esperadas AS (
    SELECT * FROM (VALUES
        -- Núcleo de la plataforma: sin estas no arranca nada
        ('Usuarios',            'Núcleo'),
        ('Proyectos',           'Núcleo'),
        ('Inmuebles',           'Núcleo'),
        ('Ventas',              'Núcleo'),
        ('Clientes',            'Núcleo'),
        ('ProyectoAreaListas',  'Núcleo'),
        -- Agregadas por PanelAdmin.sql
        ('HistorialListas',     'PanelAdmin.sql'),
        ('AjustesPrecio',       'PanelAdmin.sql'),
        ('AjustesPrecioDetalle','PanelAdmin.sql'),
        ('MediosPublicitarios', 'PanelAdmin.sql'),
        ('ProyectoActividades', 'PanelAdmin.sql'),
        ('AsistenciaFranja',    'PanelAdmin.sql'),
        -- Agregadas por Asistencias.sql
        ('AsistenciaEvento',    'Asistencias.sql'),
        ('AsistenciaDia',       'Asistencias.sql'),
        ('AsistenciaTorre',     'Asistencias.sql')
    ) AS t(Tabla, Origen)
)
SELECT  e.Tabla,
        e.Origen,
        CASE WHEN OBJECT_ID(e.Tabla, 'U') IS NULL THEN '>>> FALTA' ELSE 'ok' END AS Estado
FROM    Esperadas e
ORDER BY CASE WHEN OBJECT_ID(e.Tabla, 'U') IS NULL THEN 0 ELSE 1 END, e.Origen, e.Tabla;

PRINT '=== COLUMNAS AGREGADAS POR MIGRACIONES ===';

;WITH Columnas AS (
    SELECT * FROM (VALUES
        ('Ventas',    'MotivoAnulacion',      'Anulación de ventas'),
        ('Ventas',    'FechaAnulacion',       'Anulación de ventas'),
        ('Ventas',    'IdUsuarioAnula',       'Anulación de ventas'),
        ('Ventas',    'Observaciones',        'Observaciones de la venta'),
        ('Ventas',    'Origen',               'Ventas importadas del Excel'),
        ('Inmuebles', 'Torre',                'Torre por inmueble (viene del esquema base)'),
        ('Inmuebles', 'Etapa',                'Etapas en un mismo proyecto'),
        ('Inmuebles', 'ObservacionReserva',   'Observación de la reserva'),
        ('Inmuebles', 'EnLanzamiento',        'Total del lanzamiento'),
        ('Proyectos', 'Actividad',            'Actividad comercial'),
        ('Proyectos', 'EtapaLanzamiento',     'Actividad comercial'),
        ('Proyectos', 'HorasVigenciaReserva', 'Vigencia de la reserva'),
        ('Clientes',  'MedioPublicitario',    'Medio por el que se enteró')
    ) AS c(Tabla, Columna, Para)
)
SELECT  c.Tabla + '.' + c.Columna AS Columna,
        c.Para,
        CASE WHEN OBJECT_ID(c.Tabla, 'U') IS NULL THEN '>>> NO EXISTE LA TABLA'
             WHEN COL_LENGTH(c.Tabla, c.Columna) IS NULL THEN '>>> FALTA'
             ELSE 'ok' END AS Estado
FROM    Columnas c
ORDER BY CASE WHEN OBJECT_ID(c.Tabla,'U') IS NULL OR COL_LENGTH(c.Tabla, c.Columna) IS NULL
              THEN 0 ELSE 1 END, c.Tabla, c.Columna;

PRINT '=== ÍNDICES ===';

-- Un script de esquema generado desde otra base suele venir sin los índices no
-- agrupados: el asistente de SSMS los omite salvo que se le pidan. La sección 16
-- de PanelAdmin.sql los crea; esto confirma que quedaron.
;WITH Indices AS (
    SELECT * FROM (VALUES
        ('HistorialListas',     'IX_HistorialListas_Proy'),
        ('AsistenciaFranja',    'IX_AsistFranja_Dia'),
        ('AjustesPrecio',       'IX_AjustesPrecio_Proy'),
        ('AjustesPrecioDetalle','IX_AjusteDet_Ajuste'),
        ('ProyectoActividades', 'IX_ProyActividades_Proy'),
        ('MediosPublicitarios', 'UX_Medios_Nombre')
    ) AS i(Tabla, Indice)
)
SELECT  i.Tabla, i.Indice,
        CASE WHEN OBJECT_ID(i.Tabla,'U') IS NULL THEN '>>> NO EXISTE LA TABLA'
             WHEN NOT EXISTS (SELECT 1 FROM sys.indexes x
                              WHERE x.name = i.Indice AND x.object_id = OBJECT_ID(i.Tabla))
             THEN '>>> FALTA: corre la sección 16 de PanelAdmin.sql'
             ELSE 'ok' END AS Estado
FROM    Indices i
ORDER BY CASE WHEN OBJECT_ID(i.Tabla,'U') IS NULL
              OR NOT EXISTS (SELECT 1 FROM sys.indexes x
                             WHERE x.name = i.Indice AND x.object_id = OBJECT_ID(i.Tabla))
              THEN 0 ELSE 1 END, i.Tabla;

PRINT '=== DATOS MÍNIMOS ===';

-- Los medios publicitarios son lista fija: sin ellos el formulario de venta sale
-- sin opciones y el reporte de "cómo se enteraron" queda vacío.
SELECT  'Medios publicitarios' AS Dato,
        CASE WHEN OBJECT_ID('MediosPublicitarios','U') IS NULL THEN 0
             ELSE (SELECT COUNT(*) FROM MediosPublicitarios) END AS Cantidad,
        CASE WHEN OBJECT_ID('MediosPublicitarios','U') IS NULL THEN '>>> FALTA LA TABLA'
             WHEN (SELECT COUNT(*) FROM MediosPublicitarios) = 0 THEN '>>> VACÍA: corre la sección 14'
             ELSE 'ok' END AS Estado
UNION ALL
-- Sin un administrador no hay forma de entrar: la plataforma no tiene registro.
SELECT  'Administradores',
        (SELECT COUNT(*) FROM Usuarios WHERE Rol IN ('Administrador','SuperAdministrador')),
        CASE WHEN (SELECT COUNT(*) FROM Usuarios WHERE Rol IN ('Administrador','SuperAdministrador')) = 0
             THEN '>>> NO HAY NINGUNO: nadie puede iniciar sesión'
             ELSE 'ok' END
UNION ALL
SELECT  'Proyectos activos',
        (SELECT COUNT(*) FROM Proyectos WHERE Activo = 1),
        'informativo';

-- La columna de la contraseña lleva eñe. Si el script de esquema se guardó con otra
-- codificación, la columna puede haber quedado creada con el nombre corrupto, y la
-- aplicación falla en el login sin decir por qué. Se busca por patrón para no
-- depender de la eñe en este mismo archivo.
SELECT  'Columna de contraseña' AS Comprobacion,
        ISNULL((SELECT TOP 1 name COLLATE DATABASE_DEFAULT FROM sys.columns
                WHERE object_id = OBJECT_ID('Usuarios') AND name LIKE 'Contrase%'), '(ninguna)') AS NombreReal,
        CASE WHEN NOT EXISTS (SELECT 1 FROM sys.columns
                              WHERE object_id = OBJECT_ID('Usuarios') AND name LIKE 'Contrase%')
             THEN '>>> FALTA LA COLUMNA'
             WHEN EXISTS (SELECT 1 FROM sys.columns
                          WHERE object_id = OBJECT_ID('Usuarios') AND name = N'Contraseña')
             THEN 'ok'
             ELSE '>>> NOMBRE CORRUPTO: la eñe no sobrevivió a la codificación' END AS Estado;

PRINT '=== FIN DE LA VERIFICACIÓN ===';
PRINT 'Todo lo que aparezca con >>> hay que resolverlo antes del lanzamiento.';
GO
