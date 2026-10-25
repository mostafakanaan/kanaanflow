# Hosting KanaanFlow on the Raspberry Pi

The app is a set of static files. The Pi builds them, nginx serves them on `127.0.0.1:8081`, and the
Cloudflare Tunnel publishes them over HTTPS (installing the app and working offline both need HTTPS).
It's the same setup as the portfolio, which keeps serving on its own port.

```
phone ──https──▶ Cloudflare ──tunnel──▶ cloudflared (Pi) ──▶ nginx 127.0.0.1:8081 ──▶ /var/www/kanaanflow
```

## One-time setup

1. **Add a runner for this repo on the Pi.** Runners on a personal account belong to one repo, so the
   portfolio's runner won't pick up KanaanFlow jobs. On GitHub: *kanaanflow › Settings › Actions ›
   Runners › New self-hosted runner* (Linux, ARM64). On the Pi, follow the commands it shows in a
   **new folder** (for example `~/actions-runner-kanaanflow`), then install it as a service:

   ```bash
   sudo ./svc.sh install && sudo ./svc.sh start
   ```

   The workflow uses `sudo` for `rsync`, `install`, `nginx` and `systemctl`, the same as the portfolio.
   If the portfolio's runner user already has passwordless sudo, run this runner as the same user.

2. **Make sure `rsync` is installed:** `sudo apt install -y rsync`. The .NET SDK doesn't need to be
   installed by hand: `actions/setup-dotnet` installs it into the runner's tool cache.

3. **Add a public hostname to the tunnel** in Cloudflare (*Zero Trust › Networks › Tunnels › your
   tunnel › Public hostname*), for example `flow.<your-domain>` → `HTTP` → `localhost:8081`.
   If the tunnel is configured from `config.yml` instead, add above the catch-all rule:

   ```yaml
   - hostname: flow.<your-domain>
     service: http://localhost:8081
   ```

   then restart cloudflared: `sudo systemctl restart cloudflared`.

4. **Cloudflare settings for that hostname:** keep *Rocket Loader* off, because it rewrites scripts and
   breaks the app.

5. **Deploy:** push to `main`, or run *Actions › Build & Deploy (Self-hosted Pi) › Run workflow*.

## Installing on phones

Open `https://flow.<your-domain>`:

- **Android / Windows (Chrome, Edge):** *More › Install the app*, or the install icon in the address bar.
- **iPhone (Safari):** *Share › Add to Home Screen*.

After a deploy, an installed app picks up the new version the next time it is fully closed and reopened.

## Data on the Pi

Nothing is stored on the Pi yet: each device keeps its own copy of the book in the browser. Sync
(currently Supabase, see `sync-supabase.sql`) is what shares it between devices and acts as the backup.
