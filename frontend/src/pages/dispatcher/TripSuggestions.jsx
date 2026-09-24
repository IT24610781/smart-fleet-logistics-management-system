import { useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import EmptyState from '../../components/common/EmptyState'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import ConfirmDialog from '../../components/common/ConfirmDialog'
import { ErrorMessage, SuccessMessage } from '../../components/common/ErrorMessage'
import { useToast } from '../../context/ToastContext'
import * as tripService from '../../services/tripService'

// US-10/US-11: ranked vehicle suggestions for one trip, with the ability to
// assign one of them.
export default function TripSuggestions() {
  const { id } = useParams()
  const navigate = useNavigate()
  const { showToast } = useToast()

  const [suggestions, setSuggestions] = useState(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  const [confirmTarget, setConfirmTarget] = useState(null)
  const [assigning, setAssigning] = useState(false)
  const [assignError, setAssignError] = useState(null)
  const [assigned, setAssigned] = useState(false)

  useEffect(() => {
    let cancelled = false

    async function load() {
      setLoading(true)
      setLoadError(null)
      try {
        const data = await tripService.getSuggestions(id)
        if (cancelled) return
        setSuggestions(data)
      } catch (err) {
        if (!cancelled) {
          setLoadError(
            err.status === 404
              ? 'This trip could not be found. It may have been removed.'
              : err.message || 'Unable to load vehicle suggestions.'
          )
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    load()
    return () => {
      cancelled = true
    }
  }, [id, reloadKey])

  async function handleConfirmAssign() {
    if (!confirmTarget) return
    setAssigning(true)
    setAssignError(null)
    try {
      await tripService.assignVehicle(id, confirmTarget.vehicleId)
      setAssigned(true)
      setConfirmTarget(null)
      showToast(`${confirmTarget.registrationNo} assigned to this trip.`, 'success')
      window.setTimeout(() => navigate('/dispatcher/trips'), 1500)
    } catch (err) {
      setConfirmTarget(null)
      setAssignError(
        err.status === 409
          ? (err.message || 'This vehicle is no longer available. It may have just been assigned to another trip.')
          : err.status === 404
            ? 'This trip or vehicle could not be found. It may have changed.'
            : err.message || 'Unable to assign this vehicle. Please try again.'
      )
    } finally {
      setAssigning(false)
    }
  }

  return (
    <AppShell title="Vehicle Suggestions">
      <div className="page-header">
        <div>
          <div className="page-header-title">Suggested Vehicles</div>
          <p className="page-header-desc">
            Available vehicles that fit this trip's required capacity, ranked by the closest fit.
          </p>
        </div>
        <div className="page-header-actions">
          <Link to="/dispatcher/trips">
            <Button variant="secondary">Back to Trips</Button>
          </Link>
        </div>
      </div>

      <div className="card card-pad">
        <SuccessMessage>{assigned ? 'Vehicle assigned. Redirecting to Trips…' : null}</SuccessMessage>
        <ErrorMessage>{assignError}</ErrorMessage>

        {loading && <LoadingSpinner center label="Finding suggestions…" />}

        {!loading && loadError && (
          <>
            <ErrorMessage>{loadError}</ErrorMessage>
            <div style={{ marginTop: 'var(--space-3)' }}>
              <Button variant="secondary" onClick={() => setReloadKey((k) => k + 1)}>Try again</Button>
            </div>
          </>
        )}

        {!loading && !loadError && suggestions && suggestions.length === 0 && (
          <EmptyState
            icon="truck"
            title="No matching vehicles right now"
            description="No Available vehicle currently has enough capacity, in the right unit, for this trip."
          />
        )}

        {!loading && !loadError && suggestions && suggestions.length > 0 && (
          <div className="stack" style={{ gap: 'var(--space-3)' }}>
            {suggestions.map((s, index) => (
              <div
                key={s.vehicleId}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  gap: 'var(--space-4)',
                  padding: 'var(--space-4)',
                  borderRadius: 'var(--radius-lg)',
                  border: index === 0 ? '2px solid var(--color-accent)' : '1px solid var(--color-border)',
                }}
              >
                <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
                  <Icon name="truck" size={20} />
                  <div>
                    <div style={{ fontWeight: 600 }}>
                      {s.registrationNo} {index === 0 && <span style={{ color: 'var(--color-accent)' }}>· Best fit</span>}
                    </div>
                    <div className="page-header-desc">{s.type} · {s.capacity} capacity</div>
                  </div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-4)' }}>
                  <div style={{ textAlign: 'right' }}>
                    <div className="page-header-desc">Spare capacity</div>
                    <div style={{ fontWeight: 600 }}>{s.excessCapacity} {s.unit}</div>
                  </div>
                  <Button
                    variant={index === 0 ? 'accent' : 'secondary'}
                    disabled={assigned}
                    onClick={() => setConfirmTarget(s)}
                  >
                    Assign
                  </Button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <ConfirmDialog
        open={confirmTarget !== null}
        title="Assign this vehicle?"
        body={confirmTarget ? `Assign ${confirmTarget.registrationNo} (${confirmTarget.type}) to this trip? The vehicle will be marked On Trip.` : ''}
        confirmLabel="Assign vehicle"
        cancelLabel="Cancel"
        variant="accent"
        loading={assigning}
        onConfirm={handleConfirmAssign}
        onCancel={() => setConfirmTarget(null)}
      />
    </AppShell>
  )
}