import type { Bias, Market, NormalizedScore } from '@/lib/mock/types';

export const BASE_SCORE = 50;
export const MIN_COVERAGE = 0.6;

/**
 * Score encodings diverge from 50, never fill from 0. A left-anchored bar
 * implies 0 is "empty" and 50 is "half full"; both are wrong. 34 is a real
 * bearish signal, not a weak bullish one.
 */
export function scoreOffsetPercent(score: number): { width: number; side: 'pos' | 'neg' | 'zero' } {
  const delta = score - BASE_SCORE;
  if (Math.abs(delta) < 0.05) return { width: 0, side: 'zero' };
  return { width: Math.min(100, (Math.abs(delta) / 50) * 100), side: delta > 0 ? 'pos' : 'neg' };
}

export function biasFromScore(score: number): Bias {
  if (score >= 65) return 'bullish';
  if (score <= 35) return 'bearish';
  return 'neutral';
}

export const BIAS_TEXT: Record<Bias, string> = {
  bullish: 'text-pos',
  bearish: 'text-neg',
  neutral: 'text-fg-muted',
};

export const BIAS_SURFACE: Record<Bias, string> = {
  bullish: 'bg-pos/10 text-pos ring-1 ring-inset ring-pos/25',
  bearish: 'bg-neg/10 text-neg ring-1 ring-inset ring-neg/25',
  neutral: 'bg-neu/15 text-fg-muted ring-1 ring-inset ring-line-strong',
};

/**
 * Five-step diverging scale. The number is always printed in the cell too —
 * colour-only encoding fails for red-green colour deficiency, and traders want
 * the digit regardless. Colour is the scanning aid; the number is the truth.
 *
 * Scores carry half steps (a pair's value is a differential), but the palette
 * stays at five bands: nine shades on a diverging scale stop being
 * distinguishable at cell size, and the printed number already carries the
 * finer reading. A half step rounds AWAY from zero for colour only, so +0.5
 * reads as positive rather than washing out to neutral.
 */
export function heatmapCellClass(n: number | null): string {
  if (n === null || Number.isNaN(n)) return 'bg-surface text-fg-subtle';

  const band = Math.sign(n) * Math.min(2, Math.ceil(Math.abs(n)));

  switch (band) {
    case 2:
      return 'bg-cell-p2 text-cell-fg-p2';
    case 1:
      return 'bg-cell-p1 text-cell-fg-p1';
    case 0:
      return 'bg-cell-0 text-cell-fg-0';
    case -1:
      return 'bg-cell-n1 text-cell-fg-n1';
    case -2:
      return 'bg-cell-n2 text-cell-fg-n2';
    default:
      return 'bg-surface text-fg-subtle';
  }
}

export const MARKET_KEYS: Record<Market, string> = {
  forex: 'forex',
  metals: 'metals',
  indices: 'indices',
  dollarIndex: 'dollarIndex',
};

export function contributionColor(value: number): string {
  if (value > 0) return 'text-pos';
  if (value < 0) return 'text-neg';
  return 'text-fg-subtle';
}

/** Coverage is a qualifier, not a value — four dots stay subordinate to the score. */
export function coverageDots(coverage: number): number {
  return Math.max(0, Math.min(4, Math.round(coverage * 4)));
}
