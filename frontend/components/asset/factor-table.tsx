'use client';

import { Fragment, useState } from 'react';
import { useTranslations } from 'next-intl';
import { ChevronRight } from 'lucide-react';
import { Link } from '@/i18n/routing';
import { cn } from '@/lib/utils';
import { formatNormalized, formatSigned } from '@/lib/format';
import { contributionColor, heatmapCellClass } from '@/lib/score';
import type { FactorView } from './score-breakdown';

export function FactorTable({
  factors,
  indicatorByFactor,
}: {
  factors: FactorView[];
  indicatorByFactor: Record<string, string | undefined>;
}) {
  const t = useTranslations();
  const [open, setOpen] = useState<string | null>(null);

  return (
    <div className="overflow-hidden rounded-[12px] border border-line bg-surface">
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-left">
          <thead>
            <tr className="border-b border-line">
              <th className="w-8" />
              <Th className="min-w-[150px]">{t('common.factor')}</Th>
              <Th className="hidden min-w-[190px] md:table-cell">{t('common.rawValue')}</Th>
              <Th className="min-w-[90px] text-center">{t('common.normalizedScore')}</Th>
              <Th className="min-w-[70px] text-right">{t('common.weight')}</Th>
              <Th className="min-w-[100px] pr-4 text-right">{t('common.contribution')}</Th>
            </tr>
          </thead>
          <tbody>
            {factors.map((f) => {
              const expanded = open === f.code;
              const indicatorCode = indicatorByFactor[f.code];

              return (
                <Fragment key={f.code}>
                  <tr
                    id={`factor-${f.code}`}
                    className={cn(
                      'group cursor-pointer border-b border-line transition-ui hover:bg-surface-2 target:bg-accent/10',
                      expanded && 'bg-surface-2'
                    )}
                    onClick={() => setOpen(expanded ? null : f.code)}
                  >
                    <td className="pl-3">
                      <ChevronRight
                        className={cn(
                          'size-4 text-fg-subtle transition-transform',
                          expanded && 'rotate-90'
                        )}
                        aria-hidden
                      />
                    </td>
                    <td className="px-3 py-3.5">
                      <div className="flex flex-col gap-0.5">
                        <span className="font-mono text-[12px] font-medium uppercase text-fg-muted">
                          {f.code}
                        </span>
                        <span className="text-[13px] text-fg">{f.name}</span>
                      </div>
                    </td>
                    <td className="hidden px-3 py-3.5 md:table-cell">
                      <span
                        data-numeric
                        className={cn(
                          'text-[13px] tabular-nums',
                          f.available ? 'text-fg-muted' : 'text-fg-subtle italic'
                        )}
                      >
                        {f.rawLabel}
                      </span>
                    </td>
                    <td className="px-3 py-3.5 text-center">
                      <span
                        data-numeric
                        className={cn(
                          'inline-flex h-7 w-9 items-center justify-center rounded-[4px] text-[13px] font-semibold tabular-nums',
                          heatmapCellClass(f.n as never)
                        )}
                      >
                        {formatNormalized(f.n)}
                      </span>
                    </td>
                    <td className="px-3 py-3.5 text-right">
                      <span data-numeric className="text-[13px] tabular-nums text-fg-muted">
                        {f.weight}
                      </span>
                    </td>
                    <td className="px-3 py-3.5 pr-4 text-right">
                      <span
                        data-numeric
                        className={cn(
                          'text-[13px] font-semibold tabular-nums',
                          f.available ? contributionColor(f.contribution) : 'text-fg-subtle'
                        )}
                      >
                        {f.available ? formatSigned(f.contribution) : '—'}
                      </span>
                    </td>
                  </tr>

                  {expanded && (
                    <tr className="border-b border-line bg-surface-2">
                      <td />
                      <td colSpan={5} className="px-3 pb-4 pr-4">
                        <p className="max-w-3xl text-[13px] leading-relaxed text-fg-muted">
                          {f.explanation}
                        </p>

                        {f.readings.length > 0 && <ReadingBreakdown factor={f} />}

                        <div className="mt-3 flex flex-wrap items-center gap-3">
                          {f.weight === 0 && (
                            <span className="rounded-[5px] bg-surface-3 px-2 py-1 text-[11px] text-fg-subtle">
                              {t('indicators.weightRange')}: 0
                            </span>
                          )}
                          {indicatorCode && (
                            <Link
                              href={`/indicators?code=${indicatorCode}`}
                              className="inline-flex items-center gap-1.5 text-[12px] font-medium text-accent hover:underline"
                            >
                              {t('asset.viewIndicator')}
                              <ChevronRight className="size-3" aria-hidden />
                            </Link>
                          )}
                        </div>
                      </td>
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/**
 * The per-currency readings a factor was built from, and the arithmetic that
 * turned them into one number.
 *
 * A pair's cell shows a DIFFERENTIAL. Without this block the row displays the
 * base currency's raw value beside a score derived from both sides, and the two
 * cannot be reconciled — which breaks the one promise the product makes. The
 * formula line is written out rather than implied so the reader can check it.
 */
function ReadingBreakdown({ factor }: { factor: FactorView }) {
  const t = useTranslations();
  const isPair = factor.readings.length === 2;

  const base = factor.readings.find((r) => r.direction === 1);
  const quote = factor.readings.find((r) => r.direction === -1);

  return (
    <div className="mt-4 max-w-3xl rounded-[8px] border border-line bg-surface p-3">
      <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
        {isPair ? t('asset.pairBreakdown') : t('asset.readingBreakdown')}
      </div>

      <div className="mt-2 flex flex-col gap-1.5">
        {factor.readings.map((r) => (
          <div key={r.currency} className="flex items-baseline gap-3">
            <span className="w-11 shrink-0 font-mono text-[12px] font-medium text-fg">
              {r.currency}
            </span>
            <span className="w-[52px] shrink-0 text-[11px] text-fg-subtle">
              {isPair ? t(r.direction === 1 ? 'asset.baseSide' : 'asset.quoteSide') : ''}
            </span>
            <span data-numeric className="min-w-0 flex-1 text-[12px] tabular-nums text-fg-muted">
              {r.label}
            </span>
            <span
              data-numeric
              className={cn(
                'inline-flex h-6 w-8 shrink-0 items-center justify-center rounded-[4px] text-[12px] font-semibold tabular-nums',
                heatmapCellClass(r.n as never)
              )}
            >
              {formatNormalized(r.n)}
            </span>
          </div>
        ))}
      </div>

      {/* The computation is spelled out for BOTH shapes. A single reading still
          needs it: a USD-scoped factor on a pair where the dollar is the quote
          currency flips sign, so the reading shows -1 while the row shows +1 and
          only prose would connect them. */}
      <div className="mt-2.5 flex items-baseline gap-3 border-t border-line pt-2.5">
        <span data-numeric className="min-w-0 flex-1 text-[12px] tabular-nums text-fg-subtle">
          {isPair && base && quote
            ? `(${formatNormalized(base.n)} − ${formatNormalized(quote.n)}) ÷ 2`
            : `${formatNormalized(factor.readings[0].n)} × ${t('asset.exposureDirection')} (${
                factor.readings[0].direction === 1 ? '+1' : '−1'
              })`}
          {factor.polarity === -1 && ` × ${t('asset.polarity')} (−1)`}
        </span>
        <span
          data-numeric
          className={cn(
            'inline-flex h-6 w-8 shrink-0 items-center justify-center rounded-[4px] text-[12px] font-semibold tabular-nums',
            heatmapCellClass(factor.n as never)
          )}
        >
          {formatNormalized(factor.n)}
        </span>
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
