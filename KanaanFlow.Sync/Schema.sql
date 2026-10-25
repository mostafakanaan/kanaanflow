-- KanaanFlow sync server schema. Applied on every start; every statement is safe to run again.
-- One server holds one money book.

create table if not exists members (
    id            text        primary key,
    name          text        not null,
    role          text        not null,
    password_hash text        not null,
    active        boolean     not null default true,
    created_at    timestamptz not null default now()
);

-- Roles: Admin, Owner, Accountant, Staff (Employee), Viewer (read-only). Re-created so older databases get the new roles.
alter table members drop constraint if exists members_role_check;
alter table members add constraint members_role_check check (role in ('Admin', 'Owner', 'Accountant', 'Staff', 'Viewer'));

create unique index if not exists members_active_name on members (lower(name)) where active;

create table if not exists sessions (
    token_hash   text        primary key,
    member_id    text        not null references members (id),
    created_at   timestamptz not null default now(),
    last_seen_at timestamptz not null default now(),
    expires_at   timestamptz not null
);

create index if not exists sessions_member on sessions (member_id);

-- The append-only event log: the book's source of truth. Rows are never updated or deleted.
create table if not exists events (
    seq         bigserial   primary key,
    id          text        not null unique,
    member_id   text        not null references members (id),
    device_id   text        not null,
    at          timestamptz not null,
    kind        text        not null,
    payload     jsonb       not null,
    received_at timestamptz not null default now()
);

create index if not exists events_recorded_entry on events ((payload -> 'change' -> 'entry' ->> 'id')) where kind = 'entry.recorded';
create index if not exists events_opened_loan on events ((payload -> 'change' -> 'loan' ->> 'id')) where kind = 'loan.opened';
