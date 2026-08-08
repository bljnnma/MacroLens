import type { Bias, FactorCategory, Importance, Market } from '@/lib/mock/types';

/**
 * View types: every string is ALREADY localized.
 *
 * The API resolves locale server-side and returns single strings, so nothing
 * downstream of this layer ever sees a {mn, en} pair. The mock adapter projects
 * to the same shapes, which is what lets pages be written once against both.
 */

export interface TopSetupView {
  rank: number;
  symbol: string;
  name: string;
  market: Market;
  score: number;
  bias: Bias;
  coverage: number;
  isSufficient: boolean;
  /** True only when every weighted factor came from a provider. */
  isFullyReal: boolean;
  /** Share of the profile weight backed by provider data, 0-1. */
  realShare: number;
  dataAsOf: string;
}

export interface HeatmapFactorView {
  code: string;
  name: string;
  shortName: string;
  displayOrder: number;
}

export interface HeatmapCellView {
  factorCode: string;
  normalizedScore: number | null;
  contribution: number;
  rawLabel: string;
  weight: number;
  available: boolean;
  inProfile: boolean;
}

export interface HeatmapRowView {
  symbol: string;
  name: string;
  market: Market;
  score: number;
  bias: Bias;
  cells: HeatmapCellView[];
}

export interface HeatmapView {
  factors: HeatmapFactorView[];
  rows: HeatmapRowView[];
}

export interface FactorContributionView {
  factorCode: string;
  factorName: string;
  category: FactorCategory;
  rawValue: number | null;
  rawLabel: string;
  normalizedScore: number | null;
  weight: number;
  polarity: number;
  contribution: number;
  explanation: string;
  available: boolean;
  /** Per-currency readings behind the score — two for a pair, one for a USD-scoped factor. */
  readings: FactorReadingView[];
}

export interface FactorReadingView {
  currency: string;
  /** +1 base side, -1 quote side. */
  direction: number;
  normalizedScore: number;
  rawValue: number | null;
  label: string;
}

export interface AssetDetailView {
  symbol: string;
  name: string;
  market: Market;
  baseScore: number;
  score: number;
  bias: Bias;
  coverage: number;
  isSufficient: boolean;
  isFullyReal: boolean;
  realShare: number;
  profileName: string;
  profileVersion: number;
  engineVersion: string;
  dataAsOf: string;
  calculatedAt: string;
  factors: FactorContributionView[];
}

export interface ScoreHistoryView {
  date: string;
  score: number;
  bias: Bias;
  coverage: number;
}

export interface CalendarEventView {
  id: string;
  indicatorCode: string;
  factorCode: string;
  title: string;
  currency: string;
  flag: string;
  releasedAt: string;
  originTimeZone: string;
  importance: Importance;
  actual: number | null;
  forecast: number | null;
  previous: number | null;
  unit: string;
  biasFor: Bias | null;
}

export interface IndicatorView {
  code: string;
  factorCode: string;
  name: string;
  description: string;
  whyItMatters: string;
  howItAffects: string;
  category: FactorCategory;
  bandMinor: number;
  bandMajor: number;
  maxAgeDays: number;
  impact: Importance;
}

export interface MetaView {
  engineVersion: string;
  activeProfiles: string[];
  lastCalculation: string | null;
  dataAsOf: string | null;
  dataSource: string;
}

export interface MarketSnapshotView {
  strongestCurrency: string;
  strongestAvg: number;
  weakestCurrency: string;
  weakestAvg: number;
  riskRegime: 'on' | 'off';
  avgCoverage: number;
  assetCount: number;
  /** Assets scoring on every weighted factor — qualifies the average. */
  assetsAtFullCoverage: number;
  dataAsOf: string;
}
