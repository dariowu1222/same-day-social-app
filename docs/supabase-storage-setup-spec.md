# 規格書：Supabase Storage 媒體儲存設定（給 Codex / 操作者）

> 目的：把 app 的照片／圖音從「base64 data URL 塞在資料庫」改成「物件儲存 + DB 只存 URL」。
> 後端已用 `IMediaStorage` 抽象，不綁死 Supabase；本規格只負責「Supabase 那一端的設定 + 提供金鑰給後端」。

## 1. 建立 Storage Bucket

在 Supabase Dashboard → Storage → New bucket：

- **Bucket 名稱**：`media`
- **Public bucket**：✅ 開啟（MVP 階段用公開讀取，URL 可直接顯示；之後要強化再改私有 + signed URL）
- 進階（可留預設）：檔案大小上限可設 `5MB`、允許的 MIME 可設 `image/*,audio/*`

> bucket 內用資料夾分流（後端會自動帶）：`avatars/`、`rants/`、`chats/`。不需手動建資料夾。

## 2. 存取政策（RLS Policy）

因為**上傳一律由後端**用 service key 進行（不是前端直傳），所以：

- **讀取**：public bucket 預設就能公開讀，免額外 policy。
- **寫入／刪除**：由後端 service key 進行（service key 會繞過 RLS），**不需要**為前端 anon 角色開寫入 policy。
- ✅ 結論：**不要**對前端 anon 開放 insert/update/delete policy（保持只有後端能寫）。

若 dashboard 要求至少一條 policy 才能用，加一條「**public SELECT**」即可：

```sql
-- Storage 公開讀取（僅讀，不開寫）
create policy "public read media"
on storage.objects for select
to public
using ( bucket_id = 'media' );
```

## 3. 提供給後端的金鑰（填進 .NET User Secrets）

到 Supabase Dashboard → Project Settings → API，取得：

- **Project URL**：`https://uqacfftvowhehvkyafep.supabase.co`
- **service_role key**（⚠️ 機密，只放後端、不可進前端/版控）

填進後端 User Secrets（`UserSecretsId: 3804080e-8e15-4114-a5f3-d18eddcb3d56`），新增這幾個 key：

```json
{
  "Media:Provider": "supabase",
  "Media:SupabaseUrl": "https://uqacfftvowhehvkyafep.supabase.co",
  "Media:ServiceKey": "<service_role key>",
  "Media:Bucket": "media"
}
```

> 後端會用 `Authorization: Bearer <ServiceKey>` 上傳到
> `POST {SupabaseUrl}/storage/v1/object/{Bucket}/{folder}/{guid.ext}`，
> 成功後回傳公開 URL：`{SupabaseUrl}/storage/v1/object/public/{Bucket}/{folder}/{guid.ext}`。
> 這段後端已實作（`SupabaseMediaStorage`），你只要把上面金鑰填好即可。

## 4. 驗證方式

1. 後端填好 User Secrets、重啟。
2. 登入後上傳一張個人照片。
3. 預期：DB `users.photo_data_urls` 存的是 **`https://...supabase.co/storage/v1/object/public/media/avatars/xxx.jpg`**（不再是 `data:image/...;base64,...`）。
4. 在 Supabase Storage → media bucket → avatars/ 應能看到該檔案。

## 5. 注意事項 / 之後可強化

- 舊資料：現有的 base64 data URL **不會自動遷移**，採「之後重存才轉新格式」；要批次遷移可另排。
- 安全強化（之後）：改 private bucket + 後端發 signed URL；加上傳大小／類型驗證、病毒/NSFW 審核。
- 換供應商：未來要換 Cloudflare R2 / AWS S3，只需新增一個 `IMediaStorage` adapter + 換 config，**schema 與其他程式碼不動**。
