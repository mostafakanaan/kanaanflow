# Hosting KanaanFlow on the Raspberry Pi

The app is a set of static files. The Pi builds them, nginx serves them on `127.0.0.1:18081`, and the
Cloudflare Tunnel publishes them over HTTPS (installing the app and working offline both need HTTPS).
It's the same setup as the portfolio, which keeps serving on its own port. (Port 8081 is taken on this Pi by
`llama-server`, so KanaanFlow uses 18081.)

```
phone ──https──▶ Cloudflare ──tunnel──▶ cloudflared (Pi) ──▶ nginx 127.0.0.1:18081 ──▶ /var/www/kanaanflow
```

## One-time setup

**Quick way:** `deploy/setup-pi.sh` does steps 1–3 for you. Get a runner token (step 1 below), then on the Pi:

```bash
curl -fsSL https://raw.githubusercontent.com/mostafakanaan/kanaanflow/main/deploy/setup-pi.sh \
  | bash -s -- --token <RUNNER_TOKEN> --hostname flow.<your-domain>
```

Or step by step:

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
   tunnel › Public hostname*), for example `flow.<your-domain>` → `HTTP` → `localhost:18081`.
   If the tunnel is configured from `config.yml` instead, add above the catch-all rule:

   ```yaml
   - hostname: flow.<your-domain>
     service: http://localhost:18081
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

## Team sync and the database

Every deploy also starts two Docker containers (`deploy/docker-compose.yml`):

- **kanaanflow-db**: PostgreSQL 17. Only reachable inside Docker; its data lives in the `kanaanflow_pgdata` volume.
- **kanaanflow-sync**: the sync API (`KanaanFlow.Sync`) on `127.0.0.1:18082`, which nginx serves at `/api/`.

The first deploy installs Docker if it's missing and creates a random database password in
`/etc/kanaanflow/db.env`. It stays on the Pi; never delete that file, or the database can't be opened.

### Database design

| Table | What it holds |
|---|---|
| `members` | Team members: name, role (Admin / Owner / Accountant / Staff / Viewer), password hash (PBKDF2), active flag. |
| `sessions` | Sign-in tokens (stored only as SHA-256 hashes), one per signed-in device, 180 days sliding. |
| `events` | The append-only log: `seq` (server order), `id` (from the device, so re-sending is harmless), `member_id` (who uploaded it), `device_id`, `at`, `kind`, `payload` (the change as JSON). Rows are never updated or deleted. |

Devices upload their new events and download everything after the last `seq` they have. Every upload is
checked against the member's role, and must be made by the member uploading it (so entries can't be recorded
in someone else's name):

| Role | Can do |
|---|---|
| Admin | Everything, including other Admins and the admin panel (devices, activity log, backup). Can always reset anyone's password. |
| Owner | All entries, categories, entities and opening balances; adds and changes users except Admins. Their entries show "awaiting approval" until an Accountant approves them (informative only). |
| Accountant | Same as an Owner, and approves Owners' entries. |
| Staff (Employee) | Not offered yet. Existing Staff members record entries and undo only their own. |
| Viewer (Read-only) | Sees balances and reports; can't upload anything. |

Team changes only happen through the server. On the first start after this update, the oldest Owner of an
existing book becomes its Admin.

### First time

1. On your phone, open the app and choose **New user**. Pick a name and password and type the master password
   (`WelcomeToKanaanFlow` unless `KF_MASTER_PASSWORD` is set in `/etc/kanaanflow/db.env`). You become an Admin.
   Anything already on that device is uploaded.
2. Under **More › Users**, add each member with a name, role and password, and give them both.
3. They open the app and choose **Sign in**.

Forgot a password? On the login screen, tap **Forgot password?**, then enter the name, a new password and the
master password. That person is signed out on their other devices. Admins can also reset anyone's password
under **More › Users**.

### Backups

`kanaanflow-backup.timer` runs `pg_dump` every night at 03:30 into `/var/backups/kanaanflow/`, keeping 14 days.
Run one now with `sudo kanaanflow-backup`. Copying that folder somewhere else (another disk, cloud storage)
protects against losing the Pi itself. Every signed-in phone also keeps a full copy of the book.

Restore a backup:

```bash
sudo docker exec -i kanaanflow-db pg_restore -U kanaanflow -d kanaanflow --clean < /var/backups/kanaanflow/kanaanflow-YYYY-MM-DD.dump
```

### Useful commands

```bash
sudo docker compose -p kanaanflow ps          # are both containers up?
sudo docker logs kanaanflow-sync --tail 50    # sync service log
curl http://127.0.0.1:18082/api/health         # {"status":"ok"}
```
