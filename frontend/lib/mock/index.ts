import { ASSETS, ASSET_BY_SYMBOL } from './assets';
import { buildCalendar } from './calendar';
import { CURRENCIES, CURRENCY_SCORES } from './currency-scores';
import { computeAssetScore } from './engine';
import { FACTORS } from './factors';
import { buildHistory } from './history';
import { INDICATORS } from './indicators';
import { ENGINE_VERSION, PROFILES } from './profiles';
import type {
  AssetScore,
  CalendarEvent,
  CurrencyCode,
  HeatmapData,
  HeatmapRow,
  Indicator,
  Market,
  MarketSnapshot,
  ScoreHistoryPoint,
  TopSetupItem,
} from './types';

/**
 * A single reference instant for the whole render. Freezing it here keeps
 * "5 minutes ago" stable across every component in one page render instead of
 * drifting between them.
 */
const NOW = new Date();
const DATA_AS_OF = new Date(NOW.getTime() - 5 * 60_000).toISOString();
const CALCULATED_AT = new Date(NOW.getTime() - 4 * 60_000).toISOString();

const SCORES: AssetScore[] = ASSETS.map((asset) =>
  computeAssetScore(asset, { dataAsOf: DATA_AS_OF, calculatedAt: CALCULATED_AT })
);

const SCORE_BY_SYMBOL = new Map(SCORES.map((s) => [s.symbol, s]));

export function getReferenceTime(): Date {
  return NOW;
}

export function getAllScores(): AssetScore[] {
  return [...SCORES].sort((a, b) => b.score - a.score);
}

export function getAssetScore(symbol: string): AssetScore | undefined {
  return SCORE_BY_SYMBOL.get(symbol.toUpperCase());
}

export function getAssets() {
  return ASSETS;
}

export function getFactors() {
  return FACTORS;
}

export function getProfile(market: Market) {
  return PROFILES[market];
}

export function getEngineVersion() {
  return ENGINE_VERSION;
}

/**
 * Assets below the coverage floor are excluded from the ranking but never
 * hidden — they stay visible on Markets and on their own detail page.
 */
export function getTopSetups(limit = 8, market?: Market): TopSetupItem[] {
  return getAllScores()
    .filter((s) => s.isSufficient)
    .filter((s) => (market ? s.market === market : true))
    .slice(0, limit)
    .map((s, i) => ({
      rank: i + 1,
      symbol: s.symbol,
      name: s.name,
      market: s.market,
      score: s.score,
      bias: s.bias,
      coverage: s.coverage,
      isSufficient: s.isSufficient,
      dataAsOf: s.dataAsOf,
    }));
}

export function getRankedAssets(): TopSetupItem[] {
  return getAllScores().map((s, i) => ({
    rank: i + 1,
    symbol: s.symbol,
    name: s.name,
    market: s.market,
    score: s.score,
    bias: s.bias,
    coverage: s.coverage,
    isSufficient: s.isSufficient,
    dataAsOf: s.dataAsOf,
  }));
}

/**
 * Long format, derived from the scores — never hand-authored. The API will
 * return the same shape whether it is built from a join today or a
 * materialized view later, so this projection does not change.
 */
export function getHeatmap(limit?: number, market?: Market): HeatmapData {
  const rows: HeatmapRow[] = getAllScores()
    .filter((s) => (market ? s.market === market : true))
    .slice(0, limit ?? undefined)
    .map((s) => ({
      symbol: s.symbol,
      name: s.name,
      market: s.market,
      score: s.score,
      bias: s.bias,
      cells: FACTORS.map((f) => {
        const contribution = s.factors.find((c) => c.factorCode === f.code);
        return {
          factorCode: f.code,
          normalizedScore: contribution?.normalizedScore ?? null,
          contribution: contribution?.contribution ?? 0,
          rawLabel: contribution?.rawLabel ?? { mn: '—', en: '—' },
          weight: contribution?.weight ?? 0,
          available: contribution?.available ?? false,
          inProfile: contribution !== undefined,
        };
      }),
    }));

  return { factors: FACTORS, rows };
}

export function getScoreHistory(symbol: string, days = 30): ScoreHistoryPoint[] {
  const score = getAssetScore(symbol);
  if (!score) return [];
  return buildHistory(symbol, score.score, score.coverage, 90).slice(-days);
}

export function getCalendar(): CalendarEvent[] {
  return buildCalendar(NOW);
}

export function getRecentReleases(limit = 4): CalendarEvent[] {
  return getCalendar()
    .filter((e) => e.actual !== null)
    .sort((a, b) => b.releasedAt.localeCompare(a.releasedAt))
    .slice(0, limit);
}

export function getIndicators(): Indicator[] {
  return INDICATORS;
}

export function getIndicator(code: string): Indicator | undefined {
  return INDICATORS.find((i) => i.code === code);
}

/** Average currency factor score, used for the dashboard's market state strip. */
function currencyAverages(): { currency: CurrencyCode; avg: number }[] {
  return CURRENCIES.map((currency) => {
    const values: number[] = FACTORS.filter((f) => f.scope === 'currency')
      .map((f) => CURRENCY_SCORES[f.code]?.[currency]?.score ?? null)
      .filter((v): v is NonNullable<typeof v> => v !== null);
    const avg = values.length ? values.reduce((a, v) => a + v, 0) / values.length : 0;
    return { currency, avg: Math.round(avg * 100) / 100 };
  }).sort((a, b) => b.avg - a.avg || a.currency.localeCompare(b.currency));
}

export function getMarketSnapshot(): MarketSnapshot {
  const averages = currencyAverages();
  const strongest = averages[0];
  const weakest = averages[averages.length - 1];
  const scores = getAllScores();
  const nasdaq = SCORE_BY_SYMBOL.get('NASDAQ');

  return {
    strongestCurrency: strongest.currency,
    strongestAvg: strongest.avg,
    weakestCurrency: weakest.currency,
    weakestAvg: weakest.avg,
    riskRegime: (nasdaq?.score ?? 50) >= 55 ? 'on' : 'off',
    avgCoverage:
      Math.round((scores.reduce((a, s) => a + s.coverage, 0) / scores.length) * 1000) / 1000,
    assetCount: scores.length,
    dataAsOf: DATA_AS_OF,
  };
}

export { ASSET_BY_SYMBOL, FACTORS, ASSETS };
export * from './types';
