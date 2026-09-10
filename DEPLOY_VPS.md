# Despliegue continuo (CI/CD) a VPS InterServer

Con esto, **cada `git push` a `master` despliega solo**. No vuelves a subir carpetas a mano.

GitHub compila la app y la envía por SSH al VPS, que solo la ejecuta (ideal para 2 GB de RAM).

---

## 1. Secretos en GitHub (una sola vez)

Repo en GitHub → **Settings → Secrets and variables → Actions → New repository secret**. Crea:

| Secreto | Valor |
|---|---|
| `VPS_HOST` | IP pública de tu VPS InterServer |
| `VPS_USER` | usuario SSH (ej. `root` o uno dedicado) |
| `VPS_SSH_KEY` | **clave privada** SSH (contenido completo, ver paso 2) |
| `VPS_PORT` | `22` (o el que uses) |

## 2. Llave SSH para que GitHub entre al VPS (una sola vez)

En tu PC (o en el VPS) genera un par de llaves **solo para deploy**:
```bash
ssh-keygen -t ed25519 -f deploy_key -N ""
```
- Sube la **pública** al VPS:
  ```bash
  ssh-copy-id -i deploy_key.pub usuario@IP_DEL_VPS
  # o manual: agrega el contenido de deploy_key.pub a ~/.ssh/authorized_keys en el VPS
  ```
- Copia el contenido de la **privada** `deploy_key` y pégalo en el secreto `VPS_SSH_KEY`.

## 3. Preparar el VPS (una sola vez)

```bash
# Runtime de ASP.NET Core 10 (Ubuntu)
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0   # si no está, usa el repo de Microsoft o el script dotnet-install

# Carpeta de la app
sudo mkdir -p /var/www/plataforma
sudo chown -R www-data:www-data /var/www/plataforma

# rsync (lo usa el deploy)
sudo apt-get install -y rsync
```

### 3.1 Variables/secretos en el VPS (NO van en git)
Crea `/etc/plataforma.env` con permisos restringidos:
```bash
sudo tee /etc/plataforma.env >/dev/null <<'EOF'
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000
ConnectionStrings__DefaultConnection=Server=tcp:dbqa.database.windows.net,1433;Initial Catalog=QA_DB;Persist Security Info=False;User ID=slezcano;Password=CAMBIA_ESTA_CLAVE;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
EOF
sudo chmod 600 /etc/plataforma.env
```
> Si la BD sigue en Azure SQL, abre el firewall de Azure SQL para la IP del VPS.

### 3.2 Servicio systemd (arranca sola y se reinicia)
```bash
sudo tee /etc/systemd/system/plataforma.service >/dev/null <<'EOF'
[Unit]
Description=Plataforma Ventas (ASP.NET Core)
After=network.target

[Service]
WorkingDirectory=/var/www/plataforma
ExecStart=/usr/bin/dotnet /var/www/plataforma/Plataforma_ventas.dll
EnvironmentFile=/etc/plataforma.env
Restart=always
RestartSec=5
User=www-data

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable plataforma
# (se iniciará/reiniciará automáticamente en cada deploy)
```
> El usuario `VPS_USER` del deploy necesita poder hacer `sudo systemctl restart plataforma` y `sudo rsync` sin contraseña. Para un usuario dedicado, agrega en `visudo`:
> `deploy ALL=(ALL) NOPASSWD: /bin/systemctl restart plataforma, /usr/bin/rsync, /bin/systemctl is-active plataforma`

### 3.3 Nginx como reverse proxy + HTTPS
```bash
sudo apt-get install -y nginx
sudo tee /etc/nginx/sites-available/plataforma >/dev/null <<'EOF'
server {
    listen 80;
    server_name TU_DOMINIO_O_IP;
    location / {
        proxy_pass         http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;          # WebSockets (SignalR)
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;       # necesario para HTTPS
    }
}
EOF
sudo ln -s /etc/nginx/sites-available/plataforma /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

# HTTPS gratis con Let's Encrypt (necesitas un dominio apuntando a la IP):
sudo apt-get install -y certbot python3-certbot-nginx
sudo certbot --nginx -d TU_DOMINIO
```

## 4. Ajuste de código necesario para el reverse proxy
Detrás de Nginx, Kestrel ve las peticiones como HTTP. Como las cookies son `Secure=Always`,
hay que activar **ForwardedHeaders** para que la app reconozca el HTTPS (si no, no podrás iniciar sesión).
Pídeme que lo aplique a `Program.cs` cuando vayas a migrar (son ~5 líneas).

---

## Resultado
1. Haces `git push` a `master`.
2. GitHub compila y despliega al VPS.
3. La app se reinicia con la versión nueva. **Sin subir nada a mano.**
