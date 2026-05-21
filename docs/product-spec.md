# 同頻 Today 產品規格

## 產品定位

同頻 Today 是生活共鳴型社交 APP。它不是以明確交往、快速配對或照片滑卡為主，而是讓使用者在「今天發生了一件事」之後，遇到一個剛好懂的人。

核心句：

> 今天發生了一件事，我想遇到一個剛好懂的人。

## MVP 目標

第一版以 Web App / PWA 方向驗證：

> 使用者是否願意輸入「今天發生了什麼」，並因為今日共鳴與陌生人開始聊天。

## 核心功能

### 今日發生了什麼

使用者可以輸入今日事件，並選擇期待回應方式與可見範圍。系統用 rule-based 分析：

- eventType
- emotionTags
- valueTags
- interestTags
- responseMode
- visibility

### 今日共鳴配對

配對分數：

- eventType 相同：+30
- emotionTags 有交集：每個 +10
- valueTags 有交集：每個 +10
- interestTags 有交集：每個 +5
- responseMode 相容：+15

最低推薦門檻：40 分。

### 樹洞

使用者可發公開文章，發文模式包含：

- JUST_SAYING
- COMFORT_ME
- GIVE_ADVICE
- RANT_TOGETHER
- DISTRACT_ME
- FIND_SIMILAR

第一版支援發文、列表、我懂、留言、檢舉。

### 任務型慢熟互動

任務不是約會，而是低壓陪伴。第一版使用固定任務清單，使用者可以點選參加。

### 聊天

第一版提供聊天室與文字訊息 API，不做圖片、語音、已讀或推播。

## 安全原則

- 不要求真名
- 不顯示精準位置
- 不強迫戀愛目的
- 樹洞禁止公開個資、肉搜、仇恨言論、性騷擾、暴力威脅、自傷鼓勵、詐騙與外部連結洗版

## 未來擴充

- AI 今日事件語意分析
- AI 破冰句生成
- AI 詐騙偵測
- 真人驗證
- 手機驗證
- 小群共鳴房
- 線下活動票券
- 任務通行證
- 付費同頻 Plus
- PWA 安裝
- App 上架
- Supabase / PostgreSQL
- 金流串接：綠界 / 藍新 / LINE Pay
