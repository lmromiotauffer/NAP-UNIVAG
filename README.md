# NAP: Núcleo de Apoio Psicopedagógico · UNIVAG

Sistema web do Núcleo de Apoio Psicopedagógico (NAP) do UNIVAG, desenvolvido com .NET 10 e React 19, com Clean Architecture.

Reúne os registros do acompanhamento do aluno, do acolhimento à alta: agenda da equipe, atendimentos, encaminhamentos, PEIs, devolutivas com as coordenações de curso, atividades nos cursos e produção do núcleo. Os registros têm histórico, autoria e controle de acesso por perfil.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![EF Core](https://img.shields.io/badge/EF_Core-10-512BD4)
![React](https://img.shields.io/badge/React-19-149ECA?logo=react&logoColor=white)
![Tailwind](https://img.shields.io/badge/Tailwind_CSS-4-06B6D4?logo=tailwindcss&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-2022-CC2927?logo=microsoftsqlserver&logoColor=white)
![Redis](https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white)
![Tests](https://img.shields.io/badge/testes-xUnit_·_Vitest_·_Playwright-2E7D32)

Fases 1, 2 e 3 concluídas, incluindo a Fatia 6. O envio de e-mail está pendente; as demais restrições estão em [Limitações conhecidas](#limitações-conhecidas).

Documentação: [mapa conceitual](docs/mapa-conceitual.html) (abra no navegador) e [contrato da API](backend/openapi.json).

---

## Sumário

[Objetivo](#objetivo-do-sistema) · [Módulos](#módulos) · [Arquitetura](#arquitetura) · [Tecnologias](#tecnologias) · [Estrutura](#estrutura-da-solução) · [Como executar](#como-executar) · [Usuários de demonstração](#usuários-de-demonstração) · [Configuração](#configuração) · [Banco de dados](#banco-de-dados) · [Migrations](#migrations) · [Redis](#redis-cache) · [Autenticação e autorização](#autenticação-e-autorização) · [API](#api-rest) · [Testes](#testes) · [Observabilidade](#observabilidade) · [Segurança](#segurança) · [Deploy](#deploy) · [Decisões](#decisões-arquiteturais) · [Limitações](#limitações-conhecidas) · [Troubleshooting](#troubleshooting)

---

## Objetivo do sistema

Registrar o acompanhamento dos alunos e a produção da equipe, com auditoria e acesso por perfil, sem depender de planilhas paralelas.

As permissões estão nas políticas de `backend/src/Nap.Api/Infra/Politicas.cs` e nos escopos de cada feature:

| Perfil | O que vê | O que pode fazer |
|--------|----------|------------------|
| Administrador | Tudo, inclusive documentos arquivados e a auditoria | Administração completa (inclusive de administradores); correções justificadas; aprova, publica e arquiva PEI; reabre semana e lança ajustes de produção |
| Coordenador NAP | Igual ao Administrador | Igual ao Administrador, mas não cria nem gerencia administradores |
| Profissional NAP | Todos os alunos e a agenda da equipe; só a própria produção | Grava a própria agenda; registra e corrige os próprios atendimentos e atividades; cria encaminhamentos e PEIs; fecha a própria semana |
| Coordenador de curso | Resumo pedagógico, PEIs publicados, documentos compartilhados, atividades e produção do próprio curso, sem dados clínicos. Após uma transferência, mantém acesso ao aluno e à linha do tempo do período em que o coordenava | Registra ciência do PEI e retorno de encaminhamento; usa comunicações |
| SIP (Clínica Escola) | Só os encaminhamentos destinados ao SIP/Clínica Escola e o painel desses encaminhamentos | Registra retorno; usa comunicações |
| Gestão | Painel e produção agregados e por profissional, sem alunos | Só leitura |

Neste documento, "Equipe NAP" inclui Administrador, Coordenador NAP e Profissional NAP.

---

## Módulos

| Módulo | Rota | Funcionalidades |
|--------|------|---------------|
| Painel | `/` | Indicadores da semana, série de 6 meses, alunos por curso e situação, pendências, equipe do dia, encaminhamentos, PEIs e atividades, conforme o perfil |
| Alunos | `/alunos` | Busca, filtros (inclusive por tipo de encaminhamento) e paginação; cadastro, situação e transferência de curso; ficha com resumo, atendimentos, linha do tempo, documentos, comunicações e PEIs |
| Agenda | `/agenda` | Semana individual ou da equipe; criar, remarcar e cancelar compromissos com detecção de conflito; reuniões e lembretes com vários participantes (uma instância por pessoa) |
| Atendimentos | `/atendimentos` | Registro a partir do compromisso ou avulso, correção justificada e histórico |
| Atividades nos cursos | `/atividades` | Registro a partir do compromisso (após o término) ou avulso com justificativa; correção com histórico; coordenação de curso só lê as do próprio curso |
| Encaminhamentos | `/encaminhamentos` | Entre a equipe NAP, o SIP/Clínica Escola, serviços externos e a coordenação de curso, com retornos, prazos e alertas no painel |
| PEI | `/peis/versoes/:id` | Versões, revisão da coordenação, assinatura do profissional e do aluno (link e código temporários em `/assinar/:token`, sem login), publicação, ciência da coordenação de curso e PDF |
| Comunicações | `/comunicacoes` | Entrada, Enviados, Rascunhos, Arquivadas e Notificações; respostas em fio; vínculo opcional a curso, aluno e encaminhamento |
| Produção | `/producao` | Produção por período, fechamento semanal, ajustes, recortes por perfil e exportação em PDF e Excel com o mesmo recorte da tela |
| Administração | `/administracao` | Usuários (senha temporária e troca obrigatória), cursos, escalas com vigência, autorizações fora da escala, feriados, limites de alerta e auditoria |

Documentos do aluno: PDF, PNG, JPG, DOCX, XLSX, TXT e CSV de até 5 MiB, com checagem da assinatura do arquivo. O acesso inicial é restrito. O compartilhamento com a coordenação exige justificativa; revogações, arquivamentos e downloads são auditados.

---

## Arquitetura

### Visão em camadas

| Camada | Responsabilidades e componentes |
|---|---|
| SPA React 19 | Vite, TypeScript, Tailwind, React Router, TanStack Query, React Hook Form e Zod. Acessa a API por HTTPS/REST, com JWT e cookie de refresh. |
| Api (ASP.NET Core 10) | Controllers, filtros, ProblemDetails, rate limit, health checks e arquivos estáticos da SPA. |
| Application | Commands, queries, handlers, DTOs, validators e ports. |
| Domain | Entidades, value objects, eventos e regras de domínio. |
| Infrastructure | Implementações dos ports: EF Core 10, Identity, JWT, Redis, outbox, PDF, Excel e arquivos em disco. Usa SQL Server e Redis. |

### Regra de dependência

| Projeto | Depende de |
|---|---|
| Api | Application |
| Infrastructure | Application |
| Application | Domain |
| Domain | Nenhum outro projeto |

O `Nap.Domain` é C# puro, sem nenhum `PackageReference`. A regra é verificada por teste (`backend/tests/Nap.Api.Tests/ArquiteturaTests.cs`).

### Domain

Organizado por área: Agenda, Alunos, Atendimentos, Atividades, Comunicacoes, Configuracoes, Cursos, Documentos, Encaminhamentos, Notificacoes, Peis e Producao.

| Área | Entidades principais |
|------|----------------------|
| Agenda | `Compromisso`, `EscalaProfissional`, `Feriado`, `AutorizacaoForaEscala`, `CorrecaoRealizacao` |
| Alunos | `Aluno` (com `VinculoCurso` e `MudancaSituacao`) |
| Atendimentos / Atividades | `Atendimento` (+ `CorrecaoAtendimento`), `AtividadeCurso` (+ `CorrecaoAtividade`) |
| Encaminhamentos | `Encaminhamento` (+ `EvolucaoEncaminhamento`) |
| PEI | `Pei`, `VersaoPei`, `AssinaturaPei`, `CienciaPei`, `AcessoAssinaturaPei`, `TrocaResponsavelPei` |
| Produção | `FechamentoSemanal`, `AjusteProducao` |
| Demais | `Curso`, `Documento`, `Mensagem`, `Notificacao`, `ConfiguracaoAlertas` |

Value Objects: `IntervaloHorario`, `Matricula`, `PeriodoVigencia`. Serviços de domínio: `CalculadoraProducao`, `CalculadoraHashPei`, `EscalaPadrao`.

Eventos de domínio (`IEventoDominio`): `EncaminhamentoCriado`, `RetornoEncaminhamentoRegistrado` e `MensagemEnviada`, gravados na outbox na mesma transação do `SaveChanges` e processados em segundo plano para gerar as notificações. Violação de regra lança `DomainException` (→ 422).

### Application: casos de uso

Organizada por feature em `Features/<Feature>/{Commands,Queries,DTOs}`, com um handler explícito por caso de uso, sem MediatR. Cada arquivo reúne Command/Query, Validator (FluentValidation) e Handler. O registro dos tipos terminados em `Handler` é feito por varredura do assembly.

```
Features/  Agenda · Alunos · Atendimentos · Atividades · Auditoria · Auth · Comunicacoes
           Configuracoes · Cursos · Documentos · Encaminhamentos · Notificacoes
           Painel · Peis · Producao · Usuarios
```

Ports (em `Comum`, implementados na Infrastructure): `IUnidadeDeTrabalho`, `IUsuarioAtual`, `IRelogio`, `IAuditoria`, `ICacheConsultas`, `IChavesIdempotencia`, `IArmazenamentoArquivos`, `IConfiguracaoAlertas`, `IEnvioEmail`, `IServicoAssinatura`; além de `IServicoSessao`, `IGestaoUsuarios`, `IGeradorPdfPei`, `IGeradorPdfProducao`, `IGeradorExcelProducao` e os `I*Repository` / `I*Queries` de cada feature.

Escrita e leitura são separadas: comandos carregam a entidade pelo repositório e aplicam comportamento; consultas projetam direto para DTO.

### Infrastructure

`NapDbContext` (domínio + Identity, grava a outbox no `SaveChanges`) · configurações EF por entidade · repositórios e consultas · `ProcessadorOutbox` (hosted service) · cache do painel e validação de sessão no Redis · `ArmazenamentoEmDisco` · PDF com PDFsharp-MigraDoc e Excel com ClosedXML · `AssinaturaDemonstrativa` · `EnvioEmailNaoConfigurado` · seed de demonstração.

A agenda usa `sp_getapplock` exclusivo, vinculado à transação, com timeout de 10 s. As chaves por profissional e aluno são adquiridas em ordem fixa para impedir agendamentos simultâneos conflitantes. O fechamento semanal usa o mesmo mecanismo.

### Api: apresentação

Os controllers recebem as requisições HTTP e chamam os casos de uso. Pipeline (`Program.cs`): `ManipuladorExcecoes` (→ ProblemDetails) · HSTS e redirecionamento HTTPS fora de Development · cabeçalhos de segurança · arquivos estáticos do SPA · rate limit do login · autenticação/autorização · `ExigeTrocaDeSenha` · controllers · health checks · fallback para o `index.html`.

Filtros: `ExigeIfMatch` (controle otimista), `IdempotenciaFilter` (`Idempotency-Key`) e `AuditoriaAutorizacao` (audita 403 por política).

### Fluxo de uma requisição: agendar compromisso

1. O formulário React (React Hook Form + Zod) envia `POST /api/agenda` com JWT ao `AgendaController`.
2. A API valida o JWT, o carimbo de segurança e a sessão (cache de 30 s no Redis ou consulta ao banco), aplica `ExigeTrocaDeSenha` e verifica a política nomeada.
3. O command passa pelo validator (FluentValidation) e pelo handler.
4. O handler verifica escopo e posse: fora do escopo retorna 404; ação proibida retorna 403.
5. O servidor valida escala e feriado; violações retornam 422.
6. A API adquire `sp_getapplock` por profissional e aluno e verifica conflitos. Se houver, retorna 409 com a lista de conflitos.
7. O `SaveChanges` (Unit of Work) grava os dados e a outbox na mesma transação.
8. A API retorna 201 ou 200. Os erros seguem o formato ProblemDetails.

---

## Tecnologias

| Camada | Stack |
|--------|-------|
| Back-end | .NET 10 · ASP.NET Core 10 (SDK 10.0.401 fixado em `global.json`) |
| ORM / Banco | Entity Framework Core 10 · SQL Server 2022 |
| Cache / Sessão | Redis 7 (`Microsoft.Extensions.Caching.StackExchangeRedis`) |
| Auth | ASP.NET Core Identity + JWT Bearer + refresh rotativo em cookie |
| Validação | FluentValidation 12 |
| Documentos | PDFsharp-MigraDoc 6 (PDF) · ClosedXML (Excel) |
| API Docs | OpenAPI nativo (`Microsoft.AspNetCore.OpenApi`), gerado no build |
| Testes | xUnit v3 · Testcontainers (SQL Server e Redis) · `WebApplicationFactory` · Vitest · Testing Library · Playwright |
| Front-end | React 19 · TypeScript · Vite 8 · Tailwind CSS 4 · React Router 8 · TanStack Query 5 · React Hook Form · Zod 4 · Axios · lucide-react · openapi-typescript |

Versões centralizadas em `backend/Directory.Packages.props` (Central Package Management) e no `package-lock.json`. Não há MediatR, AutoMapper, repositório genérico, Serilog nem biblioteca de componentes de UI.

---

## Estrutura da solução

```
NAP/
├── backend/
│   ├── Nap.slnx · Directory.Build.props · Directory.Packages.props · dotnet-tools.json
│   ├── openapi.json                 ← contrato gerado no build da Api
│   ├── arquivos/                    ← uploads em desenvolvimento (ignorado pelo git)
│   ├── src/
│   │   ├── Nap.Domain/              ← C# puro, zero dependências
│   │   ├── Nap.Application/         ← Comum (ports, exceções) · Features/<Feature>/{Commands,Queries,DTOs}
│   │   ├── Nap.Infrastructure/      ← Persistencia · Identidade · Cache · Notificacoes · Arquivos · Pdf · Excel · Assinatura · Seed
│   │   └── Nap.Api/                 ← Controllers · Contratos · Infra · Program.cs (composition root)
│   └── tests/
│       ├── Nap.Domain.Tests/
│       ├── Nap.Application.Tests/
│       ├── Nap.Infrastructure.Tests/
│       └── Nap.Api.Tests/
├── frontend/
│   ├── src/                         ← auth · components/{ui,layout,graficos} · features/* · lib · styles · router.tsx
│   ├── e2e/                         ← jornadas Playwright
│   └── vite.config.ts · playwright.config.ts
├── docs/                            ← mapa conceitual
├── docker-compose.yml               ← SQL Server e Redis de desenvolvimento (só em 127.0.0.1)
├── .env.example                     ← NAP_SQL_SENHA
└── global.json
```

---

## Como executar

Pré-requisitos: .NET SDK 10.0.401 · Docker Desktop em execução · Node 24 · certificado HTTPS de desenvolvimento confiável (`dotnet dev-certs https --trust`; o cookie de refresh é `Secure`).

Comece na raiz do repositório e siga as mudanças de diretório indicadas nos comandos.

### 1. SQL Server e Redis

Copie o modelo e defina `NAP_SQL_SENHA` (senha do `sa`: 8+ caracteres, com maiúsculas, minúsculas, números e símbolos). O `.env` é ignorado pelo git.

```bash
cp .env.example .env
docker compose up -d --wait      # portas 1433 e 6379, só em 127.0.0.1
```

### 2. Segredos de desenvolvimento (nunca versionados)

```bash
dotnet user-secrets set "ConnectionStrings:Nap" "Server=127.0.0.1,1433;Database=Nap;User Id=sa;Password=<NAP_SQL_SENHA do .env>;TrustServerCertificate=True" --project backend/src/Nap.Api
dotnet user-secrets set "ConnectionStrings:Redis" "127.0.0.1:6379" --project backend/src/Nap.Api
dotnet user-secrets set "Jwt:Chave" "<base64 aleatório de 64 bytes>" --project backend/src/Nap.Api
dotnet user-secrets set "Seed:SenhaPadrao" "<10+ caracteres, com dígito e maiúscula>" --project backend/src/Nap.Api
```

Gere a `Jwt:Chave` com `openssl rand -base64 64` (mínimo de 32 bytes; a API não inicia sem ela). Para rever a senha dos usuários de demonstração: `dotnet user-secrets list --project backend/src/Nap.Api`. Se preferir arquivo, copie `backend/src/Nap.Api/appsettings.Development.example.json` para `appsettings.Development.json` (ignorado pelo git).

### 3. Subir a API (migrações + dados de demonstração)

```bash
dotnet run --project backend/src/Nap.Api --launch-profile Nap.Api -- --migrar --semear   # https://localhost:7180
```

- `--migrar` aplica as migrações. Sem essa flag, a inicialização não toca o banco nem o Redis.

- `--semear` só funciona em `Development` e é idempotente (não faz nada se `admin@nap.demo` já existe).

- Nas próximas vezes, basta `dotnet run --project backend/src/Nap.Api --launch-profile Nap.Api`.

Verifique com `curl -k https://localhost:7180/health/ready` → `Healthy`.

### 4. Subir o front-end (outro terminal)

```bash
cd frontend
npm ci
npm run dev                      # http://localhost:5173
```

O Vite faz proxy de `/api` e `/health` para `https://localhost:7180`. Se a API estiver em outro endereço, copie `frontend/.env.example` para `frontend/.env` e ajuste. Acesse http://localhost:5173 e entre com um usuário de demonstração.

---

## Usuários de demonstração

Os dados são fictícios. Todos os usuários usam a senha definida em `Seed:SenhaPadrao`.

| E-mail | Perfil |
|--------|--------|
| `admin@nap.demo` | Administrador |
| `coordenacao@nap.demo` | Coordenador NAP |
| `ana.beatriz@nap.demo`, `carlos.mendes@nap.demo`, `fernanda.lima@nap.demo`, `rafael.torres@nap.demo` | Profissional NAP |
| `coord.direito@nap.demo` | Coordenador de curso (Direito) |
| `sip@nap.demo` | SIP |
| `gestao@nap.demo` | Gestão |

Cinco falhas de login bloqueiam a conta por 15 minutos.

---

## Configuração

Hierarquia: `appsettings.json` (só valores não sensíveis, versionado) → user-secrets (desenvolvimento) → variáveis de ambiente (produção).

| Chave | Onde | Descrição |
|-------|------|-----------|
| `ConnectionStrings:Nap` | secret | SQL Server |
| `ConnectionStrings:Redis` | secret | Redis (obrigatório; a API não inicia sem a string. Se o Redis cair em execução, a API segue, mais lenta) |
| `Jwt:Chave` | secret | Chave HMAC, 32+ bytes |
| `Jwt:Emissor` / `Jwt:Audiencia` | appsettings | Emissão do token |
| `Seed:SenhaPadrao` | secret | Senha dos usuários de demonstração (`--semear`) |
| `Nap:Arquivos:Diretorio` | appsettings / env | Pasta dos documentos (padrão relativo ao content root; em produção, caminho absoluto) |
| `Nap:Arquivos:LimiteBytes` | appsettings | Tamanho máximo do upload (padrão 5242880) |
| `Nap:Outbox:IntervaloSegundos` | appsettings | Ciclo do processador da outbox (padrão 5) |

No front-end, `frontend/.env` (opcional) define o alvo do proxy do Vite; veja `frontend/.env.example`.

---

## Banco de dados

SQL Server, banco `Nap` (desenvolvimento) e `NapE2E` (e2e).

Tabelas (além das do Identity): `Cursos`, `Alunos`, `Compromissos`, `EscalasProfissional`, `Feriados`, `AutorizacoesForaEscala`, `Atendimentos`, `AtividadesCurso`, `Encaminhamentos`, `Peis`, `AcessosAssinaturaPei`, `Mensagens`, `Documentos`, `Notificacoes`, `Outbox`, `FechamentosSemanais`, `AjustesProducao`, `ConfiguracoesAlertas`, `ChavesIdempotencia`, `EventosAuditoria` e `TokensRefresh`.

Índices: matrícula do aluno e nome do curso únicos · um atendimento e uma atividade por compromisso (únicos filtrados) · um fechamento por profissional e semana · feriado único entre os ativos · notificação única por evento e destinatário (idempotência da outbox) · outbox pendente por próxima tentativa · auditoria por data, usuário e ação.

Controle otimista: as entidades editáveis têm `rowversion`; o DTO devolve `versao` e as edições exigem `If-Match`. A ausência do cabeçalho retorna 428; uma versão divergente retorna 409 ("registro alterado por outra pessoa").

Datas: instantes em UTC (`DateTimeOffset`); datas civis no fuso fixo `America/Cuiaba`.

---

## Migrations

O `DbContext` e as migrations ficam na Infrastructure; a Api é o startup project. O `dotnet-ef` está fixado em `backend/dotnet-tools.json`.

```bash
dotnet tool restore --tool-manifest backend/dotnet-tools.json
# criar
dotnet ef migrations add <Nome> --project backend/src/Nap.Infrastructure --startup-project backend/src/Nap.Api
# aplicar (equivalente a rodar a API com --migrar)
dotnet ef database update --project backend/src/Nap.Infrastructure --startup-project backend/src/Nap.Api
# script idempotente para produção (revisar antes de aplicar)
dotnet ef migrations script --idempotent --project backend/src/Nap.Infrastructure --startup-project backend/src/Nap.Api -o migracao.sql
```

São 19 migrations, de `Inicial` a `IndiceCompromissosSemDesfecho`.

---

## Redis (cache)

A Application acessa o cache pela interface `ICacheConsultas`, sem depender do Redis. O Redis tem dois usos:

- Cache do painel: TTL de 5 min, invalidado por geração (`INCR nap:geracao:painel`) quando um comando altera o que o painel mostra.

- Validação de sessão: carimbo de segurança e sessão por 30 s, com fallback ao banco.

Se o Redis falhar, a API registra um aviso e consulta o banco. Se também não conseguir validar a sessão, nega o acesso. A indisponibilidade do Redis deixa `/health/ready` em `Degraded`, sem derrubar a API.

---

## Autenticação e autorização

Autenticação: ASP.NET Core Identity como store + JWT (HS256, 15 min) mantido só em memória no front. O refresh é opaco, de 7 dias e rotativo, no cookie `nap_refresh` (`HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/auth`); `refresh` e `logout` exigem o cabeçalho `X-Requested-With: nap-spa`. Reapresentar um refresh já usado revoga a família inteira, exceto nos 30 s seguintes ao uso (duas abas renovando com o mesmo cookie).

Revogação imediata: a cada requisição o carimbo de segurança e a sessão são conferidos. Mudar perfil, curso ou situação do usuário, ou trocar/redefinir a senha, revoga todas as sessões dele.

Senha: 10+ caracteres, com dígito e maiúscula · 5 falhas bloqueiam a conta por 15 min · login limitado a 10 tentativas por minuto por IP (429) · senha temporária com troca obrigatória no primeiro acesso (até trocar, só as rotas de senha, sessão e logout respondem).

Autorização em duas camadas: a política nomeada decide a ação (`PodeConsultarAlunos`, `PodeConsultarEncaminhamentos`, `PodeAdministrar`, `EquipeNap`, `PodeConsultarProducao` e seus aliases); o escopo e a posse são verificados no handler: fora do escopo retorna 404; ação proibida retorna 403. O front só esconde botões; a regra é imposta na API.

---

## API REST

Todas as rotas exigem JWT, exceto login/refresh, a assinatura do PEI pelo aluno (`/api/assinar`) e os health checks. Respostas usam DTOs; o contrato completo (106 operações) está em [`backend/openapi.json`](backend/openapi.json).

| Prefixo | Operações | Assunto |
|---------|-----------|---------|
| `/api/auth` | 5 | login, refresh, logout, usuário atual, troca de senha |
| `/api/alunos` | 11 | alunos, ficha, documentos e PEIs do aluno |
| `/api/agenda` | 8 | compromissos e reuniões da equipe |
| `/api/atendimentos` | 4 | registro e correção |
| `/api/atividades` | 4 | atividades nos cursos |
| `/api/encaminhamentos` | 5 | encaminhamentos e retornos |
| `/api/peis` | 14 | versões, revisão, assinaturas, publicação, ciência, PDF |
| `/api/assinar` | 2 | assinatura do aluno por link (sem login) |
| `/api/comunicacoes` | 12 | mensagens, rascunhos, arquivamento |
| `/api/documentos` | 6 | download, compartilhamento, revogação, arquivamento |
| `/api/notificacoes` | 4 | notificações internas |
| `/api/painel` | 1 | painel por perfil |
| `/api/producao` | 7 | produção, fechamento, ajustes, exportação PDF/Excel |
| `/api/usuarios` · `/api/cursos` · `/api/escalas` · `/api/feriados` · `/api/configuracoes` | 22 | administração |
| `/api/auditoria` | 1 | trilha de auditoria |
| `/health/live` · `/health/ready` | — | health checks (anônimo) |

Erros seguem `ProblemDetails` (RFC 9457), sem *stack trace*:

| Exceção | HTTP |
|---------|------|
| `ValidationException` (FluentValidation) | 400 + `errors` por campo |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 (auditado) |
| `NotFoundException` | 404 |
| `ConflictException` / `ConflitoAgendaException` | 409 (o segundo com a lista `conflitos`) |
| `DomainException` | 422 |
| `If-Match` ausente | 428 |
| Rate limit do login | 429 + `Retry-After` |
| demais | 500 (mensagem genérica, com `traceId`) |

### Regenerar o OpenAPI

`backend/openapi.json` é gerado no build de `Nap.Api` (o entry point sobe sem tocar SQL nem Redis). Versione o arquivo atualizado junto com a mudança de contrato e rode `npm run api:gerar` no `frontend/` para regenerar `src/lib/api-schema.d.ts`.

---

## Testes

```bash
dotnet test --solution backend/Nap.slnx        # back-end (Testcontainers: exige o Docker em execução)
cd frontend
npm run verificar                               # contrato em dia + tipos + Vitest + build
npm run e2e                                     # jornadas Playwright contra a API real
```

| Projeto | O que cobre |
|---------|-------------|
| Nap.Domain.Tests | Entidades, value objects e `CalculadoraProducao` |
| Nap.Application.Tests | Handlers por feature, com fakes e repositórios em memória |
| Nap.Infrastructure.Tests | Consultas, repositórios, concorrência da agenda, cache, outbox, PDF/Excel, sessão e seed, com SQL Server e Redis reais via Testcontainers |
| Nap.Api.Tests | Autorização por perfil, auth, troca de senha, If-Match, idempotência, erros, health, partida e arquitetura (`ApiFactory` com Testcontainers) |
| Front-end (Vitest) | Componentes, formulários e regras de tela com Testing Library |
| E2E (Playwright) | Jornadas completas por perfil |

Os testes de back-end sobem os próprios SQL Server e Redis; não dependem do banco de desenvolvimento.

### Jornadas end-to-end

```bash
npx playwright install chromium   # uma vez
npm run e2e
```

O e2e prepara sua própria configuração, sem usar os user-secrets. Exige SQL Server e Redis em execução no Docker, o `.env` da raiz com `NAP_SQL_SENHA` e a porta 7180 livre. Pare a API de desenvolvimento antes de executar os testes. A cada execução, o `e2e/global-setup.ts`:

- apaga e recria o banco `NapE2E` (via `sqlcmd` dentro do contêiner) e limpa o banco lógico 1 do Redis, separado do cache de desenvolvimento;

- gera senha de seed e chave JWT aleatórias e sobe a API com `--migrar --semear`, aguardando `/health/ready` (log em `%TEMP%/nap-api-e2e.log`);

- fixa `Nap:Outbox:IntervaloSegundos=1` e uma pasta temporária para os arquivos (apagada ao final);

- repassa a senha aos testes por `E2E_SENHA`.

As variáveis de ambiente do setup têm precedência sobre os user-secrets e o `appsettings.Development.json`. O Vite do e2e sobe na porta 5174; se estiver ocupada, use `PORTA_E2E=5175 npm run e2e` (PowerShell: `$env:PORTA_E2E="5175"; npm run e2e`).

| Spec | Jornada |
|------|---------|
| `jornada-nucleo` | cadastro de aluno → agendamento → realizado pela agenda → ficha e linha do tempo → conflito de horário → persistência após sair e entrar |
| `perfis` | visão de cada perfil e largura da barra inferior no celular |
| `sessao` | recarga e saída da sessão |
| `jornada-encaminhamento` | encaminhamento ao SIP, notificação, acolhimento, retorno e visão da coordenação de curso sem o motivo |
| `jornada-comunicacao-documento` | documento anexado e compartilhado, mensagem com resposta, PDF baixado, documento restrito invisível à coordenação |
| `jornada-pei` | PEI percorre revisão, assinaturas, publicação e ciência da coordenação de curso |
| `jornada-administracao` | novo profissional, troca da senha temporária, escala e feriado recusando agendamentos, inativação encerrando a sessão |
| `jornada-atividades` | atividade a partir do compromisso e avulsa, correção justificada, visão do coordenador de curso |
| `jornada-producao` | fechamento da semana, bloqueio de correção, reabertura, ajuste e recortes por perfil |
| `jornada-exportacao-auditoria` | exportação PDF e Excel, 403 auditado e consulta na aba Auditoria |
| `jornada-equipe-fora-da-escala` | reunião de equipe com cancelamento individual e atendimento autorizado fora da escala contando como tempo extra |

---

## Observabilidade

Logging com `ILogger` nativo (Information; ASP.NET Core e EF Core em Warning). Em produção, `Logging__Console__FormatterName=json` emite JSON. Erros 500 devolvem o `traceId` no ProblemDetails para cruzar com o log.

Health checks: `/health/live` (o processo respondeu, sem consultar dependências) · `/health/ready` (SQL acessível e sem migração pendente → 200, senão 503; Redis fora → 200 `Degraded`).

Auditoria: tabela `EventosAuditoria`, consultável em Administração → Auditoria. Registra exportações, consultas da ficha e da linha do tempo, ações sobre documentos, acessos negados e falhas de login. Os acessos negados são gravados em contexto próprio para sobreviver ao rollback.

---

## Segurança

- Senhas com hash do Identity; nunca trafegam de volta pela API.

- Access token só em memória; refresh em cookie `HttpOnly`/`Secure`/`SameSite=Strict`, rotativo e com detecção de reuso.

- Autorização imposta no servidor (política + escopo no handler); o cliente nunca informa o próprio escopo.

- Cabeçalhos `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` e `Content-Security-Policy: frame-ancestors 'none'`; HSTS fora de Development.

- Uploads com limite de tamanho, lista de tipos e checagem da assinatura do arquivo; gravados em disco com chave opaca, fora da raiz web.

- Rate limit no login e bloqueio de conta.

- Segredos fora do código: user-secrets em desenvolvimento, variáveis de ambiente em produção.

- Consultas parametrizadas pelo EF Core; nenhuma regra de negócio em trigger ou procedure.

---

## Deploy

A hospedagem ainda não está definida; o roteiro vale para Windows/IIS, Linux ou contêiner. A API serve o frontend na mesma origem: o cookie de refresh é `SameSite=Strict` com `Path=/api/auth`, então frontend e API em sites diferentes não funcionam.

### Dependências de build e execução

O front-end é compilado em arquivos estáticos e copiado para o `wwwroot` da API.

| | Máquina de build | Servidor |
|---|---|---|
| Node.js / npm | necessário | não |
| .NET 10 SDK | necessário | não |
| Runtime ASP.NET Core 10 (no IIS, o Hosting Bundle) | não | necessário |
| `node_modules` | fica local | nunca é copiado |

### 1. Gerar o pacote

```bash
cd frontend && npm ci && npm run build && cd ..
dotnet publish backend/src/Nap.Api -c Release -o publicar -p:OpenApiGenerateDocuments=false
cp -r frontend/dist publicar/wwwroot
```

Em Linux, a imagem precisa de `tzdata` e ICU: o fuso `America/Cuiaba` é resolvido na partida.

### 2. Topologia: um único site

O navegador acessa a `Nap.Api` por HTTPS, diretamente ou por um proxy reverso. A API se conecta ao SQL Server e ao Redis e atende às rotas abaixo:

| Rota | Destino |
|---|---|
| `GET /assets/*` | `wwwroot/assets/*`, com cache imutável de 1 ano |
| `/api/*` | Controllers |
| `/health/*` | Health checks |
| Demais rotas, inclusive `/assinar/...` | `wwwroot/index.html` (SPA) |

O proxy reverso, se houver, precisa aceitar corpos de pelo menos 6 MiB (uploads de 5 MiB) e encaminhar tudo para a API.

### 3. Configuração (variáveis de ambiente, nunca no `appsettings.json`)

| Variável | Valor |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Nap` | SQL Server da instituição |
| `ConnectionStrings__Redis` | Redis (obrigatório; a API não inicia sem a string. Se o Redis cair em execução, a API segue, mais lenta, e `/health/ready` fica `Degraded`) |
| `Jwt__Chave` | segredo aleatório de 32+ bytes (ex.: `openssl rand -base64 48`) |
| `Nap__Arquivos__Diretorio` | caminho absoluto num volume persistente, fora da pasta publicada; a partida falha se não der para gravar nele |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` se houver proxy reverso terminando o HTTPS (senão o limite de login por IP vira um contador único para todos) |
| `Logging__Console__FormatterName` | `json` (opcional), para cruzar o `traceId` dos erros 500 com o log |

### 4. Banco e primeiro administrador

```bash
dotnet Nap.Api.dll --migrar                                              # aplica as migrações e sobe a API (exige DDL)
dotnet Nap.Api.dll --criar-admin admin@instituicao.edu.br "Nome Completo"  # imprime senha temporária e encerra
```

Sem DDL na conta da API, gere o script idempotente (seção [Migrations](#migrations)) e aplique-o com um usuário administrativo. `--criar-admin` só funciona sem administrador ativo; no primeiro acesso a troca de senha é obrigatória. Cursos, usuários, escalas e feriados entram pela tela Administração. `--semear` é recusado fora de Development.

### Checklist de produção

- Usar HTTPS e `ASPNETCORE_ENVIRONMENT=Production`.
- Definir segredos por variáveis de ambiente, com duplo sublinhado nas chaves.
- Aplicar as migrations.
- Monitorar `/health/ready` e gerar alerta para `Degraded`.
- Fazer o backup do SQL Server e da pasta de arquivos juntos, pois os documentos apontam para chaves dessa pasta. Testar a restauração antes de inserir dados reais.
- Executar uma única instância da API, conforme a premissa da outbox e do rate limit.

O `docker-compose.yml` é só de desenvolvimento: a imagem do SQL Server sem `MSSQL_PID` roda a edição Developer, que não tem licença para produção.

---

## Decisões arquiteturais

| Tema | Decisão |
|------|---------|
| Casos de uso | Handlers explícitos, sem MediatR; Domain e Application livres de bibliotecas de infraestrutura. |
| Sessão | JWT de 15 min + refresh rotativo de 7 dias em cookie; carimbo e sessão validados a cada requisição, com Redis de 30 s e falha fechada. |
| Autorização | Política nomeada decide a ação; escopo e posse ficam no handler (fora do escopo → 404, proibido → 403). |
| Cache | Só o painel, TTL de 5 min e invalidação por geração; Redis caído degrada para o banco. |
| Datas e fuso | UTC em `DateTimeOffset`; datas civis em `America/Cuiaba`; escala e feriado validados no servidor (422). |
| Concorrência | `sp_getapplock` por profissional e aluno; `rowversion` + `If-Match` (428/409); `Idempotency-Key`; sem `EnableRetryOnFailure`. |
| Eventos | Outbox transacional com backoff (30 s × 2ⁿ, teto de 1 h, desiste após 8 falhas) e notificações idempotentes; instância única. |
| Encaminhamentos | SIP = Clínica Escola e vê o motivo restrito; coordenação de curso não vê dados clínicos; prazos de 7 e 15 dias. |
| Documentos | Acesso inicial restrito; compartilhamento com justificativa; arquivos em disco com chave opaca. |
| PEI | Rascunho → Revisão → Assinaturas → Publicada; assinatura demonstrativa; link do aluno de 72 h com código de 6 dígitos. |
| Administração | Senha temporária com troca obrigatória; escala com vigência; alertas editáveis; Coordenador NAP não gerencia administradores. |
| Produção | Realização registrada; fechamento semanal reaberto só pela coordenação; ajustes; capacidade pela escala; tempo extra fora da escala autorizado. |
| Exportações | Só a Produção em PDF/Excel; SixLabors.Fonts fixado em 1.0.1 (Apache-2.0) por licença. |
| Inicialização | Migração e seed só por flag; `Jwt:Chave` obrigatória com 32+ bytes. |
| Implantação | SPA servido pela própria API (mesma origem); primeiro administrador por `--criar-admin`. |

---

## Limitações conhecidas

- E-mail não configurado: `IEnvioEmail` só tem a implementação `EnvioEmailNaoConfigurado`; as notificações existem só dentro do sistema.

- Assinatura do PEI demonstrativa, não certificada (`AssinaturaDemonstrativa`, com aviso na tela e no PDF).

- As sugestões de horário do formulário de compromisso (07:00–17:00) são fixas; quem valida escala e feriado é o servidor.

- O e2e cobre só o Chromium.

- Pendências institucionais: assinatura certificada, provedor de e-mail, retenção, restrição clínica entre profissionais, visão do SIP sobre o motivo restrito.

---

## Troubleshooting

| Problema | O que fazer |
|---|---|
| Testcontainers não encontra o Docker no Windows | Defina `DOCKER_HOST=npipe:////./pipe/dockerDesktopLinuxEngine`. |
| E2E se recusa a rodar: porta 7180 ocupada | A API de desenvolvimento ainda está no ar. Encerre a árvore de processos (o `dotnet run` abre um filho): `netstat -ano \| findstr :7180` e `taskkill /PID <pid> /T /F` (Linux/macOS: `lsof -i :7180` e `kill`). |
| Porta 5174 ocupada no e2e | Use `PORTA_E2E=5175 npm run e2e`. |
| E2E falhou sem mensagem clara | Veja o log da API em `%TEMP%/nap-api-e2e.log`. |
| Falha de conexão com o SQL Server | Use `127.0.0.1` na connection string, não `localhost`. |
| Login não persiste / refresh falha em desenvolvimento | O cookie é `Secure`; confie no certificado com `dotnet dev-certs https --trust`. |
| `/health/ready` responde 503 | Há migração pendente ou o SQL está inacessível; rode a API com `--migrar`. |
| API não inicia por erro em `Jwt:Chave` | A chave é obrigatória e precisa de 32+ bytes. |
| Testes intermitentes conhecidos | `Grupos_simultaneos_com_participantes_em_comum_nao_reservam_duas_vezes` (timeout do `sp_getapplock` sob carga), `ComunicacoesTests.Contagem_cai_apos_marcar_lida` e `ProcessadorOutboxTests.Queda_entre_notificar_e_marcar_nao_duplica`. Rode de novo antes de investigar. |
| Todos os usuários atingem o limite de login juntos | Há proxy reverso sem `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, e todos parecem vir do mesmo IP. |
