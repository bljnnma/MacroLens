import { getTranslations, setRequestLocale } from 'next-intl/server';
import { getIndicators } from '@/lib/mock';
import { t as tx } from '@/lib/localized';
import type { Locale } from '@/lib/mock/types';
import { PageHeader } from '@/components/data/section';
import { IndicatorsGrid, type IndicatorView } from '@/components/indicators/indicators-grid';

export default async function IndicatorsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const indicators: IndicatorView[] = getIndicators().map((i) => ({
    code: i.code,
    factorCode: i.factorCode,
    name: tx(i.name, lang),
    description: tx(i.description, lang),
    whyItMatters: tx(i.whyItMatters, lang),
    howItAffects: tx(i.howItAffects, lang),
    category: i.category,
    frequencyLabel: t(`indicators.${i.frequency}`),
    bandMinor: i.bandMinor,
    bandMajor: i.bandMajor,
    unit: i.unit,
    weightRange: i.weightRange,
  }));

  return (
    <div className="space-y-8">
      <PageHeader title={t('indicators.title')} subtitle={t('indicators.subtitle')} />
      <IndicatorsGrid indicators={indicators} />
    </div>
  );
}
