import { getTranslations, setRequestLocale } from 'next-intl/server';
import { getRankedAssets, getReferenceTime } from '@/lib/mock';
import { t as tx } from '@/lib/localized';
import { formatRelativeTime } from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { PageHeader } from '@/components/data/section';
import { MarketsWorkspace } from '@/components/filters/markets-workspace';
import type { SetupRow } from '@/components/tables/top-setups-table';

export default async function MarketsPage({ params }: { params: Promise<{ locale: string }> }) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;
  const now = getReferenceTime();

  const rows: SetupRow[] = getRankedAssets().map((s) => ({
    rank: s.rank,
    symbol: s.symbol,
    name: tx(s.name, lang),
    market: s.market,
    score: s.score,
    bias: s.bias,
    coverage: s.coverage,
    isSufficient: s.isSufficient,
    updatedLabel: formatRelativeTime(s.dataAsOf, now, lang),
  }));

  return (
    <div className="space-y-8">
      <PageHeader title={t('markets.title')} subtitle={t('markets.subtitle')} />
      <MarketsWorkspace rows={rows} />
    </div>
  );
}
