import './FormField.css'

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
}) {
  return (
    <div className="form-field">
      <label className="form-label" htmlFor={name}>
        {label}
        {!required && !readOnly && <span className="optional">Optional</span>}
      </label>
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
        className={`form-input${error ? ' invalid' : ''}${readOnly ? ' readonly' : ''}`}
        aria-invalid={!!error}
        aria-describedby={error ? `${name}-error` : hint ? `${name}-hint` : undefined}
      />
      {error && <span id={`${name}-error`} className="form-error">{error}</span>}
      {!error && hint && <span id={`${name}-hint`} className="form-hint">{hint}</span>}
    </div>
  )
}
