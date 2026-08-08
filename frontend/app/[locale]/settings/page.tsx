import { getTranslations, setRequestLocale } from 'next-intl/server';
import { fetchMeta } from '@/lib/data';
import { formatDateInZone, formatTimeInZone } from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { PageHeader } from '@/components/data/section';
import { SettingsPanel } from '@/components/settings/settings-panel';

export default async function SettingsPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const meta = await fetchMeta(lang);
  const calculatedAt = meta.lastCalculation;

  return (
    <div className="space-y-8">
      <PageHeader title={t('settings.title')} subtitle={t('settings.subtitle')} />
      <SettingsPanel
        sampleIso={meta.dataAsOf ?? new Date().toISOString()}
        engineVersion={meta.engineVersion}
        profiles={meta.activeProfiles}
        lastCalculation={
          calculatedAt
            ? `${formatDateInZone(calculatedAt, lang)} ${formatTimeInZone(calculatedAt, lang)}`
            : '—'
        }
        dataSource={meta.dataSource}
      />
    </div>
  );
}
