/** Misma regla que PhoneNumbers.TryNormalizeColombian: 57 y 10 dígitos. */
export function isColombianPhone(input: string) {
  let digits = ''
  for (const char of input) {
    if (char >= '0' && char <= '9') digits += char
  }

  if (digits.length === 10 && !digits.startsWith('57')) digits = `57${digits}`
  return digits.length === 12 && digits.startsWith('57')
}

export const phoneMessage = 'Use un número colombiano de 10 dígitos, con o sin el indicativo 57.'

export function phoneWarningText(ids: number[]) {
  if (ids.length === 1) return `Este teléfono también lo usa el cliente ${ids[0]}.`
  return `Este teléfono también lo usan los clientes ${ids.join(', ')}.`
}
