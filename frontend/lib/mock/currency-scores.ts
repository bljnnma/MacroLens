import type { CurrencyCode, LocalizedText, NormalizedScore } from './types';

/**
 * The single source of truth for the prototype.
 *
 * `c` = how bullish a factor is FOR A CURRENCY, computed once and reused across
 * every asset that touches that currency. Asset scores, heatmap cells and factor
 * tables are all DERIVED from this table — nothing downstream is hand-authored,
 * so the EURUSD row and the XAUUSD page can never contradict each other.
 *
 * Raw values are chosen so they reproduce `c` under the normalization bands in
 * docs/scoring-spec.md §4. e.g. US CPI 2.7 vs 2.8 forecast → d = -0.10, which
 * meets band_minor (0.10) but not band_major (0.30) → -1.
 *
 * Scenario: soft landing. The Fed is cutting into resilient labour, US
 * inflation is undershooting, the dollar and real yields sit near one-year
 * lows, and the ECB is still tightening. Risk-on.
 */

export interface CurrencyFactorReading {
  score: NormalizedScore | null;
  actual: number | null;
  forecast: number | null;
  previous: number | null;
  label: LocalizedText;
}

type FactorTable = Partial<Record<CurrencyCode, CurrencyFactorReading>>;

const pct = (v: number) => `${v > 0 ? '+' : ''}${v.toFixed(1)}%`;

export const CURRENCY_SCORES: Record<string, FactorTable> = {
  // Policy rate: direction (hike/cut) + cross-sectional level rank, clamped.
  RATE: {
    USD: {
      score: -1,
      actual: 3.0,
      forecast: 3.0,
      previous: 3.25,
      label: { mn: '3.00% · 25bp бууруулав', en: '3.00% · cut 25bp' },
    },
    EUR: {
      score: 2,
      actual: 4.0,
      forecast: 3.75,
      previous: 3.75,
      label: { mn: '4.00% · 25bp өсгөв', en: '4.00% · hiked 25bp' },
    },
    GBP: {
      score: 1,
      actual: 4.25,
      forecast: 4.25,
      previous: 4.25,
      label: { mn: '4.25% · өөрчлөлтгүй', en: '4.25% · unchanged' },
    },
    JPY: {
      score: 0,
      actual: 1.25,
      forecast: 1.0,
      previous: 1.0,
      label: { mn: '1.25% · 25bp өсгөв', en: '1.25% · hiked 25bp' },
    },
    AUD: {
      score: 0,
      actual: 3.85,
      forecast: 3.85,
      previous: 3.85,
      label: { mn: '3.85% · өөрчлөлтгүй', en: '3.85% · unchanged' },
    },
    CHF: {
      score: -2,
      actual: 0.5,
      forecast: 0.5,
      previous: 0.75,
      label: { mn: '0.50% · 25bp бууруулав', en: '0.50% · cut 25bp' },
    },
    CAD: {
      score: -1,
      actual: 2.5,
      forecast: 2.5,
      previous: 2.75,
      label: { mn: '2.50% · 25bp бууруулав', en: '2.50% · cut 25bp' },
    },
    NZD: {
      score: -1,
      actual: 3.0,
      forecast: 3.0,
      previous: 3.25,
      label: { mn: '3.00% · 25bp бууруулав', en: '3.00% · cut 25bp' },
    },
  },

  // Surprise vs forecast. bands: 0.10 / 0.30 pp
  CPI: {
    USD: {
      score: -1,
      actual: 2.7,
      forecast: 2.8,
      previous: 3.0,
      label: { mn: '2.7% (таамаг 2.8%)', en: '2.7% (est. 2.8%)' },
    },
    EUR: {
      score: 2,
      actual: 2.4,
      forecast: 2.1,
      previous: 2.2,
      label: { mn: '2.4% (таамаг 2.1%)', en: '2.4% (est. 2.1%)' },
    },
    GBP: {
      score: 2,
      actual: 3.6,
      forecast: 3.3,
      previous: 3.4,
      label: { mn: '3.6% (таамаг 3.3%)', en: '3.6% (est. 3.3%)' },
    },
    JPY: {
      score: 0,
      actual: 2.9,
      forecast: 2.9,
      previous: 2.8,
      label: { mn: '2.9% (таамаг 2.9%)', en: '2.9% (est. 2.9%)' },
    },
    AUD: {
      score: -1,
      actual: 2.5,
      forecast: 2.7,
      previous: 2.8,
      label: { mn: '2.5% (таамаг 2.7%)', en: '2.5% (est. 2.7%)' },
    },
    CHF: {
      score: -1,
      actual: 0.4,
      forecast: 0.6,
      previous: 0.7,
      label: { mn: '0.4% (таамаг 0.6%)', en: '0.4% (est. 0.6%)' },
    },
    CAD: {
      score: 0,
      actual: 1.9,
      forecast: 1.9,
      previous: 2.0,
      label: { mn: '1.9% (таамаг 1.9%)', en: '1.9% (est. 1.9%)' },
    },
    NZD: {
      score: null,
      actual: null,
      forecast: 2.2,
      previous: 2.3,
      label: { mn: 'Хараахан зарлагдаагүй', en: 'Not yet released' },
    },
  },

  // Quarterly. Nothing has printed this cycle — the coverage story.
  GDP: {
    USD: { score: null, actual: null, forecast: 2.1, previous: 2.4, label: { mn: 'Улирлын мэдээ хүлээгдэж буй', en: 'Quarterly print pending' } },
    EUR: { score: null, actual: null, forecast: 0.3, previous: 0.4, label: { mn: 'Улирлын мэдээ хүлээгдэж буй', en: 'Quarterly print pending' } },
    GBP: { score: null, actual: null, forecast: 0.2, previous: 0.3, label: { mn: 'Улирлын мэдээ хүлээгдэж буй', en: 'Quarterly print pending' } },
    JPY: { score: null, actual: null, forecast: 0.2, previous: 0.1, label: { mn: 'Улирлын мэдээ хүлээгдэж буй', en: 'Quarterly print pending' } },
    AUD: { score: null, actual: null, forecast: 0.5, previous: 0.6, label: { mn: 'Улирлын мэдээ хүлээгдэж буй', en: 'Quarterly print pending' } },
    CHF: { score: null, actual: null, forecast: 0.3, previous: 0.3, label: { mn: 'Улирлын мэдээ хүлээгдэж буй', en: 'Quarterly print pending' } },
    CAD: { score: null, actual: null, forecast: 0.4, previous: 0.5, label: { mn: 'Улирлын мэдээ хүлээгдэж буй', en: 'Quarterly print pending' } },
    NZD: { score: null, actual: null, forecast: 0.2, previous: 0.2, label: { mn: 'Улирлын мэдээ хүлээгдэж буй', en: 'Quarterly print pending' } },
  },

  // bands: 0.5 / 1.5 index points
  PMI: {
    USD: {
      score: -2,
      actual: 46.8,
      forecast: 49.0,
      previous: 48.6,
      label: { mn: '46.8 (таамаг 49.0)', en: '46.8 (est. 49.0)' },
    },
    EUR: {
      score: 1,
      actual: 51.8,
      forecast: 50.9,
      previous: 50.4,
      label: { mn: '51.8 (таамаг 50.9)', en: '51.8 (est. 50.9)' },
    },
    GBP: {
      score: 0,
      actual: 50.2,
      forecast: 50.4,
      previous: 50.1,
      label: { mn: '50.2 (таамаг 50.4)', en: '50.2 (est. 50.4)' },
    },
    JPY: {
      score: 1,
      actual: 49.9,
      forecast: 49.1,
      previous: 48.8,
      label: { mn: '49.9 (таамаг 49.1)', en: '49.9 (est. 49.1)' },
    },
    AUD: {
      score: 0,
      actual: 50.6,
      forecast: 50.5,
      previous: 50.2,
      label: { mn: '50.6 (таамаг 50.5)', en: '50.6 (est. 50.5)' },
    },
    CHF: {
      score: 0,
      actual: 48.9,
      forecast: 49.2,
      previous: 48.5,
      label: { mn: '48.9 (таамаг 49.2)', en: '48.9 (est. 49.2)' },
    },
    CAD: {
      score: -1,
      actual: 47.5,
      forecast: 48.8,
      previous: 48.3,
      label: { mn: '47.5 (таамаг 48.8)', en: '47.5 (est. 48.8)' },
    },
    NZD: {
      score: null,
      actual: null,
      forecast: 49.5,
      previous: 49.2,
      label: { mn: 'Хараахан зарлагдаагүй', en: 'Not yet released' },
    },
  },

  // NFP bands: 25k / 75k. Other currencies use employment change / unemployment.
  NFP: {
    USD: {
      score: 1,
      actual: 185,
      forecast: 145,
      previous: 158,
      label: { mn: '185мянга (таамаг 145мянга)', en: '185K (est. 145K)' },
    },
    EUR: {
      score: 0,
      actual: 21,
      forecast: 20,
      previous: 24,
      label: { mn: '+21мянга (таамаг +20мянга)', en: '+21K (est. +20K)' },
    },
    GBP: {
      score: -1,
      actual: 4.6,
      forecast: 4.4,
      previous: 4.4,
      label: { mn: 'Ажилгүйдэл 4.6% (таамаг 4.4%)', en: 'Unemployment 4.6% (est. 4.4%)' },
    },
    JPY: {
      score: 1,
      actual: 2.3,
      forecast: 2.5,
      previous: 2.5,
      label: { mn: 'Ажилгүйдэл 2.3% (таамаг 2.5%)', en: 'Unemployment 2.3% (est. 2.5%)' },
    },
    AUD: {
      score: 1,
      actual: 34,
      forecast: 18,
      previous: 22,
      label: { mn: '+34мянга (таамаг +18мянга)', en: '+34K (est. +18K)' },
    },
    CHF: {
      score: 0,
      actual: 2.8,
      forecast: 2.8,
      previous: 2.7,
      label: { mn: 'Ажилгүйдэл 2.8% (таамаг 2.8%)', en: 'Unemployment 2.8% (est. 2.8%)' },
    },
    CAD: {
      score: -1,
      actual: -8,
      forecast: 12,
      previous: 15,
      label: { mn: '−8мянга (таамаг +12мянга)', en: '−8K (est. +12K)' },
    },
    NZD: {
      score: 0,
      actual: 4.9,
      forecast: 4.9,
      previous: 4.8,
      label: { mn: 'Ажилгүйдэл 4.9% (таамаг 4.9%)', en: 'Unemployment 4.9% (est. 4.9%)' },
    },
  },

  // bands: 0.20 / 0.50 pp MoM
  RETAIL: {
    USD: {
      score: 1,
      actual: 0.6,
      forecast: 0.4,
      previous: 0.3,
      label: { mn: `${pct(0.6)} (таамаг ${pct(0.4)})`, en: `${pct(0.6)} (est. ${pct(0.4)})` },
    },
    EUR: {
      score: -1,
      actual: -0.1,
      forecast: 0.2,
      previous: 0.1,
      label: { mn: `${pct(-0.1)} (таамаг ${pct(0.2)})`, en: `${pct(-0.1)} (est. ${pct(0.2)})` },
    },
    GBP: {
      score: 2,
      actual: 0.8,
      forecast: 0.3,
      previous: 0.2,
      label: { mn: `${pct(0.8)} (таамаг ${pct(0.3)})`, en: `${pct(0.8)} (est. ${pct(0.3)})` },
    },
    JPY: {
      score: 0,
      actual: 0.2,
      forecast: 0.2,
      previous: 0.3,
      label: { mn: `${pct(0.2)} (таамаг ${pct(0.2)})`, en: `${pct(0.2)} (est. ${pct(0.2)})` },
    },
    AUD: {
      score: 1,
      actual: 0.5,
      forecast: 0.3,
      previous: 0.2,
      label: { mn: `${pct(0.5)} (таамаг ${pct(0.3)})`, en: `${pct(0.5)} (est. ${pct(0.3)})` },
    },
    CHF: {
      score: 0,
      actual: 0.1,
      forecast: 0.1,
      previous: 0.2,
      label: { mn: `${pct(0.1)} (таамаг ${pct(0.1)})`, en: `${pct(0.1)} (est. ${pct(0.1)})` },
    },
    CAD: {
      score: -2,
      actual: -0.4,
      forecast: 0.1,
      previous: 0.2,
      label: { mn: `${pct(-0.4)} (таамаг ${pct(0.1)})`, en: `${pct(-0.4)} (est. ${pct(0.1)})` },
    },
    NZD: {
      score: null,
      actual: null,
      forecast: 0.2,
      previous: 0.1,
      label: { mn: 'Хараахан зарлагдаагүй', en: 'Not yet released' },
    },
  },

  // Weekly CFTC net speculative positioning. Displayed, but weight 0 in v1.
  COT: {
    USD: { score: -1, actual: -12.4, forecast: null, previous: -6.1, label: { mn: 'Цэвэр −12.4мянга гэрээ', en: 'Net −12.4K contracts' } },
    EUR: { score: 1, actual: 48.2, forecast: null, previous: 39.7, label: { mn: 'Цэвэр +48.2мянга гэрээ', en: 'Net +48.2K contracts' } },
    GBP: { score: 1, actual: 31.5, forecast: null, previous: 24.9, label: { mn: 'Цэвэр +31.5мянга гэрээ', en: 'Net +31.5K contracts' } },
    JPY: { score: 2, actual: 62.8, forecast: null, previous: 41.3, label: { mn: 'Цэвэр +62.8мянга гэрээ', en: 'Net +62.8K contracts' } },
    AUD: { score: 0, actual: 3.1, forecast: null, previous: 2.4, label: { mn: 'Цэвэр +3.1мянга гэрээ', en: 'Net +3.1K contracts' } },
    CHF: { score: -1, actual: -18.6, forecast: null, previous: -14.2, label: { mn: 'Цэвэр −18.6мянга гэрээ', en: 'Net −18.6K contracts' } },
    CAD: { score: -1, actual: -22.4, forecast: null, previous: -19.8, label: { mn: 'Цэвэр −22.4мянга гэрээ', en: 'Net −22.4K contracts' } },
    NZD: { score: 0, actual: -1.2, forecast: null, previous: 0.6, label: { mn: 'Цэвэр −1.2мянга гэрээ', en: 'Net −1.2K contracts' } },
  },

  // USD-scoped: derived from market series, defined for USD only.
  DXY: {
    USD: {
      score: -2,
      actual: 96.42,
      forecast: null,
      previous: 97.11,
      label: { mn: '96.42 · 1 жилийн 14-р хувиар', en: '96.42 · 14th percentile of 1Y' },
    },
  },
  YIELD: {
    USD: {
      score: -2,
      actual: 1.12,
      forecast: null,
      previous: 1.28,
      label: { mn: '1.12% бодит · 1 жилийн 11-р хувиар', en: '1.12% real · 11th percentile of 1Y' },
    },
  },
};

export const CURRENCIES: CurrencyCode[] = [
  'USD',
  'EUR',
  'GBP',
  'JPY',
  'AUD',
  'CHF',
  'CAD',
  'NZD',
];

export function getReading(
  factorCode: string,
  currency: CurrencyCode
): CurrencyFactorReading | undefined {
  return CURRENCY_SCORES[factorCode]?.[currency];
}
