-- 檢舉處理結果有沒有寄信成功
--
-- 後台的「已處理 / 已駁回 / 已退回」清單要看得到這一欄：寄信是會失敗的
-- （Gmail SMTP 有每日額度、當事人可能根本沒有可寄的信箱），而失敗之後
-- 沒有任何地方看得出來，管理者會以為當事人已經被通知了。
--
-- 三態，所以是可空的 boolean：
--   null  = 沒有寄（SELF_HARM 一律不寄，或兩邊都查不到信箱）
--   true  = 該寄的都寄出去了
--   false = 有人沒寄成功
--
-- 執行順序：這支要先在 Supabase 跑過，再部署後端。
--
-- 沒跑就部署的話：EF 會把 outcome_mail_sent 放進每一筆 reports 的 INSERT，
-- 所以「送出檢舉」會當場失敗（使用者端的檢舉功能整條壞掉）。
-- 讀取不受影響——後台的佇列是 SELECT，照常。

alter table reports
  add column if not exists outcome_mail_sent boolean;

-- 「處理中」這個狀態從來沒有被任何程式碼寫入過，後台的下拉選單也移除了
-- （它和「待處理」是同一件事）。真的有殘留的話一併歸位——
-- RecordMapper 的 ParseEnum 讀不懂時本來就會退回 PENDING，這裡只是讓資料跟上。
update reports set status = 'PENDING' where status = 'REVIEWING';
