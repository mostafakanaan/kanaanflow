# Hosting KanaanFlow on Cloudflare

KanaanFlow runs at **https://flow.kanaan-tricot.com** as one Cloudflare Worker (`kanaanflow-flow`):

```
phone ──https──▶ Cloudflare Worker ──┬── /api/*  → sync API (KanaanFlow.Cloudflare/src/index.ts) ──▶ D1 database "kanaanflow"
                                     └── else    → the PWA (static files from dotnet publish KanaanFlow.Web)
```

- **Sync API**: a TypeScript port of `KanaanFlow.Sync` with the same `/api` contract, so the app is unchanged.
  Roles, the change policy, book resets and permanent deletes behave as on the Pi.
- **Database**: Cloudflare D1 (SQLite), schema in `KanaanFlow.Cloudflare/migrations/`. Event payloads are stored exactly
  as the app sent them, so amounts keep every digit.
- **Passwords**: PBKDF2-SHA256 with 100,000 iterations (the most Workers allow), same stored format as before.
- **Master password**: the `KF_MASTER_PASSWORD` secret on the Worker. It's needed to register (new users become Admins)
  and to reset a forgotten password. There is no public default; without it nobody can register.
  Change it with `npx wrangler secret put KF_MASTER_PASSWORD` in `KanaanFlow.Cloudflare/`.

## Deploys

Every push to `main` runs `.github/workflows/deploy-cloudflare.yml`: tests, builds the PWA, tests the sync API against a
local D1, applies new migrations and deploys. It needs two repository secrets (*Settings › Secrets and variables › Actions*):

| Secret | Value |
| --- | --- |
| `CLOUDFLARE_API_TOKEN` | A Cloudflare API token with **Workers Scripts: Edit** and **D1: Edit** for the account |
| `CLOUDFLARE_ACCOUNT_ID` | `68c8837facf88fb3fddb3a4ccfc35c6c` |

Without them the workflow builds and tests but skips the deploy.

## Local development

```bash
cd KanaanFlow.Cloudflare
npm install
printf 'KF_MASTER_PASSWORD=dev-master-password\n' > .dev.vars
npx wrangler d1 migrations apply kanaanflow --local
dotnet publish ../KanaanFlow.Web -c Release -o ../out && cp -r ../out/wwwroot public
npx wrangler dev                     # app and API on http://localhost:8787
BASE=http://localhost:8787 MASTER=dev-master-password node test/api.test.mjs   # on a fresh local database
```

## Schema changes

Add a new file in `KanaanFlow.Cloudflare/migrations/` (for example `0002_something.sql`); the deploy applies it.
Never edit a migration that has already been applied.

## Backups

D1 keeps 30 days of point-in-time history (Time Travel): `npx wrangler d1 time-travel restore kanaanflow --timestamp=...`.
For a file copy: `npx wrangler d1 export kanaanflow --remote --output=kanaanflow.sql`.

The Raspberry Pi setup (`docs/hosting-pi.md`, `deploy/`, `KanaanFlow.Sync`) is kept; its workflow now only runs by hand.
