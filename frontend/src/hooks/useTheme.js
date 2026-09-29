import { useEffect, useState } from 'react'
export function useTheme() {
  const [theme, setThemeState] = useState(() => document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light')
  useEffect(() => {
    const sync = () => setThemeState(document.documentElement.dataset.theme === 'dark' ? 'dark' : 'light')
    const storage = event => {
      if (event.key !== 'smartfleet_theme') return
      document.documentElement.dataset.theme = event.newValue === 'dark' ? 'dark' : 'light'
      sync()
    }
    window.addEventListener('smartfleet:theme-changed', sync)
    window.addEventListener('storage', storage)
    return () => { window.removeEventListener('smartfleet:theme-changed', sync); window.removeEventListener('storage', storage) }
  }, [])
  const setTheme = next => {
    if (!['light', 'dark'].includes(next)) return
    document.documentElement.dataset.theme = next
    try { localStorage.setItem('smartfleet_theme', next) } catch { /* Theme still works without storage. */ }
    setThemeState(next)
    window.dispatchEvent(new window.Event('smartfleet:theme-changed'))
  }
  return { theme, setTheme }
}
