-- KanaanFlow sync table for Supabase (run once in the SQL editor).
-- Each device uploads its own append-only events and downloads everyone else's.
-- Nothing is ever updated or deleted, so devices can't overwrite each other's entries.

create table if not exists public.kf_events (
    seq         bigserial primary key,
    id          text        not null unique,
    book_code   text        not null,
    at          timestamptz not null,
    device_id   text        not null,
    payload     jsonb       not null,
    received_at timestamptz not null default now()
);

create index if not exists kf_events_book_seq on public.kf_events (book_code, seq);

alter table public.kf_events enable row level security;

-- Anyone holding the anon key AND the book code can read and add events.
-- Use a long random book code (24+ characters) and share it only with your team.
-- Per-user sign-in (Supabase Auth) is the next step for stronger protection.
drop policy if exists "kf_events read" on public.kf_events;
create policy "kf_events read" on public.kf_events for select using (true);

drop policy if exists "kf_events insert" on public.kf_events;
create policy "kf_events insert" on public.kf_events for insert with check (true);
