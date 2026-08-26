-- 在 Supabase SQL Editor 執行
-- 商用後台：帳號狀態、會員等級、驗證狀態，以及處置歷史表。
--
-- 設計說明：
--  * users.status 是管理端的單一真相；實際「能不能登入」仍由
--    auth_accounts.is_disabled 決定（登入流程已在檢查它），兩者由後端同步。
--    這樣不必改動既有登入邏輯，也不會有兩套判斷互相矛盾。
--  * 付費機制尚未開發：membership_* 欄位先建好，目前一律 FREE，
--    之後接訂閱時不需要再改 schema 或後台介面。

ALTER TABLE public.users
    ADD COLUMN IF NOT EXISTS status                varchar(20) NOT NULL DEFAULT 'ACTIVE',
    ADD COLUMN IF NOT EXISTS suspended_until       timestamptz,
    ADD COLUMN IF NOT EXISTS status_reason         varchar(500),
    ADD COLUMN IF NOT EXISTS membership_tier       varchar(20) NOT NULL DEFAULT 'FREE',
    ADD COLUMN IF NOT EXISTS membership_expires_at timestamptz,
    ADD COLUMN IF NOT EXISTS email_verified        boolean     NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS phone_verified        boolean     NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS identity_verified     boolean     NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS last_active_at        timestamptz;

-- 後台會員列表常依狀態篩選
CREATE INDEX IF NOT EXISTS ix_users_status ON public.users (status);

-- 既有帳號回填：auth_accounts 已停用的，狀態標成 SUSPENDED，
-- 讓後台看到的狀態與實際登入權限一致。
UPDATE public.users u
SET status = 'SUSPENDED'
WHERE u.status = 'ACTIVE'
  AND EXISTS (
      SELECT 1 FROM public.auth_accounts a
      WHERE a.user_id = u.id AND a.is_disabled = true
  );

-- 帳號處置歷史：停權／解除／封鎖／註銷逐筆留存，
-- 爭議發生時查得到「誰、何時、為什麼」。
CREATE TABLE IF NOT EXISTS public.account_sanctions (
    id         varchar(64) PRIMARY KEY,
    user_id    varchar(64) NOT NULL,
    action     varchar(40) NOT NULL,
    reason     varchar(1000),
    expires_at timestamptz,
    actor_id   varchar(64) NOT NULL,
    report_id  varchar(64),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_account_sanctions_user_created
    ON public.account_sanctions (user_id, created_at);

-- 關閉 RLS（與專案既有慣例一致）。
-- 本專案由後端以單一 same_day_app 角色存取、前端不直連 DB，授權在後端做，
-- 因此所有 app 表都不啟用 RLS。Supabase 建表時會預設開啟，
-- 漏了這段就會在寫入時噴 42501 row violates row-level security policy。
ALTER TABLE public.account_sanctions DISABLE ROW LEVEL SECURITY;
