-- 驗證碼要分用途：進後台（Login）與確認不可逆處置（Reverify）。
--
-- 原本兩者共用同一張表也共用同一組碼，所以登入時收到的碼可以直接拿去
-- 通過「封鎖使用者」的再確認——兩者的授權份量差很多，不該互換。
-- 信件內容現在也會寫明這組碼能做什麼。

alter table admin_login_codes
  add column if not exists purpose varchar(20) not null default 'Login';

drop index if exists ix_admin_login_codes_user_created;

create index if not exists ix_admin_login_codes_user_purpose_created
  on admin_login_codes (user_id, purpose, created_at desc);
