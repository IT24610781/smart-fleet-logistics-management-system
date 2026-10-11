import { useEffect, useId, useRef } from 'react'
import Button from './Button'

export default function AdminDetails({ title, fields, onClose }) {
  const ref = useRef(null)
  const titleId = useId()
  useEffect(() => {
    const previous = document.activeElement
    const dialog = ref.current
    dialog.showModal()
    return () => { dialog.close(); previous?.focus() }
  }, [])
  return <dialog ref={ref} className="admin-detail-dialog" aria-labelledby={titleId} onCancel={onClose}>
    <div className="admin-detail-head"><h2 id={titleId}>{title}</h2><Button variant="secondary" onClick={onClose}>Close</Button></div>
    <dl className="admin-detail-list">{fields.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value ?? 'Not assigned'}</dd></div>)}</dl>
  </dialog>
}
