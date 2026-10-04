# Nginx TLS setup — Droplet bootstrap steps

`nginx/conf.d/sasms.conf` expects two files to already exist at
`/etc/nginx/certs/fullchain.pem` and `/etc/nginx/certs/privkey.pem` inside the `nginx`
container (mounted from `nginx/certs/` on the host via `docker-compose.yml`). That folder is
gitignored — nothing real is committed — so a fresh checkout has nothing there yet, and Nginx
will refuse to start the HTTPS server block until something is placed there. This is
deliberate; do it in two steps, not one, because a domain won't exist to validate against
until *after* the Droplet is actually created.

## Step 1 — temporary self-signed certificate (get the stack running at all)

Before a domain points at the Droplet, generate a throwaway self-signed certificate just so
`docker compose up` succeeds and you can confirm the whole stack (web, face-service, mysql,
nginx) actually starts and the app is reachable over HTTPS (with a browser warning, which is
expected and fine at this stage):

```bash
mkdir -p nginx/certs
openssl req -x509 -nodes -days 365 \
  -newkey rsa:2048 \
  -keyout nginx/certs/privkey.pem \
  -out nginx/certs/fullchain.pem \
  -subj "/CN=sasms-temp"
```

## Step 2 — real Let's Encrypt certificate (once a domain points at the Droplet)

Once you have a real domain/subdomain with DNS pointed at the Droplet's IP, replace the
self-signed files with a real certificate. The simplest approach that doesn't require adding
a fifth container just for this temporary FYP deployment: run certbot once, directly on the
Droplet host (not in Compose), in standalone mode while briefly stopping Nginx so port 80 is
free for the ACME HTTP-01 challenge:

```bash
# On the Droplet, with the compose stack already running:
docker compose stop nginx
sudo apt-get install -y certbot
sudo certbot certonly --standalone -d your-domain.example.com
# certbot writes to /etc/letsencrypt/live/your-domain.example.com/{fullchain,privkey}.pem
sudo cp /etc/letsencrypt/live/your-domain.example.com/fullchain.pem nginx/certs/fullchain.pem
sudo cp /etc/letsencrypt/live/your-domain.example.com/privkey.pem   nginx/certs/privkey.pem
docker compose start nginx
```

Also update `nginx/conf.d/sasms.conf`'s two `server_name _;` lines to the real domain at that
point (not strictly required — `_` matches any Host header — but worth doing for clarity and
so future redirects/HSTS are scoped to the real domain).

Let's Encrypt certificates expire every 90 days — for a *temporary* FYP deployment this is
likely a non-issue (manually re-run the two `certbot`/`cp` lines above if the demo period
runs long), but note it rather than silently assume the deployment is permanent.
