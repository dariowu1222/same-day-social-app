import sharp from 'sharp'
import { readFileSync, statSync } from 'fs'
import { join } from 'path'

const dir = './src/assets'
const files = ['1', '2', '3', '4', '5']

for (const name of files) {
  const input = join(dir, `${name}.png`)
  const output = join(dir, `${name}.webp`)

  const before = statSync(input).size
  await sharp(input).webp({ quality: 82 }).toFile(output)
  const after = statSync(output).size

  const saved = (((before - after) / before) * 100).toFixed(1)
  console.log(`${name}.png  ${(before / 1024).toFixed(0)} KB  →  ${name}.webp  ${(after / 1024).toFixed(0)} KB  (-${saved}%)`)
}
