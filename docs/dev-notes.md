# 開發筆記

## 技術決策

原始規格建議 Node.js + Express。考量未來維護性，本專案第一版改用 ASP.NET Core Web API + C#，前端仍使用 React + Vite + TypeScript。

## 分層

- Controllers：REST API
- Application/Services：業務邏輯
- Domain/Entities：資料模型
- Domain/Enums：列舉
- Infrastructure/Persistence：JSON storage

## 暫時不做

- 不串 OpenAI / Gemini / Claude API
- 不串金流
- 不做第三方登入
- 不做 GPS 精準定位
- 不做圖片或語音
- 不做複雜推薦演算法

## 維護注意

新增 API 時，避免在 Controller 直接讀寫檔案。資料存取應集中在 persistence / repository 類別，讓未來替換資料庫時不影響前端與 API contract。
