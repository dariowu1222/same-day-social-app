type Props = {
  content: string
  responseMode: string
  visibility: string
  isSubmitting: boolean
  onContentChange: (value: string) => void
  onResponseModeChange: (value: string) => void
  onVisibilityChange: (value: string) => void
  onSubmit: () => void
}

export default function TodayEntryForm({
  content,
  responseMode,
  visibility,
  isSubmitting,
  onContentChange,
  onResponseModeChange,
  onVisibilityChange,
  onSubmit,
}: Props) {
  return (
    <section className="panel">
      <label>
        今天發生了什麼？
        <textarea
          rows={6}
          value={content}
          onChange={(event) => onContentChange(event.target.value)}
          placeholder="不用想得太正式，像跟朋友說話一樣就好。"
        />
      </label>
      <div className="field-grid">
        <label>
          期待回應
          <select value={responseMode} onChange={(event) => onResponseModeChange(event.target.value)}>
            <option value="JUST_LISTEN">只是想被聽見</option>
            <option value="COMFORT_ME">想被安慰</option>
            <option value="GIVE_ADVICE">想聽建議</option>
            <option value="RANT_TOGETHER">想一起抱怨</option>
            <option value="DISTRACT_ME">想轉移注意力</option>
          </select>
        </label>
        <label>
          可見範圍
          <select value={visibility} onChange={(event) => onVisibilityChange(event.target.value)}>
            <option value="MATCH_ONLY">只用於配對</option>
            <option value="PUBLIC">公開</option>
            <option value="PRIVATE">私密</option>
          </select>
        </label>
      </div>
      <button onClick={onSubmit} disabled={isSubmitting || content.trim().length === 0}>
        {isSubmitting ? '分析中...' : '找到今天同頻的人'}
      </button>
    </section>
  )
}
