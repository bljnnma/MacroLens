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
