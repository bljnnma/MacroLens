'use client';

import { useState } from 'react';
import { useTranslations } from 'next-intl';
import { useRouter } from '@/i18n/routing';
import { cn } from '@/lib/utils';
import { heatmapCellClass } from '@/lib/score';
import { formatNormalized, formatSigned, formatScore } from '@/lib/format';
import type { Bias, NormalizedScore } from '@/lib/mock/types';

export interface HeatmapFactorView {
  code: string;
  label: string;
  name: string;
}

export interface HeatmapCellView {
  factorCode: string;
  n: NormalizedScore | null;
  rawLabel: string;
  weight: number;
  contribution: number;
  inProfile: boolean;
}

export interface HeatmapRowView {
  symbol: string;
  name: string;
  score: number;
  bias: Bias;
  cells: HeatmapCellView[];
}

const SCORE_TONE: Record<Bias, string> = {
  bullish: 'text-pos',
  bearish: 'text-neg',
  neutral: 'text-fg-muted',
};

export function Heatmap({
  factors,
  rows,
  showScoreColumn = true,
  compact = false,
}: {
  factors: HeatmapFactorView[];
  rows: HeatmapRowView[];
  showScoreColumn?: boolean;
  compact?: boolean;
}) {
  const t = useTranslations();
  const router = useRouter();
  // Reading a matrix cell means answering "which row, which column" at once.
  // At 10x9 the eye loses its place constantly; the crosshair is what fixes it.
  const [hover, setHover] = useState<{ row: string; col: string } | null>(null);

  const cellW = compact ? 'min-w-[64px]' : 'min-w-[84px]';
  const cellH = compact ? 'h-10' : 'h-12';

  return (
    <div className="overflow-hidden rounded-[12px] border border-line bg-surface">
      <div className="overflow-x-auto">
        <table className="w-full border-separate border-spacing-0 text-left">
          <thead>
            <tr>
              <th
                className={cn(
                  'sticky left-0 z-20 min-w-[140px] border-b border-r border-line-strong bg-surface px-4 py-2.5',
                  'text-[11px] font-medium uppercase tracking-wider text-fg-subtle'
                )}
              >
                {t('common.asset')}
              </th>
              {factors.map((f) => (
                <th
                  key={f.code}
                  title={f.name}
                  className={cn(
                    'border-b border-line bg-surface px-2 py-2.5 text-center transition-ui',
                    cellW,
                    hover?.col === f.code && 'bg-surface-2'
                  )}
                >
                  <span className="font-mono text-[11px] font-medium uppercase tracking-wide text-fg-subtle">
                    {f.code}
                  </span>
                </th>
              ))}
              {showScoreColumn && (
                <th className="sticky right-0 z-20 min-w-[92px] border-b border-l border-line-strong bg-surface px-4 py-2.5 text-right text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
                  {t('common.score')}
                </th>
              )}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.symbol}>
                <th
                  scope="row"
                  className={cn(
                    'sticky left-0 z-10 border-b border-r border-line-strong bg-surface px-4 text-left transition-ui',
                    cellH,
                    hover?.row === row.symbol && 'bg-surface-2'
                  )}
                  onMouseEnter={() => setHover({ row: row.symbol, col: '' })}
                >
                  <button
                    type="button"
                    onClick={() => router.push(`/assets/${row.symbol.toLowerCase()}`)}
                    className="flex flex-col items-start gap-0.5 text-left"
                  >
                    <span className="font-mono text-[13px] font-medium text-fg hover:text-accent">
                      {row.symbol}
                    </span>
                    {!compact && (
                      <span className="max-w-[120px] truncate text-[11px] text-fg-muted">
                        {row.name}
                      </span>
                    )}
                  </button>
                </th>

                {factors.map((f) => {
                  const cell = row.cells.find((c) => c.factorCode === f.code);
                  const n = cell?.n ?? null;
                  const active = hover?.row === row.symbol || hover?.col === f.code;
                  const outOfProfile = cell ? !cell.inProfile : true;

                  // Three distinct states, three distinct glyphs:
                  //   +2/-1/0  scored
                  //   ·        not modelled by this market's profile
                  //   ——       modelled, but data missing or stale
                  const glyph = outOfProfile ? '·' : formatNormalized(n);
                  const title = outOfProfile
                    ? `${f.name} — ${t('heatmap.notInProfile')}`
                    : n === null
                      ? `${f.name} — ${t('heatmap.unavailable')}`
                      : `${f.name} · ${row.symbol}\n${cell?.rawLabel ?? ''}\n${t('common.normalizedScore')} ${formatNormalized(n)} · ${t('common.weight')} ${cell?.weight ?? 0} · ${t('common.contribution')} ${formatSigned(cell?.contribution ?? 0)}`;

                  return (
                    <td
                      key={f.code}
                      className={cn('border-b border-line p-1', cellW)}
                      onMouseEnter={() => setHover({ row: row.symbol, col: f.code })}
                      onMouseLeave={() => setHover(null)}
                    >
                      <button
                        type="button"
                        onClick={() =>
                          router.push(`/assets/${row.symbol.toLowerCase()}#factor-${f.code}`)
                        }
                        title={title}
                        className={cn(
                          'flex w-full items-center justify-center rounded-[4px] text-[13px] font-semibold tabular-nums transition-ui',
                          compact ? 'h-8' : 'h-10',
                          outOfProfile
                            ? 'bg-transparent text-fg-subtle/40'
                            : heatmapCellClass(n),
                          active && 'ring-1 ring-inset ring-white/20',
                          (n === null || outOfProfile) && 'font-normal'
                        )}
                        data-numeric
                      >
                        {glyph}
                      </button>
                    </td>
                  );
                })}

                {showScoreColumn && (
                  <td
                    className={cn(
                      'sticky right-0 z-10 border-b border-l border-line-strong bg-surface px-4 text-right transition-ui',
                      hover?.row === row.symbol && 'bg-surface-2'
                    )}
                  >
                    <span
                      data-numeric
                      className={cn('text-[14px] font-semibold tabular-nums', SCORE_TONE[row.bias])}
                    >
                      {formatScore(row.score)}
                    </span>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

export function HeatmapLegend() {
  const t = useTranslations();
  const steps: NormalizedScore[] = [-2, -1, 0, 1, 2];

  return (
    <div className="flex flex-wrap items-center gap-4 px-1 pt-4 text-[12px] text-fg-subtle">
      <span className="uppercase tracking-wider">{t('heatmap.legend')}</span>
      <span className="flex items-center gap-1.5">
        <span>{t('heatmap.strongNegative')}</span>
        {steps.map((s) => (
          <span
            key={s}
            data-numeric
            className={cn(
              'flex size-6 items-center justify-center rounded-[4px] text-[11px] font-semibold tabular-nums',
              heatmapCellClass(s)
            )}
          >
            {formatNormalized(s)}
          </span>
        ))}
        <span>{t('heatmap.strongPositive')}</span>
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex size-6 items-center justify-center rounded-[4px] bg-surface text-[11px] text-fg-subtle">
          ——
        </span>
        {t('heatmap.unavailable')}
      </span>
      <span className="flex items-center gap-1.5">
        <span className="flex size-6 items-center justify-center rounded-[4px] text-[11px] text-fg-subtle/40">
          ·
        </span>
        {t('heatmap.notInProfile')}
      </span>
    </div>
  );
}
