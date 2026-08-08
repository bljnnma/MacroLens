import type { Bias, ScoreHistoryPoint } from './types';

/** Deterministic PRNG — the same symbol always yields the same series. */
function mulberry32(seed: number) {
  return () => {
    seed |= 0;
    seed = (seed + 0x6d2b79f5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function seedFor(symbol: string): number {
  return symbol.split('').reduce((a, c) => a * 31 + c.charCodeAt(0), 7);
}

/**
 * A mean-reverting walk that terminates exactly on the current score, so the
 * chart's last point always agrees with the badge above it.
 */
export function buildHistory(
  symbol: string,
  currentScore: number,
  coverage: number,
  days = 90
): ScoreHistoryPoint[] {
  const rand = mulberry32(seedFor(symbol));
  const anchor = 50 + (currentScore - 50) * 0.35;

  const raw: number[] = [];
  let value = anchor + (rand() - 0.5) * 8;

  for (let i = 0; i < days; i += 1) {
    const pull = (anchor - value) * 0.06;
    const drift = ((currentScore - anchor) / days) * (i / days) * 2.2;
    value += pull + drift + (rand() - 0.5) * 4.4;
    value = Math.max(4, Math.min(96, value));
    raw.push(value);
  }

  // Blend the tail into the current score so the series lands on it exactly.
  const tailLength = Math.min(10, days);
  for (let i = 0; i < tailLength; i += 1) {
    const idx = days - tailLength + i;
    const w = (i + 1) / tailLength;
    raw[idx] = raw[idx] * (1 - w) + currentScore * w;
  }
  raw[days - 1] = currentScore;

  const end = new Date();
  end.setUTCHours(0, 0, 0, 0);

  return raw.map((score, i) => {
    const d = new Date(end);
    d.setUTCDate(d.getUTCDate() - (days - 1 - i));
    const rounded = Math.round(score * 10) / 10;
    const bias: Bias = rounded >= 65 ? 'bullish' : rounded <= 35 ? 'bearish' : 'neutral';
    return {
      date: d.toISOString().slice(0, 10),
      score: rounded,
      bias,
      coverage: Math.max(0.4, Math.min(1, coverage + (rand() - 0.5) * 0.06)),
    };
  });
}
