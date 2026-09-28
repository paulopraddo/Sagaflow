export interface AuthResponse {
  userId: string
  username: string
  token: string
}

export interface RegisterResponse {
  userId: string
  email: string
}

let authToken: string | null = null

export function setApiAuthToken(token: string | null): void {
  authToken = token
}

async function request<T>(path: string, options: { method: string; body?: unknown }): Promise<T> {
  const response = await fetch(`${import.meta.env.VITE_API_URL}${path}`, {
    method: options.method,
    headers: {
      'Content-Type': 'application/json',
      ...(authToken ? { Authorization: `Bearer ${authToken}` } : {}),
    },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  })

  const data = await response.json().catch(() => null)

  if (!response.ok) {
    const message = Array.isArray(data) ? data.join(' ') : 'Falha na requisição.'
    throw new Error(message)
  }

  return data as T
}

export function register(username: string, email: string, password: string): Promise<RegisterResponse> {
  return request<RegisterResponse>('/api/auth/register', { method: 'POST', body: { username, email, password } })
}

export function verifyEmail(email: string, code: string): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/verify-email', { method: 'POST', body: { email, code } })
}

export function resendVerificationCode(email: string): Promise<void> {
  return request<void>('/api/auth/resend-code', { method: 'POST', body: { email } })
}

export function login(email: string, password: string): Promise<AuthResponse> {
  return request<AuthResponse>('/api/auth/login', { method: 'POST', body: { email, password } })
}

export function forgotPassword(email: string): Promise<void> {
  return request<void>('/api/auth/forgot-password', { method: 'POST', body: { email } })
}

export function resetPassword(email: string, code: string, newPassword: string): Promise<void> {
  return request<void>('/api/auth/reset-password', { method: 'POST', body: { email, code, newPassword } })
}
