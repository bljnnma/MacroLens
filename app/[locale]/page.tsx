import { getTranslations, setRequestLocale } from 'next-intl/server';
import { RefreshCw, TrendingDown, TrendingUp, Activity, Layers } from 'lucide-react';
import {
  getHeatmap,
  getMarketSnapshot,
  getRecentReleases,
  getReferenceTime,
  getTopSetups,
} from '@/lib/mock';
import { t as tx } from '@/lib/localized';
import {
  formatIndicatorValue,
  formatPercent,
  formatRelativeTime,
  formatSigned,
} from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { PageHeader, MetricCard, SectionHeader, SectionLink } from '@/components/data/section';
import { TopSetupsTable, type SetupRow } from '@/components/tables/top-setups-table';
import { Heatmap } from '@/components/heatmap/heatmap';
import { ReleaseFeed, type ReleaseItem } from '@/components/calendar/release-feed';

export default async function DashboardPage({
  params,
}: {
  params: Promise<{ locale: string }>;
}) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const now = getReferenceTime();
  const snapshot = getMarketSnapshot();
  const setups = getTopSetups(8);
  const heatmap = getHeatmap(6);
  const releases = getRecentReleases(4);

  const rows: SetupRow[] = setups.map((s) => ({
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

  const releaseItems: ReleaseItem[] = releases.map((r) => ({
    id: r.id,
    flag: r.flag,
    currency: r.currency,
    title: tx(r.title, lang),
    importance: r.importance,
    actual: formatIndicatorValue(r.actual, r.unit),
    forecast: formatIndicatorValue(r.forecast, r.unit),
    previous: formatIndicatorValue(r.previous, r.unit),
    biasFor: r.biasFor,
    timeLabel: formatRelativeTime(r.releasedAt, now, lang),
  }));

  return (
    <div className="space-y-12">
      <PageHeader
        title={t('dashboard.title')}
        subtitle={t('dashboard.subtitle')}
        action={
          <span className="inline-flex items-center gap-2 rounded-lg border border-line bg-surface px-3 py-2 text-[13px] text-fg-muted">
            <RefreshCw className="size-3.5" aria-hidden />
            {formatRelativeTime(snapshot.dataAsOf, now, lang)}
          </span>
        }
      />

      {/* Context strip: earns its vertical space by carrying live state, which
          is why the hero above it is only two lines. */}
      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          label={t('dashboard.strongestCurrency')}
          value={snapshot.strongestCurrency}
          detail={t('dashboard.avgFactor', { value: formatSigned(snapshot.strongestAvg, 2) })}
          tone="pos"
          icon={<TrendingUp className="size-3.5" />}
        />
        <MetricCard
          label={t('dashboard.weakestCurrency')}
          value={snapshot.weakestCurrency}
          detail={t('dashboard.avgFactor', { value: formatSigned(snapshot.weakestAvg, 2) })}
          tone="neg"
          icon={<TrendingDown className="size-3.5" />}
        />
        <MetricCard
          label={t('dashboard.riskRegime')}
          value={snapshot.riskRegime === 'on' ? t('dashboard.riskOn') : t('dashboard.riskOff')}
          detail={t('dashboard.assetsScored', { count: snapshot.assetCount })}
          icon={<Activity className="size-3.5" />}
        />
        <MetricCard
          label={t('dashboard.avgCoverage')}
          value={formatPercent(snapshot.avgCoverage)}
          detail={t('tooltip.coverage', { available: 8, total: 9 })}
          icon={<Layers className="size-3.5" />}
        />
      </section>

      <section className="space-y-4">
        <SectionHeader
          title={t('dashboard.topSetups')}
          hint={t('dashboard.topSetupsHint')}
          action={<SectionLink href="/markets" label={t('common.viewAll')} />}
        />
        <TopSetupsTable rows={rows} />
      </section>

      <section className="grid grid-cols-1 gap-8 xl:grid-cols-[1.6fr_1fr]">
        <div className="min-w-0 space-y-4">
          <SectionHeader
            title={t('dashboard.heatmapPreview')}
            hint={t('dashboard.heatmapPreviewHint')}
            action={<SectionLink href="/heatmap" label={t('common.openFull')} />}
          />
          <Heatmap
            compact
            factors={heatmap.factors.map((f) => ({
              code: f.code,
              label: tx(f.shortName, lang),
              name: tx(f.name, lang),
            }))}
            rows={heatmap.rows.map((r) => ({
              symbol: r.symbol,
              name: tx(r.name, lang),
              score: r.score,
              bias: r.bias,
              cells: r.cells.map((c) => ({
                factorCode: c.factorCode,
                n: c.normalizedScore,
                rawLabel: tx(c.rawLabel, lang),
                weight: c.weight,
                contribution: c.contribution,
              })),
            }))}
          />
        </div>

        <div className="min-w-0 space-y-4">
          <SectionHeader
            title={t('dashboard.recentReleases')}
            hint={t('dashboard.recentReleasesHint')}
            action={<SectionLink href="/calendar" label={t('common.viewAll')} />}
          />
          <ReleaseFeed items={releaseItems} />
        </div>
      </section>
    </div>
  );
}
