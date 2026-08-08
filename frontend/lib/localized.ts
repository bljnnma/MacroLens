import type { Locale, LocalizedText } from '@/lib/mock/types';

/**
 * The API will resolve locale server-side and return single strings, so this
 * helper is the prototype's stand-in for that projection — not a pattern that
 * survives into the real client.
 */
export function t(text: LocalizedText | undefined, locale: string): string {
  if (!text) return '—';
  return text[(locale as Locale) ?? 'mn'] ?? text.mn;
}
