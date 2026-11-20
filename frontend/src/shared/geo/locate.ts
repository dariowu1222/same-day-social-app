import { Geolocation } from '@capacitor/geolocation'
import { findCounty, type CountyShapes } from './countyLookup'

/**
 * 用定位找出使用者所在的縣市。
 *
 * 座標只在這個檔案裡活著：拿到之後立刻換算成縣市名稱，回傳的只有那個名稱。
 * 不存、不寫 log、不送任何請求。畫面上對使用者說的是「座標不會離開手機」，
 * 這個檔案就是那句話的全部實作——要改它之前，先確認那句話還成不成立
 * （不成立的話，隱私權政策與 Play 的資料安全表單也要一起改）。
 *
 * 權限只要「大概位置」：Android 的 manifest 只宣告 ACCESS_COARSE_LOCATION，
 * 這裡也固定 enableHighAccuracy: false。判斷縣市用不到公尺級的精度，
 * 而系統的權限視窗會因此簡單得多（不會出現「精確位置」的選項）。
 *
 * 系統權限視窗由外掛在 getCurrentPosition 裡自己跳，不要另外先呼叫
 * requestPermissions——Android 連續拒絕兩次之後就永遠不再跳視窗，
 * 所以這支只能在使用者自己按下按鈕時呼叫，不能在啟動時自動跑。
 */

export type LocateResult =
  /** 定到了，而且在 22 個縣市裡 */
  | { status: 'ok'; area: string }
  /** 定到了，但人不在台灣 */
  | { status: 'outside' }
  /** 使用者沒給權限。blocked = 系統不會再跳視窗，要去系統設定裡開 */
  | { status: 'denied'; blocked: boolean }
  /** 手機的定位服務整個關著 */
  | { status: 'off' }
  /** 逾時、訊號不好、裝置不支援、位置太不準 */
  | { status: 'unavailable' }

/**
 * 誤差半徑超過這個數字就不拿來判斷縣市。
 *
 * 桌機瀏覽器沒有 Wi-Fi 定位時會用 IP 猜，誤差動輒幾十公里；台北市東西向也才
 * 十幾公里寬。這種時候猜出來的縣市跟擲銅板差不多，而且會直接出現在選單上——
 * 與其帶入一個看起來很篤定的錯答案，不如請使用者自己選。
 */
const MAX_ACCURACY_METERS = 20_000

const POSITION_OPTIONS = {
  enableHighAccuracy: false,
  timeout: 12_000,
  // 十分鐘內定過就直接用，不必為了判斷縣市再開一次定位
  maximumAge: 10 * 60 * 1000,
} as const

let shapesPromise: Promise<CountyShapes> | null = null

/** 縣市界線約 76 KB，只有真的按下定位的人才需要，所以不放進主程式包。 */
function loadShapes(): Promise<CountyShapes> {
  shapesPromise ??= import('./taiwanCounties.json').then((module) => module.default as CountyShapes)
  // 載入失敗（網頁版斷線）不要把失敗的 promise 留著，下次按才有機會重來
  shapesPromise.catch(() => { shapesPromise = null })
  return shapesPromise
}

function failureOf(error: unknown): 'denied' | 'off' | 'unavailable' {
  const code = (error as { code?: unknown } | null)?.code

  // 瀏覽器的 GeolocationPositionError：code 是數字，1 = 使用者拒絕
  if (code === 1) return 'denied'

  // 原生外掛：code 是 'OS-PLUG-GLOC-0003' 這種字串（對照表在外掛的 README）
  if (typeof code === 'string') {
    if (code.endsWith('-0003')) return 'denied'
    // 0007 定位服務沒開、0009 請他打開但他按了取消、0017 定位與網路都關著
    if (code.endsWith('-0007') || code.endsWith('-0009') || code.endsWith('-0017')) return 'off'
  }

  return 'unavailable'
}

/**
 * 被拒絕之後，系統還會不會再跳視窗。
 * 不會的話，再按一次只會立刻失敗，畫面要改成教使用者去系統設定開。
 */
async function isBlocked(): Promise<boolean> {
  try {
    const status = await Geolocation.checkPermissions()
    return status.coarseLocation === 'denied'
  } catch {
    // 查不到狀態（舊瀏覽器沒有 Permissions API、定位服務關著）就當作還能再問
    return false
  }
}

export async function locateMyArea(): Promise<LocateResult> {
  let latitude: number
  let longitude: number

  try {
    const position = await Geolocation.getCurrentPosition(POSITION_OPTIONS)
    if (position.coords.accuracy > MAX_ACCURACY_METERS) return { status: 'unavailable' }
    latitude = position.coords.latitude
    longitude = position.coords.longitude
  } catch (error) {
    const failure = failureOf(error)
    if (failure === 'denied') return { status: 'denied', blocked: await isBlocked() }
    return { status: failure }
  }

  let shapes: CountyShapes
  try {
    shapes = await loadShapes()
  } catch {
    return { status: 'unavailable' }
  }

  const area = findCounty(latitude, longitude, shapes)
  return area ? { status: 'ok', area } : { status: 'outside' }
}
