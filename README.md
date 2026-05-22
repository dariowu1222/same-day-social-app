# same-day-social-app

暫定產品名：同頻 Today。

這是一個「生活共鳴型社交 APP」MVP。核心不是快速交友或看照片滑卡，而是讓使用者輸入「今天發生了什麼」，再透過今日事件、情緒與期待回應方式，找到剛好懂今天的陌生人。

## 技術架構

- Frontend：React + Vite + TypeScript
- Backend：ASP.NET Core Web API + C#
- Storage：Supabase PostgreSQL / EF Core；本機 JSON 暫時保留給尚未改寫的 MVP API
- Matching：rule-based 分數計算
- Moderation：rule-based 內容檢查

## 安裝方式

Backend:

```powershell
cd backend
dotnet restore
```

Supabase connection string 不要寫進 Git。需要連正式資料庫時，使用 user-secrets：

```powershell
cd backend
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=db.uqacfftvowhehvkyafep.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_SUPABASE_DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

Frontend:

```powershell
cd frontend
npm install
```

## 啟動方式

Visual Studio:

- 開啟 `SameDaySocialApp.sln`
- 將 `backend` 設為啟動專案
- 使用 `http` profile 啟動後端 API
- 前端仍建議用下方 `npm run dev` 啟動

Backend:

```powershell
cd backend
dotnet run --launch-profile http
```

Frontend:

```powershell
cd frontend
npm run dev
```

## 網址

- Frontend：http://localhost:5173
- Backend API：http://localhost:5000

## MVP 功能

- 建立 Demo user
- 輸入今日事件
- rule-based 今日事件分析
- 今日共鳴配對
- 建立聊天室骨架
- 樹洞發文
- 樹洞「我懂」、留言、檢舉
- 固定任務清單
- 任務參加紀錄
- 手機版優先 UI

## Mock / 暫時簡化

- 登入是 Demo user，不做第三方登入
- 資料存本機 JSON，不接正式資料庫
- 今日事件分析不串 AI
- 配對演算法是規則分數
- 內容安全檢查是關鍵字與 regex
- 聊天不做即時推播、已讀、圖片、語音

## 尚未實作

- 正式會員系統
- AI 語意分析與破冰句生成
- Supabase / PostgreSQL / SQL Server
- SignalR 即時聊天
- PWA 安裝設定
- 手機驗證 / 真人驗證
- 金流、訂閱、票券
- App Store / Google Play 上架

## 未來規劃

後端目前已有 Controller -> Service -> Storage 的分層。未來可將 `JsonStorageService` 替換成 SQLite / PostgreSQL / SQL Server repository，並將 `TodayAnalyzerService` 與 `MatchService` 的 rule-based 實作換成 AI matching service。
