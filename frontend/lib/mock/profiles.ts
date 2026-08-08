import type { Market, ScoringProfile } from './types';

/**
 * Weights sum to 100 per profile. Not required by the maths — the 50/maxAbs
 * scale normalises any total — but it lets a user read a weight as a percentage,
 * which matters for a product whose promise is transparency.
 *
 * The weight rows ARE the contributor selection: a factor with no row simply
 * does not participate. That is why the DollarIndex profile has no DXY row —
 * scoring the dollar index by the dollar index is circular.
 */
export const PROFILES: Record<Market, ScoringProfile> = {
  forex: {
    name: 'Forex Default',
    version: 1,
    market: 'forex',
    bullishThreshold: 65,
    bearishThreshold: 35,
    minCoverage: 0.6,
    weights: [
      { factorCode: 'RATE', weight: 31, polarity: 1 },
      { factorCode: 'CPI', weight: 17, polarity: 1 },
      { factorCode: 'NFP', weight: 14, polarity: 1 },
      { factorCode: 'PMI', weight: 11, polarity: 1 },
      { factorCode: 'GDP', weight: 9, polarity: 1 },
      { factorCode: 'DXY', weight: 7, polarity: 1 },
      { factorCode: 'RETAIL', weight: 6, polarity: 1 },
      { factorCode: 'YIELD', weight: 5, polarity: 1 },
      { factorCode: 'COT', weight: 0, polarity: 1 },
    ],
  },

  metals: {
    name: 'Metals Default',
    version: 1,
    market: 'metals',
    bullishThreshold: 65,
    bearishThreshold: 35,
    minCoverage: 0.6,
    weights: [
      { factorCode: 'YIELD', weight: 25, polarity: 1 },
      { factorCode: 'DXY', weight: 25, polarity: 1 },
      { factorCode: 'RATE', weight: 22, polarity: 1 },
      { factorCode: 'CPI', weight: 12, polarity: 1 },
      { factorCode: 'PMI', weight: 10, polarity: 1 },
      { factorCode: 'GDP', weight: 6, polarity: 1 },
      { factorCode: 'COT', weight: 0, polarity: 1 },
    ],
  },

  dollarIndex: {
    name: 'Dollar Index Default',
    version: 1,
    market: 'dollarIndex',
    bullishThreshold: 65,
    bearishThreshold: 35,
    minCoverage: 0.6,
    weights: [
      { factorCode: 'RATE', weight: 32, polarity: 1 },
      { factorCode: 'CPI', weight: 18, polarity: 1 },
      { factorCode: 'NFP', weight: 16, polarity: 1 },
      { factorCode: 'PMI', weight: 12, polarity: 1 },
      { factorCode: 'GDP', weight: 10, polarity: 1 },
      { factorCode: 'RETAIL', weight: 7, polarity: 1 },
      { factorCode: 'YIELD', weight: 5, polarity: 1 },
      { factorCode: 'COT', weight: 0, polarity: 1 },
    ],
  },

  // Polarity earns its keep here: strong US growth is bullish for USD *and*
  // bullish for the index, so a pure currency mapping gets one of them backwards.
  indices: {
    name: 'Index Default',
    version: 1,
    market: 'indices',
    bullishThreshold: 65,
    bearishThreshold: 35,
    minCoverage: 0.6,
    weights: [
      { factorCode: 'YIELD', weight: 30, polarity: -1 },
      { factorCode: 'RATE', weight: 30, polarity: -1 },
      { factorCode: 'CPI', weight: 12, polarity: -1 },
      { factorCode: 'GDP', weight: 8, polarity: 1 },
      { factorCode: 'NFP', weight: 8, polarity: 1 },
      { factorCode: 'DXY', weight: 7, polarity: -1 },
      { factorCode: 'PMI', weight: 5, polarity: 1 },
      { factorCode: 'COT', weight: 0, polarity: 1 },
    ],
  },
};

export const ENGINE_VERSION = '1.0.0';
