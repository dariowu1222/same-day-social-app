# 開發筆記

## 技術決策

原始規格建議 Node.js + Express。考量未來維護性，本專案第一版改用 ASP.NET Core Web API + C#，前端仍使用 React + Vite + TypeScript。

資料庫方向改為 Supabase PostgreSQL。不要把 Supabase connection string、database password、service role key 寫入 Git；本機開發使用 .NET User Secrets，部署使用環境變數或平台 secret。

## 分層

- Controllers：REST API
- Application/Services：業務邏輯
- Domain/Entities：資料模型
- Domain/Enums：列舉
- Infrastructure/Persistence：JSON storage
- Infrastructure/Persistence/AppDbContext：Supabase PostgreSQL / EF Core model
- Infrastructure/Persistence/Models：資料庫紀錄 model
- Infrastructure/Persistence/Configurations：EF Core table mapping

## Supabase

- Project：`same-day-social-app`
- Project ref：`uqacfftvowhehvkyafep`
- Region：`ap-northeast-1`
- Host：`db.uqacfftvowhehvkyafep.supabase.co`

已套用 migration：

- `initial_same_day_social_schema`

本地 SQL 檔：

- `backend/Infrastructure/Persistence/MigrationsSql/20260522_initial_same_day_social_schema.sql`

> 這份「已套用」清單從 2026-05 之後就沒人維護，不要當成真實狀態的依據；
> 要確認某支 .sql 跑過沒有，只能去 Supabase 查。

2026-11-04 新增（需人工執行）：

- `backend/Infrastructure/Persistence/MigrationsSql/20261104_ai_usage_logs.sql`
  —— 「今日」AI 標籤分析的用量與花費，後台「AI 用量」分頁的資料來源。
  沒跑這支的話後台整頁會顯示 0 並提示要先建表（不會炸）。

> 線上資料庫（Supabase `uqacfftvowhehvkyafep`）的狀態：下面 2026-11-06 與 2026-11-07
> 這兩支**已於 2026-11-06 由 dashboard SQL 編輯器執行完畢並驗證**（欄位、條件索引、
> 外鍵都在，並以 rollback 交易試過 EF 實際的 INSERT）。檔案留著是給新環境建置用的。

2026-11-06 新增（**要先跑再部署**）：

- `backend/Infrastructure/Persistence/MigrationsSql/20261106_rant_reply_reactions.sql`
  —— 樹洞回覆的讚去重。rant_reactions 多一欄 rant_reply_id，並把原本的唯一索引
  改成只管貼文層級那一半（不然同一個人按同一篇底下的兩則回覆會撞索引）。
  這支跟上面那幾支不同，**沒跑就部署會炸**：EF 會把新欄位放進每一筆
  rant_reactions 的 INSERT，所以按貼文的愛心與按回覆的讚都會失敗。
  讀取不受影響（愛心數是 COUNT 出來的），列表頁與詳情頁照常。
  Supabase SQL 編輯器：
  https://supabase.com/dashboard/project/uqacfftvowhehvkyafep/sql/new

2026-11-07 新增（**要先跑再部署**）：

- `backend/Infrastructure/Persistence/MigrationsSql/20261107_verification_code_attempts.sql`
  —— 驗證碼的嘗試次數。registration_verification_tokens 與 password_reset_tokens
  各多一欄 failed_attempts，猜錯五次那組碼就作廢。
  **沒跑就部署會炸**：EF 會把新欄位放進這兩張表每一筆的 INSERT，所以
  「寄註冊驗證碼」與「寄重設密碼驗證碼」兩條路都會當場失敗——新使用者
  註冊不了，忘記密碼的人也收不到信。登入與其他功能不受影響。
  Supabase SQL 編輯器：
  https://supabase.com/dashboard/project/uqacfftvowhehvkyafep/sql/new

## 暫時不做

- 不串 Gemini / Claude API。OpenAI 只用在「今日」的標籤分析（AiTodayAnalyzer），沒設 Ai:ApiKey 就整條停用
- 不串金流
- 不做第三方登入
- 不做 GPS 精準定位
- 不做圖片或語音
- 不做複雜推薦演算法

## 維護注意

新增 API 時，避免在 Controller 直接讀寫檔案。資料存取應集中在 persistence / repository 類別，讓未來替換資料庫時不影響前端與 API contract。

新資料表原則上依業務責任拆開，例如登入帳號放 `auth_accounts`、profile 放 `users`、忘記密碼放 `password_reset_tokens`、聊天室成員放 `chat_room_users`、訊息放 `chat_messages`。
