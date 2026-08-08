import { notFound } from 'next/navigation';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { ArrowLeft } from 'lucide-react';
import { Link } from '@/i18n/routing';
import { fetchAsset, fetchAssetHistory, fetchIndicators } from '@/lib/data';
import { formatPercent, formatRelativeTime, formatDateInZone } from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { SectionHeader } from '@/components/data/section';
import { DataSourceBadge, LowCoverageFlag } from '@/components/data/badges';
import { CoverageBadge } from '@/components/data/score-badge';
import { BiasGauge } from '@/components/data/bias-gauge';
import { ScoreBreakdown, type FactorView } from '@/components/asset/score-breakdown';
import { FactorTable } from '@/components/asset/factor-table';
import { ScoreHistoryChart } from '@/components/asset/score-history-chart';

export default async function AssetDetailPage({
  params,
}: {
  params: Promise<{ locale: string; symbol: string }>;
}) {
  const { locale, symbol } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const score = await fetchAsset(lang, symbol);
  if (!score) notFound();

  const [history, indicators] = await Promise.all([
    fetchAssetHistory(lang, symbol, 90),
    fetchIndicators(lang),
  ]);

  const now = new Date();

  const factors: FactorView[] = score.factors.map((f) => ({
    code: f.factorCode,
    name: f.factorName,
    shortName: f.factorCode,
    category: f.category,
    rawLabel: f.rawLabel,
    n: f.normalizedScore,
    weight: f.weight,
    polarity: f.polarity,
    contribution: f.contribution,
    explanation: f.explanation,
    available: f.available,
    readings: f.readings.map((r) => ({
      currency: r.currency,
      direction: r.direction,
      n: r.normalizedScore,
      label: r.label,
    })),
  }));

  const points = history.map((p) => ({
    date: p.date,
    label: p.date.slice(5, 10).replace('-', '/'),
    score: p.score,
  }));

  // First indicator per factor is the canonical one to link to.
  const indicatorByFactor: Record<string, string | undefined> = {};
  for (const indicator of indicators) {
    indicatorByFactor[indicator.factorCode] ??= indicator.code;
  }

  return (
    <div className="space-y-12">
      <div>
        <Link
          href="/"
          className="inline-flex items-center gap-1.5 rounded-md text-[13px] text-fg-muted transition-ui hover:text-fg"
        >
          <ArrowLeft className="size-3.5" aria-hidden />
          {t('common.back')}
        </Link>

        <div className="mt-5 flex flex-wrap items-start justify-between gap-6">
          <div className="min-w-0">
            <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
              <h1 className="font-mono text-2xl font-semibold tracking-tight text-fg">
                {score.symbol}
              </h1>
              <span className="text-[15px] text-fg-muted">{score.name}</span>
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <span className="text-[13px] text-fg-subtle">{t(`market.${score.market}`)}</span>
              <DataSourceBadge
                isFullyReal={score.isFullyReal}
                realShare={score.realShare}
                realLabel={t('provenance.real')}
                hybridLabel={t('provenance.hybrid')}
                title={score.isFullyReal ? t('provenance.realHint') : t('provenance.hybridHint')}
              />
            </div>
          </div>

          <dl className="grid grid-cols-2 gap-x-8 gap-y-1 text-right text-[12px]">
            <dt className="text-fg-subtle">{t('asset.scoringProfile')}</dt>
            <dd className="text-fg-muted">
              {score.profileName} v{score.profileVersion}
            </dd>
            <dt className="text-fg-subtle">{t('asset.engineVersion')}</dt>
            <dd className="font-mono text-fg-muted">v{score.engineVersion}</dd>
          </dl>
        </div>
      </div>

      <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        {/* Score and bias are one reading, so they share one card: the needle
            position IS the bias, and splitting them made the reader do the join. */}
        <div className="col-span-2 flex flex-col rounded-[12px] border border-line bg-surface p-5">
          <div className="flex items-center justify-between gap-3">
            <span className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
              {t('common.score')}
            </span>
            <span className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
              {t('common.bias')}
            </span>
          </div>
          <div className="flex flex-1 items-center justify-center pt-2">
            <BiasGauge score={score.score} bias={score.bias} />
          </div>
        </div>

        <div className="rounded-[12px] border border-line bg-surface p-5">
          <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            {t('common.coverage')}
          </div>
          <div data-numeric className="mt-3 text-[22px] font-semibold tabular-nums text-fg">
            {formatPercent(score.coverage)}
          </div>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <CoverageBadge
              coverage={score.coverage}
              isSufficient={score.isSufficient}
              showPercent={false}
            />
            {!score.isSufficient && <LowCoverageFlag label={t('states.lowCoverageShort')} />}
          </div>
        </div>

        <div className="rounded-[12px] border border-line bg-surface p-5">
          <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            {t('common.lastUpdated')}
          </div>
          <div className="mt-3 text-[16px] font-medium text-fg">
            {formatRelativeTime(score.dataAsOf, now, lang)}
          </div>
          <div data-numeric className="mt-2 text-[12px] tabular-nums text-fg-subtle">
            {formatDateInZone(score.dataAsOf, lang)}
          </div>
        </div>
      </section>

      {!score.isSufficient && (
        <div className="rounded-[12px] border border-warn/30 bg-warn/5 px-5 py-4 text-[13px] text-warn">
          {t('states.lowCoverage')}
        </div>
      )}

      <section className="space-y-4">
        <SectionHeader title={t('asset.howScoreFormed')} hint={t('asset.howScoreFormedHint')} />
        <ScoreBreakdown factors={factors} score={score.score} baseScore={score.baseScore} />
      </section>

      <section className="space-y-4">
        <SectionHeader title={t('asset.breakdown')} hint={t('asset.breakdownHint')} />
        <FactorTable factors={factors} indicatorByFactor={indicatorByFactor} />
      </section>

      {points.length > 1 && (
        <section className="space-y-4">
          <SectionHeader title={t('asset.history')} hint={t('asset.historyHint')} />
          <ScoreHistoryChart points={points} />
        </section>
      )}
    </div>
  );
}
