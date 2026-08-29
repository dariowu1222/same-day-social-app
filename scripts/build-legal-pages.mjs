// 從 App 內的法律文件單一來源產生公開網頁（Google Play 要求可公開存取的隱私權政策與帳號刪除說明）。
//
// 來源：frontend/src/features/auth/legalDocuments.ts
// 產出：docs/legal/{index,privacy,terms,account-deletion}.html
//
// 用法：node scripts/build-legal-pages.mjs
// 法律條文改動後重跑一次即可，不要直接手改產出的 HTML。

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const SRC = join(root, 'frontend/src/features/auth/legalDocuments.ts')
const OUT_DIR = join(root, 'docs/legal')

const CONTACT_EMAIL = 'tongpintoday@gmail.com'
const APP_NAME = '同頻 Today'
const UPDATED = '2026-08-29'

// TS 檔內容就是純物件字面值，去掉型別標註後即可直接求值
const source = readFileSync(SRC, 'utf8')
  .replace(/export type[\s\S]*?\n/g, '')
  .replace(/^type .*$/gm, '')
  .replace(/export const (\w+): LegalContent =/g, 'const $1 =')
  .split('export function')[0]  // 檔尾的 getLegalContent helper 用不到，切掉
const { termsDocument, privacyDocument } = new Function(
  `${source}; return { termsDocument, privacyDocument }`
)()

const esc = (s) =>
  String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')

const STYLE = `
:root{
  --paper:#faf7f2; --surface:#fffdf9; --ink:#3b322a; --ink-soft:#6b5f52;
  --ink-faint:#9a8d7e; --line:#e5dcce; --green:#41704f; --coral:#c95f3f;
}
@media (prefers-color-scheme: dark){
  :root{
    --paper:#15130f; --surface:#1d1a15; --ink:#ebe3d6; --ink-soft:#b3a898;
    --ink-faint:#867c6f; --line:#332e26; --green:#8fc79b; --coral:#efa287;
  }
}
*{box-sizing:border-box}
body{
  margin:0;background:var(--paper);color:var(--ink);
  font-family:"Noto Sans TC","PingFang TC","Microsoft JhengHei",system-ui,sans-serif;
  font-size:16px;line-height:1.9;-webkit-font-smoothing:antialiased;
}
main{max-width:760px;margin:0 auto;padding:48px 22px 96px}
.eyebrow{
  font-size:12px;font-weight:700;letter-spacing:.2em;color:var(--green);
  text-transform:uppercase;margin:0 0 14px;
}
h1{
  font-family:"Noto Serif TC","Songti TC",Georgia,serif;font-weight:700;
  font-size:clamp(26px,4vw,36px);line-height:1.3;margin:0 0 10px;text-wrap:balance;
}
h2{
  font-family:"Noto Serif TC","Songti TC",Georgia,serif;font-weight:700;
  font-size:19px;margin:38px 0 10px;padding-top:22px;border-top:1px solid var(--line);
}
p{margin:0 0 14px;max-width:64ch}
ol{margin:0 0 14px;padding-left:24px;max-width:64ch}
li{margin:0 0 10px}
.meta{color:var(--ink-faint);font-size:13.5px;margin:0 0 8px}
a{color:var(--coral);text-underline-offset:3px}
.card{
  background:var(--surface);border:1px solid var(--line);border-radius:14px;
  padding:20px 22px;margin:0 0 22px;
}
.card > :last-child{margin-bottom:0}
.card h2{border:0;padding-top:0;margin-top:0}
nav.docs{display:flex;flex-wrap:wrap;gap:10px;margin:0 0 30px}
nav.docs a{
  display:inline-block;padding:7px 14px;border:1px solid var(--line);
  border-radius:20px;background:var(--surface);color:var(--ink-soft);
  text-decoration:none;font-size:14px;
}
nav.docs a:hover{border-color:var(--green);color:var(--green)}
nav.docs a:focus-visible{outline:2px solid var(--green);outline-offset:2px}
footer{margin-top:56px;padding-top:20px;border-top:1px solid var(--line);
  color:var(--ink-faint);font-size:13px}
`

function page({ title, body }) {
  return `<!doctype html>
<html lang="zh-Hant">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(title)}｜${APP_NAME}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Noto+Serif+TC:wght@700&family=Noto+Sans+TC:wght@400;700&display=swap">
<style>${STYLE}</style>
</head>
<body>
<main>
<nav class="docs">
  <a href="./index.html">首頁</a>
  <a href="./privacy.html">隱私權政策</a>
  <a href="./terms.html">服務條款</a>
  <a href="./account-deletion.html">刪除帳號</a>
</nav>
${body}
<footer>${APP_NAME}　聯絡信箱：<a href="mailto:${CONTACT_EMAIL}">${CONTACT_EMAIL}</a>　最後更新：${UPDATED}</footer>
</main>
</body>
</html>
`
}

function docBody(doc) {
  const sections = doc.sections
    .map(
      (s) =>
        `<h2>${esc(s.heading)}</h2>\n<ol>\n${s.items
          .map((i) => `  <li>${esc(i)}</li>`)
          .join('\n')}\n</ol>`
    )
    .join('\n')
  return `<p class="eyebrow">${APP_NAME}</p>
<h1>${esc(doc.title)}</h1>
<p class="meta">最後更新：${UPDATED}</p>
${sections}`
}

const deletionBody = `<p class="eyebrow">${APP_NAME}</p>
<h1>刪除帳號與資料</h1>
<p class="meta">最後更新：${UPDATED}</p>

<div class="card">
<h2>在 App 內刪除（最快）</h2>
<ol>
  <li>開啟 ${APP_NAME} → 底部導覽列的「設定」</li>
  <li>進入「帳號中心」</li>
  <li>選擇「刪除帳號」並確認</li>
</ol>
<p>確認後帳號立即停用，你將被登出且無法再登入。</p>
</div>

<div class="card">
<h2>沒有 App 也可以申請</h2>
<p>若你已移除 App 或無法登入，請從<strong>你註冊時使用的電子郵件</strong>寄信到
<a href="mailto:${CONTACT_EMAIL}?subject=%E5%88%AA%E9%99%A4%E5%B8%B3%E8%99%9F%E7%94%B3%E8%AB%8B">${CONTACT_EMAIL}</a>，
主旨寫「刪除帳號申請」。我們會在收到後 7 個工作天內處理完成並回信確認。</p>
<p>為避免他人冒用，我們只受理由註冊信箱寄出的申請。</p>
</div>

<h2>會刪除哪些資料</h2>
<ol>
  <li>帳號與登入憑證（電子郵件、密碼雜湊、第三方登入綁定）</li>
  <li>個人檔案（暱稱、自我介紹、生日、照片、興趣與交友意圖等欄位）</li>
  <li>今日事件內容與情緒標籤、配對紀錄</li>
  <li>樹洞貼文、留言與反應</li>
  <li>聊天室訊息、通知、封鎖名單與個人設定</li>
</ol>

<h2>會保留哪些資料、保留多久</h2>
<ol>
  <li>他人已收到的聊天訊息，會保留在對方的對話中；這些內容屬於對方帳號的一部分，我們無法單方面刪除。</li>
  <li>檢舉與違規處理紀錄，為維護其他使用者安全並依法配合查證，最長保留 <strong>1 年</strong>。</li>
  <li>系統運作日誌（不含貼文內容）最長保留 <strong>90 天</strong>，屆期自動清除。</li>
  <li>法律要求保存之資料，依相關法令規定期間保存。</li>
</ol>
<p>除上述例外，其餘個人資料於帳號刪除後 <strong>30 天</strong>內自資料庫與備份中移除。</p>

<h2>其他做法</h2>
<p>若你只是想暫時休息，可在 App 的「設定 → 隱私設定」開啟「暫停配對」，你的資料會保留但不會出現在其他人的配對名單中。</p>`

mkdirSync(OUT_DIR, { recursive: true })

const indexBody = `<p class="eyebrow">${APP_NAME}</p>
<h1>使用者條款與資料說明</h1>
<p>這裡是 ${APP_NAME} 的法律文件與資料處理說明，內容與 App 內顯示的版本相同。</p>
<div class="card">
  <h2>文件</h2>
  <ol>
    <li><a href="./privacy.html">隱私權政策</a>——我們蒐集什麼、怎麼用、保存多久</li>
    <li><a href="./terms.html">服務條款</a>——使用規範與雙方權利義務</li>
    <li><a href="./account-deletion.html">刪除帳號與資料</a>——如何刪除、刪除範圍與保留期限</li>
  </ol>
</div>
<p>有任何疑問請來信 <a href="mailto:${CONTACT_EMAIL}">${CONTACT_EMAIL}</a>。</p>`

const files = [
  ['index.html', page({ title: '使用者條款與資料說明', body: indexBody })],
  ['privacy.html', page({ title: privacyDocument.title, body: docBody(privacyDocument) })],
  ['terms.html', page({ title: termsDocument.title, body: docBody(termsDocument) })],
  ['account-deletion.html', page({ title: '刪除帳號與資料', body: deletionBody })],
]

for (const [name, html] of files) {
  writeFileSync(join(OUT_DIR, name), html, 'utf8')
  console.log('產出', join('docs/legal', name))
}
