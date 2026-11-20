import { useState } from 'react'

/**
 * 出生年月日：年、月、日三個下拉選單。
 *
 * 為什麼不用瀏覽器內建的日期選擇器：它一打開停在「今天」，要選到三十年前的某一天
 * 得一路往回翻，在手機的 WebView 裡尤其難用。生日是「已經知道答案」的輸入，
 * 三個選單各點一下最快。
 *
 * 年份從今年列到 1940，不刻意只列成年的年份——未滿 18 歲的人應該選得到自己真正的
 * 生日，然後被明講「未滿 18 歲不能使用」，而不是在選單裡找不到自己、只好隨便挑一年。
 */

/** 與後端 AgeRules.EarliestBirthYear 一致：更早的年份後端不收。 */
const EARLIEST_YEAR = 1940

type Parts = { year: string; month: string; day: string }

type Props = {
  /** yyyy-MM-dd；三格還沒選齊時是空字串。 */
  value: string
  onChange: (birthday: string) => void
}

function partsOf(value: string): Parts {
  const [year = '', month = '', day = ''] = value ? value.split('-') : []
  // 選單的值不補零（'3' 不是 '03'），組回字串時才補
  return { year, month: month ? String(Number(month)) : '', day: day ? String(Number(day)) : '' }
}

/** 這個月有幾天。年還沒選時當作閏年，免得 2 月 29 日出生的人在選年之前選不到自己的日子。 */
function daysIn(year: string, month: string): number {
  if (!month) return 31
  return new Date(year ? Number(year) : 2000, Number(month), 0).getDate()
}

export default function BirthdayField({ value, onChange }: Props) {
  // 三格都選了才是一個生日。選到一半時交給外面的 value 是空字串，
  // 所以「選了年、還沒選月」這種半成品要自己記著。
  const [parts, setParts] = useState<Parts>(() => partsOf(value))

  function update(next: Parts) {
    // 換了年或月，原本選的日可能不存在了（31 日 → 2 月、閏年 → 平年）。清掉重選，不要默默改成別的日子。
    const day = next.day && Number(next.day) > daysIn(next.year, next.month) ? '' : next.day
    const settled = { ...next, day }
    setParts(settled)
    onChange(
      settled.year && settled.month && settled.day
        ? `${settled.year}-${settled.month.padStart(2, '0')}-${settled.day.padStart(2, '0')}`
        : '',
    )
  }

  const thisYear = new Date().getFullYear()
  const years = Array.from({ length: thisYear - EARLIEST_YEAR + 1 }, (_, index) => thisYear - index)
  const months = Array.from({ length: 12 }, (_, index) => index + 1)
  const days = Array.from({ length: daysIn(parts.year, parts.month) }, (_, index) => index + 1)

  return (
    <div className="birthday-field">
      <select
        aria-label="出生年"
        className={parts.year ? '' : 'unset'}
        value={parts.year}
        onChange={(event) => update({ ...parts, year: event.target.value })}
      >
        <option value="">年</option>
        {years.map((year) => <option key={year} value={year}>{year}</option>)}
      </select>
      <select
        aria-label="出生月"
        className={parts.month ? '' : 'unset'}
        value={parts.month}
        onChange={(event) => update({ ...parts, month: event.target.value })}
      >
        <option value="">月</option>
        {months.map((month) => <option key={month} value={month}>{month} 月</option>)}
      </select>
      <select
        aria-label="出生日"
        className={parts.day ? '' : 'unset'}
        value={parts.day}
        onChange={(event) => update({ ...parts, day: event.target.value })}
      >
        <option value="">日</option>
        {days.map((day) => <option key={day} value={day}>{day} 日</option>)}
      </select>
    </div>
  )
}
