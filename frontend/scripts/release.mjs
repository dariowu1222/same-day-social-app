/**
 * 升版腳本：一次更新所有版本號的來源。
 *
 * 版本號散在四個地方（package.json、Android 的 versionName/versionCode、
 * iOS 的 MARKETING_VERSION/CURRENT_PROJECT_VERSION），手動改很容易漏掉其中一處：
 * 漏掉 versionCode 會讓 Play Console 直接拒絕上傳；漏掉 package.json 則會讓畫面上
 * 顯示的版本與實際安裝的對不上——測試者回報問題時就分不出他手上是哪一版。
 *
 *   npm run release:patch   # 1.0.1 → 1.0.2（修 bug）
 *   npm run release:minor   # 1.0.2 → 1.1.0（加功能，相容）
 *   npm run release:major   # 1.1.0 → 2.0.0（不相容的改動）
 *
 * versionCode 一律 +1，與 versionName 無關——它只要遞增，Play 靠它判斷新舊。
 * 加 --dry 只印結果不寫檔。
 */
import { readFileSync, writeFileSync, existsSync } from 'node:fs'

const PKG = './package.json'
const GRADLE = './android/app/build.gradle'
const PBXPROJ = './ios/App/App.xcodeproj/project.pbxproj'

const [, , bumpArg, ...rest] = process.argv
const dryRun = rest.includes('--dry')

if (!['patch', 'minor', 'major'].includes(bumpArg)) {
  console.error('用法：node scripts/release.mjs <patch|minor|major> [--dry]')
  process.exit(1)
}

/** 取代字串中「唯一一處」符合的內容；找不到或找到多處都視為錯誤，避免改錯地方。 */
function replaceOnce(text, pattern, replacement, label) {
  const matches = text.match(new RegExp(pattern.source, pattern.flags + 'g'))
  if (!matches || matches.length !== 1) {
    throw new Error(`${label}：預期剛好一處符合，實際 ${matches?.length ?? 0} 處`)
  }
  return text.replace(pattern, replacement)
}

// --- package.json：算出新的 versionName ---
const pkgRaw = readFileSync(PKG, 'utf-8')
const pkg = JSON.parse(pkgRaw)
const parts = String(pkg.version).split('.').map(Number)
if (parts.length !== 3 || parts.some((n) => !Number.isInteger(n) || n < 0)) {
  throw new Error(`package.json 的 version 不是 x.y.z 格式：${pkg.version}`)
}
let [major, minor, patch] = parts
if (bumpArg === 'major') [major, minor, patch] = [major + 1, 0, 0]
if (bumpArg === 'minor') [minor, patch] = [minor + 1, 0]
if (bumpArg === 'patch') patch += 1
const oldVersion = pkg.version
const newVersion = `${major}.${minor}.${patch}`

// --- Android：versionName 跟著走，versionCode 單純 +1 ---
const gradleRaw = readFileSync(GRADLE, 'utf-8')
const oldCode = Number(gradleRaw.match(/versionCode\s+(\d+)/)?.[1])
if (!Number.isInteger(oldCode)) throw new Error(`${GRADLE} 讀不到 versionCode`)
const newCode = oldCode + 1

let gradleNext = replaceOnce(gradleRaw, /versionCode\s+\d+/, `versionCode ${newCode}`, 'versionCode')
gradleNext = replaceOnce(gradleNext, /versionName\s+"[^"]*"/, `versionName "${newVersion}"`, 'versionName')

// package.json 用字串取代而非 JSON.stringify，才不會動到既有的排版與鍵順序
const pkgNext = replaceOnce(
  pkgRaw,
  /("version":\s*")[^"]*(")/,
  `$1${newVersion}$2`,
  'package.json version',
)

// --- iOS：同一組值在 Debug/Release 兩個 build configuration 各出現一次 ---
let pbxNext = null
if (existsSync(PBXPROJ)) {
  const pbxRaw = readFileSync(PBXPROJ, 'utf-8')
  pbxNext = pbxRaw
    .replace(/MARKETING_VERSION = [^;]+;/g, `MARKETING_VERSION = ${newVersion};`)
    .replace(/CURRENT_PROJECT_VERSION = [^;]+;/g, `CURRENT_PROJECT_VERSION = ${newCode};`)
}

if (!dryRun) {
  writeFileSync(PKG, pkgNext)
  writeFileSync(GRADLE, gradleNext)
  if (pbxNext !== null) writeFileSync(PBXPROJ, pbxNext)
}

console.log(`${dryRun ? '[--dry 不寫檔] ' : ''}${oldVersion} (${oldCode})  →  ${newVersion} (${newCode})`)
console.log(`  package.json   version      ${newVersion}`)
console.log(`  build.gradle   versionName  ${newVersion}`)
console.log(`  build.gradle   versionCode  ${newCode}`)
console.log(
  pbxNext !== null
    ? `  project.pbxproj MARKETING_VERSION ${newVersion} / CURRENT_PROJECT_VERSION ${newCode}`
    : '  project.pbxproj 不存在，略過 iOS',
)
console.log('\n接著：npm run cap:play，再到 Android Studio 建置 AAB')
