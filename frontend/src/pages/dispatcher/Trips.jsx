import { useMemo, useState } from 'react'
import AppShell from '../../components/layout/AppShell'
import DataTable from '../../components/common/DataTable'
import StatusBadge from '../../components/common/StatusBadge'
import Button from '../../components/common/Button'
import FormField from '../../components/forms/FormField'
import Icon from '../../components/common/Icon'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import * as tripService from '../../services/tripService'
import { isRequired } from '../../utils/validation'
import { MOCK_TRIPS } from '../../utils/mockData'
import './Trips.css'

const TABS = ['All', 'Pending', 'Assigned', 'In Progress', 'Completed', 'Rejected']

// FR6 — trip request creation is wired to tripService.createTripRequest(),
// a placeholder until the Dispatch/Trip Service exists. Listing reads mock
// data (no GET /api/trips yet) but the table markup is API-ready.
export default function Trips() {
  const [tab, setTab] = useState('All')
  const [query, setQuery] = useState('')
  const [showForm, setShowForm] = useState(false)
  const [form, setForm] = useState({ origin: '', destination: '', notes: '' })
  const [fieldErrors, setFieldErrors] = useState({})
  const [formError, setFormError] = useState(null)
  const [success, setSuccess] = useState(false)
  const [submitting, setSubmitting] = useState(false)

  const rows = useMemo(() => {
    const byTab = tab === 'All' ? MOCK_TRIPS : MOCK_TRIPS.filter((t) => t.status === tab)
    const q = query.toLowerCase()
    if (!q) return byTab
    return byTab.filter((t) =>
      t.origin.toLowerCase().includes(q) ||
      t.destination.toLowerCase().includes(q) ||
      t.driver.toLowerCase().includes(q) ||
      t.vehicle.toLowerCase().includes(q) ||
      t.id.toLowerCase().includes(q)
    )
  }, [tab, query])

  function update(field) {
    return (e) => setForm((f) => ({ ...f, [field]: e.target.value }))
  }

  async function handleCreate(e) {
    e.preventDefault()
    setFormError(null)
    setSuccess(false)
    const errs = {}
    if (!isRequired(form.origin)) errs.origin = 'Origin is required.'
    if (!isRequired(form.destination)) errs.destination = 'Destination is required.'
    setFieldErrors(errs)
    if (Object.keys(errs).length > 0) return

    setSubmitting(true)
    try {
      await tripService.createTripRequest(form)
      setSuccess(true)
      setForm({ origin: '', destination: '', notes: '' })
    } catch (err) {
      setFormError(err.status === 404
        ? 'Trip creation isn\u2019t connected to the backend yet. This form is ready for when the Trip Service is live.'
        : (err.message || 'Something went wrong. Please try again.'))
    } finally {
      setSubmitting(false)
    }
  }

  const columns = [
    { key: 'id', header: 'Trip ID', render: (r) => <span className="cell-id">{r.id}</span> },
    { key: 'origin', header: 'Origin' },
    { key: 'destination', header: 'Destination' },
    { key: 'vehicle', header: 'Vehicle' },
    { key: 'driver', header: 'Driver' },
    { key: 'scheduledTime', header: 'Scheduled Time' },
    { key: 'status', header: 'Status', render: (r) => <StatusBadge status={r.status} /> },
    { key: 'createdDate', header: 'Created Date' },
  ]

  return (
    <AppShell title="Trips">
      <div className="page-header">
        <div>
          <div className="page-header-title">Trips</div>
          <p className="page-header-desc">Create trip requests and monitor them through their lifecycle.</p>
        </div>
        <div className="page-header-actions">
          <Button variant="accent" onClick={() => setShowForm((v) => !v)}>
            {showForm ? 'Close' : '+ Create Trip Request'}
          </Button>
        </div>
      </div>

      {showForm && (
        <div className="new-trip-panel">
          <h3 className="section-title">New trip request</h3>
          <form onSubmit={handleCreate} noValidate>
            <ErrorMessage>{formError}</ErrorMessage>
            <SuccessMessage>{success ? 'Trip request submitted.' : null}</SuccessMessage>
            <div className="form-grid-2" style={{ marginTop: 'var(--space-3)' }}>
              <FormField label="Origin" name="origin" value={form.origin} onChange={update('origin')} error={fieldErrors.origin} required />
              <FormField label="Destination" name="destination" value={form.destination} onChange={update('destination')} error={fieldErrors.destination} required />
            </div>
            <div style={{ marginTop: 'var(--space-4)' }}>
              <FormField label="Notes" name="notes" value={form.notes} onChange={update('notes')} />
            </div>
            <div style={{ marginTop: 'var(--space-4)' }}>
              <Button type="submit" loading={submitting}>{submitting ? 'Submitting…' : 'Submit request'}</Button>
            </div>
          </form>
        </div>
      )}

      <div className="toolbar">
        <div className="search-input-wrap">
          <Icon name="search" size={15} className="icon" />
          <input className="search-input" placeholder="Search by route, driver or vehicle" value={query} onChange={(e) => setQuery(e.target.value)} />
        </div>
      </div>

      <div className="trip-tabs">
        {TABS.map((t) => (
          <button key={t} className={`trip-tab${tab === t ? ' active' : ''}`} onClick={() => setTab(t)}>{t}</button>
        ))}
      </div>

      <DataTable columns={columns} rows={rows} emptyTitle="No trips match your search" />
    </AppShell>
  )
}
