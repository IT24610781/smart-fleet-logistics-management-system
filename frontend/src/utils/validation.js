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

// Capacity is a positive number with an OPTIONAL unit, e.g. "12", "500 kg",
// "8 persons", "2.5 tons". Same rule as the Fleet API (CapacityParser.cs), so
// the form and the server always agree on what is valid.
const CAPACITY_PATTERN = /^([0-9]{1,9}(?:\.[0-9]{1,2})?)\s*(\p{L}[\p{L}\p{N} ./-]{0,19})?$/u

export const CAPACITY_HINT = 'A number with an optional unit, e.g. 500 kg, 8 persons, 20 units'
export const CAPACITY_ERROR = 'Enter a valid positive number, with an optional unit (e.g. 500 kg, 8 persons).'

export function isValidCapacity(value) {
  const match = CAPACITY_PATTERN.exec(String(value ?? '').trim())
  return match !== null && Number(match[1]) > 0
}