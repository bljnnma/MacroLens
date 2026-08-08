import type { Locale } from '@/lib/mock/types';

export const UB_TIMEZONE = 'Asia/Ulaanbaatar';

/** Scores carry one decimal so `50 + Σcontributions` closes exactly on screen. */
export function formatScore(score: number): string {
  return score.toFixed(1);
}

export function formatSigned(value: number, digits = 1): string {
  const fixed = value.toFixed(digits);
  return value > 0 ? `+${fixed}` : fixed;
}

/**
 * Whole steps print bare, half steps print one decimal: +1, +1.5, -2.
 *
 * Not a fixed one decimal for everything — "+1.0" in every cell reads as
 * spurious precision, and most cells are whole. The decimal appears exactly
 * where it carries information.
 */
export function formatNormalized(n: number | null): string {
  if (n === null) return '——';
  if (n === 0) return '0';

  const text = Number.isInteger(n) ? String(Math.abs(n)) : Math.abs(n).toFixed(1);
  return n > 0 ? `+${text}` : `-${text}`;
}

export function formatPercent(value: number, digits = 0): string {
  return `${(value * 100).toFixed(digits)}%`;
}

const RELATIVE_UNITS: [Intl.RelativeTimeFormatUnit, number][] = [
  ['second', 60],
  ['minute', 60],
  ['hour', 24],
  ['day', 7],
];

/**
 * Relative rather than absolute: across a 12-hour offset, an absolute
 * "last updated" timestamp reliably confuses people about data freshness.
 */
export function formatRelativeTime(iso: string, now: Date, locale: Locale): string {
  const diffSeconds = Math.round((new Date(iso).getTime() - now.getTime()) / 1000);
  const rtf = new Intl.RelativeTimeFormat(locale === 'mn' ? 'mn' : 'en', {
    numeric: 'auto',
    style: 'narrow',
  });

  let value = diffSeconds;
  for (const [unit, step] of RELATIVE_UNITS) {
    if (Math.abs(value) < step) return rtf.format(value, unit);
    value = Math.round(value / step);
  }
  return rtf.format(value, 'week');
}

export function formatTimeInZone(
  iso: string,
  locale: Locale,
  timeZone: string = UB_TIMEZONE
): string {
  return new Intl.DateTimeFormat(locale === 'mn' ? 'mn-MN' : 'en-GB', {
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    timeZone,
  }).format(new Date(iso));
}

export function formatDateInZone(
  iso: string,
  locale: Locale,
  timeZone: string = UB_TIMEZONE
): string {
  return new Intl.DateTimeFormat(locale === 'mn' ? 'mn-MN' : 'en-GB', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    timeZone,
  }).format(new Date(iso));
}

export function formatWeekday(iso: string, locale: Locale, timeZone = UB_TIMEZONE): string {
  return new Intl.DateTimeFormat(locale === 'mn' ? 'mn-MN' : 'en-GB', {
    weekday: 'long',
    timeZone,
  }).format(new Date(iso));
}

export function dayKeyInZone(iso: string, timeZone = UB_TIMEZONE): string {
  return new Intl.DateTimeFormat('en-CA', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    timeZone,
  }).format(new Date(iso));
}

export function formatIndicatorValue(value: number | null, unit: string): string {
  if (value === null) return '—';
  if (unit === '%' || unit === 'pp') return `${value.toFixed(1)}%`;
  if (unit === 'K') return `${value > 0 ? '+' : ''}${value.toFixed(0)}K`;
  return value.toFixed(1);
}

export function countdown(iso: string, now: Date): { hours: number; minutes: number } | null {
  const diff = new Date(iso).getTime() - now.getTime();
  if (diff <= 0) return null;
  return {
    hours: Math.floor(diff / 3_600_000),
    minutes: Math.floor((diff % 3_600_000) / 60_000),
  };
}
