AZUL="#003A70"; CELESTE="#0076E3"; GRIS="#5A6472"; BORDE="#D6DEE8"
COL = {  # paleta de la aplicación
 "DISPONIBLE": ("#FBE3E5","#B02030"), "VENDIDO": ("#DDF5E4","#167A3D"),
 "EN PROCESO": ("#E4E4F7","#4A4AB0"), "RESERVADO": ("#FFEBD1","#A66000"),
}
# Datos de la imagen del cliente (estados traducidos a los de la aplicación)
lineas = [("A","INT","77.68"),("C","INT","65.56"),("E","INT","96.19"),
          ("B","EXT","55.58"),("D","EXT","65.36"),("F","EXT","77.24")]
D,V,P,R = "DISPONIBLE","VENDIDO","EN PROCESO","RESERVADO"
pisos = {
 27:[D,D,D, R,D,D], 26:[D,D,D, P,D,D], 25:[D,D,D, V,R,D], 24:[D,D,D, V,D,D],
 23:[D,D,D, V,D,D], 22:[D,D,D, R,D,D], 21:[D,D,D, P,V,D], 20:[D,D,D, P,R,D],
 19:[D,D,D, P,P,D], 18:[D,D,D, P,R,D], 17:[D,D,D, R,P,D], 16:[D,D,D, P,R,D],
 15:[D,D,D, R,V,D], 14:[V,D,D, V,R,D], 13:[R,D,D, R,R,D], 12:[D,D,D, P,R,D],
 11:[D,D,D, V,R,D], 10:[D,D,D, R,V,D], 9:[V,D,D, V,D,D], 8:[D,D,D, V,D,D],
 7:[V,D,D, V,D,D], 6:[P,D,D, R,D,D],
}
W = 1180; cw = 150; rh = 30; x0 = 90; y0 = 190
H = y0 + len(pisos)*rh + 60
s=[f'<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" font-family="Segoe UI, Arial">',
   f'<rect width="{W}" height="{H}" fill="#F4F7FA"/>',
   f'<rect x="20" y="20" width="{W-40}" height="{H-40}" rx="18" fill="#FFFFFF" stroke="{BORDE}"/>',
   f'<text x="44" y="56" font-size="17" font-weight="700" fill="{AZUL}">Mapa de ventas · Etapa 1 · Torre 1</text>',
   f'<text x="44" y="76" font-size="12" fill="{GRIS}">Una columna por línea de apartamentos, un renglón por piso. Se actualiza en vivo.</text>']
# leyenda
lx = 44
for est,(bg,fg) in COL.items():
    s.append(f'<rect x="{lx}" y="92" width="12" height="12" rx="3" fill="{bg}" stroke="{fg}"/>')
    s.append(f'<text x="{lx+17}" y="102" font-size="11" fill="{GRIS}">{est.title()}</text>'); lx += 24 + len(est)*6.5
# bandas INT / EXT
s.append(f'<rect x="{x0}" y="{y0-58}" width="{cw*3}" height="24" rx="6" fill="#E4EEF9"/>')
s.append(f'<text x="{x0+cw*1.5}" y="{y0-41}" text-anchor="middle" font-size="12" font-weight="700" fill="{AZUL}">INTERIOR</text>')
s.append(f'<rect x="{x0+cw*3+8}" y="{y0-58}" width="{cw*3-8}" height="24" rx="6" fill="#FFF0DC"/>')
s.append(f'<text x="{x0+cw*4.5}" y="{y0-41}" text-anchor="middle" font-size="12" font-weight="700" fill="#A66000">EXTERIOR</text>')
# cabeceras de columna
for i,(lin,tipo,m2) in enumerate(lineas):
    x = x0 + i*cw + (8 if tipo=="EXT" else 0)
    s.append(f'<rect x="{x}" y="{y0-30}" width="{cw-6}" height="26" rx="6" fill="{AZUL}"/>')
    s.append(f'<text x="{x+(cw-6)/2}" y="{y0-12}" text-anchor="middle" font-size="12" font-weight="700" fill="#FFFFFF">Línea {lin} · {m2} m²</text>')
s.append(f'<text x="{x0-12}" y="{y0-12}" text-anchor="end" font-size="11" font-weight="700" fill="{GRIS}">PISO</text>')
# celdas
y = y0
for piso, estados in pisos.items():
    s.append(f'<text x="{x0-12}" y="{y+20}" text-anchor="end" font-size="12" font-weight="700" fill="{GRIS}">{piso}</text>')
    for i,(lin,tipo,_) in enumerate(lineas):
        x = x0 + i*cw + (8 if tipo=="EXT" else 0)
        est = estados[i]; bg,fg = COL[est]
        s.append(f'<rect x="{x}" y="{y+2}" width="{cw-6}" height="{rh-4}" rx="6" fill="{bg}" stroke="{fg}" stroke-width="1"/>')
        s.append(f'<text x="{x+10}" y="{y+20}" font-size="12" font-weight="700" fill="{fg}">{piso}{lin}</text>')
        s.append(f'<text x="{x+cw-16}" y="{y+20}" text-anchor="end" font-size="10.5" font-weight="600" fill="{fg}">{est}</text>')
    y += rh
open("/home/user/Plataforma_ventas/docs/maquetas/mapa_por_piso.svg","w",encoding="utf-8").write("\n".join(s)+"</svg>")
