import Icon from './Icon'

export default function EmptyState({ icon = 'grid', title, description, action }) {
  return (
    <div className="flex flex-col items-center text-center gap-2 py-12 px-4">
      <div className="w-11 h-11 rounded-full bg-surface-container flex items-center justify-center text-on-surface-variant">
        <Icon name={icon} size={20} />
      </div>
      <div className="font-title-md text-title-md text-on-surface">{title}</div>
      {description && (
        <div className="font-body-sm text-body-sm text-on-surface-variant max-w-sm">{description}</div>
      )}
      {action}
    </div>
  )
}
