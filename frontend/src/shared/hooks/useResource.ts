import { useCallback, useEffect, useState } from 'react'

type Loader<T> = () => Promise<{ data: T }>

type ResourceState<T> = {
  data: T | undefined
  loading: boolean
  error: string | undefined
  reload: () => Promise<void>
  setData: React.Dispatch<React.SetStateAction<T | undefined>>
}

/**
 * 抓取資料的共用樣板：載入中 / 錯誤 / 重新載入一次寫好，全站共用。
 * 取代每頁手寫的 useState + useEffect + loadX() + try/catch。
 *
 * @param loader 回傳 { data } 的非同步函式（通常是某個 feature api）
 * @param deps   依賴變動時自動重新載入（例如 userId 改變）
 */
export function useResource<T>(loader: Loader<T>, deps: unknown[] = []): ResourceState<T> {
  const [data, setData] = useState<T>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string>()

  const reload = useCallback(async () => {
    setLoading(true)
    setError(undefined)
    try {
      const response = await loader()
      setData(response.data)
    } catch (err) {
      setError(err instanceof Error ? err.message : '載入失敗，請稍後再試。')
    } finally {
      setLoading(false)
    }
    // loader 的重建時機由呼叫端傳入的 deps 決定（動態依賴是本 hook 的設計目的）
    // eslint-disable-next-line react-hooks/use-memo, react-hooks/exhaustive-deps
  }, deps)

  useEffect(() => {
    // 掛載即載入；reload 變動（deps 改變）時自動重新抓取。
    void reload()
  }, [reload])

  return { data, loading, error, reload, setData }
}
