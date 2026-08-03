import { FACTORS, FACTOR_BY_CODE } from './factors';
import { PROFILES, ENGINE_VERSION } from './profiles';
import { getReading, type CurrencyFactorReading } from './currency-scores';
import type {
  Asset,
  AssetScore,
  Bias,
  CurrencyCode,
  FactorContribution,
  LocalizedText,
  NormalizedScore,
} from './types';

const BASE_SCORE = 50 as const;

/** Half away from zero, so -0.5 -> -1. Math.round(-0.5) is -0 in JS. */
function roundHalfAwayFromZero(v: number): number {
  return v < 0 ? -Math.round(-v) : Math.round(v);
}

function round1(v: number): number {
  return Math.round(v * 10) / 10;
}

function clampNorm(v: number): NormalizedScore {
  return Math.max(-2, Math.min(2, v)) as NormalizedScore;
}

const CURRENCY_NAME: Record<CurrencyCode, LocalizedText> = {
  USD: { mn: 'АНУ', en: 'US' },
  EUR: { mn: 'Евро бүс', en: 'Eurozone' },
  GBP: { mn: 'Их Британи', en: 'UK' },
  JPY: { mn: 'Япон', en: 'Japan' },
  AUD: { mn: 'Австрали', en: 'Australia' },
  CHF: { mn: 'Швейцар', en: 'Switzerland' },
  CAD: { mn: 'Канад', en: 'Canada' },
  NZD: { mn: 'Шинэ Зеланд', en: 'New Zealand' },
};

const num = (v: number) => (v > 0 ? `+${v.toFixed(1)}` : v.toFixed(1));

function tail(
  n: NormalizedScore,
  weight: number,
  contribution: number
): LocalizedText {
  const shared = `${n > 0 ? '+' : ''}${n} · ${weight} · ${num(contribution)}`;
  return {
    mn: `Хэвийн оноо ${shared.split(' · ')[0]} · жин ${weight} · нөлөөлөл ${num(contribution)}.`,
    en: `Normalized ${shared.split(' · ')[0]} · weight ${weight} · contribution ${num(contribution)}.`,
  };
}

/**
 * Composes an explanation from the same inputs the score used, in both
 * languages, at calculation time. Templates are versioned with the engine —
 * an explanation is a record of a calculation, not a live re-render.
 */
function explain(
  factorCode: string,
  asset: Asset,
  readings: { currency: CurrencyCode; direction: 1 | -1; reading: CurrencyFactorReading }[],
  n: NormalizedScore,
  weight: number,
  contribution: number,
  polarity: 1 | -1
): LocalizedText {
  const factor = FACTOR_BY_CODE.get(factorCode)!;
  const t = tail(n, weight, contribution);
  const dir = (score: number, locale: 'mn' | 'en') =>
    locale === 'mn'
      ? score > 0
        ? 'эерэг'
        : score < 0
          ? 'сөрөг'
          : 'төвийг сахисан'
      : score > 0
        ? 'positive'
        : score < 0
          ? 'negative'
          : 'neutral';

  if (readings.length === 2) {
    const [base, quote] = readings;
    return {
      mn: `${CURRENCY_NAME[base.currency].mn}: ${base.reading.label.mn}. ${CURRENCY_NAME[quote.currency].mn}: ${quote.reading.label.mn}. Зөрүү нь ${asset.symbol}-д ${dir(n, 'mn')}. ${t.mn}`,
      en: `${CURRENCY_NAME[base.currency].en}: ${base.reading.label.en}. ${CURRENCY_NAME[quote.currency].en}: ${quote.reading.label.en}. The differential is ${dir(n, 'en')} for ${asset.symbol}. ${t.en}`,
    };
  }

  const only = readings[0];
  const inverse = only.direction === -1;
  const polarityNote =
    polarity === -1
      ? {
          mn: ` ${asset.symbol} нь энэ хүчин зүйлд урвуу хариу үзүүлдэг.`,
          en: ` ${asset.symbol} responds inversely to this factor.`,
        }
      : { mn: '', en: '' };

  return {
    mn: `${CURRENCY_NAME[only.currency].mn} — ${factor.name.mn}: ${only.reading.label.mn}. ${inverse ? `${only.currency} нь ${asset.symbol}-ийн ханшийн валют тул нөлөө нь урвуу.` : `${asset.symbol} нь ${only.currency}-тай шууд хамааралтай.`}${polarityNote.mn} ${t.mn}`,
    en: `${CURRENCY_NAME[only.currency].en} — ${factor.name.en}: ${only.reading.label.en}. ${inverse ? `${only.currency} is the quote currency for ${asset.symbol}, so the effect inverts.` : `${asset.symbol} tracks ${only.currency} directly.`}${polarityNote.en} ${t.en}`,
  };
}

const UNAVAILABLE: LocalizedText = {
  mn: 'Өгөгдөл байхгүй эсвэл хэт хуучирсан тул энэ хүчин зүйл тооцоололд ороогүй. Хамралтын хувь буурсан.',
  en: 'No usable data for this factor, so it was excluded from the calculation. Coverage is reduced accordingly.',
};

export interface EngineOptions {
  dataAsOf: string;
  calculatedAt: string;
}

export function computeAssetScore(asset: Asset, opts: EngineOptions): AssetScore {
  const profile = PROFILES[asset.market];
  const usdExposure = asset.exposures.find((e) => e.currency === 'USD');

  // Pass 1 — normalize every enabled factor and decide availability.
  type Draft = {
    weight: number;
    polarity: 1 | -1;
    factorCode: string;
    n: NormalizedScore | null;
    readings: { currency: CurrencyCode; direction: 1 | -1; reading: CurrencyFactorReading }[];
    rawValue: number | null;
    rawLabel: LocalizedText;
  };

  const drafts: Draft[] = profile.weights.map(({ factorCode, weight, polarity }) => {
    const factor = FACTOR_BY_CODE.get(factorCode)!;
    const empty: Draft = {
      weight,
      polarity,
      factorCode,
      n: null,
      readings: [],
      rawValue: null,
      rawLabel: { mn: '—', en: '—' },
    };

    if (factor.scope === 'usd') {
      if (!usdExposure) return empty;
      const reading = getReading(factorCode, 'USD');
      if (!reading || reading.score === null) return empty;
      const s = usdExposure.direction * reading.score;
      return {
        ...empty,
        n: clampNorm(roundHalfAwayFromZero(polarity * s)),
        readings: [{ currency: 'USD', direction: usdExposure.direction, reading }],
        rawValue: reading.actual,
        rawLabel: reading.label,
      };
    }

    const readings = asset.exposures.map((e) => ({
      currency: e.currency,
      direction: e.direction,
      reading: getReading(factorCode, e.currency),
    }));

    if (readings.some((r) => !r.reading || r.reading.score === null)) {
      const first = readings[0]?.reading;
      return { ...empty, rawLabel: first?.label ?? empty.rawLabel };
    }

    const resolved = readings as {
      currency: CurrencyCode;
      direction: 1 | -1;
      reading: CurrencyFactorReading;
    }[];

    let s: number;
    if (resolved.length === 2) {
      const base = resolved.find((r) => r.direction === 1)!;
      const quote = resolved.find((r) => r.direction === -1)!;
      s = (base.reading.score! - quote.reading.score!) / 2;
    } else {
      s = resolved[0].direction * resolved[0].reading.score!;
    }

    const ordered =
      resolved.length === 2
        ? [resolved.find((r) => r.direction === 1)!, resolved.find((r) => r.direction === -1)!]
        : resolved;

    return {
      ...empty,
      n: clampNorm(roundHalfAwayFromZero(polarity * s)),
      readings: ordered,
      rawValue: ordered[0].reading.actual,
      rawLabel: ordered[0].reading.label,
    };
  });

  // Pass 2 — aggregate. maxAbs counts participating factors only, which is why
  // coverage has to be reported alongside the score.
  const participating = drafts.filter((d) => d.n !== null);
  const participatingWeight = participating.reduce((a, d) => a + d.weight, 0);
  const enabledWeight = drafts.reduce((a, d) => a + d.weight, 0);
  const maxAbs = participating.reduce((a, d) => a + 2 * d.weight, 0);
  const scale = maxAbs === 0 ? 0 : 50 / maxAbs;

  const factors: FactorContribution[] = drafts
    .map((d) => {
      const contribution = d.n === null ? 0 : round1(d.n * d.weight * scale);
      return {
        factorCode: d.factorCode,
        available: d.n !== null,
        rawValue: d.rawValue,
        rawLabel: d.rawLabel,
        normalizedScore: d.n,
        weight: d.weight,
        polarity: d.polarity,
        contribution,
        explanation:
          d.n === null
            ? UNAVAILABLE
            : explain(d.factorCode, asset, d.readings, d.n, d.weight, contribution, d.polarity),
      };
    })
    .sort((a, b) => {
      const oa = FACTOR_BY_CODE.get(a.factorCode)?.displayOrder ?? 99;
      const ob = FACTOR_BY_CODE.get(b.factorCode)?.displayOrder ?? 99;
      return oa - ob;
    });

  // Summing the ROUNDED contributions is what makes 50 + Σ = score close
  // exactly on screen. The arithmetic a user checks by hand must be the
  // arithmetic the engine did.
  const sum = round1(factors.reduce((a, f) => a + f.contribution, 0));
  const score = Math.max(0, Math.min(100, round1(BASE_SCORE + sum)));
  const coverage = enabledWeight === 0 ? 0 : participatingWeight / enabledWeight;

  const bias: Bias =
    score >= profile.bullishThreshold
      ? 'bullish'
      : score <= profile.bearishThreshold
        ? 'bearish'
        : 'neutral';

  return {
    symbol: asset.symbol,
    name: asset.name,
    market: asset.market,
    baseScore: BASE_SCORE,
    score,
    bias,
    coverage: Math.round(coverage * 1000) / 1000,
    isSufficient: coverage >= profile.minCoverage,
    profileName: profile.name,
    profileVersion: profile.version,
    engineVersion: ENGINE_VERSION,
    dataAsOf: opts.dataAsOf,
    calculatedAt: opts.calculatedAt,
    // Deterministic per symbol so the number is stable between renders.
    calculationDurationMs:
      12 + (asset.symbol.split('').reduce((a, c) => a + c.charCodeAt(0), 0) % 23),
    factors,
  };
}

export { FACTORS, BASE_SCORE };
