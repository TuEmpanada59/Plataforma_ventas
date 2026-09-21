-- ============================================================================
-- ESQUEMA BASE DE LA PLATAFORMA
--
-- Generado desde la base de pruebas con el asistente "Generar scripts" de SSMS,
-- solo esquema y sin datos. Es el punto de partida para montar una base nueva.
--
-- NO incluye los índices no agrupados: el asistente los omite. Los crea la
-- sección 16 de PanelAdmin.sql, que se ejecuta después.
--
-- Orden de montaje:  este archivo -> PanelAdmin.sql -> Asistencias.sql -> VerificarBase.sql
--
-- Ojo con la codificación: la columna Usuarios.Contraseña lleva eñe. Este archivo
-- está en UTF-8 con BOM. Si se vuelve a guardar en otra codificación, esa columna
-- puede quedar creada con el nombre corrupto y el inicio de sesión falla.
-- ============================================================================

/****** Object:  Table [dbo].[AjustesPrecio]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AjustesPrecio](
	[IdAjuste] [bigint] IDENTITY(1,1) NOT NULL,
	[IdProyecto] [int] NOT NULL,
	[Torre] [nvarchar](50) NOT NULL,
	[Metros] [nvarchar](50) NOT NULL,
	[Listas] [nvarchar](20) NOT NULL,
	[Tipo] [nvarchar](12) NOT NULL,
	[Valor] [decimal](18, 4) NOT NULL,
	[Unidades] [int] NOT NULL,
	[IdUsuario] [int] NULL,
	[Usuario] [nvarchar](150) NOT NULL,
	[Fecha] [datetime] NOT NULL,
	[Revertido] [bit] NOT NULL,
	[FechaReversion] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[IdAjuste] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AjustesPrecioDetalle]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AjustesPrecioDetalle](
	[IdDetalle] [bigint] IDENTITY(1,1) NOT NULL,
	[IdAjuste] [bigint] NOT NULL,
	[IdInmueble] [int] NOT NULL,
	[NumLista] [int] NOT NULL,
	[PrecioAnterior] [bigint] NOT NULL,
	[PrecioNuevo] [bigint] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdDetalle] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AsistenciaDia]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AsistenciaDia](
	[IdDia] [int] IDENTITY(1,1) NOT NULL,
	[IdEvento] [int] NOT NULL,
	[Fecha] [date] NULL,
	[NombreDia] [nvarchar](50) NOT NULL,
	[Orden] [int] NOT NULL,
	[Familias] [int] NOT NULL,
	[Adultos] [int] NOT NULL,
	[Ninos] [int] NOT NULL,
	[Mascotas] [int] NOT NULL,
	[AsisteCita] [int] NOT NULL,
	[Carros] [int] NOT NULL,
	[Motos] [int] NOT NULL,
	[Caminando] [int] NOT NULL,
	[AgendadosEquipo] [int] NOT NULL,
	[AgendadosLucia] [int] NOT NULL,
	[AsisteCitaLucia] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdDia] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AsistenciaEvento]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AsistenciaEvento](
	[IdEvento] [int] IDENTITY(1,1) NOT NULL,
	[IdProyecto] [int] NOT NULL,
	[Titulo] [nvarchar](200) NOT NULL,
	[Observaciones] [nvarchar](max) NOT NULL,
	[FechaCreacion] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdEvento] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AsistenciaFranja]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AsistenciaFranja](
	[IdFranja] [int] IDENTITY(1,1) NOT NULL,
	[IdDia] [int] NOT NULL,
	[Orden] [int] NOT NULL,
	[HoraDesde] [int] NOT NULL,
	[HoraHasta] [int] NOT NULL,
	[Familias] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdFranja] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AsistenciaTorre]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AsistenciaTorre](
	[IdTorre] [int] IDENTITY(1,1) NOT NULL,
	[IdDia] [int] NOT NULL,
	[Torre] [nvarchar](50) NOT NULL,
	[Orden] [int] NOT NULL,
	[Preventas] [int] NOT NULL,
	[ValorPreventa] [bigint] NOT NULL,
	[Ventas] [int] NOT NULL,
	[ValorVenta] [bigint] NOT NULL,
	[Opciones] [int] NOT NULL,
	[ValorOpciones] [bigint] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdTorre] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Clientes]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Clientes](
	[IdCliente] [int] IDENTITY(1,1) NOT NULL,
	[Nombre] [nvarchar](100) NOT NULL,
	[Apellido] [nvarchar](100) NOT NULL,
	[Documento] [nvarchar](20) NULL,
	[Celular] [nvarchar](20) NULL,
	[Correo] [nvarchar](150) NULL,
	[Direccion] [nvarchar](200) NULL,
	[IdProyecto] [int] NULL,
	[MedioPublicitario] [nvarchar](80) NULL,
 CONSTRAINT [PK__Clientes__D5946642159E5EF2] PRIMARY KEY CLUSTERED 
(
	[IdCliente] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[HistorialListas]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[HistorialListas](
	[IdHistorial] [bigint] IDENTITY(1,1) NOT NULL,
	[IdProyecto] [int] NOT NULL,
	[Metros] [nvarchar](50) NOT NULL,
	[ListaAnterior] [int] NOT NULL,
	[ListaNueva] [int] NOT NULL,
	[Motivo] [nvarchar](30) NOT NULL,
	[IdUsuario] [int] NULL,
	[Usuario] [nvarchar](150) NOT NULL,
	[Fecha] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdHistorial] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Inmuebles]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Inmuebles](
	[IdInmuebles] [int] IDENTITY(1,1) NOT NULL,
	[IdProyecto] [int] NOT NULL,
	[Apto] [nvarchar](20) NOT NULL,
	[Tipo] [nvarchar](50) NULL,
	[Piso] [nvarchar](10) NULL,
	[Metros] [nvarchar](20) NULL,
	[Lista1] [nvarchar](30) NULL,
	[Lista2] [nvarchar](30) NULL,
	[Lista3] [nvarchar](30) NULL,
	[Lista4] [nvarchar](30) NULL,
	[Lista5] [nvarchar](30) NULL,
	[Estado] [nvarchar](20) NULL,
	[Torre] [nvarchar](50) NULL,
	[IdVendedorEnProceso] [int] NULL,
	[FechaEnProceso] [datetime] NULL,
	[IdVendedorReserva] [int] NULL,
	[PrecioReserva] [bigint] NULL,
	[FechaReserva] [datetime] NULL,
	[ObservacionReserva] [nvarchar](500) NULL,
	[Etapa] [nvarchar](60) NOT NULL,
	[EnLanzamiento] [bit] NOT NULL,
 CONSTRAINT [PK__Inmueble__6B13429C9F46F935] PRIMARY KEY CLUSTERED 
(
	[IdInmuebles] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MediosPublicitarios]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MediosPublicitarios](
	[IdMedio] [int] IDENTITY(1,1) NOT NULL,
	[Nombre] [nvarchar](120) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdMedio] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProyectoActividades]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProyectoActividades](
	[IdActividad] [int] IDENTITY(1,1) NOT NULL,
	[IdProyecto] [int] NOT NULL,
	[Tipo] [nvarchar](30) NOT NULL,
	[Etapa] [nvarchar](100) NOT NULL,
	[FechaInicio] [datetime] NOT NULL,
	[TotalProyecto] [int] NOT NULL,
	[TotalLanzamiento] [int] NOT NULL,
	[HistoricoVendidas] [int] NOT NULL,
	[HistoricoReservadas] [int] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[IdActividad] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ProyectoAreaListas]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ProyectoAreaListas](
	[IdProyecto] [int] NOT NULL,
	[Metros] [varchar](20) NOT NULL,
	[ListaActual] [int] NOT NULL,
	[AptsPorLista] [int] NOT NULL,
 CONSTRAINT [PK_PAL] PRIMARY KEY CLUSTERED 
(
	[IdProyecto] ASC,
	[Metros] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Proyectos]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Proyectos](
	[IdProyectos] [int] IDENTITY(1,1) NOT NULL,
	[Nombre] [nvarchar](150) NOT NULL,
	[ListaActual] [int] NULL,
	[ApartamentosPorLista] [int] NULL,
	[FechaCarga] [datetime] NULL,
	[Activo] [bit] NULL,
	[CodigoAcceso] [varchar](20) NULL,
	[IdAdminCreador] [int] NULL,
	[IdProyecto] [int] NULL,
	[ModoLista] [varchar](10) NULL,
	[TipProyecto] [varchar](20) NOT NULL,
	[HorasVigenciaReserva] [int] NOT NULL,
	[Actividad] [nvarchar](30) NOT NULL,
	[EtapaLanzamiento] [nvarchar](100) NOT NULL,
 CONSTRAINT [PK__Proyecto__E6EE063CD54B19B0] PRIMARY KEY CLUSTERED 
(
	[IdProyectos] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Usuarios]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Usuarios](
	[IdUsuario] [int] IDENTITY(1,1) NOT NULL,
	[Nombre] [varchar](100) NOT NULL,
	[Apellido] [varchar](100) NOT NULL,
	[Documento] [varchar](20) NOT NULL,
	[Celular] [varchar](20) NOT NULL,
	[Correo] [varchar](150) NOT NULL,
	[Usuario] [varchar](100) NOT NULL,
	[Contraseña] [nvarchar](255) NOT NULL,
	[Rol] [varchar](50) NOT NULL,
	[IdProyecto] [int] NULL,
	[IdSuperior] [int] NULL,
 CONSTRAINT [PK__Usuarios__5B65BF97FDC3EDB6] PRIMARY KEY CLUSTERED 
(
	[IdUsuario] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ__Usuarios__E3237CF79E6224E6] UNIQUE NONCLUSTERED 
(
	[Usuario] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Ventas]    Script Date: 21/09/2026 11:46:21 a. m. ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Ventas](
	[IdVenta] [int] IDENTITY(1,1) NOT NULL,
	[IdInmueble] [int] NOT NULL,
	[IdCliente] [int] NOT NULL,
	[IdUsuario] [int] NOT NULL,
	[IdProyecto] [int] NOT NULL,
	[ListaAplicada] [int] NULL,
	[PrecioVenta] [bigint] NULL,
	[FechaVenta] [datetime] NULL,
	[Estado] [nvarchar](20) NULL,
	[Destino] [varchar](50) NULL,
	[MotivoAnulacion] [nvarchar](500) NULL,
	[FechaAnulacion] [datetime] NULL,
	[IdUsuarioAnula] [int] NULL,
	[Observaciones] [nvarchar](1000) NULL,
	[Origen] [nvarchar](20) NOT NULL,
 CONSTRAINT [PK__Ventas__BC1240BDDC3B0D89] PRIMARY KEY CLUSTERED 
(
	[IdVenta] ASC
)WITH (STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ('') FOR [Torre]
GO
ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ('') FOR [Metros]
GO
ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ('') FOR [Listas]
GO
ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ((0)) FOR [Unidades]
GO
ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ('') FOR [Usuario]
GO
ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT (getutcdate()) FOR [Fecha]
GO
ALTER TABLE [dbo].[AjustesPrecio] ADD  DEFAULT ((0)) FOR [Revertido]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ('') FOR [NombreDia]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Orden]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Familias]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Adultos]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Ninos]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Mascotas]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [AsisteCita]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Carros]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Motos]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [Caminando]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [AgendadosEquipo]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [AgendadosLucia]
GO
ALTER TABLE [dbo].[AsistenciaDia] ADD  DEFAULT ((0)) FOR [AsisteCitaLucia]
GO
ALTER TABLE [dbo].[AsistenciaEvento] ADD  DEFAULT ('') FOR [Titulo]
GO
ALTER TABLE [dbo].[AsistenciaEvento] ADD  DEFAULT ('') FOR [Observaciones]
GO
ALTER TABLE [dbo].[AsistenciaEvento] ADD  DEFAULT (getdate()) FOR [FechaCreacion]
GO
ALTER TABLE [dbo].[AsistenciaFranja] ADD  DEFAULT ((0)) FOR [Orden]
GO
ALTER TABLE [dbo].[AsistenciaFranja] ADD  DEFAULT ((0)) FOR [Familias]
GO
ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ('') FOR [Torre]
GO
ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [Orden]
GO
ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [Preventas]
GO
ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [ValorPreventa]
GO
ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [Ventas]
GO
ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [ValorVenta]
GO
ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [Opciones]
GO
ALTER TABLE [dbo].[AsistenciaTorre] ADD  DEFAULT ((0)) FOR [ValorOpciones]
GO
ALTER TABLE [dbo].[HistorialListas] ADD  DEFAULT ('') FOR [Metros]
GO
ALTER TABLE [dbo].[HistorialListas] ADD  DEFAULT ('') FOR [Usuario]
GO
ALTER TABLE [dbo].[HistorialListas] ADD  DEFAULT (getutcdate()) FOR [Fecha]
GO
ALTER TABLE [dbo].[Inmuebles] ADD  CONSTRAINT [DF__Inmuebles__Estad__7A672E12]  DEFAULT ('DISPONIBLE') FOR [Estado]
GO
ALTER TABLE [dbo].[Inmuebles] ADD  DEFAULT ('') FOR [Etapa]
GO
ALTER TABLE [dbo].[Inmuebles] ADD  CONSTRAINT [DF_Inmuebles_EnLanz]  DEFAULT ((1)) FOR [EnLanzamiento]
GO
ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ('PROYECTO_NUEVO') FOR [Tipo]
GO
ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ('') FOR [Etapa]
GO
ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT (getdate()) FOR [FechaInicio]
GO
ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ((0)) FOR [TotalProyecto]
GO
ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ((0)) FOR [TotalLanzamiento]
GO
ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ((0)) FOR [HistoricoVendidas]
GO
ALTER TABLE [dbo].[ProyectoActividades] ADD  DEFAULT ((0)) FOR [HistoricoReservadas]
GO
ALTER TABLE [dbo].[ProyectoAreaListas] ADD  CONSTRAINT [DF__ProyectoA__Lista__756D6ECB]  DEFAULT ((1)) FOR [ListaActual]
GO
ALTER TABLE [dbo].[ProyectoAreaListas] ADD  CONSTRAINT [DF__ProyectoA__AptsP__0697FACD]  DEFAULT ((0)) FOR [AptsPorLista]
GO
ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__Lista__6FE99F9F]  DEFAULT ((1)) FOR [ListaActual]
GO
ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__Apart__70DDC3D8]  DEFAULT ((1)) FOR [ApartamentosPorLista]
GO
ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__Fecha__71D1E811]  DEFAULT (getdate()) FOR [FechaCarga]
GO
ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__Activ__72C60C4A]  DEFAULT ((1)) FOR [Activo]
GO
ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__ModoL__4D5F7D71]  DEFAULT ('MANUAL') FOR [ModoLista]
GO
ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF__Proyectos__TipPr__607251E5]  DEFAULT ('APARTAMENTOS') FOR [TipProyecto]
GO
ALTER TABLE [dbo].[Proyectos] ADD  DEFAULT ((0)) FOR [HorasVigenciaReserva]
GO
ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF_Proyectos_Actividad]  DEFAULT ('PROYECTO_NUEVO') FOR [Actividad]
GO
ALTER TABLE [dbo].[Proyectos] ADD  CONSTRAINT [DF_Proyectos_EtapaLanz]  DEFAULT ('') FOR [EtapaLanzamiento]
GO
ALTER TABLE [dbo].[Usuarios] ADD  CONSTRAINT [DF__Usuarios__Rol__5EBF139D]  DEFAULT ('Vendedor') FOR [Rol]
GO
ALTER TABLE [dbo].[Ventas] ADD  CONSTRAINT [DF__Ventas__FechaVen__0A9D95DB]  DEFAULT (getdate()) FOR [FechaVenta]
GO
ALTER TABLE [dbo].[Ventas] ADD  CONSTRAINT [DF__Ventas__Estado__0B91BA14]  DEFAULT ('ACTIVA') FOR [Estado]
GO
ALTER TABLE [dbo].[Ventas] ADD  DEFAULT ('PLATAFORMA') FOR [Origen]
GO
ALTER TABLE [dbo].[AjustesPrecioDetalle]  WITH CHECK ADD  CONSTRAINT [FK_AjusteDet_Ajuste] FOREIGN KEY([IdAjuste])
REFERENCES [dbo].[AjustesPrecio] ([IdAjuste])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AjustesPrecioDetalle] CHECK CONSTRAINT [FK_AjusteDet_Ajuste]
GO
ALTER TABLE [dbo].[AsistenciaDia]  WITH CHECK ADD  CONSTRAINT [FK_AsistDia_Evento] FOREIGN KEY([IdEvento])
REFERENCES [dbo].[AsistenciaEvento] ([IdEvento])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AsistenciaDia] CHECK CONSTRAINT [FK_AsistDia_Evento]
GO
ALTER TABLE [dbo].[AsistenciaFranja]  WITH CHECK ADD  CONSTRAINT [FK_AsistFranja_Dia] FOREIGN KEY([IdDia])
REFERENCES [dbo].[AsistenciaDia] ([IdDia])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AsistenciaFranja] CHECK CONSTRAINT [FK_AsistFranja_Dia]
GO
ALTER TABLE [dbo].[AsistenciaTorre]  WITH CHECK ADD  CONSTRAINT [FK_AsistTorre_Dia] FOREIGN KEY([IdDia])
REFERENCES [dbo].[AsistenciaDia] ([IdDia])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AsistenciaTorre] CHECK CONSTRAINT [FK_AsistTorre_Dia]
GO
ALTER TABLE [dbo].[Clientes]  WITH CHECK ADD  CONSTRAINT [FK_Clientes] FOREIGN KEY([IdProyecto])
REFERENCES [dbo].[Proyectos] ([IdProyectos])
GO
ALTER TABLE [dbo].[Clientes] CHECK CONSTRAINT [FK_Clientes]
GO
ALTER TABLE [dbo].[Inmuebles]  WITH CHECK ADD  CONSTRAINT [FK__Inmuebles__IdPro__797309D9] FOREIGN KEY([IdProyecto])
REFERENCES [dbo].[Proyectos] ([IdProyectos])
GO
ALTER TABLE [dbo].[Inmuebles] CHECK CONSTRAINT [FK__Inmuebles__IdPro__797309D9]
GO
ALTER TABLE [dbo].[Inmuebles]  WITH CHECK ADD  CONSTRAINT [FK__Inmuebles__IdVen__7B5B524B] FOREIGN KEY([IdVendedorEnProceso])
REFERENCES [dbo].[Usuarios] ([IdUsuario])
GO
ALTER TABLE [dbo].[Inmuebles] CHECK CONSTRAINT [FK__Inmuebles__IdVen__7B5B524B]
GO
ALTER TABLE [dbo].[Proyectos]  WITH CHECK ADD  CONSTRAINT [FK__Proyectos__IdPro__208CD6FA] FOREIGN KEY([IdProyecto])
REFERENCES [dbo].[Proyectos] ([IdProyectos])
GO
ALTER TABLE [dbo].[Proyectos] CHECK CONSTRAINT [FK__Proyectos__IdPro__208CD6FA]
GO
ALTER TABLE [dbo].[Usuarios]  WITH CHECK ADD  CONSTRAINT [FK__Usuarios__IdProy__2739D489] FOREIGN KEY([IdProyecto])
REFERENCES [dbo].[Proyectos] ([IdProyectos])
GO
ALTER TABLE [dbo].[Usuarios] CHECK CONSTRAINT [FK__Usuarios__IdProy__2739D489]
GO
ALTER TABLE [dbo].[Usuarios]  WITH CHECK ADD  CONSTRAINT [FK_ADMIN] FOREIGN KEY([IdSuperior])
REFERENCES [dbo].[Usuarios] ([IdUsuario])
GO
ALTER TABLE [dbo].[Usuarios] CHECK CONSTRAINT [FK_ADMIN]
GO
ALTER TABLE [dbo].[Ventas]  WITH CHECK ADD  CONSTRAINT [FK__Ventas__IdClient__07C12930] FOREIGN KEY([IdCliente])
REFERENCES [dbo].[Clientes] ([IdCliente])
GO
ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK__Ventas__IdClient__07C12930]
GO
ALTER TABLE [dbo].[Ventas]  WITH CHECK ADD  CONSTRAINT [FK__Ventas__IdInmueb__06CD04F7] FOREIGN KEY([IdInmueble])
REFERENCES [dbo].[Inmuebles] ([IdInmuebles])
GO
ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK__Ventas__IdInmueb__06CD04F7]
GO
ALTER TABLE [dbo].[Ventas]  WITH CHECK ADD  CONSTRAINT [FK__Ventas__IdProyec__09A971A2] FOREIGN KEY([IdProyecto])
REFERENCES [dbo].[Proyectos] ([IdProyectos])
GO
ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK__Ventas__IdProyec__09A971A2]
GO
ALTER TABLE [dbo].[Ventas]  WITH CHECK ADD  CONSTRAINT [FK__Ventas__IdUsuari__08B54D69] FOREIGN KEY([IdUsuario])
REFERENCES [dbo].[Usuarios] ([IdUsuario])
GO
ALTER TABLE [dbo].[Ventas] CHECK CONSTRAINT [FK__Ventas__IdUsuari__08B54D69]
GO
