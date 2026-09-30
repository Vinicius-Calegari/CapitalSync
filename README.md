# CapitalSync

Dashboard financeiro full stack para consulta de ações e criptomoedas, com histórico de preços, comparação de ativos e integração com provedores externos.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-Web_API-512BD4)
![CI](https://img.shields.io/github/actions/workflow/status/Vinicius-Calegari/CapitalSync/dotnet-ci.yml?label=build)

## Arquitetura

O backend ASP.NET Core concentra regras de consulta, cache e integração com provedores. O frontend é servido pela própria aplicação e consome a API REST. `HttpClient` é injetado via DI e `IMemoryCache` reduz chamadas repetidas.

## Stack

- C# / .NET 10
- ASP.NET Core Web API
- HttpClient, System.Text.Json e IMemoryCache
- HTML, CSS e JavaScript
- Alpha Vantage e brapi

## Segurança

Credenciais não devem ser versionadas. Configure a chave da Alpha Vantage localmente com User Secrets ou variável de ambiente:

```bash
cd CarteiraFinanceira.Api
dotnet user-secrets init
dotnet user-secrets set "AlphaVantage:ApiKey" "SUA_CHAVE"
```

Em servidores/containers, use `AlphaVantage__ApiKey`. O arquivo `appsettings.Local.json` é ignorado pelo Git e `appsettings.Local.example.json` serve apenas como referência.

> Se uma chave já tiver sido publicada no histórico do Git, ela deve ser revogada no provedor. Remover do commit atual não invalida uma credencial já exposta.

## Executar

```bash
cd CarteiraFinanceira.Api
dotnet restore
dotnet run
```

Por padrão, consulte a URL exibida pelo `dotnet run`.

## Endpoints

```text
GET /api/inicio/status
GET /api/ativos/sugestoes
GET /api/ativos/consultar?ativo=AAPL&periodo=30
GET /api/ativos/consultar?ativo=BTC&periodo=90
GET /api/ativos/multiplas?ativos=AAPL,BTC,PETR4
```

## Qualidade

O repositório possui GitHub Actions para restaurar dependências e compilar a aplicação em cada push/PR, além de Dependabot para acompanhar NuGet e Actions.

## Estrutura

```text
CarteiraFinanceira.Api/
├── Controllers/
├── Models/
├── Services/
├── wwwroot/
├── Program.cs
└── CarteiraFinanceira.Api.csproj
```

## Próximos passos técnicos

- testes unitários e de integração
- rate limiting na API pública
- tratamento global de exceções com Problem Details
- health checks
- containerização e deploy reproduzível

---

Desenvolvido por [Vinícius Calegari](https://github.com/Vinicius-Calegari).
