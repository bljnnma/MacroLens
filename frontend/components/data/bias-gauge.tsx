import { cn } from '@/lib/utils';
import { formatScore } from '@/lib/format';
import { BiasBadge } from './badges';
import type { Bias } from '@/lib/mock/types';

const CX = 100;
const CY = 102;
const R = 78;
const STROKE = 13;

/** score 0 -> 180deg (left), 50 -> 90deg (top), 100 -> 0deg (right) */
function polar(score: number, radius: number) {
  const angle = Math.PI * (1 - Math.min(100, Math.max(0, score)) / 100);
  return {
    x: CX + radius * Math.cos(angle),
    y: CY - radius * Math.sin(angle),
  };
}

function arc(from: number, to: number, radius = R) {
  const a = polar(from, radius);
  const b = polar(to, radius);
  // Every segment is < 180deg, and we always travel left-to-right over the top.
  return `M ${a.x.toFixed(2)} ${a.y.toFixed(2)} A ${radius} ${radius} 0 0 1 ${b.x.toFixed(2)} ${b.y.toFixed(2)}`;
}

const ZONES: { bias: Bias; from: number; to: number; on: string; off: string }[] = [
  { bias: 'bearish', from: 0, to: 35, on: 'stroke-neg', off: 'stroke-neg/15' },
  { bias: 'neutral', from: 35, to: 65, on: 'stroke-neu', off: 'stroke-neu/25' },
  { bias: 'bullish', from: 65, to: 100, on: 'stroke-pos', off: 'stroke-pos/15' },
];

const SCORE_TONE: Record<Bias, string> = {
  bullish: 'text-pos',
  bearish: 'text-neg',
  neutral: 'text-fg',
};

/**
 * A speedometer, not a progress dial: the needle reads against the same 0/35/65/100
 * thresholds the engine uses, and only the zone the score actually falls in is lit.
 * So the gauge shows both where the asset sits and how far it is from flipping bias.
 */
export function BiasGauge({
  score,
  bias,
  size = 'lg',
  showScore = true,
  className,
}: {
  score: number;
  bias: Bias;
  size?: 'sm' | 'lg';
  showScore?: boolean;
  className?: string;
}) {
  const needle = polar(score, R - 26);
  const width = size === 'lg' ? 'max-w-[220px]' : 'max-w-[132px]';

  return (
    <div className={cn('flex flex-col items-center', className)}>
      <svg
        viewBox="0 0 200 118"
        className={cn('w-full', width)}
        role="img"
        aria-label={`${formatScore(score)} / 100`}
      >
        {ZONES.map((z) => (
          <path
            key={z.bias}
            d={arc(z.from, z.to)}
            fill="none"
            strokeWidth={STROKE}
            strokeLinecap="butt"
            className={z.bias === bias ? z.on : z.off}
          />
        ))}

        {/* Ticks sit outside the band so they mark the bias boundaries
            without cutting through the colour. */}
        {[35, 65].map((t) => {
          const a = polar(t, R + 8);
          const b = polar(t, R + 13);
          return (
            <line
              key={t}
              x1={a.x}
              y1={a.y}
              x2={b.x}
              y2={b.y}
              strokeWidth={1.5}
              strokeLinecap="round"
              className="stroke-line-strong"
            />
          );
        })}

        <text x={12} y={116} className="fill-fg-subtle" fontSize={9}>
          0
        </text>
        <text
          x={188}
          y={116}
          textAnchor="end"
          className="fill-fg-subtle"
          fontSize={9}
        >
          100
        </text>

        <line
          x1={CX}
          y1={CY}
          x2={needle.x}
          y2={needle.y}
          strokeWidth={3}
          strokeLinecap="round"
          className="stroke-fg"
        />
        <circle cx={CX} cy={CY} r={5.5} className="fill-fg" />
        <circle cx={CX} cy={CY} r={2} className="fill-canvas" />
      </svg>

      {showScore && (
        <div
          data-numeric
          className={cn(
            'mt-1 text-[30px] font-bold leading-none tabular-nums tracking-[-0.03em]',
            SCORE_TONE[bias]
          )}
        >
          {formatScore(score)}
        </div>
      )}

      <div className="mt-3">
        <BiasBadge bias={bias} />
      </div>
    </div>
  );
}
