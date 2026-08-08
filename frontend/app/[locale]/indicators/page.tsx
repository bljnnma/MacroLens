import { getTranslations, setRequestLocale } from 'next-intl/server';
import { fetchIndicators } from '@/lib/data';
import type { Locale } from '@/lib/mock/types';
import { PageHeader } from '@/components/data/section';
import { IndicatorsGrid, type IndicatorView } from '@/components/indicators/indicators-grid';

/** Weight ranges are a property of the profiles, not the indicator catalogue. */
const WEIGHT_RANGE: Record<string, [number, number]> = {
  RATE: [22, 32],
  CPI: [12, 18],
  GDP: [6, 13],
  PMI: [5, 15],
  NFP: [7, 16],
  RETAIL: [6, 7],
  DXY: [5, 25],
  YIELD: [5, 30],
  COT: [0, 0],
};

const UNIT_BY_FACTOR: Record<string, string> = {
  RATE: '%', CPI: 'pp', GDP: 'pp', PMI: 'index',
  NFP: 'K', RETAIL: 'pp', COT: 'K', DXY: 'index', YIELD: '%',
};

export default async function IndicatorsPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const source = await fetchIndicators(lang);

  const indicators: IndicatorView[] = source.map((i) => ({
    code: i.code,
    factorCode: i.factorCode,
    name: i.name,
    description: i.description,
    whyItMatters: i.whyItMatters,
    howItAffects: i.howItAffects,
    category: i.category,
    frequencyLabel: t(`indicators.${frequencyKey(i.code)}`),
    bandMinor: i.bandMinor,
    bandMajor: i.bandMajor,
    unit: UNIT_BY_FACTOR[i.factorCode] ?? '',
    weightRange: WEIGHT_RANGE[i.factorCode] ?? [0, 0],
  }));

  return (
    <div className="space-y-8">
      <PageHeader title={t('indicators.title')} subtitle={t('indicators.subtitle')} />
      <IndicatorsGrid indicators={indicators} />
    </div>
  );
}

function frequencyKey(code: string): string {
  if (code === 'POLICY_RATE') return 'perMeeting';
  if (code === 'GDP_QOQ') return 'quarterly';
  if (code === 'COT_NET') return 'weekly';
  if (code === 'DXY_INDEX' || code === 'US10Y_REAL') return 'daily';
  return 'monthly';
}
