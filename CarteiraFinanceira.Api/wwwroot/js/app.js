const state = {
    periodo: 30,
    ativoAtual: null,
    historicoAtual: [],
    watchlist: [],
    errosLote: [],
    sugestoes: [],
    statusApi: null
};

const storageKeys = {
    watchlist: "carteira-financeira-watchlist",
    periodo: "carteira-financeira-periodo"
};

const fallbackSuggestions = [
    { codigo: "AAPL", nome: "Apple", categoria: "Acoes internacionais" },
    { codigo: "MSFT", nome: "Microsoft", categoria: "Tecnologia" },
    { codigo: "PETR4", nome: "Petrobras", categoria: "Bolsa brasileira" },
    { codigo: "VALE3", nome: "Vale", categoria: "Bolsa brasileira" },
    { codigo: "BTC", nome: "Bitcoin", categoria: "Criptomoedas" },
    { codigo: "ETH", nome: "Ethereum", categoria: "Criptomoedas" }
];

const refs = {
    form: document.getElementById("search-form"),
    input: document.getElementById("asset-input"),
    button: document.getElementById("search-button"),
    quickChips: document.getElementById("quick-chips"),
    loadingState: document.getElementById("loading-state"),
    loadingText: document.getElementById("loading-text"),
    errorState: document.getElementById("error-state"),
    watchlist: document.getElementById("watchlist"),
    batchErrors: document.getElementById("batch-errors"),
    periodSwitch: document.getElementById("period-switch"),
    chart: document.getElementById("price-chart"),
    sourceProvider: document.getElementById("source-provider"),
    overviewSymbol: document.getElementById("overview-symbol"),
    overviewName: document.getElementById("overview-name"),
    overviewCount: document.getElementById("overview-count"),
    overviewPeriod: document.getElementById("overview-period"),
    overviewUpdated: document.getElementById("overview-updated"),
    assetName: document.getElementById("asset-name"),
    assetCode: document.getElementById("asset-code"),
    assetType: document.getElementById("asset-type"),
    assetMarket: document.getElementById("asset-market"),
    assetPrice: document.getElementById("asset-price"),
    assetVariation: document.getElementById("asset-variation"),
    assetCurrency: document.getElementById("asset-currency"),
    assetOpen: document.getElementById("asset-open"),
    assetHigh: document.getElementById("asset-high"),
    assetLow: document.getElementById("asset-low"),
    assetPreviousClose: document.getElementById("asset-previous-close"),
    assetVolume: document.getElementById("asset-volume"),
    assetStatus: document.getElementById("asset-status"),
    assetSource: document.getElementById("asset-source"),
    chartMax: document.getElementById("chart-max"),
    chartMin: document.getElementById("chart-min"),
    chartPeriodVariation: document.getElementById("chart-period-variation"),
    chartStartDate: document.getElementById("chart-start-date"),
    chartMidDate: document.getElementById("chart-mid-date"),
    chartEndDate: document.getElementById("chart-end-date")
};

document.addEventListener("DOMContentLoaded", async () => {
    hydrateState();
    bindEvents();
    renderQuickChips(state.sugestoes);
    renderWatchlist();
    updatePeriodButtons();
    renderOverview();

    await loadApiStatus();
    await loadSuggestions();
    renderChart();
});

function bindEvents() {
    refs.form.addEventListener("submit", handleSearchSubmit);
    refs.quickChips.addEventListener("click", handleQuickChipClick);
    refs.watchlist.addEventListener("click", handleWatchlistClick);
    refs.periodSwitch.addEventListener("click", handlePeriodClick);
}

async function handleSearchSubmit(event) {
    event.preventDefault();

    const symbols = parseSymbols(refs.input.value);

    if (symbols.length === 0) {
        showError("Digite ao menos um codigo de ativo para consultar.");
        return;
    }

    state.errosLote = [];
    renderBatchErrors();

    if (symbols.length === 1) {
        await loadAssetDetail(symbols[0]);
        return;
    }

    await loadMultipleAssets(symbols);
}

async function handleQuickChipClick(event) {
    const target = event.target.closest("[data-symbol]");

    if (!target) {
        return;
    }

    refs.input.value = target.dataset.symbol;
    await loadAssetDetail(target.dataset.symbol);
}

async function handleWatchlistClick(event) {
    const target = event.target.closest("[data-symbol]");

    if (!target) {
        return;
    }

    await loadAssetDetail(target.dataset.symbol);
}

async function handlePeriodClick(event) {
    const target = event.target.closest("[data-period]");

    if (!target) {
        return;
    }

    const nextPeriod = Number.parseInt(target.dataset.period, 10);

    if (!Number.isFinite(nextPeriod) || nextPeriod === state.periodo) {
        return;
    }

    state.periodo = nextPeriod;
    persistState();
    updatePeriodButtons();
    renderOverview();

    if (state.ativoAtual?.codigo) {
        await loadAssetDetail(state.ativoAtual.codigo);
    }
}

async function loadApiStatus() {
    try {
        const data = await fetchJson("/api/inicio/status");
        state.statusApi = data;
        refs.sourceProvider.textContent = data.provedor ?? "Alpha Vantage + brapi";
    } catch {
        refs.sourceProvider.textContent = "Alpha Vantage + brapi";
    }
}

async function loadSuggestions() {
    try {
        const data = await fetchJson("/api/ativos/sugestoes");
        state.sugestoes = Array.isArray(data) && data.length > 0 ? data : fallbackSuggestions;
    } catch {
        state.sugestoes = fallbackSuggestions;
    }

    renderQuickChips(state.sugestoes);
}

async function loadMultipleAssets(symbols) {
    clearError();
    setLoading(true, "Montando sua watchlist...");

    try {
        const data = await fetchJson(`/api/ativos/multiplas?ativos=${encodeURIComponent(symbols.join(","))}`);

        state.errosLote = Array.isArray(data.erros) ? data.erros : [];

        if (Array.isArray(data.ativos) && data.ativos.length > 0) {
            state.watchlist = mergeWatchlist(state.watchlist, data.ativos);
            persistState();
            renderWatchlist();
            renderBatchErrors();
            await loadAssetDetail(data.ativos[0].codigo);
            return;
        }

        renderBatchErrors();
        showError(state.errosLote[0]?.mensagem ?? "Nenhum ativo foi encontrado para a consulta em lote.");
    } catch (error) {
        showError(error.message);
    } finally {
        setLoading(false);
    }
}

async function loadAssetDetail(symbol) {
    const cleanSymbol = sanitizeSymbol(symbol);

    if (!cleanSymbol) {
        showError("Digite um codigo de ativo valido.");
        return;
    }

    clearError();
    setLoading(true, `Carregando ${cleanSymbol}...`);

    try {
        const data = await fetchJson(`/api/ativos/consultar?ativo=${encodeURIComponent(cleanSymbol)}&periodo=${state.periodo}`);

        state.ativoAtual = data.ativo;
        state.historicoAtual = Array.isArray(data.historico) ? data.historico : [];
        state.watchlist = upsertWatchlist(state.watchlist, data.ativo);

        persistState();
        refs.input.value = cleanSymbol;

        renderDashboard();
    } catch (error) {
        showError(error.message);
    } finally {
        setLoading(false);
    }
}

function renderDashboard() {
    renderOverview();
    renderAssetCard();
    renderWatchlist();
    renderBatchErrors();
    renderChart();
}

function renderOverview() {
    refs.overviewCount.textContent = String(state.watchlist.length);
    refs.overviewPeriod.textContent = `${state.periodo} dias`;

    if (!state.ativoAtual) {
        refs.overviewSymbol.textContent = "--";
        refs.overviewName.textContent = "Aguardando consulta";
        refs.overviewUpdated.textContent = "--";
        return;
    }

    refs.overviewSymbol.textContent = state.ativoAtual.codigo;
    refs.overviewName.textContent = state.ativoAtual.nomeAtivo;
    refs.overviewUpdated.textContent = formatDateTime(state.ativoAtual.ultimaAtualizacaoUtc);
}

function renderAssetCard() {
    if (!state.ativoAtual) {
        return;
    }

    const asset = state.ativoAtual;

    refs.assetName.textContent = asset.nomeAtivo;
    refs.assetCode.textContent = asset.codigo;
    refs.assetType.textContent = asset.tipoAtivo;
    refs.assetMarket.textContent = asset.mercado;
    refs.assetPrice.textContent = formatCurrency(asset.precoAtual, asset.moeda);
    refs.assetVariation.textContent = formatVariation(asset.variacaoPercentual, asset.variacaoAbsoluta, asset.moeda);
    refs.assetVariation.className = `variation-badge ${getVariationClass(asset.variacaoPercentual)}`;
    refs.assetCurrency.textContent = asset.moeda;
    refs.assetOpen.textContent = formatCurrency(asset.abertura, asset.moeda);
    refs.assetHigh.textContent = formatCurrency(asset.maximaDia, asset.moeda);
    refs.assetLow.textContent = formatCurrency(asset.minimaDia, asset.moeda);
    refs.assetPreviousClose.textContent = formatCurrency(asset.fechamentoAnterior, asset.moeda);
    refs.assetVolume.textContent = formatCompactNumber(asset.volume);
    refs.assetStatus.textContent = asset.statusMercado;
    refs.assetSource.textContent = asset.origemConsulta;

    document.title = `${asset.codigo} | Carteira Financeira Dashboard`;
}

function renderChart() {
    const points = state.historicoAtual;
    const asset = state.ativoAtual;

    if (!asset || points.length === 0) {
        refs.chart.innerHTML = buildEmptyChartMarkup();
        refs.chartMax.textContent = "--";
        refs.chartMin.textContent = "--";
        refs.chartPeriodVariation.textContent = "--";
        refs.chartStartDate.textContent = "--";
        refs.chartMidDate.textContent = "--";
        refs.chartEndDate.textContent = "--";
        return;
    }

    const prices = points.map((point) => Number(point.fechamento));
    const max = Math.max(...prices);
    const min = Math.min(...prices);
    const first = prices[0];
    const last = prices[prices.length - 1];
    const change = first !== 0 ? ((last - first) / first) * 100 : 0;

    refs.chartMax.textContent = formatCurrency(max, asset.moeda);
    refs.chartMin.textContent = formatCurrency(min, asset.moeda);
    refs.chartPeriodVariation.textContent = formatPercent(change);
    refs.chartPeriodVariation.className = getVariationClass(change) === "positive"
        ? "positive-text"
        : getVariationClass(change) === "negative"
            ? "negative-text"
            : "";

    refs.chartStartDate.textContent = formatShortDate(points[0].data);
    refs.chartMidDate.textContent = formatShortDate(points[Math.floor(points.length / 2)].data);
    refs.chartEndDate.textContent = formatShortDate(points[points.length - 1].data);

    refs.chart.innerHTML = buildChartSvg(points, asset.moeda);
}

function renderQuickChips(suggestions) {
    const entries = suggestions.length > 0 ? suggestions : fallbackSuggestions;

    refs.quickChips.innerHTML = entries
        .map((item) => `
            <button type="button" class="quick-chip" data-symbol="${item.codigo}">
                ${item.codigo} <span class="chip-meta">${item.nome}</span>
            </button>
        `)
        .join("");
}

function renderWatchlist() {
    if (state.watchlist.length === 0) {
        refs.watchlist.innerHTML = '<div class="empty-state">Suas consultas recentes vao aparecer aqui.</div>';
        return;
    }

    refs.watchlist.innerHTML = state.watchlist
        .map((item) => {
            const isActive = item.codigo === state.ativoAtual?.codigo;

            return `
                <button type="button" class="watch-item ${isActive ? "is-active" : ""}" data-symbol="${item.codigo}">
                    <div class="watch-item-header">
                        <div>
                            <strong>${item.codigo}</strong>
                            <span>${item.nomeAtivo}</span>
                        </div>
                        <span class="variation-badge ${getVariationClass(item.variacaoPercentual)}">
                            ${formatPercent(item.variacaoPercentual)}
                        </span>
                    </div>
                    <div class="watch-item-price">${formatCurrency(item.precoAtual, item.moeda)}</div>
                    <div class="watch-item-footer">
                        <span>${item.tipoAtivo}</span>
                        <span>${item.mercado}</span>
                    </div>
                </button>
            `;
        })
        .join("");
}

function renderBatchErrors() {
    if (state.errosLote.length === 0) {
        refs.batchErrors.innerHTML = "";
        return;
    }

    refs.batchErrors.innerHTML = state.errosLote
        .map((item) => `
            <div class="batch-error">
                <strong>${item.codigo}</strong>
                <div>${item.mensagem}</div>
            </div>
        `)
        .join("");
}

function setLoading(isLoading, message = "Carregando dados de mercado...") {
    refs.loadingText.textContent = message;
    refs.loadingState.classList.toggle("hidden", !isLoading);
    refs.button.disabled = isLoading;
    refs.button.textContent = isLoading ? "Consultando..." : "Buscar agora";
}

function showError(message) {
    refs.errorState.textContent = message;
    refs.errorState.classList.remove("hidden");
}

function clearError() {
    refs.errorState.textContent = "";
    refs.errorState.classList.add("hidden");
}

function updatePeriodButtons() {
    refs.periodSwitch.querySelectorAll("[data-period]").forEach((button) => {
        const period = Number.parseInt(button.dataset.period, 10);
        button.classList.toggle("is-active", period === state.periodo);
    });
}

function hydrateState() {
    const savedWatchlist = safeJsonParse(localStorage.getItem(storageKeys.watchlist));
    const savedPeriod = Number.parseInt(localStorage.getItem(storageKeys.periodo) ?? "30", 10);

    state.watchlist = Array.isArray(savedWatchlist) ? savedWatchlist.slice(0, 8) : [];
    state.periodo = Number.isFinite(savedPeriod) ? savedPeriod : 30;
    state.sugestoes = fallbackSuggestions;
}

function persistState() {
    localStorage.setItem(storageKeys.watchlist, JSON.stringify(state.watchlist.slice(0, 8)));
    localStorage.setItem(storageKeys.periodo, String(state.periodo));
}

function upsertWatchlist(current, nextAsset) {
    const filtered = current.filter((item) => item.codigo !== nextAsset.codigo);
    return [nextAsset, ...filtered].slice(0, 8);
}

function mergeWatchlist(current, incoming) {
    let merged = [...current];

    for (const asset of incoming) {
        merged = upsertWatchlist(merged, asset);
    }

    return merged.slice(0, 8);
}

function parseSymbols(value) {
    return value
        .split(",")
        .map((item) => sanitizeSymbol(item))
        .filter(Boolean)
        .filter((item, index, array) => array.indexOf(item) === index);
}

function sanitizeSymbol(value) {
    return value?.trim().toUpperCase() ?? "";
}

async function fetchJson(url) {
    const response = await fetch(url, {
        headers: {
            Accept: "application/json"
        }
    });

    const contentType = response.headers.get("content-type") ?? "";
    const payload = contentType.includes("application/json")
        ? await response.json()
        : null;

    if (!response.ok) {
        throw new Error(payload?.mensagem ?? "Nao foi possivel concluir a consulta.");
    }

    return payload;
}

function buildChartSvg(points, currency) {
    const width = 860;
    const height = 360;
    const padding = { top: 24, right: 20, bottom: 44, left: 20 };
    const plotWidth = width - padding.left - padding.right;
    const plotHeight = height - padding.top - padding.bottom;
    const prices = points.map((point) => Number(point.fechamento));
    const min = Math.min(...prices);
    const max = Math.max(...prices);
    const safeRange = max - min || 1;

    const coordinates = points.map((point, index) => {
        const x = padding.left + (plotWidth / Math.max(points.length - 1, 1)) * index;
        const y = padding.top + plotHeight - ((Number(point.fechamento) - min) / safeRange) * plotHeight;

        return { x, y, value: Number(point.fechamento), date: point.data };
    });

    const linePath = coordinates
        .map((point, index) => `${index === 0 ? "M" : "L"} ${point.x.toFixed(2)} ${point.y.toFixed(2)}`)
        .join(" ");

    const areaPath = `${linePath} L ${coordinates[coordinates.length - 1].x.toFixed(2)} ${(height - padding.bottom).toFixed(2)} L ${coordinates[0].x.toFixed(2)} ${(height - padding.bottom).toFixed(2)} Z`;

    const gridLines = Array.from({ length: 5 }, (_, index) => {
        const y = padding.top + (plotHeight / 4) * index;
        return `<line class="chart-grid-line" x1="${padding.left}" y1="${y}" x2="${width - padding.right}" y2="${y}" />`;
    }).join("");

    const markerStep = Math.max(1, Math.floor(coordinates.length / 5));
    const dots = coordinates
        .filter((_, index) => index % markerStep === 0)
        .map((point) => `<circle class="chart-dot" cx="${point.x.toFixed(2)}" cy="${point.y.toFixed(2)}" r="5" />`)
        .join("");

    const currentPoint = coordinates[coordinates.length - 1];
    const tagX = Math.max(padding.left + 60, currentPoint.x - 70);
    const tagY = Math.max(padding.top + 20, currentPoint.y - 34);
    const currentLabel = escapeHtml(formatCurrency(currentPoint.value, currency));

    return `
        <defs>
            <linearGradient id="chartGradient" x1="0%" y1="0%" x2="0%" y2="100%">
                <stop offset="0%" stop-color="rgba(13, 148, 136, 0.28)" />
                <stop offset="100%" stop-color="rgba(13, 148, 136, 0.02)" />
            </linearGradient>
        </defs>
        ${gridLines}
        <path class="chart-area" d="${areaPath}" />
        <path class="chart-line" d="${linePath}" />
        ${dots}
        <circle class="chart-current-dot" cx="${currentPoint.x.toFixed(2)}" cy="${currentPoint.y.toFixed(2)}" r="7" />
        <rect class="chart-tag" x="${tagX.toFixed(2)}" y="${tagY.toFixed(2)}" width="124" height="34" />
        <text class="chart-tag-text" x="${(tagX + 18).toFixed(2)}" y="${(tagY + 22).toFixed(2)}">${currentLabel}</text>
    `;
}

function buildEmptyChartMarkup() {
    return `
        <rect x="20" y="24" width="820" height="292" rx="24" ry="24" fill="rgba(255,255,255,0.65)" stroke="rgba(19,48,71,0.08)" />
        <text x="430" y="170" text-anchor="middle" fill="#5d7488" font-size="18" font-family="Manrope, sans-serif">
            Fa&ccedil;a uma busca para visualizar o hist&oacute;rico de pre&ccedil;os.
        </text>
    `;
}

function formatCurrency(value, currency) {
    const numericValue = Number(value ?? 0);
    const safeCurrency = currency || "USD";

    try {
        return new Intl.NumberFormat("pt-BR", {
            style: "currency",
            currency: safeCurrency,
            maximumFractionDigits: 2
        }).format(numericValue);
    } catch {
        return `${safeCurrency} ${numericValue.toFixed(2)}`;
    }
}

function formatPercent(value) {
    const numericValue = Number(value ?? 0);
    const signal = numericValue > 0 ? "+" : "";
    return `${signal}${numericValue.toFixed(2)}%`;
}

function formatVariation(percentual, absoluto, currency) {
    const percentText = formatPercent(percentual);
    const absoluteText = formatCurrency(absoluto, currency);

    return `${percentText} • ${absoluteText}`;
}

function formatCompactNumber(value) {
    return new Intl.NumberFormat("pt-BR", {
        notation: "compact",
        maximumFractionDigits: 1
    }).format(Number(value ?? 0));
}

function formatDateTime(value) {
    if (!value) {
        return "--";
    }

    return new Intl.DateTimeFormat("pt-BR", {
        dateStyle: "short",
        timeStyle: "short"
    }).format(new Date(value));
}

function formatShortDate(value) {
    if (!value) {
        return "--";
    }

    return new Intl.DateTimeFormat("pt-BR", {
        day: "2-digit",
        month: "short"
    }).format(new Date(value));
}

function getVariationClass(value) {
    const numericValue = Number(value ?? 0);

    if (numericValue > 0) {
        return "positive";
    }

    if (numericValue < 0) {
        return "negative";
    }

    return "neutral";
}

function safeJsonParse(value) {
    try {
        return value ? JSON.parse(value) : null;
    } catch {
        return null;
    }
}

function escapeHtml(value) {
    return value
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;");
}
