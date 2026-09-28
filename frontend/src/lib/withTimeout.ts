/**
 * Em navegadores mobile, promises de APIs nativas (getUserMedia, WebRTC signaling) as vezes
 * nunca resolvem nem rejeitam quando a permissao/rede trava silenciosamente, deixando a UI
 * presa num estado de "conectando" para sempre. Isso da um prazo maximo e transforma isso
 * num erro visivel, com opcao de tentar de novo.
 */
export function withTimeout<T>(promise: Promise<T>, ms: number, message: string): Promise<T> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error(message)), ms)

    promise.then(
      (value) => {
        clearTimeout(timer)
        resolve(value)
      },
      (err) => {
        clearTimeout(timer)
        reject(err)
      },
    )
  })
}
