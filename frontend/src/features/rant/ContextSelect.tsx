// 情境自訂下拉選單（說說看發文頁）：headless Radix Select 自刻外觀。
// 行為 / 鍵盤 / 無障礙 / 觸控由 Radix 處理，樣式見 styles/14-rant-context-select.css。
import * as Select from '@radix-ui/react-select'
import { ChevronDown, Check } from 'lucide-react'
import { RANT_MODES, MODE_ICONS } from './rantModes'

type Props = {
  value: string
  onChange: (value: string) => void
  label?: string
}

export default function ContextSelect({ value, onChange, label = '想被怎麼回應？' }: Props) {
  const SelectedIcon = MODE_ICONS[value]
  const selected = RANT_MODES.find((m) => m.key === value)

  return (
    <div className="context-select">
      <span className="context-select-label">{label}</span>
      <Select.Root value={value} onValueChange={onChange}>
        <Select.Trigger className="context-select-trigger" aria-label={label}>
          <span className="context-select-current">
            {SelectedIcon && <SelectedIcon size={16} className="context-select-current-icon" />}
            <Select.Value>{selected?.label}</Select.Value>
          </span>
          <Select.Icon className="context-select-chevron">
            <ChevronDown size={16} />
          </Select.Icon>
        </Select.Trigger>

        <Select.Portal>
          <Select.Content
            className="context-select-panel"
            position="popper"
            sideOffset={6}
          >
            <Select.Viewport>
              {RANT_MODES.map(({ key, label: optionLabel, Icon }) => (
                <Select.Item key={key} value={key} className="context-select-option">
                  <span className="context-select-option-icon">
                    <Icon size={16} />
                  </span>
                  <Select.ItemText>{optionLabel}</Select.ItemText>
                  <Select.ItemIndicator className="context-select-check">
                    <Check size={16} />
                  </Select.ItemIndicator>
                </Select.Item>
              ))}
            </Select.Viewport>
          </Select.Content>
        </Select.Portal>
      </Select.Root>
    </div>
  )
}
