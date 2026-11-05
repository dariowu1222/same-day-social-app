# 1.0.7（versionCode 8）上架資訊

## Play Console 欄位

| 欄位 | 值 |
|---|---|
| 應用程式套件 | `com.tongpin.today` |
| 版本名稱 | `1.0.7` |
| 版本代碼 | `8` |
| Play 版本名稱欄位 | `8 (1.0.7)` |
| 最低 SDK | 24（Android 7.0） |
| 目標 SDK | 36 |
| AAB | `frontend/android/app/build/outputs/bundle/release/app-release.aab`（6.38 MB，已簽章） |
| 後端 | https://tongpin-api-twu8.onrender.com（commit 4ea9852） |

## 版本資訊（zh-TW，257 / 500 字）

```
<zh-TW>
這一版把重心放在配對品質。

・卡片上的標籤全部改成中文，不再出現 WORK_STRESS 這類英文代號
・新增中性的「工作」分類，不再把每一件和工作有關的事都歸成工作壓力
・兩個人同時想被安慰時不再算最契合；你今天比較低落時，牌堆會保留幾張給今天還有餘裕的人
・不再配到沒填性別或生日的空白帳號，你設定的性別與年齡範圍會被確實遵守
・已註銷、停權、封鎖的帳號不再出現在牌堆
・修正右滑顯示配對成功、聊天室卻永遠進不去的問題
・今日改成先存再跳頁，存失敗會明確告訴你，不會白打一篇
・心靈小語展開更順，捲動不再掉幀
</zh-TW>
```

## 刻意沒寫進去的

- **推播通知**：client 端有 google-services.json，但 Render 上的
  `Push__FirebaseServiceAccountJsonBase64` 沒設，`POST /api/push/digest` 回 404，
  伺服器一則都送不出去。等金鑰填好再放進下一版的版本資訊。
- **今日的 AI 標籤**：藏在「會員測試AI」開關後面，還不是所有人都會碰到的功能。
- 後台「AI 用量」分頁、假資料腳本：使用者看不到。
