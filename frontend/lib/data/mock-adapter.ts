import {
  getAssetScore,
  getCalendar,
  getHeatmap,
  getIndicators,
  getMarketSnapshot,
  getRankedAssets,
  getScoreHistory,
  getTopSetups,
  getFactors,
} from '@/lib/mock';
import { INDICATOR_BY_FACTOR } from '@/lib/mock/indicators';
import { t as tx } from '@/lib/localized';
import type { Locale, Market } from '@/lib/mock/types';
import type {
  AssetDetailView,
  CalendarEventView,
  HeatmapView,
  IndicatorView,
  MarketSnapshotView,
  MetaView,
  ScoreHistoryView,
  TopSetupView,
} from './types';

/**
 * Projects the local fixtures onto the same view types the API returns, so the
 * offline path and the live path are indistinguishable to every page.
 */

const UNIT_BY_FACTOR: Record<string, string> = {
  RATE: '%', CPI: 'pp', GDP: 'pp', PMI: 'index',
  NFP: 'K', RETAIL: 'pp', COT: 'K', DXY: 'index', YIELD: '%',
};

export function mockTopSetups(locale: Locale, limit = 8, market?: Market): TopSetupView[] {
  return getTopSetups(limit, market).map((s) => ({
    rank: s.rank,
    symbol: s.symbol,
    name: tx(s.name, locale),
    market: s.market,
    score: s.score,
    bias: s.bias,
    coverage: s.coverage,
    isSufficient: s.isSufficient,
    // Offline mode is entirely fixtures by definition.
    isFullyReal: false,
    realShare: 0,
    dataAsOf: s.dataAsOf,
  }));
}

export function mockHeatmap(locale: Locale, limit?: number, market?: Market): HeatmapView {
  const data = getHeatmap(limit, market);
  return {
    factors: data.factors.map((f) => ({
      code: f.code,
      name: tx(f.name, locale),
      shortName: tx(f.shortName, locale),
      displayOrder: f.displayOrder,
    })),
    rows: data.rows.map((r) => ({
      symbol: r.symbol,
      name: tx(r.name, locale),
      market: r.market,
      score: r.score,
      bias: r.bias,
      cells: r.cells.map((c) => ({
        factorCode: c.factorCode,
        normalizedScore: c.normalizedScore,
        contribution: c.contribution,
        rawLabel: tx(c.rawLabel, locale),
        weight: c.weight,
        available: c.available,
        inProfile: c.inProfile,
      })),
    })),
  };
}

export function mockRankedAssets(locale: Locale): TopSetupView[] {
  return getRankedAssets().map((s) => ({
    rank: s.rank,
    symbol: s.symbol,
    name: tx(s.name, locale),
    market: s.market,
    score: s.score,
    bias: s.bias,
    coverage: s.coverage,
    isSufficient: s.isSufficient,
    // Offline mode is entirely fixtures by definition.
    isFullyReal: false,
    realShare: 0,
    dataAsOf: s.dataAsOf,
  }));
}

export function mockAsset(locale: Locale, symbol: string): AssetDetailView | null {
  const score = getAssetScore(symbol);
  if (!score) return null;

  const factorMeta = new Map(getFactors().map((f) => [f.code, f]));

  return {
    symbol: score.symbol,
    name: tx(score.name, locale),
    market: score.market,
    baseScore: score.baseScore,
    score: score.score,
    bias: score.bias,
    coverage: score.coverage,
    isSufficient: score.isSufficient,
    isFullyReal: false,
    realShare: 0,
    profileName: score.profileName,
    profileVersion: score.profileVersion,
    engineVersion: score.engineVersion,
    dataAsOf: score.dataAsOf,
    calculatedAt: score.calculatedAt,
    factors: score.factors.map((f) => {
      const meta = factorMeta.get(f.factorCode);
      return {
        factorCode: f.factorCode,
        factorName: meta ? tx(meta.name, locale) : f.factorCode,
        category: meta?.category ?? 'growth',
        rawValue: f.rawValue,
        rawLabel: tx(f.rawLabel, locale),
        normalizedScore: f.normalizedScore,
        weight: f.weight,
        polarity: f.polarity,
        contribution: f.contribution,
        explanation: tx(f.explanation, locale),
        available: f.available,
        // The offline mock engine has never modelled per-currency readings, so
        // the breakdown block is absent rather than fabricated.
        readings: [],
      };
    }),
  };
}

export function mockAssetHistory(symbol: string, days = 30): ScoreHistoryView[] {
  return getScoreHistory(symbol, days).map((p) => ({
    date: p.date,
    score: p.score,
    bias: p.bias,
    coverage: p.coverage,
  }));
}

export function mockCalendar(locale: Locale): CalendarEventView[] {
  return getCalendar().map((e) => ({
    id: e.id,
    indicatorCode: e.indicatorCode,
    factorCode: e.factorCode,
    title: tx(e.title, locale),
    currency: e.currency,
    flag: e.flag,
    releasedAt: e.releasedAt,
    originTimeZone: e.originTimeZone,
    importance: e.importance,
    actual: e.actual,
    forecast: e.forecast,
    previous: e.previous,
    unit: e.unit,
    biasFor: e.biasFor,
  }));
}

export function mockIndicators(locale: Locale): IndicatorView[] {
  return getIndicators().map((i) => ({
    code: i.code,
    factorCode: i.factorCode,
    name: tx(i.name, locale),
    description: tx(i.description, locale),
    whyItMatters: tx(i.whyItMatters, locale),
    howItAffects: tx(i.howItAffects, locale),
    category: i.category,
    bandMinor: i.bandMinor,
    bandMajor: i.bandMajor,
    maxAgeDays: 60,
    impact: 'medium',
  }));
}

export function mockMeta(): MetaView {
  const scores = getRankedAssets();
  return {
    engineVersion: '1.0.0',
    activeProfiles: [
      'Metals Default v1',
      'Forex Default v1',
      'Index Default v1',
      'Dollar Index Default v1',
    ],
    lastCalculation: scores[0]?.dataAsOf ?? null,
    dataAsOf: scores[0]?.dataAsOf ?? null,
    dataSource: 'Manual',
  };
}

export function mockMarketSnapshot(): MarketSnapshotView {
  const s = getMarketSnapshot();
  return {
    strongestCurrency: s.strongestCurrency,
    strongestAvg: s.strongestAvg,
    weakestCurrency: s.weakestCurrency,
    weakestAvg: s.weakestAvg,
    riskRegime: s.riskRegime,
    avgCoverage: s.avgCoverage,
    assetCount: s.assetCount,
    // The offline fixture set has no per-asset coverage detail.
    assetsAtFullCoverage: 0,
    dataAsOf: s.dataAsOf,
  };
}

export { UNIT_BY_FACTOR, INDICATOR_BY_FACTOR };
