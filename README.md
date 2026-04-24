# Carteira Financeira Dashboard

Projeto full stack em C# com ASP.NET Core Web API no back-end e dashboard responsivo em HTML, CSS e JavaScript no front-end. A aplicacao consulta dados financeiros reais, exibe resumo do ativo, historico de precos e suporta consultas multiplas em uma interface com visual de produto real.

## Tecnologias

- C#
- .NET 10
- ASP.NET Core Web API
- HttpClient
- System.Text.Json
- IMemoryCache
- HTML, CSS e JavaScript puro

## APIs externas escolhidas

O projeto usa duas fontes para deixar a busca mais confiavel:

- [Alpha Vantage](https://www.alphavantage.co/documentation/) para acoes internacionais e criptomoedas
- [brapi](https://brapi.dev/docs/acoes) para ativos brasileiros de demonstracao como `PETR4`, `VALE3`, `MGLU3` e `ITUB4`

### Chave da API

O arquivo `CarteiraFinanceira.Api/appsettings.json` ja esta configurado com uma chave da Alpha Vantage.

Se quiser trocar por uma chave propria:

1. Acesse [https://www.alphavantage.co/support/#api-key](https://www.alphavantage.co/support/#api-key)
2. Gere uma chave gratuita
3. Atualize o arquivo `CarteiraFinanceira.Api/appsettings.json`:

```json
"AlphaVantage": {
  "ApiKey": "SUA_CHAVE_AQUI"
}
```

## O que o projeto entrega

- Dashboard profissional com cards, secoes, loading e mensagens de erro
- Campo de busca para um ativo ou varios separados por virgula
- Consulta de acoes e criptomoedas
- Resumo completo com preco atual, variacao, abertura, maxima, minima, fechamento anterior e volume
- Grafico de historico de precos no proprio painel
- Watchlist de consultas recentes
- API organizada em `Models`, `Services` e `Controllers`

## Estrutura do projeto

```text
voc-um-desenvolvedor-s-nior-especialista/
|-- README.md
|-- CarteiraFinanceira.Api/
|   |-- Controllers/
|   |   |-- AtivosController.cs
|   |   |-- InicioController.cs
|   |-- Models/
|   |   |-- Api/
|   |   |   |-- AlphaVantageQuoteResponse.cs
|   |   |   |-- AlphaVantageDailyResponse.cs
|   |   |   |-- AlphaVantageDigitalCurrencyResponse.cs
|   |   |   |-- BrapiQuoteResponse.cs
|   |   |-- ApiErrorResponse.cs
|   |   |-- AtivoFinanceiroResponse.cs
|   |   |-- ConsultaAtivoDetalhadaResponse.cs
|   |   |-- ConsultaLoteResponse.cs
|   |   |-- SugestaoAtivoResponse.cs
|   |-- Services/
|   |   |-- IConsultaAtivoService.cs
|   |   |-- MercadoFinanceiroService.cs
|   |-- wwwroot/
|   |   |-- css/
|   |   |   |-- styles.css
|   |   |-- js/
|   |   |   |-- app.js
|   |   |-- index.html
|   |-- Program.cs
|   |-- appsettings.json
|   |-- appsettings.Development.json
|   |-- CarteiraFinanceira.Api.http
|   |-- CarteiraFinanceira.Api.csproj
```

## Como rodar

1. Entre na pasta do projeto:

```powershell
cd .\CarteiraFinanceira.Api
```

2. Restaure as dependencias:

```powershell
dotnet restore --configfile .\NuGet.Config
```

3. Execute a aplicacao:

```powershell
dotnet run
```

4. Abra no navegador:

```text
http://localhost:5110/
```

## Endpoints da API

- `GET /api/inicio/status`
- `GET /api/ativos/sugestoes`
- `GET /api/ativos/consultar?ativo=AAPL&periodo=30`
- `GET /api/ativos/consultar?ativo=BTC&periodo=90`
- `GET /api/ativos/multiplas?ativos=AAPL,BTC,PETR4`

## Como funciona

### Back-end

- O controller recebe o codigo do ativo informado pelo usuario.
- O service decide qual provedor usar com base no tipo do ativo.
- Acoes brasileiras gratuitas usam brapi, enquanto acoes internacionais e cripto usam Alpha Vantage.
- Depois consulta o endpoint de historico para montar os pontos do grafico.
- A API devolve um objeto organizado para o dashboard renderizar sem logica complexa no front-end.

### Front-end

- A tela inicial abre pronta para busca, sem consumir chamadas automaticas da API.
- O formulario aceita um ativo unico ou varios ativos separados por virgula.
- Consultas multiplas montam uma watchlist no painel lateral.
- O grafico usa SVG dinamico para desenhar a curva de precos sem depender de bibliotecas externas.
- O estado de loading, os erros e o periodo do grafico sao atualizados em tempo real.

## Diferenciais para portfolio

- Visual de dashboard com acabamento profissional
- Integracao real entre front-end e back-end
- Estrutura pronta para crescimento
- Codigo limpo com separacao de responsabilidades
- Fluxo completo de produto: busca, analise, comparacao e historico
