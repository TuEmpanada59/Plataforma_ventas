AZUL="#003A70"; CELESTE="#0076E3"; GRIS="#5A6472"; BORDE="#C9D4E0"; VERDE="#1EA851"; NARANJA="#CC7700"
W,H = 1320, 900

def caja(x,y,w,h,titulo,lineas,color=AZUL,relleno="#FFFFFF",fs=11):
    s=[f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="9" fill="{relleno}" stroke="{color}" stroke-width="1.8"/>',
       f'<text x="{x+w/2}" y="{y+23}" text-anchor="middle" font-family="Segoe UI, Arial" font-size="12.5" font-weight="700" fill="{color}">{titulo}</text>']
    cy=y+44
    for ln in lineas:
        s.append(f'<text x="{x+w/2}" y="{cy}" text-anchor="middle" font-family="Segoe UI, Arial" font-size="{fs}" fill="{GRIS}">{ln}</text>')
        cy+=16
    return "\n".join(s)

def ruta(puntos, texto="", color=CELESTE, punteada=False):
    d = "M " + " L ".join(f"{x} {y}" for x,y in puntos)
    dash = ' stroke-dasharray="6 4"' if punteada else ''
    s=[f'<path d="{d}" stroke="{color}" stroke-width="2" fill="none"{dash} marker-end="url(#f2)"/>']
    if texto:
        # La etiqueta se ancla al primer tramo, con fondo para que nada se cruce encima.
        (x1,y1),(x2,y2) = puntos[0], puntos[1]
        mx,my = (x1+x2)/2, (y1+y2)/2
        an = len(texto)*6.6 + 10
        s.append(f'<rect x="{mx-an/2}" y="{my-19}" width="{an}" height="18" rx="4" fill="#F4F7FA"/>')
        s.append(f'<text x="{mx}" y="{my-6}" text-anchor="middle" font-family="Segoe UI, Arial" font-size="10.5" font-weight="700" fill="{color}">{texto}</text>')
    return "\n".join(s)

s=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
   f'<defs><marker id="f2" markerWidth="9" markerHeight="9" refX="8" refY="3" orient="auto">'
   f'<path d="M0,0 L0,6 L8,3 z" fill="{CELESTE}"/></marker>'
   f'<marker id="f3" markerWidth="9" markerHeight="9" refX="8" refY="3" orient="auto">'
   f'<path d="M0,0 L0,6 L8,3 z" fill="{VERDE}"/></marker></defs>',
   f'<rect width="{W}" height="{H}" fill="#F4F7FA"/>',
   f'<text x="34" y="40" font-family="Segoe UI, Arial" font-size="19" font-weight="700" fill="{AZUL}">Arquitectura de la aplicación</text>',
   f'<text x="34" y="62" font-family="Segoe UI, Arial" font-size="13" fill="{GRIS}">ASP.NET Core 10 · Patrón Modelo-Vista-Controlador · Microsoft Azure</text>',
   f'<line x1="34" y1="76" x2="{W-34}" y2="76" stroke="{BORDE}" stroke-width="1.5"/>']

s.append(f'<text x="60" y="118" font-family="Segoe UI, Arial" font-size="11.5" font-weight="700" fill="{GRIS}">CLIENTES</text>')
s.append(caja(60,130,250,96,"Navegador del administrador",["Panel de control, inventario,","precios, clientes e informes"]))
s.append(caja(60,250,250,96,"Navegador del asesor",["Inventario del proyecto,","reservas y registro de ventas"]))

s.append(f'<rect x="400" y="100" width="560" height="560" rx="14" fill="#EAF1F9" stroke="{CELESTE}" stroke-width="2"/>')
s.append(f'<text x="680" y="128" text-anchor="middle" font-family="Segoe UI, Arial" font-size="13.5" font-weight="700" fill="{AZUL}">Azure App Service — ASP.NET Core 10</text>')
s.append(caja(430,150,500,92,"Capa de presentación · Vistas Razor",
    ["37 vistas · 2 plantillas compartidas (administrador / asesor)","CSS corporativo común"],CELESTE))
s.append(caja(430,258,500,112,"Capa de control · 10 controladores",
    ["Account · Carga · Inmuebles · Vendedor · Ventas","Clientes · Usuarios · Dashboard · Reportes · Home",
     "Filtro de autorización por rol · Antiforgery"],CELESTE))
# El hub va a la izquierda para que su notificación salga hacia los navegadores sin cruzar nada.
s.append(caja(430,386,240,112,"Hub SignalR",
    ["Difusión de cambios de","estado en tiempo real","a los navegadores"],CELESTE))
s.append(caja(690,386,240,112,"Servicios",
    ["Correo (MailKit)","Bloqueo de cuentas","Auditoría"],CELESTE))
s.append(caja(430,514,500,92,"Acceso a datos · ADO.NET",
    ["Consultas parametrizadas · Transacciones","Actualizaciones atómicas con estado esperado"],CELESTE))

s.append(f'<text x="1010" y="118" font-family="Segoe UI, Arial" font-size="11.5" font-weight="700" fill="{GRIS}">SERVICIOS EXTERNOS Y DATOS</text>')
s.append(caja(1010,262,250,100,"Generación de archivos",["EPPlus — Excel","QuestPDF — PDF"],NARANJA))
s.append(caja(1010,400,250,84,"Servidor SMTP",["Recuperación de","contraseña"],NARANJA))
s.append(caja(1010,530,250,116,"Azure SQL Database",
    ["Proyectos · Inmuebles · Ventas","Clientes · Usuarios · Listas","Asistencia · Trazabilidad"],VERDE))

s.append(ruta([(310,178),(430,190)], "HTTPS"))
s.append(ruta([(310,298),(430,300)], "HTTPS"))
s.append(ruta([(930,312),(1010,312)], "genera"))
s.append(ruta([(930,442),(1010,442)], "SMTP"))
s.append(ruta([(930,560),(1010,560)], "SQL"))
# Notificación en tiempo real: sale del hub y llega al navegador del asesor sin cruzar cajas.
s.append(f'<path d="M 430 470 L 370 470 L 370 320 L 313 320" stroke="{VERDE}" stroke-width="2" fill="none" stroke-dasharray="6 4" marker-end="url(#f3)"/>')
s.append(f'<rect x="282" y="486" width="176" height="18" rx="4" fill="#F4F7FA"/>')
s.append(f'<text x="370" y="499" text-anchor="middle" font-family="Segoe UI, Arial" font-size="10.5" font-weight="700" fill="{VERDE}">WebSocket (tiempo real)</text>')

s.append(f'<path d="M 680 660 L 680 700" stroke="{GRIS}" stroke-width="2" fill="none" marker-end="url(#f2)"/>')
s.append(caja(400,700,560,86,"Integración y despliegue continuos",
    ["Git → GitHub Actions → Azure App Service","Compilación, publicación y despliegue automáticos en cada cambio"],GRIS))

s.append(f'<rect x="1010" y="690" width="250" height="150" rx="9" fill="#FFFFFF" stroke="{BORDE}"/>')
s.append(f'<text x="1026" y="713" font-family="Segoe UI, Arial" font-size="11.5" font-weight="700" fill="{AZUL}">Seguridad transversal</text>')
for i,ln in enumerate(["BCrypt (factor 12)","Bloqueo por fuerza bruta","Consultas parametrizadas",
                       "Antiforgery en cada POST","Cabeceras HTTP endurecidas","Sesión de 20 minutos"]):
    s.append(f'<text x="1026" y="{736+i*17}" font-family="Segoe UI, Arial" font-size="10.5" fill="{GRIS}">· {ln}</text>')

s.append(f'<rect x="60" y="700" width="280" height="86" rx="9" fill="#FFFFFF" stroke="{BORDE}"/>')
s.append(f'<text x="76" y="722" font-family="Segoe UI, Arial" font-size="11.5" font-weight="700" fill="{AZUL}">Convenciones</text>')
s.append(f'<line x1="76" y1="740" x2="112" y2="740" stroke="{CELESTE}" stroke-width="2"/>')
s.append(f'<text x="120" y="744" font-family="Segoe UI, Arial" font-size="10.5" fill="{GRIS}">Petición HTTP / consulta</text>')
s.append(f'<line x1="76" y1="764" x2="112" y2="764" stroke="{VERDE}" stroke-width="2" stroke-dasharray="6 4"/>')
s.append(f'<text x="120" y="768" font-family="Segoe UI, Arial" font-size="10.5" fill="{GRIS}">Notificación del servidor</text>')
s.append('</svg>')
open("/home/user/Plataforma_ventas/docs/figuras/figura_08_arquitectura.svg","w",encoding="utf-8").write("\n".join(s))
