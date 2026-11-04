-- 在 Supabase SQL Editor 執行此檔案
-- 「今日」AI 標籤分析的用量與花費，一次呼叫一列。
--
-- 為什麼要自己存一份：OpenAI 的用量頁是以「天 × 模型」彙總的，單筆查不到，
-- 而且單筆成本在 $0.00005 這個量級，介面上會被四捨五入顯示成 $0.00。
-- 後台要回答「剛剛那則花多少」「這個月花多少」，只能自己記。
-- 應用層的 log 不算數：Render 免費方案閒置 15 分鐘就休眠，行程一停 log 就沒了。
--
-- 為什麼連三個單價也一起存：成本是我們自己乘出來的，不是對方回的。單價寫在
-- AiAnalyzerOptions 裡，換模型或 OpenAI 調價就會變。只存算完的金額，事後想
-- 重算或對帳都還原不回來；存了單價，任何一筆都能獨立驗算。
--
-- 為什麼失敗的也要存：被截斷、被模型拒絕的那幾次，token 已經燒掉也已經計費。
-- 只存成功的會讓帳面系統性偏低——而那幾次正是最需要被看見的。
--
-- 為什麼 today_entry_id 不設外鍵：這是帳務記錄，必須活得比它指向的東西久。
-- 使用者把今日記錄刪掉，花掉的錢不會跟著退，這一列就不該跟著消失或變成
-- 寫不進去。這點和 device_tokens 那些表刻意不同，同 admin_sessions 的做法。

CREATE TABLE IF NOT EXISTS ai_usage_logs (
    id                    varchar(64) PRIMARY KEY,
    today_entry_id        varchar(64),
    model                 varchar(80) NOT NULL,
    status                varchar(20) NOT NULL,
    input_tokens          integer NOT NULL DEFAULT 0,
    cached_input_tokens   integer NOT NULL DEFAULT 0,
    output_tokens         integer NOT NULL DEFAULT 0,
    reasoning_tokens      integer NOT NULL DEFAULT 0,
    estimated_cost_usd    numeric(14, 8) NOT NULL DEFAULT 0,
    input_price_per_mtok  numeric(10, 4) NOT NULL DEFAULT 0,
    cached_price_per_mtok numeric(10, 4) NOT NULL DEFAULT 0,
    output_price_per_mtok numeric(10, 4) NOT NULL DEFAULT 0,
    latency_ms            integer NOT NULL DEFAULT 0,
    error                 varchar(500),
    created_at            timestamptz NOT NULL DEFAULT now()
);

-- 後台預設就是「最近的排前面」，彙總也一律帶時間區間
CREATE INDEX IF NOT EXISTS ix_ai_usage_logs_created ON ai_usage_logs (created_at desc);

-- 換模型之後要能分開看兩個模型各自的花費
CREATE INDEX IF NOT EXISTS ix_ai_usage_logs_model_created ON ai_usage_logs (model, created_at desc);

-- 與其餘 app 表一致：關閉 RLS（後端自授權、前端不直連 DB）
ALTER TABLE ai_usage_logs DISABLE ROW LEVEL SECURITY;
