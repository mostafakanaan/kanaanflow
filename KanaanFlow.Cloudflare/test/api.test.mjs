// End-to-end checks of the sync API against `wrangler dev` (local D1). Usage: BASE=http://127.0.0.1:8787 node test/api.test.mjs
import assert from "node:assert/strict";

const BASE = process.env.BASE ?? "http://127.0.0.1:8787";
const MASTER = process.env.MASTER ?? "test-master-password";
let ip = 1;
async function call(method, path, { token, body, raw, epoch, fromIp } = {}) {
  const headers = { "content-type": "application/json", "cf-connecting-ip": fromIp ?? `10.0.0.${ip++ % 250}` };
  if (token) headers.authorization = `Bearer ${token}`;
  if (epoch) headers["x-book-epoch"] = epoch;
  const res = await fetch(BASE + path, { method, headers, body: raw ?? (body ? JSON.stringify(body) : undefined) });
  const text = await res.text();
  return { status: res.status, text, body: text && res.headers.get("content-type")?.includes("json") ? JSON.parse(text) : null };
}
const ev = (actor, change, id = crypto.randomUUID()) => ({ id, atUtc: new Date().toISOString(), deviceId: "dev1", actor, change });
let passed = 0;
async function test(name, fn) { await fn(); passed++; console.log("ok -", name); }

const status0 = await call("GET", "/api/status");
await test("status before anyone registers", () => { assert.equal(status0.status, 200); assert.equal(status0.body.ready, false); assert.ok(status0.body.epoch); });
await test("health", async () => assert.deepEqual((await call("GET", "/api/health")).body, { status: "ok" }));
await test("app shell is served for app routes", async () => { const r = await call("GET", "/people/123"); assert.equal(r.status, 200); assert.match(r.text, /app shell/); });
await test("unknown api path is JSON 404", async () => assert.equal((await call("GET", "/api/nope")).status, 404));

await test("register needs the master password", async () => {
  const r = await call("POST", "/api/register", { body: { name: "Mostafa", password: "password1", masterPassword: "wrong", memberId: "dev-member-1" } });
  assert.equal(r.status, 403); assert.equal(r.body.error, "wrong-master-password");
});
await test("register rejects short passwords", async () => {
  const r = await call("POST", "/api/register", { body: { name: "Mostafa", password: "short", masterPassword: MASTER } });
  assert.equal(r.body.error, "password-too-short");
});
const reg = await call("POST", "/api/register", { body: { name: "Mostafa", password: "password1", masterPassword: MASTER, memberId: "dev-member-1" } });
const admin = reg.body;
await test("register makes an Admin, keeps the device's member id", () => {
  assert.equal(reg.status, 200); assert.equal(admin.member.role, "Admin"); assert.equal(admin.member.id, "dev-member-1"); assert.ok(admin.token); assert.equal(admin.epoch, status0.body.epoch);
});
await test("register with a taken name", async () => {
  const r = await call("POST", "/api/register", { body: { name: "mostafa", password: "password1", masterPassword: MASTER } });
  assert.equal(r.status, 409); assert.equal(r.body.error, "name-taken");
});
await test("login is case-insensitive on name; wrong password is 401", async () => {
  assert.equal((await call("POST", "/api/login", { body: { name: "MOSTAFA", password: "password1" } })).status, 200);
  const bad = await call("POST", "/api/login", { body: { name: "Mostafa", password: "password2" } });
  assert.equal(bad.status, 401); assert.equal(bad.body.error, "wrong-name-or-password");
});
await test("me", async () => assert.deepEqual((await call("GET", "/api/me", { token: admin.token })).body, { id: "dev-member-1", name: "Mostafa", role: "Admin", active: true }));
await test("signed-out without token", async () => assert.equal((await call("GET", "/api/me")).body.error, "signed-out"));

const owner = (await call("POST", "/api/members", { token: admin.token, body: { name: "Omar", role: "Owner", password: "password1" } })).body;
const viewer = (await call("POST", "/api/members", { token: admin.token, body: { name: "Vera", role: "Viewer", password: "password1" } })).body;
await test("Admin creates members; Staff is unavailable", async () => {
  assert.equal(owner.role, "Owner"); assert.equal(viewer.role, "Viewer");
  assert.equal((await call("POST", "/api/members", { token: admin.token, body: { name: "S", role: "Staff", password: "password1" } })).body.error, "role-unavailable");
});
const ownerSession = (await call("POST", "/api/login", { body: { name: "Omar", password: "password1" } })).body;
const viewerSession = (await call("POST", "/api/login", { body: { name: "Vera", password: "password1" } })).body;
await test("Owners can't create or edit Admins", async () => {
  assert.equal((await call("POST", "/api/members", { token: ownerSession.token, body: { name: "A2", role: "Admin", password: "password1" } })).body.error, "cannot-manage-admin");
  assert.equal((await call("PATCH", "/api/members/dev-member-1", { token: ownerSession.token, body: { role: "Viewer" } })).body.error, "cannot-manage-admin");
});
await test("Viewers can't manage members", async () =>
  assert.equal((await call("POST", "/api/members", { token: viewerSession.token, body: { name: "X", role: "Viewer", password: "password1" } })).body.error, "owner-only"));
await test("last Admin can't be demoted; can't remove self", async () => {
  assert.equal((await call("PATCH", "/api/members/dev-member-1", { token: admin.token, body: { role: "Owner" } })).body.error, "last-admin");
});
await test("member list", async () => {
  const r = await call("GET", "/api/members", { token: viewerSession.token });
  assert.deepEqual(r.body.map((m) => m.name), ["Mostafa", "Omar", "Vera"]);
});

// Events
const big = "123456789012345678.123456";
const rec = ev(owner.id, { kind: "entry.recorded", entry: { id: "e1", amount: 0, isLoanPrincipal: false } });
const recRaw = JSON.stringify(rec).replace('"amount":0', `"amount":${big}`);
const loanEntry = ev(owner.id, { kind: "entry.recorded", entry: { id: "e2", isLoanPrincipal: true } });
const push1 = await call("POST", "/api/events", { token: ownerSession.token, epoch: admin.epoch, raw: `[${recRaw},${JSON.stringify(loanEntry)}, {"id":"bad","change":{"kind":"nope"},"atUtc":"2026-01-01T00:00:00Z"}]` });
await test("push accepts valid events, rejects unreadable", () => {
  assert.equal(push1.status, 200); assert.deepEqual(push1.body.accepted, [rec.id, loanEntry.id]); assert.deepEqual(push1.body.rejected, [{ id: "bad", reason: "unreadable" }]);
});
await test("re-sent events count as accepted", async () => {
  const r = await call("POST", "/api/events", { token: ownerSession.token, raw: `[${recRaw}]` });
  assert.deepEqual(r.body.accepted, [rec.id]);
});
await test("pull returns server member events and pushed events with exact numbers", async () => {
  const r = await call("GET", "/api/events?after=0&limit=500", { token: viewerSession.token });
  assert.equal(r.body.epoch, admin.epoch);
  const kinds = r.body.events.map((e) => e.event.change.kind);
  assert.deepEqual(kinds, ["member.saved", "member.saved", "member.saved", "entry.recorded", "entry.recorded"]);
  assert.ok(r.text.includes(`"amount":${big}`), "amount digits preserved");
  const first = r.body.events[0];
  assert.deepEqual(Object.keys(first.event.change)[0], "kind");
  assert.deepEqual(first.event.change.member, { id: "dev-member-1", name: "Mostafa", role: "Admin", active: true });
  const seqs = r.body.events.map((e) => e.seq); assert.deepEqual(seqs, [...seqs].sort((a, b) => a - b));
  const after = await call("GET", `/api/events?after=${seqs[2]}&limit=1`, { token: viewerSession.token });
  assert.equal(after.body.events.length, 1); assert.equal(after.body.events[0].seq, seqs[3]);
});
await test("policy: wrong actor, viewer, future time, owner can't approve or delete", async () => {
  const r = await call("POST", "/api/events", { token: ownerSession.token, body: [
    ev(admin.member.id, { kind: "entry.recorded", entry: { id: "x1" } }, "w1"),
    { ...ev(owner.id, { kind: "entry.recorded", entry: { id: "x2" } }, "f1"), atUtc: new Date(Date.now() + 3 * 86400_000).toISOString() },
    ev(owner.id, { kind: "entry.approved", entryId: "e1" }, "a1"),
    ev(owner.id, { kind: "entry.deleted", entryId: "e1", reason: "" }, "d1"),
    ev(owner.id, { kind: "member.saved", member: { id: "z", name: "z", role: "Admin" } }, "m1"),
  ] });
  assert.deepEqual(r.body.rejected, [
    { id: "w1", reason: "wrong-actor" }, { id: "f1", reason: "time-in-future" }, { id: "a1", reason: "accountant-only" },
    { id: "d1", reason: "delete-not-allowed" }, { id: "m1", reason: "members-managed-by-server" }]);
  const v = await call("POST", "/api/events", { token: viewerSession.token, body: [ev(viewer.id, { kind: "contact.saved", contact: { id: "c" } }, "v1")] });
  assert.deepEqual(v.body.rejected, [{ id: "v1", reason: "view-only" }]);
});
await test("salaries are for managers; Owners may record them", async () => {
  const r = await call("POST", "/api/events", { token: ownerSession.token, body: [ev(owner.id, { kind: "entry.recorded", entry: { id: "s1", salaryMonth: "2026-11" } }, "sal1")] });
  assert.deepEqual(r.body.accepted, ["sal1"]);
});
await test("Admin: loan principal can't be deleted; purge removes the entry's events", async () => {
  const r = await call("POST", "/api/events", { token: admin.token, body: [
    ev(admin.member.id, { kind: "entry.deleted", entryId: "e2", reason: "" }, "dl2"),
    ev(admin.member.id, { kind: "entry.approved", entryId: "e1" }, "ap1"),
    ev(admin.member.id, { kind: "entry.purged", entryId: "e1" }, "pg1"),
    ev(admin.member.id, { kind: "entry.restored", entryId: "e1" }, "rs1"),
  ] });
  assert.deepEqual(r.body.accepted, ["ap1", "pg1"]);
  assert.deepEqual(r.body.rejected, [{ id: "dl2", reason: "cannot-delete-loan-entry" }, { id: "rs1", reason: "entry-purged" }]);
  const pulled = await call("GET", "/api/events?after=0", { token: admin.token });
  const ids = pulled.body.events.map((e) => e.event.id);
  assert.ok(!ids.includes(rec.id) && !ids.includes("ap1") && ids.includes("pg1"));
});
await test("epoch mismatch is book-reset 409", async () => {
  const r = await call("POST", "/api/events", { token: ownerSession.token, epoch: "old", body: [] });
  assert.equal(r.status, 409); assert.equal(r.body.error, "book-reset"); assert.equal(r.body.epoch, admin.epoch);
});
await test("admin panel: overview, sessions, activity; others get admin-only", async () => {
  const o = await call("GET", "/api/admin/overview", { token: admin.token });
  assert.equal(o.body.members, 3); assert.ok(o.body.events > 0); assert.ok(o.body.lastChangeUtc);
  assert.ok((await call("GET", "/api/admin/sessions", { token: admin.token })).body.length >= 3);
  const a = await call("GET", "/api/admin/activity?limit=2", { token: admin.token });
  assert.equal(a.body.length, 2); assert.ok(a.body[0].seq > a.body[1].seq);
  assert.equal((await call("GET", "/api/admin/overview", { token: ownerSession.token })).body.error, "admin-only");
});
await test("change own password signs out other devices", async () => {
  const other = (await call("POST", "/api/login", { body: { name: "Omar", password: "password1" } })).body;
  assert.equal((await call("POST", "/api/me/password", { token: ownerSession.token, body: { current: "password1", new: "password22" } })).status, 204);
  assert.equal((await call("GET", "/api/me", { token: other.token })).status, 401);
  assert.equal((await call("GET", "/api/me", { token: ownerSession.token })).status, 200);
});
await test("reset password with master password", async () => {
  const r = await call("POST", "/api/reset-password", { body: { name: "Vera", masterPassword: MASTER, newPassword: "password33" } });
  assert.equal(r.status, 200); assert.equal((await call("GET", "/api/me", { token: viewerSession.token })).status, 401);
});
await test("removing a member signs them out and announces it", async () => {
  assert.equal((await call("PATCH", `/api/members/${viewer.id}`, { token: admin.token, body: { active: false } })).status, 204);
  assert.equal((await call("POST", "/api/login", { body: { name: "Vera", password: "password33" } })).status, 401);
  const pulled = await call("GET", "/api/events?after=0", { token: admin.token });
  assert.equal(pulled.body.events.at(-1).event.change.kind, "member.removed");
});
await test("admin signout of another member", async () => {
  const r = await call("POST", `/api/admin/members/${owner.id}/signout`, { token: admin.token });
  assert.ok(r.body.ended >= 1);
});
await test("reset the book: archive, new epoch, members re-announced", async () => {
  assert.equal((await call("POST", "/api/admin/reset", { token: admin.token, body: { password: "nope", removeMembers: false } })).body.error, "wrong-password");
  const r = await call("POST", "/api/admin/reset", { token: admin.token, body: { password: "password1", removeMembers: true } });
  assert.equal(r.status, 200); assert.notEqual(r.body.epoch, admin.epoch); assert.ok(r.body.archivedEvents > 0); assert.equal(r.body.removedMembers, 1);
  const pulled = await call("GET", "/api/events?after=0", { token: admin.token });
  assert.equal(pulled.body.epoch, r.body.epoch);
  assert.deepEqual(pulled.body.events.map((e) => e.event.change.member?.name), ["Mostafa"]);
});
await test("logout ends the session", async () => {
  assert.equal((await call("POST", "/api/logout", { token: admin.token })).status, 204);
  assert.equal((await call("GET", "/api/me", { token: admin.token })).status, 401);
});
await test("sign-in rate limit per client", async () => {
  let last;
  for (let i = 0; i < 11; i++) last = await call("POST", "/api/login", { fromIp: "203.0.113.9", body: { name: "x", password: "y" } });
  assert.equal(last.status, 429); assert.equal(last.body.error, "too-many-attempts");
});
console.log(`\n${passed} passed`);
