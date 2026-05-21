type Props = {
  nickname: string
  isLoading: boolean
  onNicknameChange: (value: string) => void
  onLogin: () => void
}

const previewTags = ['今天', '同頻', '慢慢來']

export default function LoginPage({ nickname, isLoading, onNicknameChange, onLogin }: Props) {
  return (
    <main className="app-shell auth-shell">
      <section className="login-hero" aria-labelledby="login-title">
        <div className="brand-mark">同</div>
        <p className="eyebrow">同頻 Today</p>
        <h1 id="login-title">今天發生了一件事，我想遇到一個剛好懂的人。</h1>
        <p className="hero-copy">不用急著配對，也不用先介紹得很完整。先從今天的感受開始，慢慢遇到同頻的人。</p>

        <div className="phone-preview" aria-label="今日共鳴預覽">
          <div className="preview-topbar">
            <span></span>
            <span></span>
          </div>
          <div className="preview-card primary">
            <small>今日事件</small>
            <p>今天有點累，但想被好好聽見。</p>
          </div>
          <div className="preview-card">
            <small>共鳴原因</small>
            <p>你們今天都有類似的委屈感，也都想慢慢聊。</p>
          </div>
          <div className="tag-row">
            {previewTags.map((tag) => (
              <span key={tag} className="tag">
                {tag}
              </span>
            ))}
          </div>
        </div>
      </section>

      <section className="login-panel">
        <div>
          <h2>先建立 Demo 暱稱</h2>
          <p>不需要真名，不顯示精準位置，也不會要求戀愛目的。</p>
        </div>
        <label>
          暱稱
          <input
            value={nickname}
            onChange={(event) => onNicknameChange(event.target.value)}
            placeholder="例如：Dario"
            autoComplete="nickname"
          />
        </label>
        <button className="login-button" onClick={onLogin} disabled={isLoading || nickname.trim().length === 0}>
          {isLoading ? '正在建立今天的入口...' : '開始今天'}
        </button>
        <p className="privacy-note">第一版只建立本機 Demo user，方便展示今日事件、共鳴配對、樹洞與任務流程。</p>
      </section>
    </main>
  )
}
