// 敏感詞產生器：讀 safety-lexicon.json，生成前端 TS 與後端 C# 兩個詞庫檔。
// 用法： node shared/lexicon/generate.mjs
import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const here = dirname(fileURLToPath(import.meta.url))
const repo = resolve(here, '..', '..')
const lex = JSON.parse(readFileSync(resolve(here, 'safety-lexicon.json'), 'utf8'))

const banner = '// 自動生成，請勿手改。改詞請編輯 shared/lexicon/safety-lexicon.json 後執行 node shared/lexicon/generate.mjs'
const tsArr = (a) => '[' + a.map((w) => JSON.stringify(w)).join(', ') + ']'
const csArr = (a) => a.map((w) => JSON.stringify(w)).join(', ')

// ── 前端 TS ──
const ts = `${banner}
export const ABUSE_ZH: string[] = ${tsArr(lex.abuse.zh)}
export const ABUSE_EN: string[] = ${tsArr(lex.abuse.en)}
`
writeFileSync(resolve(repo, 'frontend/src/features/chat/lexicon.generated.ts'), ts)

// ── 後端 C# ──
const cs = `${banner}
namespace SameDaySocialApp.Application.Services;

public static class SafetyLexicon
{
    public static readonly string[] AbuseZh = { ${csArr(lex.abuse.zh)} };
    public static readonly string[] AbuseEn = { ${csArr(lex.abuse.en)} };
    public static readonly string[] BlockPii = { ${csArr(lex.moderation.blockPii)} };
    public static readonly string[] BlockViolence = { ${csArr(lex.moderation.blockViolence)} };
    public static readonly string[] WarnEmotion = { ${csArr(lex.moderation.warnEmotion)} };
}
`
writeFileSync(resolve(repo, 'backend/Application/Services/SafetyLexicon.cs'), cs)

console.log('generated: frontend/src/features/chat/lexicon.generated.ts, backend/Application/Services/SafetyLexicon.cs')
