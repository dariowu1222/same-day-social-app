-- 後台登入：一次性驗證碼 + 可撤銷的管理 session
--
-- 為什麼要有 session 表而不是只發一個 JWT：
-- 後台看得到所有人的私訊，所以必須做得到「立刻撤銷」「閒置逾時」
-- 「剛才有沒有驗過」這三件事，純 JWT 一件都做不到。
--
-- 這個專案的慣例：所有資料表都關閉 RLS，後端用 Npgsql 直連，
-- 權限在應用層把關（見 AdminAccessService 的預設拒絕白名單）。

create table if not exists admin_login_codes (
  id          varchar(64) primary key,
  user_id     varchar(64) not null,
  code_hash   varchar(200) not null,
  attempts    integer not null default 0,
  expires_at  timestamptz not null,
  used_at     timestamptz,
  created_at  timestamptz not null default now()
);

create index if not exists ix_admin_login_codes_user_created
  on admin_login_codes (user_id, created_at desc);

create table if not exists admin_sessions (
  id                 varchar(64) primary key,
  user_id            varchar(64) not null,
  last_seen_at       timestamptz not null,
  last_verified_at   timestamptz not null,
  expires_at         timestamptz not null,
  revoked_at         timestamptz,
  created_ip         varchar(64),
  created_user_agent varchar(300),
  created_at         timestamptz not null default now()
);

create index if not exists ix_admin_sessions_user on admin_sessions (user_id);

alter table admin_login_codes disable row level security;
alter table admin_sessions    disable row level security;
