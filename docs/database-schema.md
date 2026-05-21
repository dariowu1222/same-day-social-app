# 資料結構

第一版使用本機 JSON 檔案作為簡易 DB。資料存放於 `backend/Data`，所有讀寫都透過 `JsonStorageService`，routes 不直接操作檔案。

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

## chatRooms

- id
- userIds
- sourceType
- sourceId
- createdAt

## chatMessages

- id
- chatRoomId
- senderId
- content
- createdAt

## 未來資料庫替換方向

未來改成 SQLite / PostgreSQL / SQL Server 時，保留 Controller 與 Service，替換 `JsonStorageService` 為 repository / DbContext 即可。
