# CapitalSync

> Dashboard financeiro full stack para pesquisar, consultar e comparar ativos brasileiros, internacionais e criptomoedas.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
![CI](https://img.shields.io/github/actions/workflow/status/Vinicius-Calegari/CapitalSync/dotnet-ci.yml?label=build)
[![Produção](https://img.shields.io/badge/produção-Railway-7B2BF9)](https://capitalsync-production.up.railway.app)

**Demo:** https://capitalsync-production.up.railway.app

## O problema

Dados de mercado vêm de provedores com formatos, símbolos, limites e disponibilidade diferentes. O CapitalSync centraliza essa complexidade atrás de uma API própria e entrega uma interface única para pesquisa e comparação.

## Arquitetura

```text
Browser
  │
  ▼
ASP.NET Core API
  ├── Controllers
  │      │
  │      ▼
  ├── Services ──────► IMemoryCache
  │      │
  │      ├───────────► brapi (B3)
  │      └───────────► Alpha Vantage (mercado global / fallback)
  │
  └── wwwroot (frontend)
```

O backend concentra resolução de símbolos, chamadas HTTP, normalização dos retornos e cache. O frontend não precisa conhecer detalhes de cada provedor.

## Decisões técnicas

- **ASP.NET Core + DI:** separa controllers da integração externa.
- **HttpClient gerenciado:** evita criação manual de clientes por requisição.
- **IMemoryCache:** reduz chamadas repetidas e exposição aos limites das APIs externas.
- **Fallback de provedores:** permite tratar classes de ativos e indisponibilidade de maneira diferente.
- **Configuração externa:** chaves não ficam hardcoded no código.
- **CI:** todo push/PR restaura e compila a solução.

## Endpoints

| Método | Endpoint | Objetivo |
|---|---|---|
| GET | `/api/inicio/status` | status da aplicação |
| GET | `/api/ativos/sugestoes` | sugestões de símbolos |
| GET | `/api/ativos/consultar?ativo=WEGE3&periodo=30` | consulta individual |
| GET | `/api/ativos/consultar?ativo=AAPL&periodo=30` | ativo internacional |
| GET | `/api/ativos/consultar?ativo=BTC&periodo=90` | criptomoeda |
| GET | `/api/ativos/multiplas?ativos=AAPL,BTC,PETR4` | comparação |

## Executar localmente

Requisitos: SDK .NET compatível com o projeto.

```bash
git clone https://github.com/Vinicius-Calegari/CapitalSync.git
cd CapitalSync/CarteiraFinanceira.Api
dotnet restore
dotnet user-secrets init
dotnet user-secrets set "AlphaVantage:ApiKey" "SUA_CHAVE"
dotnet run
```

Em containers/produção use `AlphaVantage__ApiKey`.

## Segurança

Credenciais e arquivos locais estão fora do fluxo normal de versionamento. Se uma chave já tiver aparecido em histórico Git, a medida correta é **revogá-la no provedor**, não apenas removê-la do arquivo atual.

Nunca envie uma chave real em issue, commit ou pull request.

## Estrutura

```text
CarteiraFinanceira.Api/
├── Controllers/       # superfície HTTP
├── Models/            # contratos e respostas externas
├── Services/          # integrações e regras de consulta
├── wwwroot/           # interface web
├── Program.cs         # composição da aplicação
└── *.csproj
```

## Engenharia / qualidade

O repositório possui GitHub Actions e Dependabot. O objetivo é que o projeto seja verificável por build automatizado, não apenas por uma demo funcionando no navegador.

### Limitações conhecidas

- disponibilidade e limites dependem dos provedores externos;
- cache é local à instância da aplicação;
- ainda falta uma suíte automatizada de testes de integração;
- dados de mercado não devem ser tratados como recomendação financeira.

## Roadmap

- [ ] testes unitários e de integração
- [ ] rate limiting para endpoints públicos
- [ ] Problem Details / tratamento global de exceções
- [ ] health checks de aplicação e provedores
- [ ] métricas e observabilidade
- [ ] cache distribuído caso a aplicação passe a escalar horizontalmente

---

Desenvolvido por [Vinícius Calegari](https://github.com/Vinicius-Calegari) — Full Stack Development + Engenharia de Controle e Automação.
