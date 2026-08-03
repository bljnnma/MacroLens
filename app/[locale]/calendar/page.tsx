import { getTranslations, setRequestLocale } from 'next-intl/server';
import { getCalendar, getReferenceTime } from '@/lib/mock';
import { t as tx } from '@/lib/localized';
import {
  countdown,
  dayKeyInZone,
  formatDateInZone,
  formatIndicatorValue,
  formatTimeInZone,
  formatWeekday,
} from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { PageHeader } from '@/components/data/section';
import {
  CalendarList,
  type CalendarDayView,
  type CalendarEventView,
} from '@/components/calendar/calendar-list';

export default async function CalendarPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const now = getReferenceTime();
  const todayKey = dayKeyInZone(now.toISOString());
  const tomorrowKey = dayKeyInZone(new Date(now.getTime() + 86_400_000).toISOString());

  const grouped = new Map<string, CalendarEventView[]>();

  for (const e of getCalendar()) {
    const key = dayKeyInZone(e.releasedAt);
    const cd = countdown(e.releasedAt, now);

    const view: CalendarEventView = {
      id: e.id,
      timeLabel: formatTimeInZone(e.releasedAt, lang),
      originLabel: e.originTimeZone,
      flag: e.flag,
      currency: e.currency,
      title: tx(e.title, lang),
      importance: e.importance,
      actual: e.actual === null ? null : formatIndicatorValue(e.actual, e.unit),
      forecast: formatIndicatorValue(e.forecast, e.unit),
      previous: formatIndicatorValue(e.previous, e.unit),
      biasFor: e.biasFor,
      countdownLabel: cd ? t('calendar.inHours', { hours: cd.hours, minutes: cd.minutes }) : null,
    };

    grouped.set(key, [...(grouped.get(key) ?? []), view]);
  }

  const days: CalendarDayView[] = [...grouped.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([key, events]) => {
      const iso = new Date(`${key}T00:00:00Z`).toISOString();
      const weekday = formatWeekday(iso, lang, 'UTC');
      const isRelative = key === todayKey || key === tomorrowKey;

      return {
        key,
        heading: isRelative
          ? key === todayKey
            ? t('calendar.today')
            : t('calendar.tomorrow')
          : weekday,
        dateLabel: formatDateInZone(iso, lang, 'UTC'),
        // Only shown when the heading is relative, otherwise it repeats it.
        weekday: isRelative ? weekday : '',
        events,
      };
    });

  return (
    <div className="space-y-8">
      <PageHeader title={t('calendar.title')} subtitle={t('calendar.subtitle')} />
      <CalendarList days={days} />
    </div>
  );
}
