-- 在 Supabase SQL Editor 執行
-- 檢舉統一表：取代分散的 rant_reports / chat_reports，讓審核端只需查一張表。
-- 多型設計（target_type + target_id），日後新增可檢舉的標的不必再改 schema。

CREATE TABLE IF NOT EXISTS public.reports (
    id               varchar(64) PRIMARY KEY,
    target_type      varchar(40)  NOT NULL,
    target_id        varchar(64)  NOT NULL,
    reporter_id      varchar(64)  NOT NULL,
    reported_user_id varchar(64),
    category         varchar(40)  NOT NULL,
    detail           varchar(1000),
    status           varchar(20)  NOT NULL DEFAULT 'PENDING',
    severity         integer      NOT NULL DEFAULT 0,
    -- 被檢舉的內容之後可能被刪改，檢舉當下先留快照，審核時才有東西可看
    context_snapshot text,
    reviewed_by      varchar(64),
    reviewed_at      timestamptz,
    resolution       varchar(1000),
    created_at       timestamptz  NOT NULL DEFAULT now()
);

-- 審核佇列排序：待處理優先、嚴重度高的先看、同級距先進先出
CREATE INDEX IF NOT EXISTS ix_reports_status_severity_created
    ON public.reports (status, severity, created_at);

-- 同一人對同一標的只能檢舉一次
CREATE UNIQUE INDEX IF NOT EXISTS ux_reports_target_reporter
    ON public.reports (target_type, target_id, reporter_id);

-- 累犯統計
CREATE INDEX IF NOT EXISTS ix_reports_reported_user
    ON public.reports (reported_user_id);

-- 審核操作軌跡：後台讀得到私訊與貼文，因此每次查看與處置都要留痕
CREATE TABLE IF NOT EXISTS public.moderation_audits (
    id         varchar(64) PRIMARY KEY,
    actor_id   varchar(64) NOT NULL,
    action     varchar(40) NOT NULL,
    report_id  varchar(64),
    note       varchar(1000),
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_moderation_audits_actor_created
    ON public.moderation_audits (actor_id, created_at);


-- 關閉 RLS（與專案既有慣例一致）。
-- 本專案由後端以單一 same_day_app 角色存取、前端不直連 DB，授權在後端做，
-- 因此所有 app 表都不啟用 RLS。Supabase 建表時會預設開啟，
-- 漏了這段就會在寫入時噴 42501 row violates row-level security policy。
ALTER TABLE public.reports           DISABLE ROW LEVEL SECURITY;
ALTER TABLE public.moderation_audits DISABLE ROW LEVEL SECURITY;

-- ── 遷移既有檢舉 ─────────────────────────────────────
-- 舊資料沒有分類（前端從未送出 reason），一律歸為 OTHER、嚴重度 20。
-- 舊表保留不刪，確認新流程無誤後再由人工決定是否清理。

INSERT INTO public.reports (
    id, target_type, target_id, reporter_id, reported_user_id,
    category, detail, status, severity, context_snapshot, created_at)
SELECT
    'report_migrated_' || r.id,
    'RANT_POST',
    r.rant_post_id,
    r.user_id,
    p.user_id,
    'OTHER',
    r.reason,
    'PENDING',
    20,
    left(p.content, 2000),
    r.created_at
FROM public.rant_reports r
LEFT JOIN public.rant_posts p ON p.id = r.rant_post_id
ON CONFLICT (target_type, target_id, reporter_id) DO NOTHING;

INSERT INTO public.reports (
    id, target_type, target_id, reporter_id, reported_user_id,
    category, detail, status, severity, context_snapshot, created_at)
SELECT
    'report_migrated_' || c.id,
    'USER',
    c.reported_user_id,
    c.reporter_id,
    c.reported_user_id,
    'OTHER',
    c.reason,
    'PENDING',
    20,
    '（由聊天室檢舉遷移，聊天室 ' || c.chat_room_id || '）',
    c.created_at
FROM public.chat_reports c
ON CONFLICT (target_type, target_id, reporter_id) DO NOTHING;
