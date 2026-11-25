-- 樹洞：刪除改成先保留 30 天，貼文作者可以隱藏回應
--
-- 三件事共用這四個欄位：
--
--  1. 刪主貼文（參考 Threads）：貼文不再出現在牆上，但底下別人的回應不跟著消失，
--     帶著連結進來會看到「這則貼文已被作者刪除」和還留著的回應。
--     rant_posts.deleted_at 有值＝作者刪了。
--
--  2. 收回回應：以前是當場把那一列刪掉，有人可以留一句難聽的話、等對方看到就收回，
--     之後誰也查不到。現在是 rant_replies.deleted_at 有值＝收回了，對所有人隱形，
--     只有後台查得到。
--
--  3. 貼文作者隱藏回應：rant_replies.hidden_at 有值＝被隱藏。hidden_by 記是誰藏的：
--       AUTHOR = 貼文作者自己藏的（其他人看不到；作者看得到、可以取消；回應的人自己還看得到）
--       ADMIN  = 後台處理檢舉時藏的（所有人都看不到，貼文作者也不能取消）
--
-- 保留期：deleted_at 起算 30 天，之後由 POST /api/maintenance/purge-deleted-rants 清掉
--（排程在 .github/workflows/purge-report-content.yml）。
--
-- 執行順序：這支要先在 Supabase 跑過，再重啟／部署後端。
--
-- 沒跑就啟動新版後端的話，整面牆會掛掉：EF 會把新欄位放進每一筆 rant_posts 與
-- rant_replies 的 SELECT，欄位不存在就是 500。反過來沒有問題——欄位都可以是 null，
-- 線上還在跑的舊版後端不認得它們，照常讀寫。

alter table rant_posts
  add column if not exists deleted_at timestamptz;

alter table rant_replies
  add column if not exists deleted_at timestamptz,
  add column if not exists hidden_at timestamptz,
  add column if not exists hidden_by varchar(16);

-- 清理排程用：只掃「已經刪掉的」那幾列，不必翻整張表
create index if not exists ix_rant_posts_deleted_at
  on rant_posts(deleted_at)
  where deleted_at is not null;

create index if not exists ix_rant_replies_deleted_at
  on rant_replies(deleted_at)
  where deleted_at is not null;
