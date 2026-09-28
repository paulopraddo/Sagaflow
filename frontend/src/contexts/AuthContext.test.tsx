import { act, renderHook } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AuthProvider, useAuth } from './AuthContext'
import * as api from '../lib/api'

vi.mock('../lib/api')

function renderAuth() {
  return renderHook(() => useAuth(), { wrapper: AuthProvider })
}

describe('AuthContext', () => {
  beforeEach(() => {
    localStorage.clear()
    vi.clearAllMocks()
  })

  it('começa sem usuário quando não há sessão salva', () => {
    const { result } = renderAuth()

    expect(result.current.user).toBeNull()
  })

  it('restaura o usuário do localStorage ao montar', () => {
    localStorage.setItem('boilerplate:auth', JSON.stringify({ userId: '1', username: 'joao', token: 'tok' }))

    const { result } = renderAuth()

    expect(result.current.user).toEqual({ userId: '1', username: 'joao', token: 'tok' })
  })

  it('login com sucesso define o usuário e persiste no localStorage', async () => {
    vi.mocked(api.login).mockResolvedValue({ userId: '1', username: 'joao', token: 'tok' })
    const { result } = renderAuth()

    await act(async () => {
      await result.current.login('joao@example.com', 'senha1234')
    })

    expect(result.current.user).toEqual({ userId: '1', username: 'joao', token: 'tok' })
    expect(JSON.parse(localStorage.getItem('boilerplate:auth')!)).toEqual({
      userId: '1',
      username: 'joao',
      token: 'tok',
    })
  })

  it('login com falha propaga o erro e não define usuário', async () => {
    vi.mocked(api.login).mockRejectedValue(new Error('credenciais inválidas'))
    const { result } = renderAuth()

    let caughtError: unknown = null
    await act(async () => {
      try {
        await result.current.login('joao@example.com', 'errada')
      } catch (err) {
        caughtError = err
      }
    })

    expect(caughtError).toBeInstanceOf(Error)
    expect((caughtError as Error).message).toBe('credenciais inválidas')
    expect(result.current.user).toBeNull()
  })

  it('register não autentica o usuário, só retorna o e-mail', async () => {
    vi.mocked(api.register).mockResolvedValue({ userId: '1', email: 'joao@example.com' })
    const { result } = renderAuth()

    let returnedEmail = ''
    await act(async () => {
      returnedEmail = await result.current.register('joao', 'joao@example.com', 'senha1234')
    })

    expect(returnedEmail).toBe('joao@example.com')
    expect(result.current.user).toBeNull()
  })

  it('verifyEmail autentica o usuário', async () => {
    vi.mocked(api.verifyEmail).mockResolvedValue({ userId: '1', username: 'joao', token: 'tok' })
    const { result } = renderAuth()

    await act(async () => {
      await result.current.verifyEmail('joao@example.com', '123456')
    })

    expect(result.current.user).toEqual({ userId: '1', username: 'joao', token: 'tok' })
  })

  it('forgotPassword chama a api com o e-mail', async () => {
    vi.mocked(api.forgotPassword).mockResolvedValue(undefined)
    const { result } = renderAuth()

    await act(async () => {
      await result.current.forgotPassword('joao@example.com')
    })

    expect(api.forgotPassword).toHaveBeenCalledWith('joao@example.com')
  })

  it('resetPassword chama a api com e-mail, codigo e nova senha', async () => {
    vi.mocked(api.resetPassword).mockResolvedValue(undefined)
    const { result } = renderAuth()

    await act(async () => {
      await result.current.resetPassword('joao@example.com', '123456', 'novaSenha123')
    })

    expect(api.resetPassword).toHaveBeenCalledWith('joao@example.com', '123456', 'novaSenha123')
  })

  it('logout limpa o usuário e o localStorage', async () => {
    vi.mocked(api.login).mockResolvedValue({ userId: '1', username: 'joao', token: 'tok' })
    const { result } = renderAuth()

    await act(async () => {
      await result.current.login('joao@example.com', 'senha1234')
    })

    act(() => {
      result.current.logout()
    })

    expect(result.current.user).toBeNull()
    expect(localStorage.getItem('boilerplate:auth')).toBeNull()
  })
})
