/**
 * These types mirror the API response contracts in docs/architecture.md §7.
 *
 * The point of the mirroring is that swapping mocks for a real backend later
 * means replacing function bodies in lib/mock/index.ts and nothing else —
 * no component ever learns the difference.
 */

export type Locale = 'mn' | 'en';

export interface LocalizedText {
  mn: string;
  en: string;
}

export type Bias = 'bullish' | 'neutral' | 'bearish';

export type Market = 'forex' | 'metals' | 'dollarIndex' | 'indices';

export type FactorCategory =
  | 'policy'
  | 'inflation'
  | 'growth'
  | 'labour'
  | 'sentiment'
  | 'positioning';

/** Factors are either defined per-currency, or USD-only (series derived). */
export type FactorScope = 'currency' | 'usd';

export type NormalizedScore = -2 | -1 | 0 | 1 | 2;

export type CurrencyCode =
  | 'USD'
  | 'EUR'
  | 'GBP'
  | 'JPY'
  | 'AUD'
  | 'CHF'
  | 'CAD'
  | 'NZD';

export type Importance = 'high' | 'medium' | 'low';

export interface Factor {
  code: string;
  name: LocalizedText;
  /** Compact label for the heatmap column and factor chips. */
  shortName: LocalizedText;
  description: LocalizedText;
  category: FactorCategory;
  scope: FactorScope;
  displayOrder: number;
}

export interface CurrencyExposure {
  currency: CurrencyCode;
  /** +1 = base side, -1 = quote side. */
  direction: 1 | -1;
}

export interface Asset {
  symbol: string;
  name: LocalizedText;
  market: Market;
  exposures: CurrencyExposure[];
  displayOrder: number;
}

export interface ProfileWeight {
  factorCode: string;
  weight: number;
  /** Equity indices respond inversely to USD-bullish data on some factors. */
  polarity: 1 | -1;
}

export interface ScoringProfile {
  name: string;
  version: number;
  market: Market;
  weights: ProfileWeight[];
  bullishThreshold: number;
  bearishThreshold: number;
  minCoverage: number;
}

export interface FactorContribution {
  factorCode: string;
  available: boolean;
  rawValue: number | null;
  rawLabel: LocalizedText;
  normalizedScore: NormalizedScore | null;
  weight: number;
  polarity: 1 | -1;
  /** Weighted score points. Sums with baseScore to the final score exactly. */
  contribution: number;
  explanation: LocalizedText;
}

export interface AssetScore {
  symbol: string;
  name: LocalizedText;
  market: Market;
  baseScore: 50;
  score: number;
  bias: Bias;
  /** 0..1 — participating weight over total enabled weight. */
  coverage: number;
  isSufficient: boolean;
  profileName: string;
  profileVersion: number;
  engineVersion: string;
  dataAsOf: string;
  calculatedAt: string;
  calculationDurationMs: number;
  factors: FactorContribution[];
}

export interface TopSetupItem {
  rank: number;
  symbol: string;
  name: LocalizedText;
  market: Market;
  score: number;
  bias: Bias;
  coverage: number;
  isSufficient: boolean;
  dataAsOf: string;
}

export interface HeatmapCell {
  factorCode: string;
  normalizedScore: NormalizedScore | null;
  contribution: number;
  rawLabel: LocalizedText;
  weight: number;
  /** Data exists and was usable. */
  available: boolean;
  /**
   * Whether this market's profile scores this factor at all. "Not modelled
   * here" and "data missing" are different claims and must not share a glyph.
   */
  inProfile: boolean;
}

export interface HeatmapRow {
  symbol: string;
  name: LocalizedText;
  market: Market;
  score: number;
  bias: Bias;
  cells: HeatmapCell[];
}

export interface HeatmapData {
  factors: Factor[];
  rows: HeatmapRow[];
}

export interface ScoreHistoryPoint {
  date: string;
  score: number;
  bias: Bias;
  coverage: number;
}

export interface Indicator {
  code: string;
  factorCode: string;
  name: LocalizedText;
  description: LocalizedText;
  whyItMatters: LocalizedText;
  howItAffects: LocalizedText;
  category: FactorCategory;
  frequency: 'monthly' | 'quarterly' | 'weekly' | 'daily' | 'perMeeting';
  bandMinor: number;
  bandMajor: number;
  unit: string;
  weightRange: [number, number];
}

export interface CalendarEvent {
  id: string;
  indicatorCode: string;
  factorCode: string;
  title: LocalizedText;
  currency: CurrencyCode;
  flag: string;
  releasedAt: string;
  originTimeZone: string;
  importance: Importance;
  actual: number | null;
  forecast: number | null;
  previous: number | null;
  unit: string;
  /** Direction of the surprise for the releasing currency, null if unreleased. */
  biasFor: Bias | null;
}

export interface MarketSnapshot {
  strongestCurrency: CurrencyCode;
  strongestAvg: number;
  weakestCurrency: CurrencyCode;
  weakestAvg: number;
  riskRegime: 'on' | 'off';
  avgCoverage: number;
  assetCount: number;
  dataAsOf: string;
}
