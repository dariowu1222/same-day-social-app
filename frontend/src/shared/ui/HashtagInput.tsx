import { useState } from 'react'

const PRESET_TAGS = [
  '職場', '感情', '家人', '朋友', '金錢壓力',
  '身體健康', '自我懷疑', '日常瑣事', '學業', '社交焦慮',
  '睡眠', '飲食',
]
const MAX_TAGS = 5

type Props = {
  value: string[]
  onChange: (tags: string[]) => void
  /**
   * 收起來（預設）只露一列標籤，點了就加上；展開才看得到全部，以及搜尋／自訂的輸入框。
   * 標籤是選填的，以前十二個全攤開，把下面的東西整個擠出畫面。
   */
  expanded?: boolean
}

/**
 * 標籤：選到的排最前面、再點一次就拿掉；沒選的接在後面。
 *
 * 以前選到的標籤是放進輸入框裡的深色小膠囊，另外還有一個下拉選單——
 * 同一批標籤在畫面上出現三次（輸入框裡、下拉裡、下面一整排）。現在只有一排。
 */
export default function HashtagInput({ value, onChange, expanded = false }: Props) {
  const [inputValue, setInputValue] = useState('')

  const query = expanded ? inputValue.trim() : ''
  const full = value.length >= MAX_TAGS
  const rest = PRESET_TAGS.filter((tag) => !value.includes(tag) && (query === '' || tag.includes(query)))
  const showCustomOption = query !== '' && !PRESET_TAGS.includes(query) && !value.includes(query) && !full

  function addTag(raw: string) {
    const tag = raw.replace(/^#/, '').trim()
    if (!tag || value.includes(tag) || full) return
    onChange([...value, tag])
    setInputValue('')
  }

  function removeTag(tag: string) {
    onChange(value.filter((t) => t !== tag))
  }

  return (
    <div className={`hashtag-input-wrapper${expanded ? ' is-expanded' : ''}`}>
      {expanded && (
        <div className="hashtag-input-field">
          <input
            className="hashtag-text-input"
            value={inputValue}
            maxLength={12}
            placeholder="搜尋或新增標籤"
            onChange={(e) => setInputValue(e.target.value.replace(/^#/, ''))}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                if (query) addTag(query)
              }
            }}
          />
        </div>
      )}

      <div className="hashtag-presets">
        {value.map((tag) => (
          <button
            key={tag}
            type="button"
            className="hashtag-preset-chip is-on"
            aria-pressed="true"
            onClick={() => removeTag(tag)}
          >
            #{tag}
          </button>
        ))}
        {showCustomOption && (
          <button type="button" className="hashtag-preset-chip is-new" onClick={() => addTag(query)}>
            ＋ 新增 #{query}
          </button>
        )}
        {rest.map((tag) => (
          <button
            key={tag}
            type="button"
            className="hashtag-preset-chip"
            aria-pressed="false"
            disabled={full}
            onClick={() => addTag(tag)}
          >
            #{tag}
          </button>
        ))}
      </div>

      {full && <p className="hashtag-limit-hint">最多 {MAX_TAGS} 個標籤</p>}
    </div>
  )
}
