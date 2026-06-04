# 同頻 Today App — 目前狀態交接

> 給 Codex 閱讀的專案現況說明
> 更新時間：2026-06-04

---

## 專案簡介

生活共鳴型社交 App。使用者每天輸入「今天發生的一件事」，系統根據事件內容與情緒標籤配對有共鳴的人，低壓認識、慢慢建立關係。

**技術架構**
- 前端：React 19 + TypeScript + Vite（PWA）
- 後端：ASP.NET Core .NET 9 + C#
- 資料庫：Supabase PostgreSQL + Entity Framework Core
- 行動端：Capacitor（Android + iOS 打包）
- 寄信：Gmail SMTP（dotnet user-secrets）
- 部署：尚未部署，本地開發中

**重要路徑**
- 前端：`frontend/`
- 後端：`backend/`
- App ID：`com.tongpin.today`

---

## 已完成項目

### 登入 / 註冊頁
- 所有 input 補上 autoComplete 屬性
- 密碼強度即時指示器（弱 / 中 / 強）
- 送出按鈕加 loading spinner
- 「記住我」功能（勾選 → localStorage，不勾 → sessionStorage）
- Email 格式 + 確認密碼 onBlur 即時驗證
- 忘記密碼步驟指示器加連接線與已完成狀態
- 性別選取按鈕選中視覺修正
- 服務條款彈窗關閉按鈕對齊修正
- 移除假手機狀態列

### 法律文件
- 服務條款重寫為正式法律版本（10 條）
- 隱私權政策重寫，符合個資法（10 條）

### 後端功能
- Gmail SMTP 寄信（註冊驗證碼、密碼重設驗證碼）
- 修正 EF Core FK constraint 錯誤
- ProfileController 支援 PostgreSQL 使用者讀寫

### 新帳號 Onboarding 流程
- 5 張可左右滑動介紹頁（含插圖）
- 個人資料設定頁（自我介紹 + 興趣標籤）
- PNG → WebP，8.8MB 壓縮至 329KB
- localStorage flag 記錄完成狀態（key：`same-day-onboarding-{userId}`）
- **已驗證：第一次登入顯示教學，回頭用戶直接進內頁，邏輯正確**

### TodayPage 心靈小語功能（2026-06-04，Codex 實作）

- 移除原本固定顯示的大型名言卡
- `TodayEntryForm.tsx`：新增 `SoulNote` 型別與收合式「心靈小語」區塊（預設收起，點擊展開）
- `TodayPage.tsx`：依快速入口 key（`tired` / `wronged` / `talk`）對應心靈小語資料，隨機從對應組抽取一筆；選「今天還不錯」（`good`）不顯示小語
- `global.css`：新增 `.soul-note`、`.soul-note-toggle`、`.soul-note-body`、`.soul-note-chevron` 等樣式與下拉動畫；移除舊名言卡樣式
- 心靈小語內容：每個情境 2 則，附真實作者（Carl Rogers、Viktor Frankl、Daniel Kahneman、Albert Bandura）+ 原著出處 + 中文情境短語
- 狀態提升至 `TodayPage`：`soulNote`（useMemo 計算）與 `isSoulNoteOpen` 由父層管理，`TodayEntryForm` 僅接收 props

### 行動端（Capacitor）
- Android + iOS 專案已建立
- 開發模式設定完成：
  - `capacitor.config.ts`：啟用 `server.url = http://10.0.2.2:5173`（live reload）
  - `vite.config.ts`：`host: '0.0.0.0'`，加入 `/api` proxy 至 `localhost:5000`
  - `api/client.ts`：API base URL 改為空字串（由 proxy 接管）
  - `gradle.properties`：加入 `android.overridePathCheck=true`
- **已驗證：Android 模擬器可成功啟動並顯示畫面**

### 開發環境
- WSL2 + Ubuntu + tmux 多視窗環境
- 桌面捷徑「今日啟動開發」：一鍵啟動 Vite dev server + Android Studio

---

## 進行中

- 無（Android 模擬器已跑通，暫無阻塞中的工作）

---

## 待完成（依優先序）

### 高優先
- [ ] 後端部署到雲端（Render / Railway / fly.io 擇一）
- [ ] 生產環境 API URL 設定（`VITE_API_BASE_URL` env var）

### 中優先
- [ ] Google OAuth 登入（後端 `/api/auth/google` endpoint + 前端串接）
- [ ] ProfilePage 實際呼叫 API 儲存（目前是假資料）
- [ ] 推播通知（Capacitor Push Notification plugin）

### 低優先
- [ ] iOS 測試（需要 Mac + Xcode）
- [ ] Google Play 上架準備（App 圖示、截圖、說明文字）
- [ ] App Store 上架準備

---

## 多 Agent 協作說明

本專案採用 Claude + Codex 雙 AI 協作：

| | 角色 | 職責 |
|---|---|---|
| **Claude** | 統籌長 & 品質長 | 需求拆解、任務分派、校驗產出、回報用戶 |
| **Codex** | 首席開發工程師 | 程式撰寫、創意技術方案、演算法與邏輯實作 |

溝通橋樑：`C:\Users\cq4dario\source\ai-bridge\`
- `messages/`：Claude 分派給 Codex 的任務
- `shared/`：Codex 的產出與回應
- `history/`：對話歷史

Codex 收到任務後，將成果寫入 `shared/`，標記 `[Codex]` 與時間戳記，由 Claude 校驗後整合。
