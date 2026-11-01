-- 在 Supabase SQL Editor 執行此檔案
-- 未讀數：每個成員在每間房的「已讀水位」。
--
-- 水位放 chat_room_users 而不是 chat_member_settings，是因為前者的列一定存在
-- （CreateRoom 必寫），後者是懶建立的（只有按過釘選／靜音／備註才有列），
-- 水位放那裡的話絕大多數使用者查不到，一律退回 joined_at。
--
-- 未讀數 = 該房 created_at > COALESCE(last_read_at, joined_at)
--          且 sender_id <> 自己 且 is_recalled = false 的訊息數。

ALTER TABLE public.chat_room_users
    ADD COLUMN IF NOT EXISTS last_read_at timestamptz;

-- 既有成員一律視為「已讀到現在」。
-- 不補這一行的話，水位會退回 joined_at，第一次開 App 的人會看到
-- 「配對以來的每一則訊息」全部算成未讀——明明他早就讀過了。
-- 這支只需要跑一次；重跑的副作用是把當下真正的未讀也標成已讀，影響有限。
UPDATE public.chat_room_users
SET last_read_at = now()
WHERE last_read_at IS NULL;
