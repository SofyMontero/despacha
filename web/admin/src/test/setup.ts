import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'
import { resetApiState } from '../api/client'

afterEach(() => {
  cleanup()
  resetApiState()
  vi.unstubAllGlobals()
})
