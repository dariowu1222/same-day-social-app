# 交接紀錄（跨裝置 / 跨 session）

> 這份檔案由「交接文件處」session 維護，記錄進行中工作的最新狀態。
> 新裝置或新工程師接手時，先讀這份。

---

## 2026-06-13 — Facebook 登入設定（進行中）

**狀態：** 只差 `email` 權限尚未加入

**Facebook App ID：** `27976348205283552`

**下一步（在 Facebook 開發者後台操作）：**

1. 開啟使用案例頁：
   `https://developers.facebook.com/apps/27976348205283552/use-cases/`
2. 點「驗證並透過 Facebook 登入以驗證用戶並索取資料」卡片右邊的「自訂 / 編輯」
3. 在權限（Permissions）清單找到 `email` → 按「新增」（Add）
4. 加完後回瀏覽器，關掉 FB 錯誤視窗，重新點「使用 Facebook 繼續」

**備用路徑**（若上面找不到 email）：
`https://developers.facebook.com/apps/27976348205283552/app-review/permissions/`
（`email` 與 `public_profile` 預設是標準存取權，開發模式自己測即可）

---

## 2026-06-12 — 新裝置後端連線設定（已完成）

新電腦（`C:\Users\p0989\Desktop\claude floder\同頻Today App`）用 `dotnet user-secrets` 設定：
- `Smtp:Username` / `Smtp:Password` / `Smtp:FromEmail`（tongpintoday@gmail.com）
- `ConnectionStrings:DefaultConnection`（Supabase pooler，注意密碼中有連續兩個 K：`...Br4KK8Kud...`）

查公司電腦的 secrets：
`dotnet user-secrets list --project "C:\Users\cq4dario\source\同頻Today App\backend"`

---

## 2026-06-11 — 實機測試建置（已推 main，待實機驗證）

- `device-test.bat`：Windows 一鍵腳本（查 IP、防火牆、後端、打包、開 Android Studio）
- 詳細步驟：`docs/device-build.md`
- iOS 需要 Mac + Xcode，流程相同（`npm run cap:device:ios`）
