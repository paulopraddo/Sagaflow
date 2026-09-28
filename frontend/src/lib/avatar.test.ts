import { describe, expect, it } from 'vitest'
import { getAvatarColor, getInitials } from './avatar'

describe('getInitials', () => {
  it('retorna as duas primeiras letras em maiúsculo', () => {
    expect(getInitials('joao')).toBe('JO')
  })

  it('remove espaços nas extremidades antes de pegar as iniciais', () => {
    expect(getInitials('  ana  ')).toBe('AN')
  })

  it('retorna "?" para nome vazio ou só com espaços', () => {
    expect(getInitials('')).toBe('?')
    expect(getInitials('   ')).toBe('?')
  })
})

describe('getAvatarColor', () => {
  it('é determinístico para o mesmo valor', () => {
    expect(getAvatarColor('peer-123')).toBe(getAvatarColor('peer-123'))
  })

  it('retorna sempre uma cor em formato hexadecimal', () => {
    expect(getAvatarColor('qualquer-coisa')).toMatch(/^#[0-9a-f]{6}$/i)
  })

  it('gera cores diferentes para seeds diferentes', () => {
    const colors = new Set(['a', 'b', 'c', 'd', 'e', 'f', 'g'].map(getAvatarColor))
    expect(colors.size).toBeGreaterThan(1)
  })
})
