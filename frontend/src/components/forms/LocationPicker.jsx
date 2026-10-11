import { useCallback, useRef, useState } from 'react'
import { MapContainer, Marker, TileLayer, useMapEvents } from 'react-leaflet'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import Button from '../common/Button'
import Icon from '../common/Icon'
import { ErrorMessage } from '../common/ErrorMessage'

// Vite bundles the marker image differently to how Leaflet's CSS expects it
// by default, which otherwise shows a broken-image icon. This is the
// standard fix.
delete L.Icon.Default.prototype._getIconUrl
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon-2x.png',
  iconUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-icon.png',
  shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
})

const DEFAULT_CENTER = [7.8731, 80.7718] // Sri Lanka, a reasonable default view

// One reverse-geocode lookup per explicit click/GPS fix - never on typing or
// dragging. Nominatim's usage policy forbids live search-as-you-type; a
// single lookup per deliberate action stays well within fair use.
async function reverseGeocode(lat, lng) {
  const url = `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat=${lat}&lon=${lng}`
  const response = await fetch(url, { headers: { Accept: 'application/json' } })
  if (!response.ok) throw new Error('Lookup failed')
  const data = await response.json()
  return data.display_name || `${lat.toFixed(5)}, ${lng.toFixed(5)}`
}

function ClickToPlacePin({ onPick }) {
  useMapEvents({
    click(e) {
      onPick(e.latlng.lat, e.latlng.lng)
    },
  })
  return null
}

// A location field with three ways to fill it in:
//   1. Type an address directly (no coordinates - works exactly as before)
//   2. "Use my location" - one GPS fix, reverse-geocoded once
//   3. "Pick on map" - click a point, reverse-geocoded once
//
// value: { address, lat, lng }  (lat/lng are null for a typed-only address)
export default function LocationPicker({ label, value, onChange, required, error, placeholder, showCurrentLocation = true, disabled = false }) {
  const [mapOpen, setMapOpen] = useState(false)
  const [locating, setLocating] = useState(false)
  const [lookupError, setLookupError] = useState(null)
  const mapRef = useRef(null)

  // Derived straight from props on every render - no local "pin" state to
  // keep in sync, so there's nothing that can drift out of step with the
  // value the parent form actually holds.
  const pin = value.lat != null && value.lng != null ? [value.lat, value.lng] : null

  const applyPoint = useCallback(
    async (lat, lng) => {
      setLookupError(null)
      try {
        const address = await reverseGeocode(lat, lng)
        onChange({ address, lat, lng })
      } catch {
        // Reverse geocoding failed - still keep the precise point, just fall
        // back to raw coordinates as the display text so nothing is lost.
        setLookupError('Could not look up an address for this point, but the exact location was saved.')
        onChange({ address: `${lat.toFixed(5)}, ${lng.toFixed(5)}`, lat, lng })
      }
    },
    [onChange]
  )

  function handleUseCurrentLocation() {
    if (!window.navigator.geolocation) {
      setLookupError('Your browser does not support location services.')
      return
    }
    setLocating(true)
    setLookupError(null)
    window.navigator.geolocation.getCurrentPosition(
      async (position) => {
        await applyPoint(position.coords.latitude, position.coords.longitude)
        setMapOpen(true)
        setLocating(false)
      },
      () => {
        setLookupError('Could not get your location. Check your browser\u2019s location permission, or pick a point on the map instead.')
        setLocating(false)
      },
      { enableHighAccuracy: true, timeout: 10000 }
    )
  }

  function handleTypedChange(e) {
    // Typing manually clears any previously picked precise point (lat/lng
    // go back to null) - the two never disagree with each other.
    onChange({ address: e.target.value, lat: null, lng: null })
  }

  return (
    <div className="flex flex-col gap-1.5">
      <label className="font-title-md text-body-sm text-on-surface" htmlFor={`${label}-input`}>
        {label}{required && ' *'}
      </label>

      <input
        id={`${label}-input`}
        className={`w-full rounded-xl border px-3.5 py-2.5 font-body-md text-body-md text-on-surface placeholder:text-on-surface-variant/60 transition-colors focus:outline-none focus:ring-2 focus:ring-primary/50 ${
          error ? 'border-error' : 'border-outline-variant'
        } ${disabled ? 'bg-surface-container text-on-surface-variant cursor-not-allowed' : 'bg-surface-container-lowest'}`}
        value={value.address}
        onChange={handleTypedChange}
        placeholder={placeholder}
        autoComplete="off"
        readOnly={disabled}
      />
      {error && <span className="font-body-sm text-body-sm text-error">{error}</span>}

      {!disabled && (
        <div className="flex items-center gap-2" style={{ marginTop: 'var(--space-1)' }}>
          {showCurrentLocation && (
            <Button type="button" variant="secondary" size="sm" loading={locating} onClick={handleUseCurrentLocation}>
              <Icon name="pin" size={14} /> Use my location
            </Button>
          )}
          <Button type="button" variant="secondary" size="sm" onClick={() => setMapOpen((v) => !v)}>
            <Icon name="route" size={14} /> {mapOpen ? 'Close map' : 'Pick on map'}
          </Button>
          {pin && (
            <span className="font-body-sm text-body-sm text-on-surface-variant">
              📍 {pin[0].toFixed(5)}, {pin[1].toFixed(5)}
            </span>
          )}
        </div>
      )}

      {lookupError && <ErrorMessage>{lookupError}</ErrorMessage>}

      {mapOpen && !disabled && (
        <div style={{ height: 280, borderRadius: 'var(--radius-lg)', overflow: 'hidden', marginTop: 'var(--space-2)' }}>
          <MapContainer
            center={pin || DEFAULT_CENTER}
            zoom={pin ? 14 : 8}
            style={{ height: '100%', width: '100%' }}
            ref={mapRef}
          >
            <TileLayer
              attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
              url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />
            <ClickToPlacePin onPick={applyPoint} />
            {pin && <Marker position={pin} />}
          </MapContainer>
        </div>
      )}
    </div>
  )
}