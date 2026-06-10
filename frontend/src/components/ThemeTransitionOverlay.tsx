import { useMemo } from 'react'

// 主題過渡動畫已簡化為 CSS 0.3s ease，此 overlay 不再使用
export default function ThemeTransitionOverlay() { return null }

/* ── 全域螢火蟲（夜間模式 / 樹洞頁永遠顯示）── */
export function GlobalNightEffects() {
  const fireflies = useMemo(() =>
    Array.from({ length: 45 }, (_, i) => {
      const size = 2 + Math.random() * 3
      const hue = 90 + Math.random() * 50
      const color = `hsl(${hue.toFixed(0)}, 85%, 65%)`
      return {
        id: i,
        top: `${Math.random() * 95}%`,
        left: `${Math.random() * 95}%`,
        size,
        color,
        glow: `0 0 ${(size * 3).toFixed(0)}px ${(size * 1.5).toFixed(0)}px ${color}99`,
        fx: `${((Math.random() - 0.5) * 80).toFixed(0)}px`,
        fy: `${((Math.random() - 0.5) * 60).toFixed(0)}px`,
        dur: `${(6 + Math.random() * 8).toFixed(1)}s`,
        delay: `-${(Math.random() * 14).toFixed(1)}s`,
      }
    }),
    []
  )

  return (
    <div className="global-night-effects" aria-hidden="true">
      {fireflies.map(f => (
        <div
          key={f.id}
          className="global-firefly"
          style={{
            top: f.top,
            left: f.left,
            width: f.size,
            height: f.size,
            background: f.color,
            boxShadow: f.glow,
            '--fx': f.fx,
            '--fy': f.fy,
            '--fd': f.dur,
            '--delay': f.delay,
          } as React.CSSProperties}
        />
      ))}
    </div>
  )
}
