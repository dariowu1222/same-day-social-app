-- 驗證碼要能被猜錯幾次就作廢
--
-- 為什麼現在才需要：註冊與重設密碼的驗證碼是 6 位數、有效 10 分鐘，而唯一的
-- 防線是 AuthController 的每 IP 每分鐘 10 次。一個 IP 猜不完一百萬組，但是
-- 一千個 IP 在十分鐘內可以猜掉四十萬組左右——也就是大約四成的機率直接
-- 重設掉別人的密碼。單一驗證碼自己要會數「被猜錯幾次」，這件事才擋得住。
--
-- 重設密碼那邊還有另一半：RequestPasswordResetAsync 從來不作廢舊的碼
-- （註冊那邊有做），所以同一個信箱可以先申請五次，讓五組碼同時有效，
-- 猜中的機率再乘五。那一半是程式改掉的，不需要欄位。
--
-- 執行順序：這支要先在 Supabase 跑過，再部署後端。
--
-- 沒跑就部署的話：EF 會把 failed_attempts 放進這兩張表每一筆的 INSERT，
-- 所以「寄註冊驗證碼」與「寄重設密碼驗證碼」兩條路都會當場失敗——
-- 新使用者註冊不了，忘記密碼的人也收不到信。登入與其他功能不受影響。

alter table registration_verification_tokens
  add column if not exists failed_attempts integer not null default 0;

alter table password_reset_tokens
  add column if not exists failed_attempts integer not null default 0;
