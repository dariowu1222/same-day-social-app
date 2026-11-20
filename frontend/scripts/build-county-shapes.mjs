// 產生 src/shared/geo/taiwanCounties.json —— 22 個縣市的簡化界線。
//
// 用途：使用者同意定位之後，在「手機上」把座標換算成所在縣市（見 src/shared/geo）。
// 界線資料跟著 App 一起出貨，換算不經過任何伺服器，座標不離開裝置。
//
// 為什麼不用「離哪個縣市政府最近」：新北市包著台北市，新店、汐止離台北市政府
// 比離板橋近，會被判成台北市；民雄離嘉義市也比離太保近。要準就得用界線。
//
// 資料來源：npm 套件 taiwan-atlas（MIT）的 counties-10t.json，
// 它是內政部「直轄市、縣市界線(TWD97經緯度)」的 TopoJSON 轉製版
// （https://data.gov.tw/dataset/7442，政府資料開放授權條款－第 1 版）。
// 這支腳本只讀那個 JSON，不執行套件裡的任何程式，所以套件不列為專案相依。
//
// 重新產生（縣市界線變動時才需要，上一次是 2014 年桃園升格）：
//   npm pack taiwan-atlas            # 在任意空資料夾
//   tar -xzf taiwan-atlas-*.tgz
//   node scripts/build-county-shapes.mjs <解開的資料夾>/package/counties-10t.json
//   npm run test:geo                 # 用地標驗一次
import { readFileSync, writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const input = process.argv[2]
if (!input) {
  console.error('用法：node scripts/build-county-shapes.mjs <counties-10t.json 的路徑>')
  process.exit(1)
}

// 簡化容許誤差。150 公尺遠小於定位本身的誤差（Android 的「大概位置」是公里級），
// 卻能把點數從一萬五千降到幾千。
const TOLERANCE_KM = 0.15
// 輸出座標放大 1000 倍存成整數：精度約 100 公尺，檔案比小數小得多。
const SCALE = 1000

// 台灣緯度附近，一度經度／緯度各是幾公里（只用來量簡化誤差，不必很準）
const KM_PER_LON = 101.9
const KM_PER_LAT = 110.6

const topo = JSON.parse(readFileSync(input, 'utf8'))
const [sx, sy] = topo.transform.scale
const [tx, ty] = topo.transform.translate

// ── 1. 解開 TopoJSON 的弧：差分編碼 + 量化 ──
const arcs = topo.arcs.map((arc) => {
  let x = 0
  let y = 0
  return arc.map(([dx, dy]) => {
    x += dx
    y += dy
    return [x * sx + tx, y * sy + ty]
  })
})

// ── 2. 逐「弧」簡化，而不是逐個多邊形 ──
// 相鄰縣市共用同一條弧。對弧簡化，兩邊拿到的是同一條線，界線才不會出現
// 縫隙（哪個縣市都不算）或重疊（先比到哪個算哪個）。
function simplify(points) {
  if (points.length <= 2) return points
  const keep = new Uint8Array(points.length)
  keep[0] = 1
  keep[points.length - 1] = 1
  const stack = [[0, points.length - 1]]
  while (stack.length > 0) {
    const [first, last] = stack.pop()
    const [ax, ay] = points[first]
    const [bx, by] = points[last]
    const ux = (bx - ax) * KM_PER_LON
    const uy = (by - ay) * KM_PER_LAT
    const lengthSq = ux * ux + uy * uy
    let worst = -1
    let worstDistance = 0
    for (let i = first + 1; i < last; i++) {
      const px = (points[i][0] - ax) * KM_PER_LON
      const py = (points[i][1] - ay) * KM_PER_LAT
      // 封閉的弧（整圈小島）頭尾同一點，線段長度是 0，改量到那一點的距離
      const t = lengthSq === 0 ? 0 : Math.max(0, Math.min(1, (px * ux + py * uy) / lengthSq))
      const distance = Math.hypot(px - t * ux, py - t * uy)
      if (distance > worstDistance) {
        worstDistance = distance
        worst = i
      }
    }
    if (worst !== -1 && worstDistance > TOLERANCE_KM) {
      keep[worst] = 1
      stack.push([first, worst], [worst, last])
    }
  }
  return points.filter((_, i) => keep[i])
}

const simplified = arcs.map((arc) =>
  simplify(arc).map(([lon, lat]) => [Math.round(lon * SCALE), Math.round(lat * SCALE)]),
)

// ── 3. 把弧接回一圈一圈的環 ──
function ringOf(arcIndexes) {
  const points = []
  for (const index of arcIndexes) {
    // 負的索引代表反著走（TopoJSON 的規則是 ~i）
    const arc = index >= 0 ? simplified[index] : [...simplified[~index]].reverse()
    for (const point of arc) {
      const previous = points[points.length - 1]
      if (!previous || previous[0] !== point[0] || previous[1] !== point[1]) points.push(point)
    }
  }
  // 環的頭尾是同一點，存的時候拿掉尾巴，查的時候自己繞回來
  const first = points[0]
  const last = points[points.length - 1]
  if (points.length > 1 && first[0] === last[0] && first[1] === last[1]) points.pop()
  return points
}

let droppedRings = 0
let totalPoints = 0
const counties = topo.objects.counties.geometries.map((geometry) => {
  const polygonArcs = geometry.type === 'Polygon' ? [geometry.arcs] : geometry.arcs
  const polygons = []
  for (const rings of polygonArcs) {
    const kept = []
    rings.forEach((ringArcs, ringIndex) => {
      const points = ringOf(ringArcs)
      // 簡化加上取整之後剩不到三個點，就不成面了（幾十公尺的礁岩）。
      // 外環沒了，整個多邊形連洞一起丟；這種地方交給查詢時的「離岸不遠算最近的縣市」。
      if (points.length < 3) {
        droppedRings++
        return
      }
      if (ringIndex > 0 && kept.length === 0) return
      totalPoints += points.length
      kept.push(points.flat())
    })
    if (kept.length > 0) polygons.push(kept)
  }
  return { name: geometry.properties.COUNTYNAME, polygons }
})

counties.sort((a, b) => a.name.localeCompare(b.name, 'zh-Hant'))

const output = resolve(dirname(fileURLToPath(import.meta.url)), '../src/shared/geo/taiwanCounties.json')
const json = JSON.stringify({ scale: SCALE, counties })
writeFileSync(output, json + '\n')

console.log(`縣市 ${counties.length} 個｜多邊形 ${counties.reduce((n, c) => n + c.polygons.length, 0)} 個｜點 ${totalPoints}（原始 ${arcs.reduce((n, a) => n + a.length, 0)}）｜丟掉退化的環 ${droppedRings} 個`)
console.log(`寫入 ${output}（${(json.length / 1024).toFixed(1)} KB）`)
for (const county of counties) {
  const points = county.polygons.reduce((n, rings) => n + rings.reduce((m, r) => m + r.length / 2, 0), 0)
  console.log(`  ${county.name}  多邊形 ${county.polygons.length}  點 ${points}`)
}
