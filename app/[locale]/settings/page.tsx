import { getTranslations, setRequestLocale } from 'next-intl/server';
import { getAllScores, getEngineVersion, getReferenceTime } from '@/lib/mock';
import { formatDateInZone, formatTimeInZone } from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { PageHeader } from '@/components/data/section';
import { SettingsPanel } from '@/components/settings/settings-panel';

export default async function SettingsPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const scores = getAllScores();
  const profiles = [...new Set(scores.map((s) => `${s.profileName} v${s.profileVersion}`))];
  const latest = scores[0];

  return (
    <div className="space-y-8">
      <PageHeader title={t('settings.title')} subtitle={t('settings.subtitle')} />
      <SettingsPanel
        sampleIso={latest.dataAsOf}
        engineVersion={getEngineVersion()}
        profiles={profiles}
        lastCalculation={`${formatDateInZone(latest.calculatedAt, lang)} ${formatTimeInZone(latest.calculatedAt, lang)}`}
      />
    </div>
  );
}
