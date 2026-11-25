import { ArrowDownUp, ChevronDown } from 'lucide-react'
import ActionMenu from '../../shared/ui/ActionMenu'
import { REPLY_SORT_OPTIONS, sameSort, setReplySort, sortLabel, useReplySort } from './replySort'

/**
 * 回應列表上方那顆排序鈕。按鈕上直接寫著現在是怎麼排的，點開是六個選項。
 *
 * 六個選項都寫成一句完整的話（「愛心多的在前」），不做「同一項再點一次就反過來」
 * 那種手勢：那要先知道有這回事才用得到，而選單攤開六行並不佔地方。
 */
export default function ReplySortMenu() {
  const sort = useReplySort()

  return (
    <ActionMenu
      label="回應排序"
      className="reply-sort"
      trigger={(
        <>
          <ArrowDownUp size={13} strokeWidth={2.4} aria-hidden="true" />
          <span>{sortLabel(sort)}</span>
          <ChevronDown size={13} strokeWidth={2.6} aria-hidden="true" />
        </>
      )}
      items={REPLY_SORT_OPTIONS.map((option, index) => ({
        key: `${option.sort.key}-${option.sort.desc ? 'desc' : 'asc'}`,
        label: option.label,
        hint: option.hint,
        checked: sameSort(option.sort, sort),
        // 時間、愛心、熱門三組之間各畫一條線
        separated: index > 0 && REPLY_SORT_OPTIONS[index - 1].group !== option.group,
        onSelect: () => setReplySort(option.sort),
      }))}
    />
  )
}
