import Icon from './Icon'

export default function StatCard({ label, value, icon, delta }) {
  const deltaColor = delta?.direction === 'down' ? 'text-error' : 'text-secondary'
  return (
    <div className="rounded-xl border border-outline-variant bg-surface-container-lowest p-4">
      <div className="flex items-center justify-between">
        <span className="font-label-md text-label-md uppercase tracking-wide text-on-surface-variant">{label}</span>
        {icon && (
          <span className="w-8 h-8 rounded-lg bg-primary-container/30 text-on-primary-container flex items-center justify-center">
            <Icon name={icon} size={15} />
          </span>
        )}
      </div>
      <span className="mt-2 block font-headline-lg text-headline-lg text-on-surface">{value}</span>
      {delta && <span className={`mt-1 block font-body-sm text-body-sm ${deltaColor}`}>{delta.text}</span>}
    </div>
  )
}
