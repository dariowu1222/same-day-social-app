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

## 暫時不做

- 不串 OpenAI / Gemini / Claude API
- 不串金流
- 不做第三方登入
- 不做 GPS 精準定位
- 不做圖片或語音
- 不做複雜推薦演算法

## 維護注意

新增 API 時，避免在 Controller 直接讀寫檔案。資料存取應集中在 persistence / repository 類別，讓未來替換資料庫時不影響前端與 API contract。

新資料表原則上依業務責任拆開，例如登入帳號放 `auth_accounts`、profile 放 `users`、忘記密碼放 `password_reset_tokens`、聊天室成員放 `chat_room_users`、訊息放 `chat_messages`。
