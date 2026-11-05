-- 在 Supabase SQL Editor 執行此檔案
-- 聊天室閒置到期，以及後台可調的規則設定。
--
-- 一、chat_rooms 多兩個時間戳
--
-- 為什麼是「標記」而不是直接刪 chat_room_users：
-- 到期的房間要留在列表上，點進去看得到歷史訊息，但只剩「退出聊天室」一個動作。
-- 直接把 membership 刪掉的話，房間會無聲消失——使用者不知道發生什麼事，
-- 也沒機會在最後看一眼。真正的退出由本人按下去，不由排程代勞。
--
-- 為什麼 expiry_notified_at 要獨立存，不靠查 notifications：
-- 鎖定的前提是「已經通知過、而且通知超過 24 小時」。排程漏跑一天（GitHub Actions
-- 會發生），房間會從第 29 天跳到第 31 天；沒有這個欄位就無從判斷通知到底發出去
-- 沒有，使用者會在毫無預警的情況下看到聊天室被鎖。存在這裡比回查 notifications
-- 便宜，也不會因為使用者把通知清掉就失去依據。
--
-- 兩個都放在 chat_rooms 而不是 chat_room_users：到期是整間房的事（雙方都沒說話），
-- 不是單邊的狀態。放成員那層會變成兩列各自到期，還要處理兩邊不同步。
--
-- 二、app_settings
--
-- 為什麼不寫在 appsettings / 環境變數：那兩個都要重新部署才生效。這是營運規則，
-- 要讓管理者在後台改完就算數。
--
-- 為什麼 value 存 text 而不是分型別的欄位：目前只有整數與時間，為它們各開一欄
-- 等於預設未來的設定都長這樣。text 讓之後要存開關或字串時不必再改表結構，
-- 型別轉換與合法範圍由後端那一層負責（讀壞了就退回程式裡的預設值）。
--
-- 為什麼要 updated_by：這些值會直接改變使用者看得到的行為（聊天室被鎖、配不到
-- 人）。出事的時候要查得出是誰在什麼時候改的。

ALTER TABLE chat_rooms ADD COLUMN IF NOT EXISTS expiry_notified_at timestamptz;
ALTER TABLE chat_rooms ADD COLUMN IF NOT EXISTS expired_at timestamptz;

-- 排程每次都要掃「還沒鎖定」的房間，已鎖定的永遠不必再看。
CREATE INDEX IF NOT EXISTS ix_chat_rooms_pending_expiry
    ON chat_rooms (created_at)
    WHERE expired_at IS NULL;

CREATE TABLE IF NOT EXISTS app_settings (
    key         varchar(64) PRIMARY KEY,
    value       text        NOT NULL,
    updated_at  timestamptz NOT NULL DEFAULT now(),
    updated_by  varchar(64)
);

-- 其餘表一律關閉 RLS（見 20260624_user_settings_disable_rls.sql）：
-- 這個專案的授權全部在後端做，開著 RLS 只會讓 app 角色讀不到自己的資料。
ALTER TABLE app_settings DISABLE ROW LEVEL SECURITY;

INSERT INTO app_settings (key, value) VALUES
    -- 聊天室閒置幾天後鎖定。前一天發通知。
    ('chat.idle_days',    '30'),
    -- 每人同時進行中的聊天對象上限。
    ('chat.max_partners', '100'),
    -- 既有資料的寬限起點：到期時間取「最後一則訊息」與這個時間的較晚者。
    --
    -- 為什麼需要：全站 360 間房有 358 間已經超過 30 天沒訊息（多數是假資料，
    -- 但真實帳號也有 28 間）。沒有這個下限的話，功能上線當天排程一跑，使用者
    -- 一早打開就發現聊天室整排被鎖，而他從來沒收到過任何提醒。
    --
    -- 為什麼是設定值而不是去改 chat_rooms.created_at：改資料會弄髒「房間建立
    -- 時間」的語意，而且不可逆。這裡只是一條全域下限，哪天要取消寬限把它設成
    -- 很早的時間（或刪掉這一列）就好，既有資料一個字都沒被動過。
    ('chat.expiry_since', to_char(now() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"'))
ON CONFLICT (key) DO NOTHING;
