# API 規格

Base URL: `http://localhost:5000`

所有 API 回傳格式：

```json
{
  "success": true,
  "data": {}
}
```

錯誤格式：

```json
{
  "success": false,
  "code": "CONTENT_BLOCKED",
  "message": "這篇內容可能包含個資、攻擊或高風險內容，請調整後再發布。"
}
```

## Auth

### POST /api/auth/demo-login

Request:

```json
{
  "nickname": "使用者名稱"
}
```

Response:

```json
{
  "success": true,
  "data": {
    "userId": "user_xxx",
    "nickname": "使用者名稱"
  }
}
```

## Today Entry

### POST /api/today-entries

Request:

```json
{
  "userId": "user_xxx",
  "content": "今天上班被主管念，覺得很委屈",
  "responseMode": "COMFORT_ME",
  "visibility": "MATCH_ONLY"
}
```

### GET /api/today-entries/user/:userId

取得指定使用者的今日事件紀錄。

## Match

### GET /api/matches/today/:userId

取得今日共鳴配對。

### POST /api/matches/:matchId/like

標記使用者想聊聊。

## Rant Board

### POST /api/rants

建立樹洞文章。

### GET /api/rants

取得公開樹洞文章。

### POST /api/rants/:rantId/understand

按「我懂」。

### POST /api/rants/:rantId/replies

新增留言。

### POST /api/rants/:rantId/report

檢舉文章。第一版檢舉滿三次會隱藏。

## Tasks

### GET /api/tasks

取得固定任務清單。

### POST /api/tasks/:taskId/join

參加任務。

## Chat

### GET /api/chats/user/:userId

取得使用者聊天室。

### POST /api/chats

建立聊天室。

### GET /api/chats/:chatRoomId/messages

取得訊息。

### POST /api/chats/:chatRoomId/messages

送出文字訊息。
