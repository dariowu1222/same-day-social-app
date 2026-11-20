import { Capacitor } from '@capacitor/core'
import type { LocateResult } from '../../shared/geo/locate'

// 定位沒成功時要對使用者說的話。onboarding 的定位說明頁與「我的」頁共用，
// 兩邊講法不一樣的話，同一個狀況會被描述成兩件事。

type Failure = Exclude<LocateResult, { status: 'ok' }>

/** 權限被系統鎖住時，要去哪裡把它打開。每個平台的路徑不一樣。 */
function whereToEnable(): string {
  switch (Capacitor.getPlatform()) {
    case 'android':
      return '到手機的「設定 → 應用程式 → 同頻 Today → 權限 → 位置」開啟'
    case 'ios':
      return '到「設定 → 同頻 Today → 位置」開啟'
    default:
      return '在瀏覽器網址列的鎖頭圖示裡，把「位置」改成允許'
  }
}

/**
 * @param where 這句話出現在哪一頁。只差在「之後還能在哪裡開」那半句——
 *   人已經在「我的」頁了，就不必再叫他去「我的」。
 */
export function locateFailureCopy(result: Failure, where: 'onboarding' | 'profile'): { title: string; detail: string } {
  switch (result.status) {
    case 'denied':
      if (result.blocked) {
        return { title: '定位權限是關閉的', detail: `想用的話，${whereToEnable()}。不開也沒關係，可以自己選縣市` }
      }
      return {
        title: '沒有開啟定位',
        detail: where === 'onboarding'
          ? '沒關係，可以自己選縣市。之後想開，到「我的」就能用定位帶入'
          : '沒關係，可以自己選縣市',
      }
    case 'off':
      return { title: '手機的定位服務沒開', detail: '打開手機的定位後再試一次，或是自己選縣市' }
    case 'outside':
      return { title: '你現在好像不在台灣', detail: '目前只有台灣的 22 個縣市，可以自己選一個常待的地方' }
    case 'unavailable':
      return { title: '暫時抓不到位置', detail: '可能是訊號不好。可以再試一次，或是自己選縣市' }
  }
}
