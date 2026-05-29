# same-day-social-app

暫定產品名：同頻 Today

這是一個生活共鳴型社交 Web App / PWA MVP。核心不是滑照片或快速配對，而是讓使用者輸入「今天發生了什麼」，再透過今日事件、情緒與期待回應方式找到同頻的人。

## 技術架構

- Frontend：React + Vite + TypeScript
- Backend：ASP.NET Core Web API + C#
- Database：Supabase PostgreSQL + EF Core
- Local fallback：部分 MVP API 保留 JSON storage，方便未設定資料庫時 demo
- Matching：rule-based 分數計算
- Moderation：rule-based 內容檢查
- Email：SMTP 寄送忘記密碼驗證碼

## 安裝

Backend:

```powershell
cd backend
dotnet restore
```

Frontend:

```powershell
cd frontend
npm install
```

## 本機設定

Supabase connection string 不要寫進 Git。請用 .NET User Secrets：

```powershell
cd backend
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=db.uqacfftvowhehvkyafep.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_SUPABASE_DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

忘記密碼需要 SMTP，沒有設定時 API 會回傳 `SMTP_NOT_CONFIGURED`，不會假裝寄信成功。

```powershell
dotnet user-secrets set "Smtp:Host" "smtp.example.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:EnableSsl" "true"
dotnet user-secrets set "Smtp:Username" "your-smtp-username"
dotnet user-secrets set "Smtp:Password" "your-smtp-password"
dotnet user-secrets set "Smtp:FromEmail" "no-reply@example.com"
dotnet user-secrets set "Smtp:FromName" "同頻 Today"
```

## 啟動

Visual Studio:

- 開啟 `SameDaySocialApp.sln`
- 將 `backend` 設為啟動專案
- 使用 `http` profile 啟動
- 後端會透過 SPA proxy 啟動前端 Vite dev server

手動啟動：

```powershell
cd backend
dotnet run --launch-profile http
```

```powershell
cd frontend
npm run dev
```

## 網址

- Frontend：http://localhost:5173
- Backend API：http://localhost:5000

## MVP 功能

- Demo user
- 正式註冊 / 登入
- 忘記密碼寄送 Email 驗證碼
- 今日事件輸入與 rule-based 分析
- 今日共鳴配對
- 樹洞發文、我懂、留言、檢舉
- 任務清單與參與紀錄
- 文字聊天室骨架

## 尚未實作

- 第三方登入
- 手機驗證
- 圖片上傳
- 語音聊天
- 推播
- 金流
- AI 語意分析與 AI 破冰句
- 正式 RLS policy

## 未來規劃

- AI 今日事件語意分析
- AI 詐騙偵測
- 真人驗證
- 小群共鳴房
- 任務通行證
- 付費同頻 Plus
- PWA 安裝與 App 上架
