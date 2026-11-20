// 座標 → 所在縣市。純計算，不碰網路、不碰裝置。
//
// 這裡刻意不 import 任何東西：界線資料由呼叫端傳進來。這樣 App 可以等到
// 使用者真的按下定位才去載那 70 幾 KB，scripts/check-county-lookup.mjs
// 也能直接用 node 跑這個檔來驗地標。

/** 一個縣市的界線。環是攤平的 [經度, 緯度, 經度, 緯度…]，數值已乘上 scale。 */
export type CountyShape = {
  name: string
  /** 多邊形的清單；每個多邊形的第一個環是外框，其餘是洞（例如嘉義縣中間的嘉義市）。 */
  polygons: number[][][]
}

export type CountyShapes = {
  scale: number
  counties: CountyShape[]
}

/**
 * 不在任何縣市的界線裡、但離岸這麼近以內，仍然算最近的那個縣市。
 *
 * 兩個原因：界線簡化過，海岸與小島的邊緣會被削掉一點；而 Android 給的
 * 「大概位置」誤差是公里級，站在漁港或海堤上的人常常被定在海面上。
 * 再遠就當作人不在台灣，交給使用者自己選。
 */
export const OFFSHORE_TOLERANCE_KM = 3

// 台灣緯度附近一度各是幾公里。只用來量「離岸多遠」，誤差在幾個百分點內就夠用。
const KM_PER_LON_DEGREE = 101.9
const KM_PER_LAT_DEGREE = 110.6

/** 點是不是在這一圈裡面（射線法：往右射一條線，數它穿過幾條邊）。 */
function insideRing(ring: number[], x: number, y: number): boolean {
  let inside = false
  const count = ring.length / 2
  for (let i = 0, j = count - 1; i < count; j = i++) {
    const xi = ring[i * 2]
    const yi = ring[i * 2 + 1]
    const xj = ring[j * 2]
    const yj = ring[j * 2 + 1]
    if ((yi > y) !== (yj > y) && x < ((xj - xi) * (y - yi)) / (yj - yi) + xi) inside = !inside
  }
  return inside
}

/**
 * 點是不是在這個多邊形裡面。每穿進一個環就反轉一次：在外框裡算「內」，
 * 再落進洞裡就又算「外」。少了這一步，嘉義市的人會先被嘉義縣的外框收走。
 */
function insidePolygon(rings: number[][], x: number, y: number): boolean {
  let inside = false
  for (const ring of rings) {
    if (insideRing(ring, x, y)) inside = !inside
  }
  return inside
}

/** 點到這一圈最近的邊有幾公里。 */
function distanceToRingKm(ring: number[], x: number, y: number, scale: number): number {
  const kx = KM_PER_LON_DEGREE / scale
  const ky = KM_PER_LAT_DEGREE / scale
  let best = Infinity
  const count = ring.length / 2
  for (let i = 0, j = count - 1; i < count; j = i++) {
    const ax = (ring[j * 2] - x) * kx
    const ay = (ring[j * 2 + 1] - y) * ky
    const bx = (ring[i * 2] - x) * kx
    const by = (ring[i * 2 + 1] - y) * ky
    const ux = bx - ax
    const uy = by - ay
    const lengthSq = ux * ux + uy * uy
    // 把原點（要查的點）投影到線段 a→b 上，超出兩端就夾回端點
    const t = lengthSq === 0 ? 0 : Math.max(0, Math.min(1, -(ax * ux + ay * uy) / lengthSq))
    const distance = Math.hypot(ax + t * ux, ay + t * uy)
    if (distance < best) best = distance
  }
  return best
}

/**
 * 這個座標在哪個縣市。不在台灣（離任何縣市的界線都超過容許距離）回 null。
 *
 * 注意參數順序是「緯度、經度」，跟定位 API 給的 coords 一樣；
 * 界線資料裡存的則是「經度、緯度」。
 *
 * NaN 或無限大不必另外擋：它們跟任何數字比大小都是 false，
 * 所以既不會落在哪個環裡、也不會在容許距離內，自然走到最後回 null。
 */
export function findCounty(latitude: number, longitude: number, shapes: CountyShapes): string | null {
  const x = longitude * shapes.scale
  const y = latitude * shapes.scale

  for (const county of shapes.counties) {
    for (const rings of county.polygons) {
      if (insidePolygon(rings, x, y)) return county.name
    }
  }

  let nearest: string | null = null
  let nearestKm = OFFSHORE_TOLERANCE_KM
  for (const county of shapes.counties) {
    for (const rings of county.polygons) {
      // 只量外框：洞的邊同時是另一個縣市的外框，那個縣市自己會量到
      const km = distanceToRingKm(rings[0], x, y, shapes.scale)
      if (km <= nearestKm) {
        nearestKm = km
        nearest = county.name
      }
    }
  }
  return nearest
}
