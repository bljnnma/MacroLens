import { getTranslations, setRequestLocale } from 'next-intl/server';
import { fetchCalendar } from '@/lib/data';
import { dayKeyInZone } from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { PageHeader } from '@/components/data/section';
import { SessionsView, type SessionEvent } from '@/components/sessions/sessions-view';

export default async function SessionsPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const now = new Date();
  const todayKey = dayKeyInZone(now.toISOString());

  const events: SessionEvent[] = (await fetchCalendar(lang))
    .filter((e) => dayKeyInZone(e.releasedAt) === todayKey)
    .map((e) => ({
      id: e.id,
      releasedAt: e.releasedAt,
      title: e.title,
      currency: e.currency,
      importance: e.importance,
    }));

  return (
    <div className="space-y-8">
      <PageHeader
        title={t('sessions.title')}
        subtitleEn={t('sessions.subtitleEn')}
        subtitle={t('sessions.subtitle')}
      />
      <SessionsView nowIso={now.toISOString()} events={events} />
    </div>
  );
}
