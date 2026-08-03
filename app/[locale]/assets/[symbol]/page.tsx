import { notFound } from 'next/navigation';
import { getTranslations, setRequestLocale } from 'next-intl/server';
import { ArrowLeft } from 'lucide-react';
import { Link } from '@/i18n/routing';
import {
  getAssetScore,
  getReferenceTime,
  getScoreHistory,
  getFactors,
} from '@/lib/mock';
import { INDICATOR_BY_FACTOR } from '@/lib/mock/indicators';
import { t as tx } from '@/lib/localized';
import { formatPercent, formatRelativeTime, formatDateInZone } from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { SectionHeader } from '@/components/data/section';
import { BiasBadge, LowCoverageFlag } from '@/components/data/badges';
import { ScoreBar, CoverageBadge } from '@/components/data/score-badge';
import { ScoreBreakdown, type FactorView } from '@/components/asset/score-breakdown';
import { FactorTable } from '@/components/asset/factor-table';
import { ScoreHistoryChart } from '@/components/asset/score-history-chart';
import { cn } from '@/lib/utils';

export default async function AssetDetailPage({
  params,
}: {
  params: Promise<{ locale: string; symbol: string }>;
}) {
  const { locale, symbol } = await params;
  setRequestLocale(locale);
  const t = await getTranslations();
  const lang = locale as Locale;

  const score = getAssetScore(symbol);
  if (!score) notFound();

  const now = getReferenceTime();
  const factorMeta = new Map(getFactors().map((f) => [f.code, f]));

  const factors: FactorView[] = score.factors.map((f) => {
    const meta = factorMeta.get(f.factorCode)!;
    return {
      code: f.factorCode,
      name: tx(meta.name, lang),
      shortName: tx(meta.shortName, lang),
      category: meta.category,
      rawLabel: tx(f.rawLabel, lang),
      n: f.normalizedScore,
      weight: f.weight,
      contribution: f.contribution,
      explanation: tx(f.explanation, lang),
      available: f.available,
    };
  });

  const history = getScoreHistory(symbol, 90).map((p) => ({
    date: p.date,
    label: p.date.slice(5).replace('-', '/'),
    score: p.score,
  }));

  const indicatorByFactor = Object.fromEntries(
    getFactors().map((f) => [f.code, INDICATOR_BY_FACTOR.get(f.code)?.code])
  );

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
              <span className="text-[15px] text-fg-muted">{tx(score.name, lang)}</span>
            </div>
            <p className="mt-1.5 text-[13px] text-fg-subtle">{t(`market.${score.market}`)}</p>
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

      {/* Score, bias, coverage and freshness are only meaningful together — a 91
          at 41% coverage is a different claim from a 91 at 94%. */}
      <section className="grid grid-cols-2 gap-4 xl:grid-cols-4">
        <div className="rounded-[12px] border border-line bg-surface p-5">
          <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            {t('common.score')}
          </div>
          <div
            data-numeric
            className={cn(
              'mt-3 text-[32px] font-bold leading-none tabular-nums tracking-[-0.03em]',
              score.bias === 'bullish'
                ? 'text-pos'
                : score.bias === 'bearish'
                  ? 'text-neg'
                  : 'text-fg'
            )}
          >
            {score.score.toFixed(1)}
          </div>
          <ScoreBar score={score.score} bias={score.bias} className="mt-3" width={110} />
        </div>

        <div className="rounded-[12px] border border-line bg-surface p-5">
          <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            {t('common.bias')}
          </div>
          <div className="mt-3">
            <BiasBadge bias={score.bias} />
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
            <CoverageBadge coverage={score.coverage} isSufficient={score.isSufficient} />
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
        <SectionHeader
          title={t('asset.howScoreFormed')}
          hint={t('asset.howScoreFormedHint')}
        />
        <ScoreBreakdown factors={factors} score={score.score} baseScore={score.baseScore} />
      </section>

      <section className="space-y-4">
        <SectionHeader title={t('asset.breakdown')} hint={t('asset.breakdownHint')} />
        <FactorTable factors={factors} indicatorByFactor={indicatorByFactor} />
      </section>

      <section className="space-y-4">
        <SectionHeader title={t('asset.history')} hint={t('asset.historyHint')} />
        <ScoreHistoryChart points={history} />
      </section>
    </div>
  );
}
