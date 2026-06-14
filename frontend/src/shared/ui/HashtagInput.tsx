import { useRef, useState } from 'react'

const PRESET_TAGS = [
  '職場', '感情', '家人', '朋友', '金錢壓力',
  '身體健康', '自我懷疑', '日常瑣事', '學業', '社交焦慮',
  '睡眠', '飲食',
]
const MAX_TAGS = 5

type Props = {
  value: string[]
  onChange: (tags: string[]) => void
}

export default function HashtagInput({ value, onChange }: Props) {
  const [inputValue, setInputValue] = useState('')
  const [showDropdown, setShowDropdown] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)

  const filtered = PRESET_TAGS.filter(
    (tag) => !value.includes(tag) && (inputValue === '' || tag.includes(inputValue)),
  )
  const showCustomOption =
    inputValue.trim() !== '' &&
    !PRESET_TAGS.includes(inputValue.trim()) &&
    !value.includes(inputValue.trim())

  function addTag(raw: string) {
    const tag = raw.replace(/^#/, '').trim()
    if (!tag || value.includes(tag) || value.length >= MAX_TAGS) return
    onChange([...value, tag])
    setInputValue('')
    inputRef.current?.focus()
  }

  function removeTag(tag: string) {
    onChange(value.filter((t) => t !== tag))
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault()
      if (inputValue.trim()) addTag(inputValue)
    }
    if (e.key === 'Backspace' && !inputValue && value.length > 0) {
      removeTag(value[value.length - 1])
    }
  }

  return (
    <div className="hashtag-input-wrapper">
      <div className="hashtag-input-container">
        <div
          className="hashtag-input-field"
          onClick={() => {
            setShowDropdown(true)
            inputRef.current?.focus()
          }}
        >
          {value.map((tag) => (
            <span key={tag} className="hashtag-chip">
              #{tag}
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation()
                  removeTag(tag)
                }}
              >
                ×
              </button>
            </span>
          ))}
          {value.length < MAX_TAGS && (
            <input
              ref={inputRef}
              className="hashtag-text-input"
              value={inputValue}
              placeholder={value.length === 0 ? '搜尋或新增標籤...' : ''}
              onChange={(e) => {
                setInputValue(e.target.value.replace(/^#/, ''))
                setShowDropdown(true)
              }}
              onFocus={() => setShowDropdown(true)}
              onBlur={() => setTimeout(() => setShowDropdown(false), 150)}
              onKeyDown={handleKeyDown}
            />
          )}
        </div>

        {showDropdown && (filtered.length > 0 || showCustomOption) && (
          <div className="hashtag-dropdown">
            {showCustomOption && (
              <button
                type="button"
                className="hashtag-dropdown-item custom"
                onClick={() => addTag(inputValue)}
              >
                <span className="hashtag-add-icon">+</span>
                新增「#{inputValue}」
              </button>
            )}
            {filtered.map((tag) => (
              <button
                key={tag}
                type="button"
                className="hashtag-dropdown-item"
                onClick={() => addTag(tag)}
              >
                #{tag}
              </button>
            ))}
          </div>
        )}
      </div>

      <div className="hashtag-presets">
        {PRESET_TAGS.filter((t) => !value.includes(t)).map((tag) => (
          <button
            key={tag}
            type="button"
            className="hashtag-preset-chip"
            disabled={value.length >= MAX_TAGS}
            onClick={() => addTag(tag)}
          >
            #{tag}
          </button>
        ))}
      </div>

      {value.length >= MAX_TAGS && (
        <p className="hashtag-limit-hint">最多 {MAX_TAGS} 個標籤</p>
      )}
    </div>
  )
}
