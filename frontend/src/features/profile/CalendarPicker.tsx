import { useState, useEffect, useRef } from 'react'
import { MONTH_NAMES, DOW_LABELS, DEFAULT_BIRTHDAY_YEAR } from './profileUtils'

// 生日用日曆選擇器：月曆 + 年份快選，點外部關閉。
export function CalendarPicker({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const currentYear = new Date().getFullYear()
  const [open, setOpen] = useState(false)
  const [showYearGrid, setShowYearGrid] = useState(false)
  const [viewYear,  setViewYear]  = useState(() => value ? +value.split('-')[0] : DEFAULT_BIRTHDAY_YEAR)
  const [viewMonth, setViewMonth] = useState(() => value ? +value.split('-')[1] - 1 : 0)
  const containerRef = useRef<HTMLDivElement>(null)

  const selParts = value ? value.split('-') : null
  const selYear  = selParts ? +selParts[0] : null
  const selMonth = selParts ? +selParts[1] - 1 : null
  const selDay   = selParts ? +selParts[2] : null

  // 點外部關閉
  useEffect(() => {
    if (!open) return
    function onDown(e: MouseEvent) {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setOpen(false); setShowYearGrid(false)
      }
    }
    document.addEventListener('mousedown', onDown)
    return () => document.removeEventListener('mousedown', onDown)
  }, [open])

  // value 改變時同步 view
  useEffect(() => {
    if (value) {
      const [y, m] = value.split('-')
      setViewYear(+y); setViewMonth(+m - 1)
    }
  }, [value])

  function prevMonth() {
    if (viewMonth === 0) { setViewMonth(11); setViewYear(y => y - 1) }
    else setViewMonth(m => m - 1)
  }
  function nextMonth() {
    if (viewMonth === 11) { setViewMonth(0); setViewYear(y => y + 1) }
    else setViewMonth(m => m + 1)
  }

  // 建立日曆 grid
  const firstDow = new Date(viewYear, viewMonth, 1).getDay()
  const totalDays = new Date(viewYear, viewMonth + 1, 0).getDate()
  const cells: (number | null)[] = [
    ...Array(firstDow).fill(null),
    ...Array.from({ length: totalDays }, (_, i) => i + 1),
  ]
  while (cells.length % 7 !== 0) cells.push(null)

  const yearRange = Array.from({ length: currentYear - 1950 + 1 }, (_, i) => currentYear - i)
  const displayText = selYear && selMonth !== null && selDay
    ? `${selYear}年${MONTH_NAMES[selMonth]}${selDay}日`
    : ''

  return (
    <div className="cal-picker" ref={containerRef}>
      <div
        className="cal-input"
        onClick={() => {
          if (!value && !open) {
            setViewYear(DEFAULT_BIRTHDAY_YEAR)
            setViewMonth(0)
          }
          setOpen(o => !o)
          setShowYearGrid(false)
        }}
        role="button"
        tabIndex={0}
      >
        {displayText
          ? <span>{displayText}</span>
          : <span className="cal-placeholder">選擇生日</span>
        }
        <svg className="cal-chevron" width="14" height="14" viewBox="0 0 14 14" fill="none">
          <path d="M3 5L7 9L11 5" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round"/>
        </svg>
      </div>

      {open && (
        <div className="cal-popup">
          {showYearGrid ? (
            <>
              <div className="cal-header">
                <button className="cal-month-label" onClick={() => setShowYearGrid(false)}>
                  ← 選擇年份
                </button>
              </div>
              <div className="cal-year-grid">
                {yearRange.map(y => (
                  <button
                    key={y}
                    className={`cal-year-cell${y === viewYear ? ' selected' : ''}`}
                    onClick={() => { setViewYear(y); setShowYearGrid(false) }}
                  >
                    {y}
                  </button>
                ))}
              </div>
            </>
          ) : (
            <>
              <div className="cal-header">
                <button className="cal-nav" onClick={prevMonth}>‹</button>
                <button className="cal-month-label" onClick={() => setShowYearGrid(true)}>
                  {viewYear}年{MONTH_NAMES[viewMonth]}
                </button>
                <button className="cal-nav" onClick={nextMonth}>›</button>
              </div>
              <div className="cal-dow">
                {DOW_LABELS.map(d => <span key={d}>{d}</span>)}
              </div>
              <div className="cal-grid">
                {cells.map((day, i) => {
                  const isSelected = day !== null
                    && day === selDay && viewMonth === selMonth && viewYear === selYear
                  return (
                    <button
                      key={i}
                      className={`cal-day${isSelected ? ' selected' : ''}${day === null ? ' empty' : ''}`}
                      disabled={day === null}
                      onClick={() => {
                        if (!day) return
                        onChange(`${viewYear}-${String(viewMonth + 1).padStart(2,'0')}-${String(day).padStart(2,'0')}`)
                        setOpen(false)
                      }}
                    >
                      {day ?? ''}
                    </button>
                  )
                })}
              </div>
            </>
          )}
        </div>
      )}
    </div>
  )
}
