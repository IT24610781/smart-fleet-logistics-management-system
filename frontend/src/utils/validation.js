// Small, dependency-free client-side validation helpers shared by every form.
export function isRequired(value) {
  return value !== undefined && value !== null && String(value).trim().length > 0
}

export function isValidEmail(value) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)
}

export function isValidPassword(value) {
  return typeof value === 'string' && value.length >= 8
}

export function isValidUsername(value) {
  // 3-20 chars, letters/numbers/underscore/dot, must start with a letter.
  return /^[a-zA-Z][a-zA-Z0-9_.]{2,19}$/.test(value)
}

export function isValidContactNumber(value) {
  // Accepts digits, spaces, +, - with 7-15 digits total.
  const digits = value.replace(/\D/g, '')
  return digits.length >= 7 && digits.length <= 15
}

export function isValidCapacity(value) {
  return isRequired(value)
}