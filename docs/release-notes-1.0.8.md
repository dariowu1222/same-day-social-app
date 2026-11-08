# 1.0.8（versionCode 9）上架資訊

## Play Console 欄位

| 欄位 | 值 |
|---|---|
| 應用程式套件 | `com.tongpin.today` |
| 版本名稱 | `1.0.8` |
| 版本代碼 | `9` |
| Play 版本名稱欄位 | `9 (1.0.8)` |
| 最低 SDK | 24（Android 7.0） |
| 目標 SDK | 36 |
| AAB | `frontend/android/app/build/outputs/bundle/release/app-release.aab`（6.38 MB，TONGPIN 簽章） |
| 後端 | https://tongpin-api-twu8.onrender.com（commit `ded74f1`） |

## 部署前置：先跑 migration，再推

`render.yaml` 的 `autoDeployTrigger: commit` 代表 push 到 main 後端就自動重新部署。
這一版的 `reports` 多了一欄，**SQL 沒先跑就部署，使用者端的「送出檢舉」會整條壞掉**——
EF 會把 `outcome_mail_sent` 放進每一筆 reports 的 INSERT。讀取不受影響，後台佇列照常。

這支是加欄位、可重複執行，先跑不會影響目前線上的舊版後端。

```sql
alter table reports
  add column if not exists outcome_mail_sent boolean;

update reports set status = 'PENDING' where status = 'REVIEWING';
```

https://supabase.com/dashboard/project/uqacfftvowhehvkyafep/sql/new

（2026-11-08 查過：`outcome_mail_sent` 尚未存在，`REVIEWING` 殘留 0 筆，所以第二句是空操作。）

## 版本資訊（zh-TW，約 290 / 500 字）

```
<zh-TW>
這一版把重心放在樹洞的回應與檢舉。

・有人回應過的貼文，卡面直接看得到是誰回的
・留言框移到回覆底下，點哪一則就從哪一則展開
・檢舉改成原地展開，不再跳出一個白色面板；夜間模式終於跟著變暗
・回應也能檢舉了，和貼文一樣是紅色的「檢舉」
・檢舉處理完會收到結果通知信，被誤判的貼文可以退回原作者
・對方帳號停權時，聊天室會明說正在審核，不再讓你白打一段訊息
・聊天室抬頭顯示對方暱稱，不再出現房號
・「公開個人檔案」的開關現在真的有作用
・改了暱稱，樹洞上的舊貼文也會跟著更新
・全站文字重新校過一遍
</zh-TW>
```

## 刻意沒寫進去的

- **安全性修補**：這一版補了不少（驗證碼猜測次數、上傳來源限制、個人資料外洩面、
  各欄位長度與速率限制）。修補清單本身就是攻擊地圖，照慣例不寫進公開的版本資訊。
- **推播通知**：狀態和 1.0.7 一樣——client 端有 `google-services.json`，但 Firebase
  專案根本沒建，Render 上的 `Push__FirebaseServiceAccountJsonBase64` 是空的，
  伺服器一則都送不出去。等金鑰填好再放進版本資訊。
- **今日的 AI 標籤**：仍在「會員測試AI」開關後面。
- **後台**：檢舉狀態收斂成四種、寄信結果落地、隱藏貼文可退回、逾期檢舉清內容、
  驗證碼不再有 60 秒重寄間隔——使用者看不到。

## 這一版之後還欠的

- 服務條款與隱私權政策目前只存在 App 內（`frontend/src/features/auth/legalDocuments.ts`），
  **沒有公開網址**。Play Console 的「應用程式內容 → 交易商身分」要求提供服務條款連結，
  得先把它產成一頁 HTML 掛上去。
- 日間的檢舉紅 `#d4785a` 對比 3.17，低於 12px 小字的 AA 門檻 4.5。
  要過門檻的話換成 `#c0573c`（4.51，一樣是陶土紅但更偏磚色）。
