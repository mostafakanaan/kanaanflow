// KanaanFlow sync service on Cloudflare Workers + D1.
// A port of KanaanFlow.Sync (ASP.NET Core + Postgres) with the same /api contract, so the app needs no changes.
// Everything that isn't /api/* is the PWA, served from static assets.

export interface Env {
  DB: D1Database;
  ASSETS: Fetcher;
  /** Needed to create new users and to reset forgotten passwords. */
  KF_MASTER_PASSWORD?: string;
}

// ---------- Roles (KanaanFlow.Core/Finance/RoleRules.cs) ----------

type Role = "Owner" | "Staff" | "Viewer" | "Admin" | "Accountant";
// The enum's numbers, as older events and clients may send them.
const ROLE_NAMES: Role[] = ["Owner", "Staff", "Viewer", "Admin", "Accountant"];

function parseRole(value: unknown): Role | null {
  if (typeof value !== "string") return null;
  if ((ROLE_NAMES as string[]).includes(value)) return value as Role;
  if (/^\d+$/.test(value) && Number(value) < ROLE_NAMES.length) return ROLE_NAMES[Number(value)];
  return null;
}

const canRecord = (r: Role) => r !== "Viewer";
const canManageBook = (r: Role) => r === "Admin" || r === "Owner" || r === "Accountant";
const canManageMembers = canManageBook;
const canApprove = (r: Role) => r === "Admin" || r === "Accountant";
const canDelete = (r: Role) => r === "Admin" || r === "Accountant";
const canPurge = (r: Role) => r === "Admin";
const isAvailable = (r: Role) => r !== "Staff";
const canAssign = (actor: Role, role: Role) =>
  canManageMembers(actor) && isAvailable(role) && (actor === "Admin" || role !== "Admin");
const canEdit = (actor: Role, target: Role) => canManageMembers(actor) && (actor === "Admin" || target !== "Admin");

// ---------- Events (KanaanFlow.Core/Finance/Events) ----------

const KINDS = new Set([
  "entry.recorded", "entry.voided", "entry.approved", "entry.deleted", "entry.restored", "entry.purged",
  "contact.saved", "loan.opened", "loan.voided", "entryCategory.saved", "entryCategory.removed",
  "peopleCategory.saved", "peopleCategory.removed", "openingBalance.set", "salary.set", "member.saved", "member.removed",
]);

const MAX_PAGE = 500;
const SESSION_DAYS = 180;

interface Incoming {
  id: string;
  raw: string;
  at: string;
  atMs: number;
  deviceId: string;
  actor: string;
  kind: string;
  change: Record<string, any>;
  entryId: string | null;
  loanId: string | null;
  principal: boolean;
}

/** The entry a change is about (EntryChanges.EntryIdOf). */
function entryIdOf(kind: string, change: Record<string, any>): string | null {
  if (kind === "entry.recorded") return str(change.entry?.id) ?? "";
  if (["entry.voided", "entry.approved", "entry.deleted", "entry.restored", "entry.purged"].includes(kind)) return str(change.entryId) ?? "";
  return null;
}

/** Why a member may not upload a change, or null (KanaanFlow.Sync/Policy/ChangePolicy.cs). */
function checkPolicy(role: Role, memberId: string, e: Incoming, entryOwner: (id: string) => string | null, loanOwner: (id: string) => string | null): string | null {
  if (!canRecord(role)) return "view-only";
  if (e.kind === "member.saved" || e.kind === "member.removed") return "members-managed-by-server";
  if (e.kind === "entry.approved") return canApprove(role) ? null : "accountant-only";
  if (e.kind === "entry.deleted" || e.kind === "entry.restored") return canDelete(role) ? null : "delete-not-allowed";
  if (e.kind === "entry.purged") return canPurge(role) ? null : "admin-only";
  const salaryMonth = e.change.entry?.salaryMonth;
  if (e.kind === "salary.set" || (e.kind === "entry.recorded" && salaryMonth !== undefined && salaryMonth !== null)) {
    return canManageBook(role) ? null : "salaries-managers-only";
  }
  if (canManageBook(role)) return null;
  switch (e.kind) {
    case "entry.recorded":
    case "contact.saved":
    case "loan.opened":
      return null;
    case "entry.voided":
      return entryOwner(str(e.change.entryId) ?? "") === memberId ? null : "not-your-entry";
    case "loan.voided":
      return loanOwner(str(e.change.loanId) ?? "") === memberId ? null : "not-your-loan";
    default:
      return "owner-only";
  }
}

// ---------- Helpers ----------

const str = (v: unknown): string | null => (typeof v === "string" ? v : null);
const nowIso = () => new Date().toISOString();
const newId = () => crypto.randomUUID().replace(/-/g, "");
const isValidId = (id: unknown): id is string => typeof id === "string" && id.length > 0 && id.length <= 64 && /^[A-Za-z0-9-]+$/.test(id);
const sessionExpiry = () => new Date(Date.now() + SESSION_DAYS * 86400_000).toISOString();

const JSON_HEADERS = { "content-type": "application/json; charset=utf-8", "cache-control": "no-store", "x-content-type-options": "nosniff" };

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: JSON_HEADERS });
}

function rawJson(text: string, status = 200): Response {
  return new Response(text, { status, headers: JSON_HEADERS });
}

const error = (code: string, status = 400) => json({ error: code }, status);
const unauthorized = () => error("signed-out", 401);
const adminOnly = () => error("admin-only", 403);
const noContent = () => new Response(null, { status: 204, headers: { "cache-control": "no-store" } });

async function readJson(request: Request): Promise<Record<string, any>> {
  try {
    const body = await request.json();
    return body && typeof body === "object" && !Array.isArray(body) ? (body as Record<string, any>) : {};
  } catch {
    return {};
  }
}

/** Case-insensitive property lookup, like ASP.NET's JSON binding. */
function prop(body: Record<string, any>, name: string): any {
  if (name in body) return body[name];
  const key = Object.keys(body).find((k) => k.toLowerCase() === name.toLowerCase());
  return key === undefined ? undefined : body[key];
}

function checkName(name: unknown): string | null {
  const trimmed = (typeof name === "string" ? name : "").trim();
  return trimmed.length === 0 || trimmed.length > 80 ? "invalid-name" : null;
}

function checkPassword(password: unknown): string | null {
  return typeof password !== "string" || password.length < 8 || password.length > 200 ? "password-too-short" : null;
}

function checkRole(role: unknown): string | null {
  return parseRole(role) ? null : "invalid-role";
}

function checkAssign(me: Member, role: Role): string | null {
  if (!canManageMembers(me.role)) return "owner-only";
  if (!isAvailable(role)) return "role-unavailable";
  return canAssign(me.role, role) ? null : "cannot-manage-admin";
}

function checkEdit(me: Member, target: Role): string | null {
  if (!canManageMembers(me.role)) return "owner-only";
  return canEdit(me.role, target) ? null : "cannot-manage-admin";
}

// ---------- Passwords and tokens (KanaanFlow.Sync/Auth/PasswordHasher.cs) ----------
// "pbkdf2-sha256$iterations$salt$hash" with standard base64. Workers allow at most 100,000 PBKDF2 iterations;
// hashes keep their own count, so it can change later without breaking existing passwords.

const PBKDF2_ITERATIONS = 100_000;
const DUMMY_HASH = "pbkdf2-sha256$100000$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=";
const enc = new TextEncoder();

const toB64 = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes));
const fromB64 = (text: string) => Uint8Array.from(atob(text), (c) => c.charCodeAt(0));

async function pbkdf2(password: string, salt: Uint8Array, iterations: number, length: number): Promise<Uint8Array> {
  const key = await crypto.subtle.importKey("raw", enc.encode(password), "PBKDF2", false, ["deriveBits"]);
  const bits = await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt, iterations }, key, length * 8);
  return new Uint8Array(bits);
}

async function hashPassword(password: string): Promise<string> {
  const salt = crypto.getRandomValues(new Uint8Array(16));
  const hash = await pbkdf2(password, salt, PBKDF2_ITERATIONS, 32);
  return ["pbkdf2-sha256", String(PBKDF2_ITERATIONS), toB64(salt), toB64(hash)].join("$");
}

async function verifyPassword(password: string, stored: string): Promise<boolean> {
  const parts = stored.split("$");
  if (parts.length !== 4 || parts[0] !== "pbkdf2-sha256" || !/^\d+$/.test(parts[1])) return false;
  const iterations = Number(parts[1]);
  if (iterations < 1 || iterations > PBKDF2_ITERATIONS) return false;
  try {
    const salt = fromB64(parts[2]);
    const expected = fromB64(parts[3]);
    const actual = await pbkdf2(password, salt, iterations, expected.length);
    return timingSafeEqual(actual, expected);
  } catch {
    return false;
  }
}

function timingSafeEqual(a: Uint8Array, b: Uint8Array): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a[i] ^ b[i];
  return diff === 0;
}

async function sha256(text: string): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.digest("SHA-256", enc.encode(text)));
}

async function sameText(given: unknown, expected: string): Promise<boolean> {
  return timingSafeEqual(await sha256(typeof given === "string" ? given : ""), await sha256(expected));
}

function newToken(): string {
  return toB64(crypto.getRandomValues(new Uint8Array(32))).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
}

async function hashToken(token: string): Promise<string> {
  return [...(await sha256(token))].map((b) => b.toString(16).padStart(2, "0")).join("").toUpperCase();
}

// ---------- Store ----------

interface Member {
  id: string;
  name: string;
  role: Role;
  active: boolean;
}

const memberDto = (m: Member) => ({ id: m.id, name: m.name, role: m.role, active: m.active });

/** A server-made event, in the app's JSON shape (BookJsonContext: camelCase, enums as strings, "kind" first). */
function serverEvent(change: Record<string, unknown>, actor: string) {
  const at = nowIso();
  const id = newId();
  const payload = JSON.stringify({ id, atUtc: at, deviceId: "server", actor, change });
  return { id, at, kind: change.kind as string, payload };
}

function memberSavedEvent(m: Member, actor: string) {
  return serverEvent({ kind: "member.saved", member: { id: m.id, name: m.name, role: m.role, active: true } }, actor);
}

function insertServerEvent(db: D1Database, ev: { id: string; at: string; kind: string; payload: string }, memberId: string) {
  return db
    .prepare("insert or ignore into events (id, member_id, device_id, at, kind, payload, received_at) values (?, ?, 'server', ?, ?, ?, ?)")
    .bind(ev.id, memberId, ev.at, ev.kind, ev.payload, nowIso());
}

async function epochOf(db: D1Database): Promise<string> {
  return (await db.prepare("select epoch from book where id = 1").first<string>("epoch"))!;
}

function sessionInsert(db: D1Database, tokenHash: string, memberId: string) {
  const now = nowIso();
  return db
    .prepare("insert into sessions (token_hash, member_id, created_at, last_seen_at, expires_at) values (?, ?, ?, ?, ?)")
    .bind(tokenHash, memberId, now, now, sessionExpiry());
}

const isUniqueViolation = (e: unknown) => String((e as Error)?.message ?? e).includes("UNIQUE");

async function authenticate(request: Request, env: Env): Promise<Member | null> {
  const token = bearer(request);
  if (!token) return null;
  const hash = await hashToken(token);
  const now = nowIso();
  const row = await env.DB.prepare(
    "select m.id, m.name, m.role from sessions s join members m on m.id = s.member_id " +
      "where s.token_hash = ? and s.expires_at > ? and m.active = 1",
  )
    .bind(hash, now)
    .first<{ id: string; name: string; role: Role }>();
  if (row) {
    // Using a session extends it.
    await env.DB.prepare("update sessions set last_seen_at = ?, expires_at = ? where token_hash = ?").bind(now, sessionExpiry(), hash).run();
  }
  return row ? { id: row.id, name: row.name, role: parseRole(row.role) ?? "Viewer", active: true } : null;
}

function bearer(request: Request): string | null {
  const header = request.headers.get("authorization") ?? "";
  return header.startsWith("Bearer ") && header.length > 7 ? header.substring(7).trim() : null;
}

const clientIp = (request: Request) => request.headers.get("cf-connecting-ip") ?? "unknown";

/** A few sign-in attempts per minute per client, shared by every sign-in endpoint. */
async function tooManyAttempts(request: Request, env: Env): Promise<boolean> {
  const minute = Math.floor(Date.now() / 60_000);
  const row = await env.DB.prepare(
    "insert into signin_attempts (bucket, minute, count) values (?1, ?2, 1) " +
      "on conflict (bucket) do update set count = case when minute = ?2 then count + 1 else 1 end, minute = ?2 returning count",
  )
    .bind(clientIp(request), minute)
    .first<number>("count");
  if (Math.random() < 0.02) {
    await env.DB.prepare("delete from signin_attempts where minute < ?").bind(minute - 5).run();
  }
  return (row ?? 0) > 10;
}

// ---------- Endpoints ----------

async function register(request: Request, env: Env, master: string): Promise<Response> {
  const body = await readJson(request);
  if (!(await sameText(prop(body, "masterPassword"), master))) return error("wrong-master-password", 403);
  const name = prop(body, "name");
  const password = prop(body, "password");
  const problem = checkName(name) ?? checkPassword(password);
  if (problem) return error(problem);

  const db = env.DB;
  const wantedId = prop(body, "memberId");
  const taken = isValidId(wantedId) ? await db.prepare("select 1 as x from members where id = ?").bind(wantedId).first() : true;
  const admin: Member = { id: taken ? newId() : (wantedId as string), name: (name as string).trim(), role: "Admin", active: true };
  const token = newToken();
  try {
    await db.batch([
      db.prepare("insert into members (id, name, role, password_hash, created_at) values (?, ?, 'Admin', ?, ?)")
        .bind(admin.id, admin.name, await hashPassword(password), nowIso()),
      insertServerEvent(db, memberSavedEvent(admin, admin.id), admin.id),
      sessionInsert(db, await hashToken(token), admin.id),
    ]);
  } catch (e) {
    if (isUniqueViolation(e)) return error("name-taken", 409);
    throw e;
  }
  return json({ token, member: memberDto(admin), epoch: await epochOf(db) });
}

async function login(request: Request, env: Env): Promise<Response> {
  const body = await readJson(request);
  const name = String(prop(body, "name") ?? "").trim();
  const password = String(prop(body, "password") ?? "");
  const row = await env.DB.prepare("select id, name, role, password_hash from members where active = 1 and lower(name) = lower(?)")
    .bind(name)
    .first<{ id: string; name: string; role: Role; password_hash: string }>();
  if (!row || !(await verifyPassword(password, row.password_hash))) {
    // Spend the same time when the name is unknown, so names can't be probed.
    if (!row) await verifyPassword(password, DUMMY_HASH);
    return error("wrong-name-or-password", 401);
  }

  const token = newToken();
  await sessionInsert(env.DB, await hashToken(token), row.id).run();
  return json({ token, member: { id: row.id, name: row.name, role: row.role, active: true }, epoch: await epochOf(env.DB) });
}

async function resetPassword(request: Request, env: Env, master: string): Promise<Response> {
  const body = await readJson(request);
  if (!(await sameText(prop(body, "masterPassword"), master))) return error("wrong-master-password", 403);
  const newPassword = prop(body, "newPassword");
  const problem = checkPassword(newPassword);
  if (problem) return error(problem);

  const db = env.DB;
  const name = String(prop(body, "name") ?? "").trim();
  const row = await db.prepare("select id, name, role from members where active = 1 and lower(name) = lower(?)").bind(name).first<Member>();
  // Hash either way, so an unknown name takes as long as a real one.
  const hash = await hashPassword(newPassword);
  if (!row) return error("wrong-name-or-password", 401);

  const token = newToken();
  await db.batch([
    db.prepare("update members set password_hash = ? where id = ?").bind(hash, row.id),
    db.prepare("delete from sessions where member_id = ?").bind(row.id),
    sessionInsert(db, await hashToken(token), row.id),
  ]);
  console.warn(`Password of ${row.name} (${row.id}) was reset with the master password from ${clientIp(request)}.`);
  return json({ token, member: { id: row.id, name: row.name, role: row.role, active: true }, epoch: await epochOf(db) });
}

async function changeOwnPassword(request: Request, env: Env, me: Member): Promise<Response> {
  const body = await readJson(request);
  const next = prop(body, "new");
  const problem = checkPassword(next);
  if (problem) return error(problem);

  const db = env.DB;
  const hash = await db.prepare("select password_hash from members where id = ? and active = 1").bind(me.id).first<string>("password_hash");
  if (!hash || !(await verifyPassword(String(prop(body, "current") ?? ""), hash))) return error("wrong-password", 403);

  // Sign out the member's other devices, keep this one.
  await db.batch([
    db.prepare("update members set password_hash = ? where id = ?").bind(await hashPassword(next), me.id),
    db.prepare("delete from sessions where member_id = ? and token_hash <> ?").bind(me.id, await hashToken(bearer(request)!)),
  ]);
  return noContent();
}

async function listMembers(env: Env): Promise<Response> {
  const { results } = await env.DB.prepare("select id, name, role, active from members order by active desc, created_at").all<any>();
  return json(results.map((r) => ({ id: r.id, name: r.name, role: r.role, active: r.active === 1 })));
}

async function createMember(request: Request, env: Env, me: Member): Promise<Response> {
  const body = await readJson(request);
  const name = prop(body, "name");
  const roleText = prop(body, "role");
  const password = prop(body, "password");
  const problem = checkName(name) ?? checkRole(roleText) ?? checkPassword(password);
  if (problem) return error(problem);
  const role = parseRole(roleText)!;
  const denied = checkAssign(me, role);
  if (denied) return error(denied, 403);

  const db = env.DB;
  const member: Member = { id: newId(), name: (name as string).trim(), role, active: true };
  try {
    await db.batch([
      db.prepare("insert into members (id, name, role, password_hash, created_at) values (?, ?, ?, ?, ?)")
        .bind(member.id, member.name, role, await hashPassword(password), nowIso()),
      insertServerEvent(db, memberSavedEvent(member, me.id), me.id),
    ]);
  } catch (e) {
    if (isUniqueViolation(e)) return error("name-taken", 409);
    throw e;
  }
  return json(memberDto(member));
}

async function updateMember(request: Request, env: Env, me: Member, id: string): Promise<Response> {
  const body = await readJson(request);
  const roleText = prop(body, "role");
  const password = prop(body, "password");
  const activeValue = prop(body, "active");
  const problem = (roleText != null ? checkRole(roleText) : null) ?? (password != null ? checkPassword(password) : null);
  if (problem) return error(problem);

  const db = env.DB;
  const current = await db.prepare("select id, name, role, active from members where id = ?").bind(id).first<any>();
  if (!current) return error("not-found", 404);
  const currentRole = parseRole(current.role) ?? "Viewer";
  const wantedRole = roleText != null ? parseRole(roleText)! : currentRole;
  const denied = checkEdit(me, currentRole) ?? (wantedRole !== currentRole ? checkAssign(me, wantedRole) : null);
  if (denied) return error(denied, 403);

  const wasActive = current.active === 1;
  const active = typeof activeValue === "boolean" ? activeValue : wasActive;
  const losesAdmin = currentRole === "Admin" && wasActive && (wantedRole !== "Admin" || !active);
  if (losesAdmin) {
    const admins = await db.prepare("select count(*) as n from members where active = 1 and role = 'Admin'").first<number>("n");
    if ((admins ?? 0) <= 1) return error("last-admin", 409);
  }
  if (!active && id === me.id) return error("cannot-remove-self", 409);

  const statements: D1PreparedStatement[] = [
    db.prepare("update members set role = ?, active = ?, password_hash = coalesce(?, password_hash) where id = ?")
      .bind(wantedRole, active ? 1 : 0, password != null ? await hashPassword(password) : null, id),
  ];
  if (!active || password != null) {
    // Removed members, and anyone whose password was reset, sign in again everywhere.
    statements.push(db.prepare("delete from sessions where member_id = ?").bind(id));
  }
  if (!active && wasActive) {
    statements.push(insertServerEvent(db, serverEvent({ kind: "member.removed", memberId: id }, me.id), me.id));
  } else if (active && (wantedRole !== currentRole || !wasActive)) {
    statements.push(insertServerEvent(db, memberSavedEvent({ id, name: current.name, role: wantedRole, active: true }, me.id), me.id));
  }

  try {
    await db.batch(statements);
  } catch (e) {
    if (isUniqueViolation(e)) return error("name-taken", 409);
    throw e;
  }
  return noContent();
}

async function pullEvents(url: URL, env: Env): Promise<Response> {
  const after = Math.max(0, Number(url.searchParams.get("after") ?? 0) || 0);
  const limit = Math.min(Math.max(Number(url.searchParams.get("limit") ?? MAX_PAGE) || MAX_PAGE, 1), MAX_PAGE);
  // One batch is one consistent read, so a reset can't slip in between the epoch and the rows.
  const [epochRows, rows] = await env.DB.batch([
    env.DB.prepare("select epoch from book where id = 1"),
    env.DB.prepare("select seq, payload from events where seq > ? order by seq limit ?").bind(after, limit),
  ]);
  const epoch = (epochRows.results[0] as any).epoch as string;
  // Payloads go out exactly as stored, so amounts keep every digit.
  const events = (rows.results as { seq: number; payload: string }[]).map((r) => `{"seq":${r.seq},"event":${r.payload}}`);
  return rawJson(`{"epoch":${JSON.stringify(epoch)},"events":[${events.join(",")}]}`);
}

/** The elements of a JSON array as their original text, so numbers aren't rounded by JavaScript. */
function splitJsonArray(text: string): string[] | null {
  let i = 0;
  while (i < text.length && /\s/.test(text[i])) i++;
  if (text[i] !== "[") return null;
  i++;
  const items: string[] = [];
  let depth = 0;
  let start = -1;
  let inString = false;
  for (; i < text.length; i++) {
    const c = text[i];
    if (inString) {
      if (c === "\\") i++;
      else if (c === '"') inString = false;
      continue;
    }
    if (c === '"') {
      inString = true;
      if (start < 0) start = i;
    } else if (c === "{" || c === "[") {
      if (start < 0) start = i;
      depth++;
    } else if (c === "}" || c === "]") {
      if (depth === 0) {
        if (c !== "]") return null;
        if (start >= 0) items.push(text.slice(start, i).trim());
        return items;
      }
      depth--;
    } else if (c === "," && depth === 0) {
      if (start < 0) return null;
      items.push(text.slice(start, i).trim());
      start = -1;
    } else if (!/\s/.test(c) && start < 0) {
      start = i;
    }
  }
  return null;
}

function readIncoming(raw: string): Incoming | { id: string; unreadable: true } {
  let node: any;
  try {
    node = JSON.parse(raw);
  } catch {
    return { id: "", unreadable: true };
  }
  const id = str(node?.id) ?? "";
  const change = node?.change;
  const kind = str(change?.kind);
  const atText = str(node?.atUtc);
  const atMs = atText ? Date.parse(atText) : NaN;
  if (!change || typeof change !== "object" || !kind || !KINDS.has(kind) || Number.isNaN(atMs) || !isValidId(id)) {
    return { id, unreadable: true };
  }
  if (node.actor != null && typeof node.actor !== "string") return { id, unreadable: true };
  if (node.deviceId != null && typeof node.deviceId !== "string") return { id, unreadable: true };
  return {
    id,
    raw,
    at: new Date(atMs).toISOString(),
    atMs,
    deviceId: (str(node.deviceId) ?? "").slice(0, 64),
    actor: str(node.actor) ?? "",
    kind,
    change,
    entryId: entryIdOf(kind, change),
    loanId: kind === "loan.opened" ? str(change.loan?.id) ?? "" : null,
    principal: kind === "entry.recorded" && change.entry?.isLoanPrincipal === true,
  };
}

async function pushEvents(request: Request, env: Env, me: Member): Promise<Response> {
  const items = splitJsonArray(await request.text());
  if (!items || items.length > MAX_PAGE) return error("expected-array-of-events");

  const incoming: Incoming[] = [];
  const rejected: { id: string; reason: string }[] = [];
  for (const raw of items) {
    const e = readIncoming(raw);
    if ("unreadable" in e) rejected.push({ id: e.id, reason: "unreadable" });
    else incoming.push(e);
  }

  const db = env.DB;
  const ids = JSON.stringify(incoming.map((e) => e.id));
  const entryIds = JSON.stringify([...new Set(incoming.map((e) => e.entryId).filter((x): x is string => x !== null))]);
  const voidedEntries = incoming.filter((e) => e.kind === "entry.voided").map((e) => str(e.change.entryId) ?? "");
  const voidedLoans = incoming.filter((e) => e.kind === "loan.voided").map((e) => str(e.change.loanId) ?? "");

  // Every lookup in one consistent read.
  const [epochRows, knownRows, purgedRows, principalRows, entryOwnerRows, loanOwnerRows] = await db.batch([
    db.prepare("select epoch from book where id = 1"),
    db.prepare("select id from events where id in (select value from json_each(?))").bind(ids),
    db.prepare("select entry_id from events where kind = 'entry.purged' and entry_id in (select value from json_each(?))").bind(entryIds),
    db.prepare("select entry_id from events where kind = 'entry.recorded' and principal = 1 and entry_id in (select value from json_each(?))").bind(entryIds),
    db.prepare("select entry_id, member_id from events where kind = 'entry.recorded' and entry_id in (select value from json_each(?))").bind(JSON.stringify(voidedEntries)),
    db.prepare("select loan_id, member_id from events where kind = 'loan.opened' and loan_id in (select value from json_each(?))").bind(JSON.stringify(voidedLoans)),
  ]);

  // Devices from before epochs existed send none; everyone else must be on the current book.
  const epoch = (epochRows.results[0] as any).epoch as string;
  const deviceEpoch = request.headers.get("x-book-epoch");
  if (deviceEpoch && deviceEpoch !== epoch) return json({ error: "book-reset", epoch }, 409);

  const known = new Set((knownRows.results as any[]).map((r) => r.id as string));
  const purged = new Set((purgedRows.results as any[]).map((r) => r.entry_id as string));
  const principals = new Set((principalRows.results as any[]).map((r) => r.entry_id as string));
  const entryOwners = new Map<string, string>();
  for (const r of entryOwnerRows.results as any[]) if (!entryOwners.has(r.entry_id)) entryOwners.set(r.entry_id, r.member_id);
  const loanOwners = new Map<string, string>();
  for (const r of loanOwnerRows.results as any[]) if (!loanOwners.has(r.loan_id)) loanOwners.set(r.loan_id, r.member_id);

  const accepted: string[] = [];
  const toInsert: Incoming[] = [];
  const purges: string[] = [];
  const tomorrow = Date.now() + 86400_000;
  for (const e of incoming) {
    if (known.has(e.id)) {
      accepted.push(e.id);
      continue;
    }

    // Every change is tied to the person who made it, so nobody can record in someone else's name.
    const reason =
      e.atMs > tomorrow ? "time-in-future"
      : e.actor !== me.id ? "wrong-actor"
      : e.entryId !== null && purged.has(e.entryId) ? "entry-purged"
      : (e.kind === "entry.deleted" || e.kind === "entry.purged") && principals.has(e.entryId ?? "") ? "cannot-delete-loan-entry"
      : checkPolicy(me.role, me.id, e, (id) => entryOwners.get(id) ?? null, (id) => loanOwners.get(id) ?? null);
    if (reason) {
      rejected.push({ id: e.id, reason });
      continue;
    }

    toInsert.push(e);
    known.add(e.id);
    accepted.push(e.id);
    // Later events in the same upload see this one, as they would in the server's transaction.
    if (e.kind === "entry.recorded" && e.entryId !== null) {
      if (!entryOwners.has(e.entryId)) entryOwners.set(e.entryId, me.id);
      if (e.principal) principals.add(e.entryId);
    }
    if (e.kind === "loan.opened" && e.loanId !== null && !loanOwners.has(e.loanId)) loanOwners.set(e.loanId, me.id);
    if (e.kind === "entry.purged" && e.entryId !== null) {
      purged.add(e.entryId);
      purges.push(e.entryId);
    }
  }

  if (toInsert.length > 0) {
    // One statement for the whole upload: rows keep their order, so sequence numbers follow it.
    const rows = JSON.stringify(toInsert.map((e) => [e.id, e.deviceId, e.at, e.kind, e.raw, e.entryId, e.loanId, e.principal ? 1 : 0]));
    const statements = [
      db.prepare(
        "insert or ignore into events (id, member_id, device_id, at, kind, payload, received_at, entry_id, loan_id, principal) " +
          "select value ->> 0, ?1, value ->> 1, value ->> 2, value ->> 3, value ->> 4, ?2, value ->> 5, value ->> 6, value ->> 7 " +
          "from json_each(?3) order by key",
      ).bind(me.id, nowIso(), rows),
    ];
    if (purges.length > 0) {
      // A permanent delete removes the entry and every change about it, keeping only the purge tombstone.
      statements.push(
        db.prepare("delete from events where kind <> 'entry.purged' and entry_id in (select value from json_each(?))").bind(JSON.stringify(purges)),
      );
      console.warn(`${me.name} permanently deleted entries ${purges.join(", ")}.`);
    }
    await db.batch(statements);
  }

  return json({ accepted, rejected });
}

async function resetBook(request: Request, env: Env, me: Member): Promise<Response> {
  const body = await readJson(request);
  const db = env.DB;
  const hash = await db.prepare("select password_hash from members where id = ? and active = 1").bind(me.id).first<string>("password_hash");
  if (!hash || !(await verifyPassword(String(prop(body, "password") ?? ""), hash))) return error("wrong-password", 403);

  const removeMembers = prop(body, "removeMembers") === true;
  const keep = await hashToken(bearer(request)!);
  const oldEpoch = await epochOf(db);
  const newEpoch = newId();
  const now = nowIso();

  // The team list lived in the log too: announce the remaining members again in the empty book.
  const { results: active } = await db.prepare("select id, name, role from members where active = 1 order by created_at").all<any>();
  const team: Member[] = active
    .filter((m) => !removeMembers || m.id === me.id)
    .map((m) => ({ id: m.id, name: m.name, role: parseRole(m.role) ?? "Viewer", active: true }));

  const statements: D1PreparedStatement[] = [
    db.prepare(
      "insert into events_archive (seq, id, member_id, device_id, at, kind, payload, received_at, epoch, archived_at) " +
        "select seq, id, member_id, device_id, at, kind, payload, received_at, ?, ? from events",
    ).bind(oldEpoch, now),
    db.prepare("delete from events"),
    db.prepare("update book set epoch = ?, reset_at = ? where id = 1").bind(newEpoch, now),
  ];
  if (removeMembers) {
    statements.push(db.prepare("update members set active = 0 where active = 1 and id <> ?").bind(me.id));
    // Everyone else is signed out, the Admin's other devices included; this device stays signed in.
    statements.push(db.prepare("delete from sessions where token_hash <> ?").bind(keep));
  }
  for (const m of team) statements.push(insertServerEvent(db, memberSavedEvent(m, me.id), me.id));

  const results = await db.batch(statements);
  const archived = results[0].meta.changes ?? 0;
  const removed = removeMembers ? results[3].meta.changes ?? 0 : 0;
  console.warn(`${me.name} reset the book: ${archived} events archived under epoch ${oldEpoch}, ${removed} members removed.`);
  return json({ epoch: newEpoch, archivedEvents: archived, removedMembers: removed });
}

async function adminOverview(env: Env): Promise<Response> {
  const row = await env.DB.prepare(
    "select (select count(*) from members where active = 1) as members, (select count(*) from events) as events, " +
      "(select count(*) from sessions where expires_at > ?) as sessions, (select max(received_at) from events) as last",
  )
    .bind(nowIso())
    .first<any>();
  return json({ members: row.members, events: row.events, sessions: row.sessions, lastChangeUtc: row.last ?? null });
}

async function adminSessions(env: Env): Promise<Response> {
  const { results } = await env.DB.prepare(
    "select m.id, m.name, s.created_at, s.last_seen_at from sessions s join members m on m.id = s.member_id " +
      "where s.expires_at > ? and m.active = 1 order by s.last_seen_at desc limit 200",
  )
    .bind(nowIso())
    .all<any>();
  return json(results.map((r) => ({ memberId: r.id, name: r.name, createdUtc: r.created_at, lastSeenUtc: r.last_seen_at })));
}

async function adminActivity(url: URL, env: Env): Promise<Response> {
  const limit = Math.min(Math.max(Number(url.searchParams.get("limit") ?? 100) || 100, 1), MAX_PAGE);
  const { results } = await env.DB.prepare(
    "select e.seq, e.at, e.received_at, e.kind, e.device_id, m.name from events e join members m on m.id = e.member_id order by e.seq desc limit ?",
  )
    .bind(limit)
    .all<any>();
  return json(results.map((r) => ({ seq: r.seq, atUtc: r.at, receivedUtc: r.received_at, kind: r.kind, deviceId: r.device_id, name: r.name })));
}

// ---------- Routing ----------

const SIGNIN_LIMITED = new Set(["/api/register", "/api/login", "/api/reset-password", "/api/me/password", "/api/admin/reset"]);

async function handleApi(request: Request, env: Env, url: URL): Promise<Response> {
  const path = url.pathname.replace(/\/+$/, "") || "/";
  const method = request.method;
  // Without KF_MASTER_PASSWORD nobody can register or reset passwords: there is no public default here.
  const master = env.KF_MASTER_PASSWORD ?? "";

  if (method === "POST" && SIGNIN_LIMITED.has(path) && (await tooManyAttempts(request, env))) {
    return error("too-many-attempts", 429);
  }

  if (method === "GET" && path === "/api/health") {
    await env.DB.prepare("select 1").first();
    return json({ status: "ok" });
  }
  if (method === "GET" && path === "/api/status") {
    const row = await env.DB.prepare("select exists (select 1 from members) as ready, (select epoch from book where id = 1) as epoch").first<any>();
    return json({ ready: row.ready === 1, epoch: row.epoch });
  }
  if (method === "POST" && path === "/api/register") return master ? register(request, env, master) : error("wrong-master-password", 403);
  if (method === "POST" && path === "/api/login") return login(request, env);
  if (method === "POST" && path === "/api/reset-password") return master ? resetPassword(request, env, master) : error("wrong-master-password", 403);
  if (method === "POST" && path === "/api/logout") {
    const token = bearer(request);
    if (token) await env.DB.prepare("delete from sessions where token_hash = ?").bind(await hashToken(token)).run();
    return noContent();
  }

  // Everything below needs a signed-in member.
  const known =
    path === "/api/me" || path === "/api/me/password" || path === "/api/members" || path.startsWith("/api/members/") ||
    path.startsWith("/api/admin/") || path === "/api/events";
  if (!known) return error("not-found", 404);

  const me = await authenticate(request, env);
  if (!me) return unauthorized();

  if (method === "GET" && path === "/api/me") return json(memberDto(me));
  if (method === "POST" && path === "/api/me/password") return changeOwnPassword(request, env, me);
  if (method === "GET" && path === "/api/members") return listMembers(env);
  if (method === "POST" && path === "/api/members") return createMember(request, env, me);
  const memberMatch = path.match(/^\/api\/members\/([^/]+)$/);
  if (method === "PATCH" && memberMatch) return updateMember(request, env, me, decodeURIComponent(memberMatch[1]));
  if (method === "GET" && path === "/api/events") return pullEvents(url, env);
  if (method === "POST" && path === "/api/events") return pushEvents(request, env, me);

  if (path.startsWith("/api/admin/")) {
    if (me.role !== "Admin") return adminOnly();
    if (method === "GET" && path === "/api/admin/overview") return adminOverview(env);
    if (method === "GET" && path === "/api/admin/sessions") return adminSessions(env);
    if (method === "GET" && path === "/api/admin/activity") return adminActivity(url, env);
    if (method === "POST" && path === "/api/admin/reset") return resetBook(request, env, me);
    const signout = path.match(/^\/api\/admin\/members\/([^/]+)\/signout$/);
    if (method === "POST" && signout) {
      const id = decodeURIComponent(signout[1]);
      if (id === me.id) return error("cannot-remove-self", 409);
      const result = await env.DB.prepare("delete from sessions where member_id = ?").bind(id).run();
      return json({ ended: result.meta.changes ?? 0 });
    }
  }

  return error("not-found", 404);
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);
    if (url.pathname === "/api" || url.pathname.startsWith("/api/")) {
      try {
        return await handleApi(request, env, url);
      } catch (e) {
        console.error("API error", e);
        return error("server-error", 500);
      }
    }
    return env.ASSETS.fetch(request);
  },
} satisfies ExportedHandler<Env>;
