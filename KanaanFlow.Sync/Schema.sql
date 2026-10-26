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

-- The append-only event log: the book's source of truth. Rows are never updated; they are only deleted when an Admin
-- permanently deletes an entry (entry.purged), which removes that entry's events and keeps the tombstone.
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
create index if not exists events_about_entry on events ((payload -> 'change' ->> 'entryId'));
create index if not exists events_opened_loan on events ((payload -> 'change' -> 'loan' ->> 'id')) where kind = 'loan.opened';

-- The book's version. A reset gives it a new epoch, which tells every device to drop its old copy.
create table if not exists book (
    id       int         primary key check (id = 1),
    epoch    text        not null,
    reset_at timestamptz
);

insert into book (id, epoch) values (1, md5(random()::text || clock_timestamp()::text)) on conflict (id) do nothing;

-- Events from before a reset, kept so a reset can be undone by hand.
create table if not exists events_archive (
    seq         bigint      not null,
    id          text        not null,
    member_id   text        not null,
    device_id   text        not null,
    at          timestamptz not null,
    kind        text        not null,
    payload     jsonb       not null,
    received_at timestamptz not null,
    epoch       text        not null,
    archived_at timestamptz not null default now()
);
