// 用各地地標驗「座標 → 縣市」。跑法：npm run test:geo
//
// 這個專案的前端沒有測試框架，所以用 node 內建的 test runner 直接跑
// src/shared/geo/countyLookup.ts（node 22.18 起能直接讀 .ts）。
//
// 地標是刻意挑的，每一組都對應一種會出錯的方式——見各組的說明。
import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { findCounty, OFFSHORE_TOLERANCE_KM } from '../src/shared/geo/countyLookup.ts'

const read = (path) => readFileSync(new URL(path, import.meta.url), 'utf8')
const shapes = JSON.parse(read('../src/shared/geo/taiwanCounties.json'))

const where = (latitude, longitude) => findCounty(latitude, longitude, shapes)

function expectAll(cases) {
  for (const [place, latitude, longitude, expected] of cases) {
    assert.equal(where(latitude, longitude), expected, `${place}（${latitude}, ${longitude}）`)
  }
}

test('界線資料的縣市清單和選單一字不差', () => {
  // 換算出來的名字會直接存成「所在縣市」，後端只收清單裡的值——
  // 這裡多一個字不同（台／臺），定位成功的人反而存檔失敗。
  const source = read('../src/features/profile/profileFields.ts')
  const block = source.match(/export const AREA_OPTIONS = \[([\s\S]*?)\]/)
  assert.ok(block, 'profileFields.ts 裡找不到 AREA_OPTIONS')
  const options = [...block[1].matchAll(/'([^']+)'/g)].map((m) => m[1]).sort()
  const names = shapes.counties.map((c) => c.name).sort()
  assert.deepEqual(names, options)
})

test('每個縣市的市中心都落在自己的縣市', () => {
  expectAll([
    ['台北車站', 25.0478, 121.5170, '台北市'],
    ['板橋車站', 25.0143, 121.4637, '新北市'],
    ['基隆車站', 25.1319, 121.7394, '基隆市'],
    ['桃園車站', 24.9892, 121.3135, '桃園市'],
    ['新竹車站', 24.8016, 120.9717, '新竹市'],
    ['竹北車站', 24.8390, 121.0094, '新竹縣'],
    ['苗栗車站', 24.5701, 120.8224, '苗栗縣'],
    ['台中車站', 24.1369, 120.6850, '台中市'],
    ['彰化車站', 24.0817, 120.5385, '彰化縣'],
    ['南投市區', 23.9090, 120.6840, '南投縣'],
    ['斗六車站', 23.7117, 120.5410, '雲林縣'],
    ['嘉義車站', 23.4791, 120.4411, '嘉義市'],
    ['嘉義縣政府（太保）', 23.4518, 120.2555, '嘉義縣'],
    ['台南車站', 22.9972, 120.2126, '台南市'],
    ['高雄車站', 22.6394, 120.3025, '高雄市'],
    ['屏東車站', 22.6693, 120.4862, '屏東縣'],
    ['宜蘭車站', 24.7546, 121.7580, '宜蘭縣'],
    ['花蓮車站', 23.9934, 121.6011, '花蓮縣'],
    ['台東車站', 22.7937, 121.1232, '台東縣'],
    ['馬公市區', 23.5650, 119.5660, '澎湖縣'],
    ['金城鎮', 24.4320, 118.3180, '金門縣'],
    ['南竿', 26.1600, 119.9500, '連江縣'],
  ])
})

test('離隔壁縣市政府比較近的地方，仍然算自己的縣市', () => {
  // 用「離哪個縣市政府最近」來猜的話，這幾個全錯——這是要用界線的理由。
  expectAll([
    ['新店（離台北市政府比板橋近）', 24.9677, 121.5417, '新北市'],
    ['汐止（離台北市政府比板橋近）', 25.0680, 121.6617, '新北市'],
    ['淡水', 25.1678, 121.4456, '新北市'],
    ['鶯歌（離桃園市政府比板橋近）', 24.9544, 121.3553, '新北市'],
    ['中壢', 24.9537, 121.2256, '桃園市'],
    ['民雄（離嘉義市政府比太保近）', 23.5551, 120.4316, '嘉義縣'],
    ['阿里山', 23.5100, 120.8030, '嘉義縣'],
    ['日月潭', 23.8570, 120.9160, '南投縣'],
  ])
})

test('被別的縣市整個包住的城市不會被外面那一圈收走', () => {
  // 台北市在新北市的外框裡、嘉義市在嘉義縣的外框裡，資料裡是「洞」。
  // 洞沒處理的話，結果取決於清單裡誰排前面——而現在的排序剛好是
  // 台北市、嘉義市排在前面，所以照原本的順序查，洞壞了也看不出來。
  // 因此正著查一次、倒過來再查一次：答案不該跟順序有關。
  const enclosed = [
    ['台北 101', 25.0340, 121.5645, '台北市'],
    ['士林', 25.0930, 121.5260, '台北市'],
    ['嘉義市政府', 23.4810, 120.4530, '嘉義市'],
  ]
  expectAll(enclosed)

  const reversed = { ...shapes, counties: [...shapes.counties].reverse() }
  for (const [place, latitude, longitude, expected] of enclosed) {
    assert.equal(findCounty(latitude, longitude, reversed), expected, `${place}（清單倒過來查）`)
  }
})

test('離島算得到', () => {
  expectAll([
    ['綠島', 22.6600, 121.4900, '台東縣'],
    ['蘭嶼', 22.0450, 121.5500, '台東縣'],
    ['小琉球', 22.3400, 120.3700, '屏東縣'],
    ['墾丁', 21.9450, 120.7980, '屏東縣'],
    ['東引', 26.3660, 120.4900, '連江縣'],
    ['烈嶼（小金門）', 24.4300, 118.2400, '金門縣'],
  ])
})

test('被定在岸邊海面上的人，算最近的縣市', () => {
  // 「大概位置」的誤差是公里級，站在海堤上常常被定到海裡
  expectAll([
    ['淡水河口外', 25.1900, 121.3950, '新北市'],
    ['旗津外海', 22.6000, 120.2550, '高雄市'],
    ['花蓮港外', 23.9800, 121.6450, '花蓮縣'],
  ])
})

test('離岸時同時靠近兩個縣市，取比較近的那個', () => {
  // 用兩個假的方塊，不靠真實海岸線：A 在西、B 在東，中間隔 4 公里的水。
  // 站在離 A 1 公里、離 B 3 公里的水面上，兩個都在容許距離內——要算 A。
  // 清單裡 A 排前面，所以「留下最後一個比到的」這種寫法會答成 B。
  const square = (west, east) => [[[west, 24000, east, 24000, east, 24100, west, 24100]]]
  const kmToLon = (km) => (km / 101.9) * 1000
  const strait = {
    scale: 1000,
    counties: [
      { name: 'A', polygons: square(121000 - 100, 121000) },
      { name: 'B', polygons: square(121000 + kmToLon(4), 121000 + kmToLon(4) + 100) },
    ],
  }
  const longitude = (121000 + kmToLon(1)) / 1000
  assert.equal(findCounty(24.05, longitude, strait), 'A')
  assert.equal(findCounty(24.05, longitude, { ...strait, counties: [...strait.counties].reverse() }), 'A')
})

test('不在台灣就是不在台灣', () => {
  expectAll([
    ['東京', 35.6812, 139.7671, null],
    ['香港', 22.3193, 114.1694, null],
    ['廈門島', 24.4800, 118.0900, null],
    ['花蓮外海 200 公里', 23.5000, 123.6000, null],
    ['赤道與本初子午線交點（定位失敗時常見的假座標）', 0, 0, null],
  ])
  assert.equal(findCounty(Number.NaN, 121.5, shapes), null)
})

test('離岸容許距離沒有被放得太寬', () => {
  // 花蓮港正東方：3 公里內還算花蓮，10 公里外就不算了
  assert.ok(OFFSHORE_TOLERANCE_KM <= 5)
  assert.equal(where(23.9800, 121.7300), null, '花蓮港外約 11 公里')
})
