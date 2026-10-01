import Icon from '../common/Icon'

export default function FormField({
  label,
  name,
  type = 'text',
  value,
  onChange,
  error,
  hint,
  required = false,
  readOnly = false,
  placeholder,
  autoComplete,
  maxLength,
  rightSlot,
  success = false,
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label className="flex items-center justify-between font-title-md text-body-sm text-on-surface" htmlFor={name}>
        <span>{label}</span>
        {!required && !readOnly && (
          <span className="font-label-sm text-label-sm text-on-surface-variant">Optional</span>
        )}
      </label>
      <div className="relative">
        <input
          id={name}
          name={name}
          type={type}
          value={value}
          onChange={onChange}
          readOnly={readOnly}
          required={required}
          placeholder={placeholder}
          autoComplete={autoComplete}
          maxLength={maxLength}
          aria-invalid={!!error}
          aria-describedby={error ? `${name}-error` : hint ? `${name}-hint` : undefined}
          className={`w-full rounded-xl border px-3.5 py-2.5 font-body-md text-body-md text-on-surface placeholder:text-on-surface-variant/60 transition-colors focus:outline-none focus:ring-2 focus:ring-primary/50 ${
            error ? 'border-error' : 'border-outline-variant'
          } ${readOnly ? 'bg-surface-container text-on-surface-variant cursor-not-allowed' : 'bg-surface-container-lowest'}`}
        />
        {rightSlot && <div className="absolute inset-y-0 right-2 flex items-center">{rightSlot}</div>}
      </div>
      {error && (
        <span id={`${name}-error`} className="font-body-sm text-body-sm text-error">
          {error}
        </span>
      )}
      {!error && hint && (
      
        <span
          id={`${name}-hint`}
          className={`font-body-sm text-body-sm flex items-center gap-1 ${success ? '' : 'text-on-surface-variant'}`}
          style={success ? { color: 'var(--success)' } : undefined}
        >
          {success && <Icon name="check" size={14} className="shrink-0" />}
          {hint}
        </span>
      )}
    </div>
  )
}
