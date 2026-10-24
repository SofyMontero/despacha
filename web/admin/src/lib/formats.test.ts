import { describe, expect, it } from 'vitest'
import { formatMoneyInput, parseMoney } from './money'
import { isColombianPhone } from './phone'

describe('precios', () => {
  it('acepta punto, coma y miles', () => {
    expect(parseMoney('45000.50')).toBe(45000.5)
    expect(parseMoney('45000,50')).toBe(45000.5)
    expect(parseMoney('45.000')).toBe(45000)
    expect(parseMoney('0')).toBe(0)
    expect(parseMoney('-1')).toBeNull()
    expect(parseMoney('1.2345')).toBeNull()
  })

  it('prepara el valor para el formulario', () => {
    expect(formatMoneyInput(45000)).toBe('45000')
    expect(formatMoneyInput(45000.5)).toBe('45000.5')
  })
})

describe('teléfonos', () => {
  it('acepta 10 dígitos y 57', () => {
    expect(isColombianPhone('300 123 4567')).toBe(true)
    expect(isColombianPhone('+57 3001234567')).toBe(true)
    expect(isColombianPhone('12345')).toBe(false)
    expect(isColombianPhone('5730012345678')).toBe(false)
  })
})
