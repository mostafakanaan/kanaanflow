-- KanaanFlow sync database on Cloudflare D1 (SQLite). Same model as KanaanFlow.Sync/Schema.sql (Postgres).
-- Times are ISO 8601 UTC text ("2026-11-15T00:00:00.000Z"), so text comparison orders them.

create table members (
    id            text    primary key,
    name          text    not null,
    role          text    not null check (role in ('Admin', 'Owner', 'Accountant', 'Staff', 'Viewer')),
    password_hash text    not null,
    active        integer not null default 1,
    created_at    text    not null
);

create unique index members_active_name on members (lower(name)) where active = 1;

create table sessions (
    token_hash   text primary key,
    member_id    text not null references members (id),
    created_at   text not null,
    last_seen_at text not null,
    expires_at   text not null
);

create index sessions_member on sessions (member_id);

-- The append-only event log: the book's source of truth. payload is the event JSON exactly as the device sent it.
-- entry_id, loan_id and principal are copied out of the payload so the server's checks don't have to parse JSON.
create table events (
    seq         integer primary key autoincrement,
    id          text    not null unique,
    member_id   text    not null references members (id),
    device_id   text    not null,
    at          text    not null,
    kind        text    not null,
    payload     text    not null,
    received_at text    not null,
    entry_id    text,
    loan_id     text,
    principal   integer not null default 0
);

create index events_entry on events (entry_id, kind) where entry_id is not null;
create index events_loan on events (loan_id, kind) where loan_id is not null;

-- The book's version. A reset gives it a new epoch, which tells every device to drop its old copy.
create table book (
    id       integer primary key check (id = 1),
    epoch    text    not null,
    reset_at text
);

insert into book (id, epoch) values (1, lower(hex(randomblob(16))));

-- Events from before a reset, kept so a reset can be undone by hand.
create table events_archive (
    seq         integer not null,
    id          text    not null,
    member_id   text    not null,
    device_id   text    not null,
    at          text    not null,
    kind        text    not null,
    payload     text    not null,
    received_at text    not null,
    epoch       text    not null,
    archived_at text    not null
);

-- Sign-in attempts per client per minute (password guessing protection).
create table signin_attempts (
    bucket text    primary key,
    minute integer not null,
    count  integer not null
);
