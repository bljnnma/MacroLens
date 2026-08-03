import { useTranslations } from 'next-intl';
import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react';
import { cn } from '@/lib/utils';
import { BIAS_SURFACE } from '@/lib/score';
import type { Bias, FactorCategory, Importance } from '@/lib/mock/types';

const ICON: Record<Bias, typeof ArrowUpRight> = {
  bullish: ArrowUpRight,
  bearish: ArrowDownRight,
  neutral: Minus,
};

/**
 * Sized to content, never fixed-width: "Өсөх хандлагатай" is roughly three
 * times the width of "Bullish", and truncating a bias label is not an option.
 */
export function BiasBadge({
  bias,
  short = false,
  className,
}: {
  bias: Bias;
  short?: boolean;
  className?: string;
}) {
  const t = useTranslations('bias');
  const Icon = ICON[bias];
  const label = short ? t(`${bias}Short`) : t(bias);

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-[6px] px-2 py-1 text-[12px] font-medium leading-none',
        BIAS_SURFACE[bias],
        className
      )}
    >
      <Icon className="size-3 shrink-0" aria-hidden />
      {label}
    </span>
  );
}

/** Factor codes stay Latin monospace in both locales — they are identifiers. */
export function FactorChip({ code, className }: { code: string; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-[5px] border border-line bg-surface-2 px-1.5 py-0.5 font-mono text-[11px] font-medium text-fg-muted',
        className
      )}
    >
      {code}
    </span>
  );
}

const CATEGORY_TONE: Record<FactorCategory, string> = {
  policy: 'text-accent',
  inflation: 'text-warn',
  growth: 'text-pos',
  labour: 'text-fg-muted',
  sentiment: 'text-fg-muted',
  positioning: 'text-fg-subtle',
};

export function CategoryLabel({ category }: { category: FactorCategory }) {
  const t = useTranslations('category');
  return (
    <span className={cn('text-[11px] font-medium uppercase tracking-wider', CATEGORY_TONE[category])}>
      {t(category)}
    </span>
  );
}

/** Bars, not stars: bars read as magnitude, stars read as quality. */
export function ImpactBars({ importance }: { importance: Importance }) {
  const filled = importance === 'high' ? 3 : importance === 'medium' ? 2 : 1;
  const tone =
    importance === 'high' ? 'bg-neg' : importance === 'medium' ? 'bg-warn' : 'bg-fg-subtle';

  return (
    <span className="inline-flex items-end gap-[2px]" aria-label={importance}>
      {[0, 1, 2].map((i) => (
        <span
          key={i}
          className={cn(
            'w-[3px] rounded-[1px]',
            i < filled ? tone : 'bg-surface-3',
            i === 0 ? 'h-1.5' : i === 1 ? 'h-2.5' : 'h-3.5'
          )}
        />
      ))}
    </span>
  );
}

export function LowCoverageFlag({ label }: { label: string }) {
  return (
    <span className="inline-flex items-center gap-1 rounded-[5px] bg-warn/10 px-1.5 py-0.5 text-[11px] font-medium text-warn ring-1 ring-inset ring-warn/25">
      <span aria-hidden>⚠</span>
      {label}
    </span>
  );
}
