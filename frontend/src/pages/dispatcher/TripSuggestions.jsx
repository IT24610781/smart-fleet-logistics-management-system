import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import AppShell from '../../components/layout/AppShell'
import Button from '../../components/common/Button'
import Icon from '../../components/common/Icon'
import EmptyState from '../../components/common/EmptyState'
import LoadingSpinner from '../../components/common/LoadingSpinner'
import { ErrorMessage } from '../../components/common/ErrorMessage'
import * as tripService from '../../services/tripService'

// US-10: ranked vehicle suggestions for one trip. Read-only for now -
// actually assigning a vehicle is US-11.
export default function TripSuggestions() {
  const { id } = useParams()
  const [suggestions, setSuggestions] = useState(null)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

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
                <div style={{ textAlign: 'right' }}>
                  <div className="page-header-desc">Spare capacity</div>
                  <div style={{ fontWeight: 600 }}>{s.excessCapacity} {s.unit}</div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </AppShell>
  )
}