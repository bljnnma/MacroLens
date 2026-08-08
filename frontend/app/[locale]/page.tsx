import { getTranslations, setRequestLocale } from 'next-intl/server';
import { RefreshCw, TrendingDown, TrendingUp, Activity, Layers } from 'lucide-react';
import {
  fetchHeatmap,
  fetchMarketSnapshot,
  fetchRecentReleases,
  fetchTopSetups,
} from '@/lib/data';
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

  // One round trip each, in parallel — the dashboard's four blocks are
  // independent reads.
  const [snapshot, setups, heatmap, releases] = await Promise.all([
    fetchMarketSnapshot(lang),
    fetchTopSetups(lang, 8),
    fetchHeatmap(lang, 6),
    fetchRecentReleases(lang, 4),
  ]);

  const now = new Date();

  const rows: SetupRow[] = setups.map((s) => ({
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

  const releaseItems: ReleaseItem[] = releases.map((r) => ({
    id: r.id,
    flag: r.flag,
    currency: r.currency,
    title: r.title,
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
          // Derived, not hardcoded. This read "8/9 factors" from the prototype
          // era and survived two profile versions, contradicting the 100% it
          // sits under.
          detail={t('dashboard.fullyCovered', {
            count: snapshot.assetsAtFullCoverage,
            total: snapshot.assetCount,
          })}
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
              label: f.shortName,
              name: f.name,
            }))}
            rows={heatmap.rows.map((r) => ({
              symbol: r.symbol,
              name: r.name,
              score: r.score,
              bias: r.bias,
              cells: r.cells.map((c) => ({
                factorCode: c.factorCode,
                n: c.normalizedScore as never,
                rawLabel: c.rawLabel,
                weight: c.weight,
                contribution: c.contribution,
                inProfile: c.inProfile,
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
