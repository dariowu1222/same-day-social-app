// 把本機 .NET User Secrets 整理成 Render 環境變數，照表單順序印出來，方便逐格複製貼上。
//
// 用法：node scripts/render-env.mjs
//       node scripts/render-env.mjs --mask   （只檢查格式，值以 ***** 遮蔽）
//
// ⚠️ 這個腳本會把機密印在終端機，用完請清畫面（PowerShell: cls）。
//    輸出內容不要貼進聊天視窗、不要存檔、不要進版控。

import { execSync } from 'node:child_process'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const MASK = process.argv.includes('--mask')

const POOL_PARAMS =
  ';Maximum Pool Size=8;Connection Idle Lifetime=20;Connection Pruning Interval=10;Timeout=15;Command Timeout=30'
const SUPABASE_URL = 'https://uqacfftvowhehvkyafep.supabase.co'

let raw = ''
try {
  raw = execSync('dotnet user-secrets list', {
    cwd: join(root, 'backend'),
    encoding: 'utf8',
    stdio: ['ignore', 'pipe', 'pipe'],
  })
} catch {
  console.error('讀不到 User Secrets。請確認在專案 backend 目錄下可以執行 dotnet user-secrets list。')
  process.exit(1)
}

const secrets = {}
for (const line of raw.split(/\r?\n/)) {
  const i = line.indexOf(' = ')
  if (i > 0) secrets[line.slice(0, i).trim()] = line.slice(i + 3).trim()
}

// 連線字串：確保 session pooler 5432 + 補上連線池參數（缺了會讓後端在連續請求後卡死）
function buildConnectionString() {
  let cs = secrets['ConnectionStrings:DefaultConnection']
  if (!cs) return null
  const notes = []
  if (/Port=6543/.test(cs)) {
    cs = cs.replace('Port=6543', 'Port=5432')
    notes.push('已把 6543 改成 5432（transaction pooler 會造成 Npgsql 協定錯位）')
  }
  if (!/Maximum Pool Size/i.test(cs)) {
    cs = cs.replace(/;+$/, '') + POOL_PARAMS
    notes.push('已補上連線池參數（Supabase pooler 槽位有限，不補會在連續請求後整個卡死）')
  }
  return { value: cs, notes }
}

const cs = buildConnectionString()

const rows = [
  ['ConnectionStrings__DefaultConnection', cs?.value, cs?.notes ?? []],
  ['Smtp__Host', 'smtp.gmail.com', ['本機沒設，靠 appsettings.Development.json；正式環境不載入該檔，必須填']],
  ['Smtp__Username', secrets['Smtp:Username'], []],
  ['Smtp__Password', secrets['Smtp:Password'], []],
  ['Smtp__FromEmail', secrets['Smtp:FromEmail'], []],
  ['Media__SupabaseUrl', SUPABASE_URL, []],
  [
    'Media__ServiceKey',
    secrets['Media:ServiceKey'],
    ['本機沒有 → 到 Supabase 後台複製 service_role key：',
     'https://supabase.com/dashboard/project/uqacfftvowhehvkyafep/settings/api',
     '留空不會讓服務啟動失敗，但照片會退回 base64 存進資料庫'],
  ],
  [
    'Authentication__Facebook__AppSecret',
    secrets['Authentication:Facebook:AppSecret'],
    ['本機沒有 → 留空即可。沒設定時 FB 登入直接停用，不影響啟動與 Google 登入'],
  ],
]

const show = (v) => (MASK ? '*'.repeat(Math.min(String(v).length, 24)) : v)

console.log('\n═══ Render 環境變數（照表單順序）═══')
if (MASK) console.log('（--mask 模式：值已遮蔽，只檢查有沒有值）\n')
else console.log('⚠️ 以下含機密，複製完請清畫面（cls），不要貼給任何人\n')

let missing = 0
for (const [key, value, notes] of rows) {
  console.log('── ' + key)
  if (value) {
    console.log('   ' + show(value))
  } else {
    missing++
    console.log('   （空白）')
  }
  for (const n of notes) console.log('   ↳ ' + n)
  console.log('')
}

console.log(`共 ${rows.length} 格，其中 ${missing} 格需要你另外處理或留空。`)
console.log('填完按 Deploy Blueprint。清畫面：cls\n')
