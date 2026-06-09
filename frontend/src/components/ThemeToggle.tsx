import { Moon, Sun } from 'lucide-react'
import { useTheme } from '../context/ThemeContext'

export default function ThemeToggle() {
  const { mode, isAnimating, startToggle } = useTheme()
  return (
    <button
      className={`theme-toggle ${mode}`}
      onClick={startToggle}
      disabled={isAnimating}
      aria-label={mode === 'day' ? '切換夜間模式' : '切換日間模式'}
    >
      {mode === 'day'
        ? <Moon size={17} strokeWidth={1.8} />
        : <Sun size={17} strokeWidth={1.8} />}
    </button>
  )
}
