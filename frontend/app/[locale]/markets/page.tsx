import { getTranslations, setRequestLocale } from 'next-intl/server';
import { fetchRankedAssets } from '@/lib/data';
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

  const assets = await fetchRankedAssets(lang);
  const now = new Date();

  const rows: SetupRow[] = assets.map((s) => ({
    rank: s.rank,
    symbol: s.symbol,
    name: s.name,
    market: s.market,
    score: s.score,
    bias: s.bias,
    coverage: s.coverage,
    isSufficient: s.isSufficient,
    isFullyReal: s.isFullyReal,
    realShare: s.realShare,
    updatedLabel: formatRelativeTime(s.dataAsOf, now, lang),
  }));

  return (
    <div className="space-y-8">
      <PageHeader title={t('markets.title')} subtitle={t('markets.subtitle')} />
      <MarketsWorkspace rows={rows} />
    </div>
  );
}
