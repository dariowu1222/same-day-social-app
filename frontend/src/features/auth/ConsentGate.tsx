import { useEffect, useState } from 'react'
import { consentSettledKey } from '../../shared/lib/storageKeys'
import { getConsentStatus, giveConsent } from './api'
import ConsentScreen from './ConsentScreen'

/**
 * 既有帳號的補問：登入後，如果這個帳號從來沒同意過條款、沒確認過年齡，
 * 就蓋上確認畫面，確認完才能繼續用。
 *
 * 會缺的是以前用 Google／Facebook 建的帳號——那條路當時按一下就建好了，什麼都沒問。
 * 新的社群註冊已經在建立帳號之前就問了，Email 註冊本來就有，所以大多數人
 * 這裡只會默默問一次後端、得到「不缺」，之後連問都不問。
 *
 * 為什麼擋在 App 裡而不是後端：擋在登入或每一支 API 上的話，還在用舊版 App 的人
 * 會被整個鎖在外面，而舊版沒有這個畫面可以讓他補。所以後端只回答「缺不缺」，
 * 要不要擋由有這個畫面的版本自己決定。
 *
 * 查不到答案（斷線、後端還沒更新到有這支端點）就不擋，下次開 App 再問。
 * 寧可晚一點補到，也不要因為一次連線失敗把人關在一個出不去的畫面。
 */

type Props = {
  userId: string
  /** 不同意時的出口。 */
  onLogout: () => void
}

function isSettled(userId: string): boolean {
  try {
    return localStorage.getItem(consentSettledKey(userId)) === '1'
  } catch {
    return false
  }
}

function markSettled(userId: string) {
  try {
    localStorage.setItem(consentSettledKey(userId), '1')
  } catch {
    // 私密視窗寫不進去就算了，代價只是下次開 App 再問一次後端
  }
}

export default function ConsentGate({ userId, onLogout }: Props) {
  const [required, setRequired] = useState(false)

  useEffect(() => {
    // 這台裝置上已經確認過這個帳號不缺，就不必每次啟動都問
    if (isSettled(userId)) return

    let cancelled = false
    getConsentStatus()
      .then((status) => {
        if (cancelled) return
        if (status.required) setRequired(true)
        else markSettled(userId)
      })
      .catch(() => {
        // 查不到就不擋，見檔頭的說明
      })
    return () => { cancelled = true }
  }, [userId])

  if (!required) return null

  return (
    <div className="consent-gate">
      <ConsentScreen
        heading="繼續之前，補確認兩件事"
        description="配對會用到你的出生年月日，也需要你同意服務條款。這兩項之前沒有問齊，確認一次就好"
        onSubmit={async (answer) => {
          await giveConsent(answer)
          markSettled(userId)
          setRequired(false)
        }}
        exitPrompt="不同意的話可以"
        exitLabel="先登出"
        onExit={onLogout}
      />
    </div>
  )
}
