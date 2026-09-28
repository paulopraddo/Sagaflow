import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../contexts/AuthContext'

function ForgotPasswordPage() {
  const { forgotPassword, resetPassword } = useAuth()
  const navigate = useNavigate()

  const [step, setStep] = useState<'request' | 'reset'>('request')
  const [email, setEmail] = useState('')
  const [code, setCode] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [info, setInfo] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleRequestCode(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setInfo(null)
    setIsSubmitting(true)

    try {
      await forgotPassword(email)
      setInfo('Se esse e-mail estiver cadastrado, enviamos um código para redefinir a senha.')
      setStep('reset')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao solicitar redefinição de senha.')
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleResetPassword(event: React.FormEvent) {
    event.preventDefault()
    setError(null)
    setInfo(null)
    setIsSubmitting(true)

    try {
      await resetPassword(email, code, newPassword)
      navigate('/login')
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Falha ao redefinir a senha.')
    } finally {
      setIsSubmitting(false)
    }
  }

  if (step === 'request') {
    return (
      <div className="auth-page">
        <form className="auth-form" onSubmit={handleRequestCode}>
          <h1>Esqueci minha senha</h1>
          <p className="auth-hint">Informe seu e-mail para receber um código de redefinição de senha.</p>

          <label>
            E-mail
            <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </label>

          {error && <div className="chat-error">{error}</div>}

          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Enviando...' : 'Enviar código'}
          </button>

          <p className="auth-switch">
            <Link to="/login">Voltar para o login</Link>
          </p>
        </form>
      </div>
    )
  }

  return (
    <div className="auth-page">
      <form className="auth-form" onSubmit={handleResetPassword}>
        <h1>Redefinir senha</h1>
        <p className="auth-hint">Informe o código recebido por e-mail e escolha uma nova senha.</p>

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

        <label>
          Nova senha
          <input
            type="password"
            value={newPassword}
            onChange={(e) => setNewPassword(e.target.value)}
            minLength={8}
            required
          />
        </label>

        {error && <div className="chat-error">{error}</div>}
        {info && <div className="auth-info">{info}</div>}

        <button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Redefinindo...' : 'Redefinir senha'}
        </button>

        <button type="button" className="auth-secondary-button" onClick={() => setStep('request')}>
          Reenviar código
        </button>

        <p className="auth-switch">
          <Link to="/login">Voltar para o login</Link>
        </p>
      </form>
    </div>
  )
}

export default ForgotPasswordPage
