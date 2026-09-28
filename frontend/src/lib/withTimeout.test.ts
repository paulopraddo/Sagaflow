import { describe, expect, it, vi } from 'vitest'
import { withTimeout } from './withTimeout'

describe('withTimeout', () => {
  it('resolve com o valor original quando a promise termina antes do prazo', async () => {
    const result = await withTimeout(Promise.resolve('ok'), 1000, 'deu timeout')

    expect(result).toBe('ok')
  })

  it('rejeita com o erro original quando a promise falha antes do prazo', async () => {
    await expect(withTimeout(Promise.reject(new Error('falhou')), 1000, 'deu timeout')).rejects.toThrow('falhou')
  })

  it('rejeita com a mensagem de timeout quando a promise nunca resolve', async () => {
    vi.useFakeTimers()

    const neverResolves = new Promise(() => {})
    const assertion = expect(withTimeout(neverResolves, 5000, 'deu timeout')).rejects.toThrow('deu timeout')

    await vi.advanceTimersByTimeAsync(5000)
    await assertion

    vi.useRealTimers()
  })
})
