AZUL="#003A70"; CELESTE="#0076E3"; GRIS="#5A6472"; BORDE="#C9D4E0"; PK="#CC7700"; FK="#1EA851"
W,H = 1400, 1010

def tabla(x, y, nombre, campos, w=250):
    alto = 30 + len(campos)*17 + 8
    s=[f'<rect x="{x}" y="{y}" width="{w}" height="{alto}" rx="7" fill="#FFFFFF" stroke="{BORDE}" stroke-width="1.5"/>',
       f'<rect x="{x}" y="{y}" width="{w}" height="26" rx="7" fill="{AZUL}"/>',
       f'<rect x="{x}" y="{y+18}" width="{w}" height="8" fill="{AZUL}"/>',
       f'<text x="{x+9}" y="{y+18}" font-family="Consolas, Courier New" font-size="12.5" font-weight="700" fill="#FFFFFF">{nombre}</text>']
    cy = y + 43
    for campo, marca in campos:
        col = PK if marca=="PK" else FK if marca=="FK" else GRIS
        peso = "700" if marca in ("PK","FK") else "400"
        s.append(f'<text x="{x+9}" y="{cy}" font-family="Consolas, Courier New" font-size="10.5" font-weight="{peso}" fill="{col}">{(marca+" ") if marca else "   "}{campo}</text>')
        cy += 17
    return "\n".join(s), alto

def etiqueta(x, y, txt, color=CELESTE):
    return (f'<rect x="{x-9}" y="{y-13}" width="18" height="17" rx="3" fill="#F4F7FA"/>'
            f'<text x="{x}" y="{y}" text-anchor="middle" font-family="Segoe UI, Arial" '
            f'font-size="11" font-weight="700" fill="{color}">{txt}</text>')

def rel(puntos, co, cd, color=CELESTE, punteada=False):
    """Relación con ruteo ortogonal. co/cd: cardinalidad en origen y destino."""
    d = "M " + " L ".join(f"{x} {y}" for x, y in puntos)
    dash = ' stroke-dasharray="5 4"' if punteada else ''
    s=[f'<path d="{d}" stroke="{color}" stroke-width="1.7" fill="none"{dash} marker-end="url(#flecha)"/>']
    (x1,y1),(x2,y2) = puntos[0], puntos[-1]
    # La cardinalidad se dibuja por fuera de la caja, en el sentido de salida/llegada.
    dx1 = 14 if puntos[1][0] > x1 else -14 if puntos[1][0] < x1 else 0
    dy1 = 0 if dx1 else (14 if puntos[1][1] > y1 else -14)
    px1,py1 = (x1+dx1, y1-8) if dx1 else (x1+12, y1+dy1)
    dx2 = -18 if puntos[-2][0] < x2 else 18 if puntos[-2][0] > x2 else 0
    dy2 = 0 if dx2 else (-16 if puntos[-2][1] < y2 else 16)
    px2,py2 = (x2+dx2, y2-8) if dx2 else (x2+12, y2+dy2)
    s.append(etiqueta(px1, py1, co, color)); s.append(etiqueta(px2, py2, cd, color))
    return "\n".join(s)

s=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
   f'<defs><marker id="flecha" markerWidth="9" markerHeight="9" refX="8" refY="3" orient="auto">'
   f'<path d="M0,0 L0,6 L8,3 z" fill="{CELESTE}"/></marker></defs>',
   f'<rect width="{W}" height="{H}" fill="#F4F7FA"/>',
   f'<text x="34" y="40" font-family="Segoe UI, Arial" font-size="19" font-weight="700" fill="{AZUL}">Modelo entidad-relación — Plataforma de Lanzamientos Inmobiliarios</text>',
   f'<text x="34" y="62" font-family="Segoe UI, Arial" font-size="13" fill="{GRIS}">SQL Server · PK: clave primaria · FK: clave foránea</text>',
   f'<line x1="34" y1="76" x2="{W-34}" y2="76" stroke="{BORDE}" stroke-width="1.5"/>']

t,_=tabla(60,110,"Proyectos",[("IdProyectos","PK"),("Nombre",""),("TipProyecto",""),("CodigoAcceso",""),
    ("ListaActual",""),("ApartamentosPorLista",""),("HorasVigenciaReserva",""),("IdAdminCreador","FK"),
    ("FechaCarga",""),("Activo","")]); s.append(t)            # 110..316
t,_=tabla(560,110,"Inmuebles",[("IdInmuebles","PK"),("IdProyecto","FK"),("Apto",""),("Torre",""),("Etapa",""),
    ("Piso",""),("Metros",""),("Tipo",""),("Lista1 … Lista5",""),("Estado",""),("IdVendedorEnProceso","FK"),
    ("IdVendedorReserva","FK"),("PrecioReserva",""),("FechaReserva",""),("ObservacionReserva","")],260); s.append(t)  # 110..403
t,_=tabla(1080,110,"Usuarios",[("IdUsuario","PK"),("Nombre",""),("Apellido",""),("Celular",""),
    ("Usuario",""),("Contraseña",""),("Rol",""),("IdProyecto","FK")]); s.append(t)   # 110..284
t,_=tabla(60,400,"ProyectoAreaListas",[("IdProyecto","FK"),("Metros",""),("ListaActual",""),("AptsPorLista","")]); s.append(t)  # 400..506
t,_=tabla(60,560,"HistorialListas",[("IdHistorial","PK"),("IdProyecto","FK"),("Metros",""),
    ("ListaAnterior",""),("ListaNueva",""),("Motivo",""),("IdUsuario","FK"),("Fecha","")]); s.append(t)  # 560..734
t,_=tabla(560,470,"Ventas",[("IdVenta","PK"),("IdInmueble","FK"),("IdCliente","FK"),("IdUsuario","FK"),
    ("IdProyecto","FK"),("ListaAplicada",""),("PrecioVenta",""),("Destino",""),("Estado",""),
    ("Origen",""),("Observaciones",""),("MotivoAnulacion",""),("FechaVenta","")],260); s.append(t)  # 470..729
t,_=tabla(1080,340,"Clientes",[("IdCliente","PK"),("Nombre",""),("Apellido",""),("Documento",""),
    ("Celular",""),("Correo",""),("Direccion",""),("MedioPublicitario","")]); s.append(t)  # 340..514
t,_=tabla(1080,590,"MediosPublicitarios",[("IdMedio","PK"),("Nombre","")]); s.append(t)   # 590..662
t,_=tabla(60,800,"AjustesPrecio",[("IdAjuste","PK"),("IdProyecto","FK"),("Torre / Metros",""),
    ("Tipo / Valor",""),("Revertido","")]); s.append(t)       # 800..923
t,_=tabla(345,800,"AjustesPrecioDetalle",[("IdDetalle","PK"),("IdAjuste","FK"),("IdInmueble","FK"),
    ("NumLista",""),("PrecioAnterior",""),("PrecioNuevo","")],215); s.append(t)   # 800..940
t,_=tabla(620,800,"AsistenciaDia",[("IdDia","PK"),("IdEvento","FK"),("Fecha",""),("Familias","")],200); s.append(t)
t,_=tabla(880,800,"AsistenciaFranja",[("IdFranja","PK"),("IdDia","FK"),("HoraDesde / Hasta",""),("Familias","")],205); s.append(t)

s.append(rel([(310,180),(560,180)], "1","N"))                               # Proyectos → Inmuebles
s.append(rel([(1080,250),(825,250)], "1","N"))                              # Usuarios → Inmuebles
s.append(rel([(180,316),(180,400)], "1","N"))                               # Proyectos → ProyectoAreaListas
s.append(rel([(60,270),(32,270),(32,640),(60,640)], "1","N"))               # Proyectos → HistorialListas
s.append(rel([(60,240),(16,240),(16,860),(60,860)], "1","N"))               # Proyectos → AjustesPrecio
s.append(rel([(690,403),(690,470)], "1","1"))                               # Inmuebles → Ventas
s.append(rel([(1080,500),(825,500)], "1","N"))                              # Clientes → Ventas
s.append(rel([(1080,200),(1050,200),(1050,690),(825,690)], "1","N"))        # Usuarios → Ventas
s.append(rel([(310,862),(345,862)], "1","N"))                               # AjustesPrecio → Detalle
s.append(rel([(820,860),(880,860)], "1","N"))                               # AsistenciaDia → Franja
s.append(rel([(1160,590),(1160,514)], "N","1", GRIS, True))                 # Clientes ← catálogo de medios
s.append(f'<text x="1175" y="562" font-family="Segoe UI, Arial" font-size="10" font-style="italic" fill="{GRIS}">valida contra</text>')

s.append(f'<rect x="345" y="955" width="1020" height="42" rx="8" fill="#FFFFFF" stroke="{BORDE}"/>')
s.append(f'<text x="361" y="972" font-family="Segoe UI, Arial" font-size="11" font-weight="700" fill="{AZUL}">Nota</text>')
s.append(f'<text x="405" y="972" font-family="Segoe UI, Arial" font-size="11" fill="{GRIS}">El medio publicitario se almacena como texto en Clientes y se valida contra el catálogo: eliminar un medio no altera los clientes ya registrados.</text>')
s.append(f'<text x="361" y="989" font-family="Segoe UI, Arial" font-size="11" fill="{GRIS}">AsistenciaDia depende de AsistenciaEvento (un evento por lanzamiento), omitida por claridad.</text>')
s.append('</svg>')
open("/home/user/Plataforma_ventas/docs/figuras/figura_07_mer.svg","w",encoding="utf-8").write("\n".join(s))
print("MER regenerado")
