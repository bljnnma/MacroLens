'use client';

import { Fragment, useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Link } from '@/i18n/routing';
import { cn } from '@/lib/utils';
import { ScoreBadge, CoverageBadge } from '@/components/data/score-badge';
import { BiasBadge, LowCoverageFlag } from '@/components/data/badges';
import { BASE_SCORE } from '@/lib/score';
import type { Bias, Market } from '@/lib/mock/types';

export interface SetupRow {
  rank: number;
  symbol: string;
  name: string;
  market: Market;
  score: number;
  bias: Bias;
  coverage: number;
  isSufficient: boolean;
  updatedLabel: string;
}

const MARKET_TABS: (Market | 'all')[] = ['all', 'forex', 'metals', 'indices'];

export function TopSetupsTable({
  rows,
  showFilters = true,
  compact = false,
}: {
  rows: SetupRow[];
  showFilters?: boolean;
  compact?: boolean;
}) {
  const t = useTranslations();
  const [market, setMarket] = useState<Market | 'all'>('all');

  const visible = useMemo(
    () =>
      rows.filter((r) =>
        market === 'all'
          ? true
          : market === 'indices'
            ? r.market === 'indices' || r.market === 'dollarIndex'
            : r.market === market
      ),
    [rows, market]
  );

  // The neutral line is where the model's central concept becomes visible:
  // 50 is not a sort position, it is the boundary the score is measured from.
  const dividerIndex = visible.findIndex((r) => r.score < BASE_SCORE);

  return (
    <div className="overflow-hidden rounded-[12px] border border-line bg-surface">
      {showFilters && (
        <div className="sticky top-14 z-20 flex flex-wrap items-center gap-2 border-b border-line bg-surface/95 px-4 py-3 backdrop-blur-sm">
          {MARKET_TABS.map((m) => (
            <button
              key={m}
              type="button"
              onClick={() => setMarket(m)}
              className={cn(
                'rounded-lg px-3 py-1.5 text-[13px] font-medium transition-ui',
                market === m
                  ? 'bg-accent/15 text-accent ring-1 ring-inset ring-accent/30'
                  : 'text-fg-muted hover:bg-surface-2 hover:text-fg'
              )}
            >
              {m === 'all' ? t('market.all') : t(`market.${m}`)}
            </button>
          ))}
          <span data-numeric className="ml-auto text-[12px] tabular-nums text-fg-subtle">
            {t('markets.resultCount', { count: visible.length })}
          </span>
        </div>
      )}

      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-left">
          <thead>
            <tr className="border-b border-line">
              <Th className="w-10 pl-4 text-right">#</Th>
              <Th className="min-w-[180px]">{t('common.asset')}</Th>
              <Th className="hidden min-w-[110px] md:table-cell">{t('common.market')}</Th>
              <Th className="min-w-[150px]">{t('common.score')}</Th>
              <Th className="min-w-[140px]">{t('common.bias')}</Th>
              <Th className="hidden min-w-[110px] lg:table-cell">{t('common.coverage')}</Th>
              <Th className="hidden min-w-[110px] pr-4 text-right sm:table-cell">
                {t('common.lastUpdated')}
              </Th>
            </tr>
          </thead>
          <tbody>
            {visible.map((row, i) => (
              <Fragment key={row.symbol}>
                {i === dividerIndex && dividerIndex > 0 && (
                  <tr aria-hidden>
                    <td colSpan={7} className="p-0">
                      <div className="flex items-center gap-3 px-4 py-1.5">
                        <span className="h-px flex-1 border-t border-dashed border-line-strong" />
                        <span
                          data-numeric
                          className="shrink-0 text-[11px] font-medium uppercase tracking-wider text-fg-subtle"
                        >
                          50 · {t('common.neutralLine')}
                        </span>
                        <span className="h-px flex-1 border-t border-dashed border-line-strong" />
                      </div>
                    </td>
                  </tr>
                )}
                <tr className="group border-b border-line last:border-0">
                  <Td className={cn('pl-4 text-right', compact ? 'py-2.5' : 'py-3.5')}>
                    <span data-numeric className="text-[13px] tabular-nums text-fg-subtle">
                      {row.rank}
                    </span>
                  </Td>
                  <Td className={compact ? 'py-2.5' : 'py-3.5'}>
                    <Link
                      href={`/assets/${row.symbol.toLowerCase()}`}
                      className="flex flex-col gap-0.5 rounded-md outline-none"
                    >
                      <span className="font-mono text-[13px] font-medium text-fg transition-ui group-hover:text-accent">
                        {row.symbol}
                      </span>
                      <span className="truncate text-[12px] text-fg-muted">{row.name}</span>
                    </Link>
                  </Td>
                  <Td className={cn('hidden md:table-cell', compact ? 'py-2.5' : 'py-3.5')}>
                    <span className="text-[13px] text-fg-muted">{t(`market.${row.market}`)}</span>
                  </Td>
                  <Td className={compact ? 'py-2.5' : 'py-3.5'}>
                    <ScoreBadge
                      score={row.score}
                      bias={row.bias}
                      size="md"
                      muted={!row.isSufficient}
                    />
                  </Td>
                  <Td className={compact ? 'py-2.5' : 'py-3.5'}>
                    <BiasBadge bias={row.bias} />
                  </Td>
                  <Td className={cn('hidden lg:table-cell', compact ? 'py-2.5' : 'py-3.5')}>
                    <span className="flex items-center gap-2">
                      <CoverageBadge
                        coverage={row.coverage}
                        isSufficient={row.isSufficient}
                      />
                      {!row.isSufficient && (
                        <LowCoverageFlag label={t('states.lowCoverageShort')} />
                      )}
                    </span>
                  </Td>
                  <Td
                    className={cn(
                      'hidden pr-4 text-right sm:table-cell',
                      compact ? 'py-2.5' : 'py-3.5'
                    )}
                  >
                    <span className="text-[13px] text-fg-muted">{row.updatedLabel}</span>
                  </Td>
                </tr>
              </Fragment>
            ))}
          </tbody>
        </table>

        {visible.length === 0 && (
          <div className="px-4 py-16 text-center">
            <p className="text-[14px] text-fg-muted">{t('markets.empty')}</p>
          </div>
        )}
      </div>
    </div>
  );
}

function Th({ children, className }: { children?: React.ReactNode; className?: string }) {
  return (
    <th
      className={cn(
        'px-3 py-2.5 text-[11px] font-medium uppercase tracking-wider text-fg-subtle',
        className
      )}
    >
      {children}
    </th>
  );
}

function Td({ children, className }: { children?: React.ReactNode; className?: string }) {
  return (
    <td
      className={cn('px-3 align-middle transition-ui group-hover:bg-surface-2', className)}
    >
      {children}
    </td>
  );
}
