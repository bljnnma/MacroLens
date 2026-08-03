import { useTranslations } from 'next-intl';
import { ArrowDownRight, ArrowUpRight, Clock, Minus } from 'lucide-react';
import { cn } from '@/lib/utils';
import { ImpactBars } from '@/components/data/badges';
import type { Bias, Importance } from '@/lib/mock/types';

export interface CalendarEventView {
  id: string;
  timeLabel: string;
  originLabel: string;
  flag: string;
  currency: string;
  title: string;
  importance: Importance;
  actual: string | null;
  forecast: string;
  previous: string;
  biasFor: Bias | null;
  countdownLabel: string | null;
}

export interface CalendarDayView {
  key: string;
  heading: string;
  dateLabel: string;
  weekday: string;
  events: CalendarEventView[];
}

const TONE: Record<Bias, { icon: typeof ArrowUpRight; className: string }> = {
  bullish: { icon: ArrowUpRight, className: 'text-pos' },
  bearish: { icon: ArrowDownRight, className: 'text-neg' },
  neutral: { icon: Minus, className: 'text-fg-muted' },
};

export function CalendarList({ days }: { days: CalendarDayView[] }) {
  const t = useTranslations();

  return (
    <div className="space-y-10">
      {days.map((day) => (
        <section key={day.key}>
          <div className="sticky top-14 z-20 -mx-1 flex flex-wrap items-baseline gap-x-3 gap-y-1 bg-canvas/95 px-1 py-3 backdrop-blur-sm">
            <h2 className="text-[13px] font-semibold uppercase tracking-wider text-fg">
              {day.heading}
            </h2>
            <span data-numeric className="text-[13px] tabular-nums text-fg-muted">
              {day.dateLabel}
            </span>
            {day.weekday && <span className="text-[13px] text-fg-subtle">{day.weekday}</span>}
            <span className="ml-auto text-[12px] text-fg-subtle">
              {t('calendar.eventCount', { count: day.events.length })}
            </span>
          </div>

          <div className="divide-y divide-line overflow-hidden rounded-[12px] border border-line bg-surface">
            {day.events.map((e) => {
              const tone = e.biasFor ? TONE[e.biasFor] : null;
              const Icon = tone?.icon;

              return (
                <div
                  key={e.id}
                  className="flex flex-wrap items-start gap-x-5 gap-y-3 p-4 transition-ui hover:bg-surface-2 sm:flex-nowrap"
                >
                  {/* Times are Ulaanbaatar, labelled. Every release here is
                      published in NY/London/Frankfurt — an unlabelled clock at
                      UTC+8 is a dangerous calendar, not just a confusing one. */}
                  <div className="w-16 shrink-0">
                    <div data-numeric className="text-[14px] font-semibold tabular-nums text-fg">
                      {e.timeLabel}
                    </div>
                    <div className="text-[10px] font-medium uppercase tracking-wider text-fg-subtle">
                      UB
                    </div>
                  </div>

                  <div className="flex w-20 shrink-0 items-center gap-2">
                    <span className="text-[15px] leading-none" aria-hidden>
                      {e.flag}
                    </span>
                    <span className="font-mono text-[12px] font-medium text-fg-muted">
                      {e.currency}
                    </span>
                  </div>

                  <div className="shrink-0 pt-0.5">
                    <ImpactBars importance={e.importance} />
                  </div>

                  <div className="min-w-[200px] flex-1">
                    <p className="text-[13px] font-medium leading-snug text-fg">{e.title}</p>
                    <div className="mt-2 flex flex-wrap items-baseline gap-x-4 gap-y-1">
                      <Metric
                        label={t('common.actual')}
                        value={e.actual ?? '—'}
                        strong={e.actual !== null}
                      />
                      <Metric label={t('common.forecast')} value={e.forecast} />
                      <Metric label={t('common.previous')} value={e.previous} />
                    </div>
                  </div>

                  <div className="shrink-0 self-center text-right">
                    {tone && Icon ? (
                      <span
                        className={cn(
                          'inline-flex items-center gap-1 text-[12px] font-medium',
                          tone.className
                        )}
                      >
                        <Icon className="size-3.5" aria-hidden />
                        {t('calendar.biasFor', { currency: e.currency })}
                      </span>
                    ) : e.countdownLabel ? (
                      <span
                        data-numeric
                        className="inline-flex items-center gap-1.5 rounded-md bg-surface-3 px-2 py-1 text-[12px] tabular-nums text-fg-muted"
                      >
                        <Clock className="size-3" aria-hidden />
                        {e.countdownLabel}
                      </span>
                    ) : (
                      <span className="text-[12px] text-fg-subtle">{t('calendar.pending')}</span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </section>
      ))}
    </div>
  );
}

function Metric({ label, value, strong }: { label: string; value: string; strong?: boolean }) {
  return (
    <span className="flex items-baseline gap-1.5">
      <span className="text-[11px] uppercase tracking-wider text-fg-subtle">{label}</span>
      <span
        data-numeric
        className={cn(
          'text-[13px] tabular-nums',
          strong ? 'font-semibold text-fg' : 'text-fg-muted'
        )}
      >
        {value}
      </span>
    </span>
  );
}
