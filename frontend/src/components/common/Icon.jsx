// Small dependency-free icon set (no external icon package required by the
// mandated stack). Add a path here rather than pulling in a new library.
const PATHS = {
  grid: 'M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 0h6v6h-6v-6z',
  users: 'M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm8 2a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5zM2 19c0-3.3 2.7-6 6-6s6 2.7 6 6M14.5 13.2c2.5.4 4.5 2.5 4.5 5.3',
  truck: 'M2 6h11v9H2V6zm11 3h4l3 3v3h-7V9zM6 18.5a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm11 0a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z',
  chart: 'M4 20V10m6 10V4m6 16v-7',
  user: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm-7 8c0-3.9 3.1-7 7-7s7 3.1 7 7',
  route: 'M5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4zm14-10a2 2 0 1 0 0-4 2 2 0 0 0 0 4zM5 17c6 0 4-8 9-8h5',
  pin: 'M12 21s7-6.1 7-11.3A7 7 0 0 0 5 9.7C5 14.9 12 21 12 21zm0-8.5a2.7 2.7 0 1 0 0-5.4 2.7 2.7 0 0 0 0 5.4z',
  logout: 'M9 4H5a1 1 0 0 0-1 1v14a1 1 0 0 0 1 1h4m5-4 4-4-4-4m4 4H9',
  search: 'M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16zm9 2-4.35-4.35',
  plus: 'M12 5v14M5 12h14',
  x: 'M18 6 6 18M6 6l12 12',
  check: 'M20 6 9 17l-5-5',
  alert: 'M12 9v4m0 4h.01M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z',
  menu: 'M3 6h18M3 12h18M3 18h18',
  chevronDown: 'm6 9 6 6 6-6',
  close: 'M18 6 6 18M6 6l12 12',
  chevronLeft: 'm15 18-6-6 6-6',
  chevronRight: 'm9 18 6-6-6-6',
  eye: 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7zm10 3.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7z',
  eyeOff: 'M3 3l18 18M10.6 10.6a3.5 3.5 0 0 0 4.8 4.8M9.9 5.1A10.4 10.4 0 0 1 12 5c6.5 0 10 7 10 7a13.2 13.2 0 0 1-3.1 3.9M6.2 6.2A13.2 13.2 0 0 0 2 12s3.5 7 10 7c1.3 0 2.5-.2 3.6-.6',
}

export default function Icon({ name, size = 16, className = '', strokeWidth = 1.8 }) {
  const d = PATHS[name]
  if (!d) return null
  return (
    <svg
      className={className}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={d} />
    </svg>
  )
}
