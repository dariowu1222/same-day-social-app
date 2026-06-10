import { useEffect, useMemo, useRef, useState } from 'react'
import { useTheme } from '../context/ThemeContext'

const NIGHT_CIRCLE_SIZE = 10

function expansionScale() {
  return (Math.hypot(window.innerWidth, window.innerHeight) * 2.2) / NIGHT_CIRCLE_SIZE
}

/* ── Night transition (day → night) ── */
function NightTransition({ onDone, onFinish }: { onDone: () => void; onFinish: () => void }) {
  const doneRef = useRef(onDone)
  const finishRef = useRef(onFinish)
  doneRef.current = onDone
  finishRef.current = onFinish

  const scale = useMemo(expansionScale, [])

  useEffect(() => {
    const t = setTimeout(() => {
      doneRef.current()
      finishRef.current()
    }, 1650)
    return () => clearTimeout(t)
  }, [])

  return (
    <div className="theme-overlay" aria-hidden="true">
      <div
        className="night-circle"
        style={{ '--expand-scale': scale } as React.CSSProperties}
      />
    </div>
  )
}

/* ── Dawn transition (night → day) ── */
const DAWN_COLORS = ['#ffe8a0', '#ffd46e', '#ffbf50', '#ffecd0', '#fff5e0']

type Particle = {
  id: number; left: string; top: string; color: string; size: number
  glow: string; dx: string; dy: string; dur: string
}

function DawnTransition({ onCommit, onFinish }: { onCommit: () => void; onFinish: () => void }) {
  const commitRef = useRef(onCommit)
  const finishRef = useRef(onFinish)
  commitRef.current = onCommit
  finishRef.current = onFinish

  const [showParticles, setShowParticles] = useState(false)
  const lightScale = useMemo(
    () => (Math.hypot(window.innerWidth, window.innerHeight) * 2.2) / 40, []
  )
  const particles = useMemo<Particle[]>(() =>
    Array.from({ length: 22 }, (_, i) => {
      const size = 2 + Math.random() * 4
      const color = DAWN_COLORS[Math.floor(Math.random() * DAWN_COLORS.length)]
      return {
        id: i,
        left: `${20 + Math.random() * 60}%`,
        top: `${60 + Math.random() * 35}%`,
        color, size,
        glow: `0 0 ${(size * 3).toFixed(0)}px ${(size * 1.5).toFixed(0)}px ${color}99`,
        dx: `${((Math.random() - 0.5) * 60).toFixed(0)}px`,
        dy: `-${(80 + Math.random() * 160).toFixed(0)}px`,
        dur: `${(1.2 + Math.random() * 1).toFixed(2)}s`,
      }
    }), []
  )

  useEffect(() => {
    const t1 = setTimeout(() => setShowParticles(true), 180)
    const t2 = setTimeout(() => commitRef.current(), 900)
    const t3 = setTimeout(() => finishRef.current(), 1750)
    return () => { clearTimeout(t1); clearTimeout(t2); clearTimeout(t3) }
  }, [])

  return (
    <div className="theme-overlay dawn-overlay" aria-hidden="true">
      <div className="dawn-light" style={{ '--light-scale': lightScale } as React.CSSProperties} />
      {showParticles && particles.map(p => (
        <div key={p.id} className="dawn-particle" style={{
          left: p.left, top: p.top, width: p.size, height: p.size,
          background: p.color, boxShadow: p.glow,
          '--dx': p.dx, '--dy': p.dy, animationDuration: p.dur,
        } as React.CSSProperties} />
      ))}
    </div>
  )
}

/* ── Entry point ── */
export default function ThemeTransitionOverlay() {
  const { pendingMode, commitMode, finishAnimation } = useTheme()
  if (!pendingMode) return null
  return pendingMode === 'night'
    ? <NightTransition onDone={commitMode} onFinish={finishAnimation} />
    : <DawnTransition onCommit={commitMode} onFinish={finishAnimation} />
}

/* ── 全域螢火蟲（夜間模式）── */
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
        size, color,
        glow: `0 0 ${(size * 3).toFixed(0)}px ${(size * 1.5).toFixed(0)}px ${color}99`,
        fx: `${((Math.random() - 0.5) * 80).toFixed(0)}px`,
        fy: `${((Math.random() - 0.5) * 60).toFixed(0)}px`,
        dur: `${(6 + Math.random() * 8).toFixed(1)}s`,
        delay: `-${(Math.random() * 14).toFixed(1)}s`,
      }
    }), []
  )

  return (
    <div className="global-night-effects" aria-hidden="true">
      {fireflies.map(f => (
        <div key={f.id} className="global-firefly" style={{
          top: f.top, left: f.left, width: f.size, height: f.size,
          background: f.color, boxShadow: f.glow,
          '--fx': f.fx, '--fy': f.fy, '--fd': f.dur, '--delay': f.delay,
        } as React.CSSProperties} />
      ))}
    </div>
  )
}
