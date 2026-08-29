# 後端部署到 Render（免費方案）

> 目標：把 `backend/`（.NET 8 Web API）跑在一個公開的 https 網址上，讓 Android App 連得到。
> 資料庫沿用現有的 Supabase，不需要另外開。
> 需要你親自登入的步驟都標了 **【你做】**，其餘我已經備好。

---

## 免費方案的限制（先知道再決定）

| | Render Free |
|---|---|
| 費用 | $0，不需信用卡 |
| 休眠 | **15 分鐘沒有流量就休眠**，下一個請求要等約 50 秒冷啟動 |
| 適合 | 內部測試、封閉測試、Demo |
| 不適合 | 正式對外營運（測試者第一次開會等很久，容易誤判成當機） |

> 之後要消除冷啟動，把 plan 從 `free` 改成 `starter`（約 US$7/月）即可，不需改任何程式。

---

## 第 0 步：先把要填的值準備好（不需登入）

在終端機執行，會列出你本機的所有機密設定，等一下要複製到 Render：

```bash
cd "C:\Users\p0989\Desktop\claude floder\同頻Today App\backend"
```

```bash
dotnet user-secrets list
```

你會看到 `ConnectionStrings:DefaultConnection`、`Smtp:*`、`Media:*`、`Authentication:Facebook:AppSecret` 等。

⚠️ **連線字串要改兩個地方再貼上 Render**：

1. 埠號必須是 **5432**（session pooler），不能是 6543
2. 結尾補上連線池參數（沒補的話連續請求會把 pooler 槽位吃滿，整個後端會卡死）：

```
;Maximum Pool Size=8;Connection Idle Lifetime=20;Connection Pruning Interval=10;Timeout=15;Command Timeout=30
```

---

## 第 1 步：【你做】註冊 Render

👉 https://dashboard.render.com/register

用 GitHub 帳號登入最快（之後選 repo 不必再授權一次）。免費方案不需要信用卡。

---

## 第 2 步：【你做】用 Blueprint 建立服務

👉 https://dashboard.render.com/select-repo?type=blueprint

1. 找到並選擇 `dariowu1222/same-day-social-app`
   （第一次會要求授權 Render 讀取你的 GitHub，按 **Configure account** → 選這個 repo）
2. Render 會自動讀到 repo 根目錄的 `render.yaml`，顯示要建立 **tongpin-api**
3. Blueprint Name 隨意，按 **Apply**

這一步不會馬上成功——因為機密環境變數還沒填，服務會啟動失敗。這是預期的，繼續下一步。

---

## 第 3 步：【你做】填入機密環境變數

👉 https://dashboard.render.com/

進入 **tongpin-api** → 左側 **Environment** → 每個標示 `sync: false` 的變數按 **Add** 填值：

| 變數 | 值從哪來 |
|---|---|
| `ConnectionStrings__DefaultConnection` | 第 0 步的連線字串（**改 5432 + 補連線池參數**） |
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Username` | 第 0 步的 `Smtp:Username`（tongpintoday@gmail.com） |
| `Smtp__Password` | 第 0 步的 `Smtp:Password`（Gmail 應用程式密碼） |
| `Smtp__FromEmail` | 同 Username |
| `Media__SupabaseUrl` | `https://uqacfftvowhehvkyafep.supabase.co` |
| `Media__ServiceKey` | 第 0 步的 `Media:ServiceKey`（Supabase service_role key） |
| `Authentication__Facebook__AppSecret` | 第 0 步的同名值（沒有就先略過，FB 登入本來就還沒通） |

其餘變數（`Jwt__Secret`、`ASPNETCORE_ENVIRONMENT`、`Smtp__Port`…）`render.yaml` 已經帶好，不用動。

> `Jwt__Secret` 由 Render 自動產生。**不要事後去改它**——換掉等於所有使用者的登入權杖失效，全部被登出。

填完按 **Save, rebuild, and deploy**。

---

## 第 4 步：確認部署成功（我可以幫你驗）

部署完成後，服務網址長這樣：`https://tongpin-api.onrender.com`（實際名稱以 Render 顯示為準）

用瀏覽器或終端機打健康檢查：

```bash
curl https://tongpin-api.onrender.com/health
```

預期回傳：`{"status":"ok"}`

如果失敗，到 Render 的 **Logs** 分頁看啟動訊息。常見錯誤：

| 訊息 | 原因與修法 |
|---|---|
| `正式環境必須設定 ConnectionStrings:DefaultConnection` | 連線字串沒填或名稱打錯（注意是**兩個底線** `__`） |
| `缺少 Jwt:Secret` / `Jwt:Secret 太短` | 密鑰未產生或少於 32 位元組，在 Environment 重新產生一個長一點的 |
| `RowDescription while expecting ParseCompleteMessage` | 連線字串用了 6543，改成 5432 |
| 一開始正常、連續請求後整個卡住 | 連線字串結尾少了連線池參數（見第 0 步） |

---

## 第 5 步：把網址接進 App（我做）

拿到網址後，填進 `frontend/.env.production`：

```
VITE_API_BASE_URL=https://tongpin-api.onrender.com
```

然後重新打包（我可以跑）：

```bash
cd "C:\Users\p0989\Desktop\claude floder\同頻Today App\frontend"
```

```bash
npm run cap:play
```

```bash
cd "C:\Users\p0989\Desktop\claude floder\同頻Today App\frontend\android"
```

```bash
./gradlew bundleRelease
```

產出：`frontend/android/app/build/outputs/bundle/release/app-release.aab`

---

## 第 6 步：【你做】Google 登入要加上新網址

Google OAuth 的授權來源目前只有 localhost。加上 Render 網址：

👉 https://console.cloud.google.com/apis/credentials

找到 Web client（ID 開頭 `740360755278-`）→ **已授權的 JavaScript 來源** 加入你的 Render 網址 → 儲存。

> 純 Android App 走原生登入不一定會擋，但網頁版與部分流程會檢查來源，先加起來比較省事。

---

## 之後每次更新

`render.yaml` 設了 `autoDeployTrigger: commit`，所以 **push 到 main 就會自動重新部署後端**，不必手動操作。前端（App）則要重新打包才會更新。
