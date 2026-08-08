'use client';

import { useTranslations } from 'next-intl';
import { cn } from '@/lib/utils';
import { formatUbMinutes, MINUTES_PER_DAY, type SessionId, type SessionState } from '@/lib/sessions';

export interface TimelineRow {
  id: SessionId;
  label: string;
  openUb: number;
  closeUb: number;
  wraps: boolean;
  state: SessionState;
}

export interface TimelineOverlap {
  from: number;
  to: number;
  label: string;
  major: boolean;
}

const pct = (minutes: number) => (minutes / MINUTES_PER_DAY) * 100;
const HOURS = [0, 3, 6, 9, 12, 15, 18, 21];

/** Label column (76px) + gap-3 (12px). Overlays start here so every layer —
 *  grid, overlaps, bars, playhead — shares one 0–1440 scale. */
const TRACK_OFFSET = 88;

export function SessionTimeline({
  rows,
  overlaps,
  nowMinutes,
}: {
  rows: TimelineRow[];
  overlaps: TimelineOverlap[];
  nowMinutes: number;
}) {
  const t = useTranslations('sessions');

  return (
    <div className="rounded-[12px] border border-line bg-surface p-5 sm:p-6">
      <div className="flex items-center justify-between gap-4 pb-4">
        <span className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
          {t('timelineTitle')}
        </span>
        <span className="text-[11px] uppercase tracking-wider text-fg-subtle">UB · UTC+8</span>
      </div>

      <div className="relative">
        <div
          className="pointer-events-none absolute inset-y-0 right-0 z-0"
          style={{ left: TRACK_OFFSET }}
        >
          {HOURS.map((h) => (
            <div
              key={h}
              className="absolute top-0 h-full w-px bg-line"
              style={{ left: `${pct(h * 60)}%` }}
            />
          ))}
          <div className="absolute right-0 top-0 h-full w-px bg-line" />

          {/* Overlap bands sit behind the bars — context for the rows, not
              another row competing with them. */}
          {overlaps.map((o) => (
            <div
              key={`${o.from}-${o.to}`}
              className={cn('absolute top-0 h-full', o.major ? 'bg-accent/15' : 'bg-accent/5')}
              style={{ left: `${pct(o.from)}%`, width: `${pct(o.to - o.from)}%` }}
            />
          ))}
        </div>

        <div className="relative z-10 space-y-2">
          {rows.map((row) => (
            <div key={row.id} className="flex items-center gap-3">
              <span
                className={cn(
                  'w-[76px] shrink-0 truncate text-[12px] font-medium',
                  row.state === 'open' ? 'text-fg' : 'text-fg-subtle'
                )}
              >
                {row.label}
              </span>

              <div className="relative h-8 flex-1 rounded-md bg-surface-2">
                {segmentsFor(row).map((seg, i) => (
                  <div
                    key={i}
                    className={cn(
                      'absolute inset-y-0 flex items-center justify-center overflow-hidden rounded-md text-[10px] font-medium transition-ui',
                      row.state === 'open'
                        ? 'bg-accent/70 text-white'
                        : 'bg-surface-3 text-fg-subtle'
                    )}
                    style={{ left: `${pct(seg.from)}%`, width: `${pct(seg.to - seg.from)}%` }}
                  >
                    {seg.to - seg.from > 170 && (
                      <span data-numeric className="truncate px-1 tabular-nums">
                        {formatUbMinutes(seg.from)}–{formatUbMinutes(seg.to)}
                      </span>
                    )}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>

        <div
          className="pointer-events-none absolute inset-y-0 right-0 z-20"
          style={{ left: TRACK_OFFSET }}
        >
          <div
            className="absolute inset-y-0 w-px bg-fg"
            style={{ left: `${pct(nowMinutes)}%` }}
            aria-hidden
          />
        </div>
      </div>

      <div className="relative mt-2 h-4" style={{ marginLeft: TRACK_OFFSET }}>
        {HOURS.map((h) => (
          <span
            key={h}
            data-numeric
            className="absolute top-0 -translate-x-1/2 text-[10px] tabular-nums text-fg-subtle"
            style={{ left: `${pct(h * 60)}%` }}
          >
            {String(h).padStart(2, '0')}
          </span>
        ))}
        <span
          data-numeric
          className="absolute right-0 top-0 text-[10px] tabular-nums text-fg-subtle"
        >
          24
        </span>
      </div>

      <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-2 border-t border-line pt-4 text-[11px] text-fg-subtle">
        <Legend swatch="bg-accent/70" label={t('open')} />
        <Legend swatch="bg-surface-3" label={t('closed')} />
        <Legend swatch="bg-accent/15" label={t('overlapLegend')} />
        <span className="flex items-center gap-1.5">
          <span className="h-3 w-px bg-fg" />
          {t('nowLegend')}
        </span>
      </div>
    </div>
  );
}

function Legend({ swatch, label }: { swatch: string; label: string }) {
  return (
    <span className="flex items-center gap-1.5">
      <span className={cn('h-2.5 w-4 rounded-sm', swatch)} />
      {label}
    </span>
  );
}

/** A window past UB midnight renders as two segments, not one wrapped bar. */
function segmentsFor(row: TimelineRow): { from: number; to: number }[] {
  if (!row.wraps) return [{ from: row.openUb, to: row.closeUb }];
  return [
    { from: row.openUb, to: MINUTES_PER_DAY },
    { from: 0, to: row.closeUb - MINUTES_PER_DAY },
  ];
}
