-- 回覆層級的「我懂」要能去重
--
-- 為什麼現在才需要：貼文的愛心一直都有去重（rant_reactions 一列一個人），
-- 回覆的沒有——RantService.LikeReply 從頭到尾只有 reply.LikeCount += 1，
-- 連呼叫者是誰都沒傳進來。同一個帳號可以一直按，上限只有速率限制的每分鐘 60 次。
--
-- 為什麼不另開一張表：這就是 rant_reactions 在做的事，差別只在對象是回覆而不是貼文。
-- 多一欄比多一張表好維護，而且 rant_post_id 留著，仍然查得到「這個讚屬於哪一篇」。
--
-- 為什麼原本那個唯一索引要重建成條件索引：
-- ux_rant_reactions_post_user_type 是 (rant_post_id, user_id, reaction_type) 三欄唯一。
-- 加了 rant_reply_id 之後，同一個人在同一篇底下按兩則不同回覆的讚，會是兩列
-- (同一個 post、同一個 user、同一個 UNDERSTAND)，照舊索引會被擋掉。
-- 改成「只有貼文層級那些列（rant_reply_id is null）才受這條唯一限制」。
--
-- 執行順序：這支要先在 Supabase 跑過，再部署後端。
--
-- 沒跑就部署的話，壞掉的不只回覆的讚：EF 會把 rant_reply_id 放進每一筆
-- rant_reactions 的 INSERT，所以按貼文的愛心也會一起失敗（Understand 與
-- LikeReply 兩支）。讀取不受影響——愛心數是 COUNT(*) 出來的，不碰欄位清單，
-- 列表頁與詳情頁照常。

alter table rant_reactions
  add column if not exists rant_reply_id varchar(64)
  references rant_replies(id) on delete cascade;

drop index if exists ux_rant_reactions_post_user_type;

create unique index if not exists ux_rant_reactions_post_user_type
  on rant_reactions(rant_post_id, user_id, reaction_type)
  where rant_reply_id is null;

create unique index if not exists ux_rant_reactions_reply_user_type
  on rant_reactions(rant_reply_id, user_id, reaction_type)
  where rant_reply_id is not null;

-- 不補舊資料：線上 25 則回覆的 like_count 全部是 0，沒有既有的讚要對應回來。
