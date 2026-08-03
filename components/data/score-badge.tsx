import { cn } from '@/lib/utils';
import { formatScore, formatPercent } from '@/lib/format';
import { scoreOffsetPercent, coverageDots } from '@/lib/score';
import type { Bias } from '@/lib/mock/types';

const TONE: Record<Bias, { text: string; bar: string }> = {
  bullish: { text: 'text-pos', bar: 'bg-pos' },
  bearish: { text: 'text-neg', bar: 'bg-neg' },
  neutral: { text: 'text-fg-muted', bar: 'bg-neu' },
};

/**
 * Number plus a bar that grows FROM the 50 baseline, never from the left edge.
 * The centre tick is what makes "34 is bearish" legible at a glance.
 */
export function ScoreBar({
  score,
  bias,
  className,
  width = 72,
}: {
  score: number;
  bias: Bias;
  className?: string;
  width?: number;
}) {
  const { width: pct, side } = scoreOffsetPercent(score);
  const tone = TONE[bias];

  return (
    <div
      className={cn('relative h-1.5 shrink-0 rounded-full bg-surface-3', className)}
      style={{ width }}
      aria-hidden
    >
      <div className="absolute left-1/2 top-1/2 h-2.5 w-px -translate-x-1/2 -translate-y-1/2 bg-line-strong" />
      {side !== 'zero' && (
        <div
          className={cn('absolute top-0 h-full rounded-full', tone.bar)}
          style={
            side === 'pos'
              ? { left: '50%', width: `${pct / 2}%` }
              : { right: '50%', width: `${pct / 2}%` }
          }
        />
      )}
    </div>
  );
}

export function ScoreBadge({
  score,
  bias,
  size = 'md',
  showBar = true,
  muted = false,
}: {
  score: number;
  bias: Bias;
  size?: 'sm' | 'md' | 'lg';
  showBar?: boolean;
  muted?: boolean;
}) {
  const tone = TONE[bias];
  const sizes = {
    sm: 'text-[13px] font-semibold',
    md: 'text-[15px] font-semibold',
    lg: 'text-[28px] font-bold tracking-[-0.02em]',
  };

  return (
    <div className="flex items-center gap-2.5">
      <span
        data-numeric
        className={cn(sizes[size], muted ? 'text-fg-muted' : tone.text, 'tabular-nums')}
      >
        {formatScore(score)}
      </span>
      {showBar && <ScoreBar score={score} bias={bias} width={size === 'lg' ? 120 : 64} />}
    </div>
  );
}

/**
 * Coverage is a qualifier, not a value — dots read as confidence and stay
 * subordinate to the score they modify.
 */
export function CoverageBadge({
  coverage,
  isSufficient,
  className,
  showPercent = true,
}: {
  coverage: number;
  isSufficient: boolean;
  className?: string;
  showPercent?: boolean;
}) {
  const filled = coverageDots(coverage);

  return (
    <span className={cn('inline-flex items-center gap-2', className)}>
      <span className="flex gap-[3px]" aria-hidden>
        {[0, 1, 2, 3].map((i) => (
          <span
            key={i}
            className={cn(
              'size-1.5 rounded-full',
              i < filled ? (isSufficient ? 'bg-fg-muted' : 'bg-warn') : 'bg-surface-3'
            )}
          />
        ))}
      </span>
      {showPercent && (
        <span
          data-numeric
          className={cn('text-[13px] tabular-nums', isSufficient ? 'text-fg-muted' : 'text-warn')}
        >
          {formatPercent(coverage)}
        </span>
      )}
    </span>
  );
}
