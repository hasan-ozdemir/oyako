// Codex developer note: Explains the purpose and flow of webapp-oyako/src/main.tsx for maintainers.
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)

// Guards this branch so the UI handles the condition intentionally.
if ('serviceWorker' in navigator && import.meta.env.PROD) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // PWA kaydi kritik akisi etkilememeli.
    })
  })
}
