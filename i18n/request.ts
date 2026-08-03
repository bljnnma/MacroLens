import { getRequestConfig } from 'next-intl/server';
import { routing, type Locale } from './routing';

export default getRequestConfig(async ({ requestLocale }) => {
  const requested = await requestLocale;
  const locale: Locale = routing.locales.includes(requested as Locale)
    ? (requested as Locale)
    : routing.defaultLocale;

  return {
    locale,
    messages: (await import(`../messages/${locale}.json`)).default,
    // Every release on the calendar is published in NY/London/Frankfurt time
    // while the audience sits at UTC+8. Rendering defaults to Ulaanbaatar.
    timeZone: 'Asia/Ulaanbaatar',
  };
});
