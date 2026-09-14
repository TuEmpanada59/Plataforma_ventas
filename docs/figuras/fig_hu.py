# Tarjetas de historias de usuario, con la estética de un tablero ágil.
AZUL = "#003A70"; CELESTE = "#0076E3"; GRIS = "#5A6472"; BORDE = "#D6DEE8"
VERDE = "#1EA851"; NARANJA = "#CC7700"

def envolver(texto, ancho):
    palabras, lineas, act = texto.split(), [], ""
    for p in palabras:
        if len(act) + len(p) + 1 <= ancho: act = (act + " " + p).strip()
        else: lineas.append(act); act = p
    if act: lineas.append(act)
    return lineas

def tarjeta(x, y, w, h, cod, titulo, como, quiero, para, prio):
    s = []
    s.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="#FFFFFF" stroke="{BORDE}" stroke-width="1.5"/>')
    s.append(f'<rect x="{x}" y="{y}" width="{w}" height="34" rx="10" fill="{AZUL}"/>')
    s.append(f'<rect x="{x}" y="{y+24}" width="{w}" height="10" fill="{AZUL}"/>')
    s.append(f'<text x="{x+12}" y="{y+22}" font-family="Segoe UI, Arial" font-size="12.5" font-weight="700" fill="#FFFFFF">{cod}</text>')
    s.append(f'<circle cx="{x+w-18}" cy="{y+17}" r="6" fill="{VERDE}"/>')
    ty = y + 52
    for ln in envolver(titulo, 30):
        s.append(f'<text x="{x+12}" y="{ty}" font-family="Segoe UI, Arial" font-size="13" font-weight="700" fill="{AZUL}">{ln}</text>')
        ty += 17
    ty += 6
    for etiqueta, txt in (("Como", como), ("Quiero", quiero), ("Para", para)):
        s.append(f'<text x="{x+12}" y="{ty}" font-family="Segoe UI, Arial" font-size="10" font-weight="700" fill="{CELESTE}">{etiqueta.upper()}</text>')
        ty += 14
        for ln in envolver(txt, 40):
            s.append(f'<text x="{x+12}" y="{ty}" font-family="Segoe UI, Arial" font-size="11" fill="{GRIS}">{ln}</text>')
            ty += 14
        ty += 4
    s.append(f'<line x1="{x+12}" y1="{y+h-30}" x2="{x+w-12}" y2="{y+h-30}" stroke="{BORDE}"/>')
    s.append(f'<text x="{x+12}" y="{y+h-12}" font-family="Segoe UI, Arial" font-size="10.5" fill="{GRIS}">Prioridad</text>')
    for i in range(5):
        col = NARANJA if i < prio else "#E5EAF0"
        s.append(f'<rect x="{x+68+i*13}" y="{y+h-21}" width="9" height="9" rx="2" fill="{col}"/>')
    s.append(f'<text x="{x+w-12}" y="{y+h-12}" text-anchor="end" font-family="Segoe UI, Arial" font-size="10.5" font-weight="700" fill="{VERDE}">Implementada</text>')
    return "\n".join(s)

def figura(nombre, subtitulo, tarjetas):
    W, H = 1180, 700
    cw, ch, gx, gy = 360, 270, 30, 28
    s = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}">',
         f'<rect width="{W}" height="{H}" fill="#F4F7FA"/>',
         f'<text x="30" y="40" font-family="Segoe UI, Arial" font-size="19" font-weight="700" fill="{AZUL}">Plataforma de Lanzamientos Inmobiliarios</text>',
         f'<text x="30" y="62" font-family="Segoe UI, Arial" font-size="13.5" fill="{GRIS}">{subtitulo}</text>',
         f'<line x1="30" y1="76" x2="{W-30}" y2="76" stroke="{BORDE}" stroke-width="1.5"/>']
    for i, t in enumerate(tarjetas):
        col, fila = i % 3, i // 3
        x = 30 + col * (cw + gx); y = 96 + fila * (ch + gy)
        s.append(tarjeta(x, y, cw, ch, *t))
    s.append('</svg>')
    open(f"/home/user/Plataforma_ventas/docs/figuras/{nombre}.svg", "w", encoding="utf-8").write("\n".join(s))

figura("figura_04_hu_autenticacion_inventario",
       "Historias de usuario — Autenticación, carga de inventario y control de estados", [
 ("HU-101", "Inicio de sesión", "usuario registrado", "iniciar sesión con mi usuario y contraseña",
  "acceder a las funciones de mi rol", 5),
 ("HU-102", "Bloqueo por intentos fallidos", "responsable de seguridad",
  "que la cuenta se bloquee tras varios intentos fallidos", "impedir ataques de fuerza bruta", 4),
 ("HU-104", "Autorización por roles", "propietario del sistema",
  "que cada pantalla valide el rol del usuario en sesión",
  "que nadie acceda a funciones que no le corresponden", 5),
 ("HU-301", "Carga masiva desde Excel", "administrador",
  "cargar el proyecto desde el archivo de lista de precios",
  "tener el inventario disponible sin digitarlo", 5),
 ("HU-401", "Grilla de inmuebles por proyecto", "administrador",
  "ver el inventario agrupado por área, torre y etapa",
  "conocer el estado y el precio de cada unidad", 4),
 ("HU-402", "Cambios de estado sin condiciones de carrera", "propietario del sistema",
  "que dos asesores no puedan tomar la misma unidad",
  "evitar que se venda dos veces un mismo inmueble", 5),
])

figura("figura_05_hu_venta_reservas",
       "Historias de usuario — Flujo de venta del asesor, reservas y cumplimiento", [
 ("HU-403", "Listas de precio con escalamiento", "administrador",
  "que la lista suba sola cada cierto número de ventas",
  "aplicar la política de precios sin intervención manual", 5),
 ("HU-502", "Tomar una unidad disponible", "asesor",
  "marcar como en proceso la unidad que estoy negociando",
  "que ningún otro asesor la ofrezca al mismo tiempo", 5),
 ("HU-503", "Reservar con precio bloqueado", "asesor",
  "reservar una unidad congelando su precio actual",
  "respetar el valor prometido aunque la lista suba", 5),
 ("HU-601", "Verificación SAGRILAFT previa", "oficial de cumplimiento",
  "confirmar la consulta antes de registrar al cliente",
  "cumplir la normativa antilavado", 5),
 ("HU-602", "Registrar la venta", "asesor",
  "registrar la venta con cliente nuevo o existente",
  "cerrar la operación con el precio correcto", 5),
 ("HU-404", "Administración de reservas", "administrador",
  "ver las reservas activas, su asesor y su observación",
  "hacer seguimiento a lo que está por cerrarse", 3),
])

figura("figura_06_hu_informes_asistencia",
       "Historias de usuario — Monitoreo, informes y cuadro de asistencia", [
 ("HU-801", "Tablero de control en tiempo real", "administrador",
  "ver los indicadores y el mapa del inventario en vivo",
  "seguir el avance del lanzamiento mientras ocurre", 4),
 ("HU-901", "Informe del lanzamiento en pantalla", "gerencia",
  "consultar el avance por torre, área y asesor",
  "decidir dónde concentrar el esfuerzo comercial", 4),
 ("HU-902", "Exportación a Excel", "administrador",
  "descargar el inventario y las ventas en Excel",
  "compartir y trabajar la información fuera del sistema", 3),
 ("HU-903", "Informe técnico en PDF", "gerencia",
  "generar un informe consolidado con la imagen corporativa",
  "presentar los resultados del evento", 3),
 ("HU-907", "Análisis de horas pico", "gerencia",
  "cruzar la afluencia con las ventas por franja horaria",
  "conocer en qué momentos del evento se concreta", 2),
 ("HU-1001", "Cuadro de asistencia", "administrador",
  "registrar las familias atendidas por día y franja",
  "medir la efectividad de la convocatoria", 3),
])
print("SVG de historias de usuario generados")
