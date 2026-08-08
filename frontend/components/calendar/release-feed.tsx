import { useTranslations } from 'next-intl';
import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react';
import { cn } from '@/lib/utils';
import { ImpactBars } from '@/components/data/badges';
import type { Bias, Importance } from '@/lib/mock/types';

export interface ReleaseItem {
  id: string;
  flag: string;
  currency: string;
  title: string;
  importance: Importance;
  actual: string;
  forecast: string;
  previous: string;
  biasFor: Bias | null;
  timeLabel: string;
}

const TONE: Record<Bias, { icon: typeof ArrowUpRight; className: string }> = {
  bullish: { icon: ArrowUpRight, className: 'text-pos' },
  bearish: { icon: ArrowDownRight, className: 'text-neg' },
  neutral: { icon: Minus, className: 'text-fg-muted' },
};

export function ReleaseFeed({ items }: { items: ReleaseItem[] }) {
  const t = useTranslations();

  return (
    <div className="divide-y divide-line overflow-hidden rounded-[12px] border border-line bg-surface">
      {items.map((item) => {
        const tone = item.biasFor ? TONE[item.biasFor] : null;
        const Icon = tone?.icon;

        return (
          <div key={item.id} className="p-4 transition-ui hover:bg-surface-2">
            <div className="flex items-start justify-between gap-3">
              <div className="flex min-w-0 items-center gap-2">
                <span className="text-[15px] leading-none" aria-hidden>
                  {item.flag}
                </span>
                <span className="font-mono text-[12px] font-medium text-fg-muted">
                  {item.currency}
                </span>
              </div>
              <ImpactBars importance={item.importance} />
            </div>

            <p className="mt-2 text-[13px] font-medium leading-snug text-fg">{item.title}</p>

            <div className="mt-3 flex flex-wrap items-baseline gap-x-4 gap-y-1">
              <Metric label={t('common.actual')} value={item.actual} strong />
              <Metric label={t('common.forecast')} value={item.forecast} />
              <Metric label={t('common.previous')} value={item.previous} />
            </div>

            <div className="mt-3 flex items-center justify-between gap-3">
              {tone && Icon ? (
                <span className={cn('flex items-center gap-1 text-[12px] font-medium', tone.className)}>
                  <Icon className="size-3.5" aria-hidden />
                  {t('calendar.biasFor', { currency: item.currency })}
                </span>
              ) : (
                <span className="text-[12px] text-fg-subtle">{t('calendar.pending')}</span>
              )}
              <span className="text-[12px] text-fg-subtle">{item.timeLabel}</span>
            </div>
          </div>
        );
      })}
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
