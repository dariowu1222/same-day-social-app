# API 規格

Base URL：`http://localhost:5000`

成功回應：

```json
{
  "success": true,
  "data": {}
}
```

失敗回應：

```json
{
  "success": false,
  "code": "INVALID_REQUEST",
  "message": "錯誤說明"
}
```

## Auth

### POST /api/auth/demo-login

Demo 用假登入，不驗證密碼。

Request:

```json
{
  "nickname": "使用者名稱"
}
```

### POST /api/auth/register

正式註冊，會寫入 `users` 與 `auth_accounts`。

Request:

```json
{
  "nickname": "小同",
  "email": "user@example.com",
  "password": "abc12345",
  "confirmPassword": "abc12345",
  "birthYear": "1995",
  "gender": "PRIVATE"
}
```

防呆規則：

- nickname：2 到 20 字
- email：必須是 Email 格式，且不可重複
- password：8 到 64 碼，需同時包含英文與數字
- confirmPassword：必須與 password 相同

### POST /api/auth/login

Request:

```json
{
  "email": "user@example.com",
  "password": "abc12345"
}
```

### POST /api/auth/password-reset/request

產生 6 位數驗證碼並透過 SMTP 寄信。SMTP 未設定或寄送失敗時，不回傳成功。

Request:

```json
{
  "email": "user@example.com"
}
```

### POST /api/auth/password-reset/verify

Request:

```json
{
  "email": "user@example.com",
  "code": "123456"
}
```

### POST /api/auth/password-reset/confirm

驗證碼正確且未過期時，更新 `auth_accounts.password_hash`，並將 token 標記為已使用。

Request:

```json
{
  "email": "user@example.com",
  "code": "123456",
  "newPassword": "newabc12345",
  "confirmPassword": "newabc12345"
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

取得指定使用者的今日事件。

## Match

### GET /api/matches/today/:userId

取得今日共鳴配對。

### POST /api/matches/:matchId/like

對配對按「想聊聊」。

## Rant Board

### POST /api/rants

新增樹洞文章。

### GET /api/rants

取得公開樹洞文章。

### POST /api/rants/:rantId/understand

按「我懂」。

### POST /api/rants/:rantId/replies

新增留言。

### POST /api/rants/:rantId/report

檢舉文章。

## Tasks

### GET /api/tasks

取得任務清單。

### POST /api/tasks/:taskId/join

加入任務。

## Chat

### GET /api/chats/user/:userId

取得使用者聊天室。

### POST /api/chats

建立聊天室。

### GET /api/chats/:chatRoomId/messages

取得聊天室訊息。

### POST /api/chats/:chatRoomId/messages

送出文字訊息。
