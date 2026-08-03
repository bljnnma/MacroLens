import { CURRENCY_SCORES } from './currency-scores';
import { INDICATOR_BY_CODE } from './indicators';
import type { Bias, CalendarEvent, CurrencyCode, Importance } from './types';

const FLAG: Record<CurrencyCode, string> = {
  USD: '🇺🇸',
  EUR: '🇪🇺',
  GBP: '🇬🇧',
  JPY: '🇯🇵',
  AUD: '🇦🇺',
  CHF: '🇨🇭',
  CAD: '🇨🇦',
  NZD: '🇳🇿',
};

const ORIGIN_TZ: Record<CurrencyCode, string> = {
  USD: 'America/New_York',
  EUR: 'Europe/Frankfurt',
  GBP: 'Europe/London',
  JPY: 'Asia/Tokyo',
  AUD: 'Australia/Sydney',
  CHF: 'Europe/Zurich',
  CAD: 'America/Toronto',
  NZD: 'Pacific/Auckland',
};

interface EventSeed {
  indicatorCode: string;
  factorCode: string;
  currency: CurrencyCode;
  /** Hours from the reference instant. Negative = already released. */
  offsetHours: number;
  importance: Importance;
}

/**
 * Released events pull their numbers straight from CURRENCY_SCORES, so the
 * calendar can never disagree with the heatmap cell it produced.
 */
const SEEDS: EventSeed[] = [
  { indicatorCode: 'CPI_YOY', factorCode: 'CPI', currency: 'USD', offsetHours: -2, importance: 'high' },
  { indicatorCode: 'PMI_MFG', factorCode: 'PMI', currency: 'EUR', offsetHours: -5, importance: 'medium' },
  { indicatorCode: 'RETAIL_MOM', factorCode: 'RETAIL', currency: 'GBP', offsetHours: -21, importance: 'medium' },
  { indicatorCode: 'POLICY_RATE', factorCode: 'RATE', currency: 'EUR', offsetHours: -27, importance: 'high' },
  { indicatorCode: 'NFP', factorCode: 'NFP', currency: 'USD', offsetHours: -30, importance: 'high' },
  { indicatorCode: 'PMI_MFG', factorCode: 'PMI', currency: 'USD', offsetHours: -46, importance: 'high' },
  { indicatorCode: 'CPI_YOY', factorCode: 'CPI', currency: 'CAD', offsetHours: -52, importance: 'medium' },
  { indicatorCode: 'RETAIL_MOM', factorCode: 'RETAIL', currency: 'AUD', offsetHours: -55, importance: 'low' },

  { indicatorCode: 'POLICY_RATE', factorCode: 'RATE', currency: 'USD', offsetHours: 4, importance: 'high' },
  { indicatorCode: 'CPI_YOY', factorCode: 'CPI', currency: 'JPY', offsetHours: 9, importance: 'medium' },
  { indicatorCode: 'EMPLOY_CHANGE', factorCode: 'NFP', currency: 'AUD', offsetHours: 21, importance: 'medium' },
  { indicatorCode: 'PMI_MFG', factorCode: 'PMI', currency: 'GBP', offsetHours: 26, importance: 'medium' },
  { indicatorCode: 'RETAIL_MOM', factorCode: 'RETAIL', currency: 'USD', offsetHours: 31, importance: 'high' },
  { indicatorCode: 'CPI_YOY', factorCode: 'CPI', currency: 'NZD', offsetHours: 47, importance: 'medium' },
  { indicatorCode: 'POLICY_RATE', factorCode: 'RATE', currency: 'GBP', offsetHours: 53, importance: 'high' },
  { indicatorCode: 'GDP_QOQ', factorCode: 'GDP', currency: 'USD', offsetHours: 72, importance: 'high' },
  { indicatorCode: 'PMI_MFG', factorCode: 'PMI', currency: 'CHF', offsetHours: 78, importance: 'low' },
  { indicatorCode: 'GDP_QOQ', factorCode: 'GDP', currency: 'EUR', offsetHours: 96, importance: 'high' },
  { indicatorCode: 'EMPLOY_CHANGE', factorCode: 'NFP', currency: 'CAD', offsetHours: 101, importance: 'medium' },
  { indicatorCode: 'CPI_YOY', factorCode: 'CPI', currency: 'CHF', offsetHours: 120, importance: 'low' },
];

export function buildCalendar(reference: Date): CalendarEvent[] {
  return SEEDS.map((seed, i) => {
    const indicator = INDICATOR_BY_CODE.get(seed.indicatorCode)!;
    const reading = CURRENCY_SCORES[seed.factorCode]?.[seed.currency];
    const released = seed.offsetHours < 0;

    const releasedAt = new Date(reference.getTime() + seed.offsetHours * 3_600_000);
    // Macro data lands on the half hour far more often than not.
    releasedAt.setUTCMinutes(seed.offsetHours % 2 === 0 ? 30 : 0, 0, 0);

    const score = reading?.score ?? null;
    const biasFor: Bias | null =
      !released || score === null ? null : score > 0 ? 'bullish' : score < 0 ? 'bearish' : 'neutral';

    return {
      id: `${seed.currency}-${seed.indicatorCode}-${i}`,
      indicatorCode: seed.indicatorCode,
      factorCode: seed.factorCode,
      title: indicator.name,
      currency: seed.currency,
      flag: FLAG[seed.currency],
      releasedAt: releasedAt.toISOString(),
      originTimeZone: ORIGIN_TZ[seed.currency],
      importance: seed.importance,
      actual: released ? (reading?.actual ?? null) : null,
      forecast: reading?.forecast ?? null,
      previous: reading?.previous ?? null,
      unit: indicator.unit,
      biasFor,
    };
  }).sort((a, b) => a.releasedAt.localeCompare(b.releasedAt));
}
