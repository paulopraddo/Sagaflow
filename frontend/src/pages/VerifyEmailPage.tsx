import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

interface LocationState {
  email?: string
}

function VerifyEmailPage() {
  const { verifyEmail, resendVerificationCode } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const emailFromState = (location.state as LocationState | null)?.email ?? ''

  const [email, setEmail] = useState(emailFromState)
  const [code, setCode] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [info, setInfo] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isResending, setIsResending] = useState(false)

  async function handleSubmit(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setInfo(null)
    setIsSubmitting(true)

    try {
      await verifyEmail(email, code)
      navigate('/')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao confirmar o e-mail.')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleResend() {
    if (!email) {
      setError('Informe seu e-mail para reenviar o código.')
      return
    }

    setError(null)
    setInfo(null)
    setIsResending(true)

    try {
      await resendVerificationCode(email)
      setInfo('Enviamos um novo código para o seu e-mail.')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao reenviar o código.')
    } finally {
      setIsResending(false)
    }
  }

  return (
    <div className="auth-page">
      <form className="auth-form" onSubmit={handleSubmit}>
        <h1>Confirme seu e-mail</h1>
        <p className="auth-hint">
          Enviamos um código de 6 dígitos para o seu e-mail. Informe-o abaixo para ativar sua conta.
        </p>

        <label>
          E-mail
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </label>

        <label>
          Código
          <input
            type="text"
            inputMode="numeric"
            maxLength={6}
            placeholder="000000"
            value={code}
            onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
            required
          />
        </label>

        {error && <div className="chat-error">{error}</div>}
        {info && <div className="auth-info">{info}</div>}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Confirmando...' : 'Confirmar'}
        </button>

        <button type="button" className="auth-secondary-button" onClick={handleResend} disabled={isResending}>
          {isResending ? 'Enviando...' : 'Reenviar código'}
        </button>

        <p className="auth-switch">
          <Link to="/login">Voltar para o login</Link>
        </p>
      </form>
    </div>
  )
}

export default VerifyEmailPage
