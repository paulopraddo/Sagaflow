import { useAuth } from '../contexts/AuthContext'

function HomePage() {
  const { user, logout } = useAuth()

  return (
    <div className="empty-state">
      <h1>Bem-vindo, {user?.username}!</h1>
      <p>Você está autenticado. Comece a construir sua aplicação a partir daqui.</p>
      <button type="button" onClick={logout}>
        Sair
      </button>
    </div>
  )
}

export default HomePage
