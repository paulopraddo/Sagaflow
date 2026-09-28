# Boilerplate

Boilerplate full-stack para iniciar novos projetos rapidamente: backend em .NET com Clean Architecture + DDD e frontend em React/Vite. Já vem com um módulo de autenticação completo e funcional (registro, login, verificação de e-mail, esqueci/redefinir senha, JWT) servindo como exemplo de como estruturar qualquer outro módulo do domínio.

- **Backend**: .NET 10, Clean Architecture + DDD, CQRS com MediatR, EF Core + PostgreSQL, JWT.
- **Frontend**: React 19 + Vite + TypeScript.

## Estrutura

```
backend/
  Boilerplate.API/             # Controllers, Program.cs, configuração HTTP
  Boilerplate.Application/     # Casos de uso (CQRS: Commands/Queries + Handlers)
  Boilerplate.Domain/          # Entidades, value objects, interfaces de repositório
  Boilerplate.Infrastructure/  # EF Core, repositórios, JWT, hashing, envio de e-mail
  Boilerplate.Tests/           # Testes de unidade (Domain + Application)
frontend/
  src/
    components/                # ErrorBoundary, RequireAuth
    contexts/                  # AuthContext
    lib/                       # cliente HTTP (api.ts), utilitários
    pages/                     # Login, Register, ForgotPassword, VerifyEmail, Home
```

O módulo `Users`/`Auth` percorre as quatro camadas do backend (Domain → Application → Infrastructure → API) e serve de referência para criar novos módulos: copie a estrutura de pastas (`Domain/<Modulo>`, `Application/<Modulo>/Commands|Queries`, configuração EF em `Infrastructure/Persistence/Configurations`, repositório em `Infrastructure/Persistence/Repositories`, controller em `API/Controllers`) e registre as novas dependências em `Application/DependencyInjection.cs` / `Infrastructure/DependencyInjection.cs`.

## Pré-requisitos

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (para o PostgreSQL local) ou uma instância PostgreSQL própria

## Rodando localmente

### 1. Banco de dados

```bash
docker compose up -d
```

Sobe um PostgreSQL em `localhost:5432` (db `boilerplate`, usuário/senha `postgres`).

### 2. Backend

> **Importante:** este boilerplate não inclui migrations do EF Core (foram removidas junto com o domínio de exemplo original). Antes do primeiro `dotnet run`, configure a connection string em `appsettings.Development.json` (ou variável de ambiente) e gere a migration inicial:
>
> ```bash
> cd backend
> dotnet ef migrations add InitialCreate --project Boilerplate.Infrastructure --startup-project Boilerplate.API
> ```
>
> Se o `dotnet-ef` não estiver instalado: `dotnet tool install --global dotnet-ef`.

```bash
cd backend
dotnet run --project Boilerplate.API
```

A API sobe em `http://localhost:5225` (porta definida em `Properties/launchSettings.json`). As migrations pendentes são aplicadas automaticamente ao iniciar (`Program.cs` chama `dbContext.Database.MigrateAsync()`).

Configure os segredos abaixo antes de rodar (veja [Variáveis de ambiente](#variáveis-de-ambiente-backend)). Para desenvolvimento local, `appsettings.Development.json` já traz um segredo JWT de exemplo — **não usar em produção**.

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
```

A aplicação sobe em `http://localhost:5173` e usa `VITE_API_URL` para apontar para o backend (já configurado em `.env.development`).

## Testes

```bash
# Backend
cd backend
dotnet test Boilerplate.slnx

# Frontend
cd frontend
npm run test        # roda uma vez
npm run test:watch  # modo watch
npm run test:coverage
```

Lint do frontend: `npm run lint` (oxlint).

## Variáveis de ambiente (backend)

Configuráveis via `appsettings.json` / `appsettings.Development.json` ou variáveis de ambiente (`ConnectionStrings__Boilerplate`, `Jwt__Secret`, etc.).

| Chave | Descrição |
|---|---|
| `ConnectionStrings:Boilerplate` | Connection string do PostgreSQL. |
| `Jwt:Issuer` / `Jwt:Audience` | Issuer/audience do token JWT. |
| `Jwt:Secret` | Chave simétrica usada para assinar os JWTs. **Trocar em produção.** |
| `Jwt:ExpirationMinutes` | Validade do token, em minutos. |
| `Cors:AllowedOrigins` | Lista de origens permitidas pelo CORS (URLs do frontend). |
| `Brevo:ApiKey` | API key da [Brevo](https://www.brevo.com/), usada para enviar e-mails de verificação de cadastro/reset de senha. |
| `Brevo:SenderEmail` / `Brevo:SenderName` | Remetente usado nos e-mails enviados. |
| `PORT` | Porta HTTP da API (usada por Railway/Fly/Render; opcional em dev). |

## Variáveis de ambiente (frontend)

| Chave | Descrição |
|---|---|
| `VITE_API_URL` | URL base da API backend. |

## Rate limiting

A API limita requisições por IP: 100 req/min globalmente e 10 req/min nos endpoints de `/api/auth/*` (login, registro, verificação, reenvio de código), para dificultar força bruta. Excedendo o limite, a API responde `429 Too Many Requests`.

## Health check

`GET /health` verifica se a API está no ar e consegue se conectar ao PostgreSQL, retornando `200 Healthy` ou `503 Unhealthy`. Útil para probes de infraestrutura (Railway/Fly/Render, load balancers, etc).

## Deploy

- **Backend**: `backend/Dockerfile` builda e roda a API; aplica migrations pendentes automaticamente ao iniciar. Pensado para plataformas como Railway/Fly/Render (lê a porta de `PORT` e confia em cabeçalhos `X-Forwarded-*` de proxy reverso).
- **Frontend**: `frontend/vercel.json` configura o deploy estático na Vercel.

## CI

Workflow em [`.github/workflows/ci.yml`](.github/workflows/ci.yml) builda e testa backend e frontend em cada push/PR para `main`.

## O que este boilerplate NÃO inclui de propósito

Este é um ponto de partida, não um produto pronto. Antes de aceitar usuários reais em produção, considere adicionar:

- Observabilidade (logging estruturado, métricas, error tracking).
- Refresh token / revogação de sessão para o JWT.
- Lockout de conta após tentativas de login inválidas.
- Gestão de segredos via vault (hoje vivem em `appsettings`/variáveis de ambiente simples).
- Exclusão de conta / exportação de dados do usuário (LGPD/GDPR), se aplicável ao seu produto.
