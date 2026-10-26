// 年齡區間的雙向滑桿。
//
// 三個地方在用：Onboarding 的意圖設定、配對頁的「調整偏好」、我的→年齡偏好。
// 原本只有 Onboarding 有，另外兩處是數字輸入框；抽成共用元件後三邊的
// 行為與夾限邏輯只剩一份，不會各自飄。
//
// 配色走 CSS 變數（--ar-*），各頁在自己的樣式檔覆寫即可，結構與互動
// 只維護這一份。預設值沿用 Onboarding 的暖橘，所以 Onboarding 不必覆寫。

export const AGE_FLOOR = 18
export const AGE_CEIL = 99

type Props = {
  min: number
  max: number
  onChange: (min: number, max: number) => void
  label?: string
  /** 外層再掛一個 class，給各頁覆寫 --ar-* 用 */
  className?: string
}

export default function AgeRangeSlider({ min, max, onChange, label = '年齡範圍', className = '' }: Props) {
  const span = AGE_CEIL - AGE_FLOOR
  const pct = (value: number) => ((value - AGE_FLOOR) / span) * 100

  return (
    <div className={`age-range-field${className ? ` ${className}` : ''}`}>
      <div className="age-range-head">
        <span className="age-range-label">{label}</span>
        <span className="age-range-value">{min} – {max} 歲</span>
      </div>
      <div className="age-range">
        <div className="age-range-rail">
          <div className="age-range-fill" style={{ left: `${pct(min)}%`, right: `${100 - pct(max)}%` }} />
        </div>
        {/* 兩個 range 疊在一起，夾限保證 min 一定小於 max，兩個圓點不會交叉 */}
        <input
          className="age-range-input"
          type="range"
          min={AGE_FLOOR}
          max={AGE_CEIL}
          value={min}
          aria-label="最小年齡"
          onChange={event => onChange(Math.min(Number(event.target.value), max - 1), max)}
        />
        <input
          className="age-range-input"
          type="range"
          min={AGE_FLOOR}
          max={AGE_CEIL}
          value={max}
          aria-label="最大年齡"
          onChange={event => onChange(min, Math.max(Number(event.target.value), min + 1))}
        />
      </div>
    </div>
  )
}
