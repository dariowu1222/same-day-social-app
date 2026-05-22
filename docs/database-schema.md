# 資料結構

目前已建立 Supabase PostgreSQL project：

- project：`same-day-social-app`
- project id / ref：`uqacfftvowhehvkyafep`
- region：`ap-northeast-1`
- database host：`db.uqacfftvowhehvkyafep.supabase.co`

第一版原本使用本機 JSON 檔案作為簡易 DB；現在已補上 PostgreSQL schema 與 EF Core model。下一步登入後端會逐步改成：

```text
Controller -> Service -> AppDbContext / Repository -> Supabase PostgreSQL
```

本機 JSON 仍暫時保留，方便尚未改寫的 MVP API 繼續可跑。

## users

- id
- nickname
- ageRange
- gender
- locationArea
- bio
- interestTags
- valueTags
- responsePreference
- createdAt
- updatedAt

## auth_accounts

- id
- userId
- username
- passwordHash
- lastLoginAt
- isDisabled
- createdAt
- updatedAt

## password_reset_tokens

- id
- userId
- codeHash
- expiresAt
- usedAt
- createdAt

## todayEntries

- id
- userId
- content
- eventType
- emotionTags
- valueTags
- interestTags
- responseMode
- visibility
- createdAt

## matches

- id
- userId
- matchedUserId
- matchScore
- matchType
- sharedTags
- reason
- icebreaker
- userLiked
- matchedUserLiked
- createdAt

## rantPosts

- id
- userId
- nickname
- content
- mode
- emotionTags
- createdAt
- likeCount
- replyCount
- isHidden
- reportCount
- replies

PostgreSQL 中樹洞資料拆成：

- `rant_posts`
- `rant_replies`
- `rant_reactions`
- `rant_reports`

## tasks

- id
- title
- description
- category
- duration
- difficulty
- participantLimit
- participantUserIds
- createdAt

PostgreSQL 中任務參與紀錄拆到 `task_participants`。

## chatRooms

- id
- userIds
- sourceType
- sourceId
- createdAt

PostgreSQL 中聊天室成員拆到 `chat_room_users`。

## chatMessages

- id
- chatRoomId
- senderId
- content
- createdAt

## 連線設定

不要把 Supabase connection string 寫進 Git。請用 .NET User Secrets 或部署環境變數。

本機開發建議：

```powershell
cd backend
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=db.uqacfftvowhehvkyafep.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_SUPABASE_DATABASE_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

## RLS 注意事項

目前 Supabase 回報 14 張 public table 尚未啟用 Row Level Security。因為本專案規劃由 ASP.NET Core 後端連資料庫，不讓前端直接用 Supabase anon key 操作資料，所以目前先不自動啟用 RLS，避免沒有 policy 時阻擋後端操作。

正式上線前若要讓前端直接使用 Supabase client，必須先設計 RLS policy。

## 未來資料庫替換方向

後續應逐步把 `JsonStorageService` 替換成 repository / DbContext，保留 Controller 與 Service 的 API contract。
