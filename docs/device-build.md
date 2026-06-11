# 同頻 Today — 實機測試建置指南（Android / iOS）

> 目標：把目前的 app 裝到實體手機上測試。
> 前提：手機和電腦連**同一個 Wi-Fi**，後端跑在電腦上。

---

## 最快路徑：一鍵腳本（Windows + Android）

repo 根目錄**點兩下 `device-test.bat`**，它會自動完成下面第 0～1 步的所有事情：

1. 偵測電腦區網 IP、寫入 `frontend/.env.device`
2. 防火牆放行 5000 埠（跳一次系統管理員確認視窗，按「是」）
3. 新視窗啟動後端（0.0.0.0:5000）
4. 前端打包（device 模式）→ 開啟 Android Studio

之後只要：手機接 USB（開 USB 偵錯）→ Android Studio 選手機 → 按 Run。
下面的手動步驟留作參考與疑難排解用。

---

## 觀念：兩種模式

| | 開發模式（預設） | 實機測試模式 |
|---|---|---|
| 網頁來源 | 連電腦的 Vite dev server（live reload） | 打包進 app（dist） |
| 適用 | Android 模擬器日常開發 | 實體手機測試、未來上架 |
| 指令 | `npx cap sync` | `npm run cap:device` |

切換由環境變數 `CAP_BUILD=device` 控制（`capacitor.config.ts` 內判斷），
**日常開發流程完全不變**。

---

## 第 0 步：共同準備（Android / iOS 都要做）

### 0-1. 查電腦的區網 IP

在 Windows 上開 cmd：

```
ipconfig
```

找到目前連線網卡的「IPv4 位址」，例如 `192.168.1.105`。

### 0-2. 建立 `.env.device`

```
cd frontend
copy .env.device.example .env.device
```

打開 `frontend/.env.device`，把 IP 改成上一步查到的：

```
VITE_API_BASE_URL=http://192.168.1.105:5000
```

> 這個檔案不會被 commit（已在 .gitignore），每台開發機自己設定。

### 0-3. 後端用 0.0.0.0 啟動（讓手機連得到）

Visual Studio 啟動設定下拉選 **device** profile，或用指令：

```
cd backend
dotnet run --launch-profile device
```

（等同 `dotnet run --urls http://0.0.0.0:5000`）

### 0-4. Windows 防火牆放行 5000 埠（只需設定一次）

用系統管理員身分開 PowerShell：

```powershell
New-NetFirewallRule -DisplayName "SameDay Backend 5000" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
```

### 0-5. 驗證手機連得到後端

手機瀏覽器開 `http://192.168.1.105:5000`（換成你的 IP），
看到「同頻 Today 正在啟動」頁面就表示通了。

---

## Android 實機

### 1. 建置並開啟 Android Studio

```
cd frontend
npm run cap:device:android
```

（= 打包 dist + sync + 開 Android Studio）

### 2. 手機開啟開發人員模式

1. 設定 → 關於手機 → 連點「版本號碼」7 次
2. 設定 → 開發人員選項 → 開啟「USB 偵錯」
3. USB 線接電腦，手機上點「允許 USB 偵錯」

### 3. 安裝執行

Android Studio 上方裝置下拉選你的手機 → 點綠色 ▶ Run。

或不開 Android Studio、直接命令列建 APK：

```
cd frontend\android
gradlew assembleDebug
```

APK 產出位置：`frontend/android/app/build/outputs/apk/debug/app-debug.apk`
（可直接傳給其他人安裝測試，安裝時手機要允許「未知來源」）

---

## iOS 實機（需要 Mac + Xcode）

### 1. 在 Mac 上建置並開啟 Xcode

```
cd frontend
npm install
npm run cap:device:ios
```

> Mac 上查電腦 IP：`ifconfig | grep "inet "`，`.env.device` 填 Mac 的 IP，
> 後端也要跑在同一台 Mac（或同網段的 Windows 機器）。

### 2. Xcode 簽章設定（第一次需要）

1. 左側點 **App** 專案 → TARGETS → App → **Signing & Capabilities**
2. 勾選 Automatically manage signing
3. Team 選你的 Apple ID（免費帳號即可，Xcode → Settings → Accounts 加入）
4. 免費帳號的 app 有效 7 天，過期重新 Run 一次即可

### 3. 安裝執行

1. iPhone 接上 Mac，Xcode 上方裝置選你的 iPhone → ▶ Run
2. 第一次會提示不受信任的開發者：
   iPhone 設定 → 一般 → VPN 與裝置管理 → 信任你的開發者憑證

---

## 疑難排解

| 症狀 | 原因 / 解法 |
|---|---|
| app 開啟全白 | 大多是還在開發模式（live reload 連不到 dev server）。確認用 `npm run cap:device` 而不是 `npx cap sync` |
| 「目前無法連線到伺服器」 | 依序檢查：後端是否用 device profile 跑起來、防火牆 5000 是否放行、`.env.device` 的 IP 是否正確、手機是否同一個 Wi-Fi |
| 「連線逾時」 | 同上；另外有些路由器開「AP 隔離」會擋裝置互連，要在路由器設定關閉 |
| iOS 連不到 http | 確認 Info.plist 有 `NSAllowsArbitraryLoads`（本 repo 已加） |
| Android 連不到 http | 確認 `network_security_config.xml` 有 `base-config cleartextTrafficPermitted="true"`（本 repo 已加） |
| 改了 `.env.device` 沒生效 | 環境變數是 build 時寫死進 dist 的，改完要重跑 `npm run cap:device` |

---

## 上架前要改回來的清單

實機測試用的放寬設定，正式上架（後端部署 https）前要收回：

- [ ] `frontend/capacitor.config.ts`：`android.allowMixedContent` 改回 `false`
- [ ] `frontend/android/.../network_security_config.xml`：移除 `<base-config cleartextTrafficPermitted="true" />`
- [ ] `frontend/ios/App/App/Info.plist`：移除 `NSAppTransportSecurity` 整段
- [ ] `.env.device` 改用正式後端網址（https），或改用 `.env.production`
- [ ] 後端 JWT Secret 換成正式金鑰
