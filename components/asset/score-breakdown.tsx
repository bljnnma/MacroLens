'use client';

import { useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { cn } from '@/lib/utils';
import { formatScore, formatSigned, formatNormalized } from '@/lib/format';
import { contributionColor } from '@/lib/score';
import type { FactorCategory } from '@/lib/mock/types';

export interface FactorView {
  code: string;
  name: string;
  shortName: string;
  category: FactorCategory;
  rawLabel: string;
  n: number | null;
  weight: number;
  contribution: number;
  explanation: string;
  available: boolean;
}

/**
 * The signature moment: a horizontal waterfall is the only chart form where
 * `50 + Σcontributions = score` is literally visible. The baseline block is
 * rendered as part of the calculation, not as chart chrome — otherwise the
 * arithmetic does not close to the eye.
 */
export function ScoreBreakdown({
  factors,
  score,
  baseScore = 50,
}: {
  factors: FactorView[];
  score: number;
  baseScore?: number;
}) {
  const t = useTranslations();
  const [active, setActive] = useState<string | null>(null);

  // Zero-weight factors (COT in v1) are shown in the heatmap and the audit
  // table, but never in the waterfall — a 0.0 segment is visual noise.
  const scoring = useMemo(
    () => factors.filter((f) => f.weight > 0 && f.available && f.contribution !== 0),
    [factors]
  );

  const segments = useMemo(() => {
    const sorted = [...scoring].sort((a, b) => b.contribution - a.contribution);
    let cursor = baseScore;
    return sorted.map((f) => {
      const start = f.contribution >= 0 ? cursor : cursor + f.contribution;
      const seg = { factor: f, start, size: Math.abs(f.contribution) };
      cursor += f.contribution;
      return seg;
    });
  }, [scoring, baseScore]);

  return (
    <div className="rounded-[12px] border border-line bg-surface p-5 sm:p-6">
      <div className="mb-6 flex items-end justify-between gap-6">
        <div>
          <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            {t('common.baseScore')}
          </div>
          <div data-numeric className="mt-1 text-[22px] font-semibold tabular-nums text-fg-muted">
            {baseScore.toFixed(1)}
          </div>
        </div>
        <div className="text-right">
          <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
            {t('common.finalScore')}
          </div>
          <div
            data-numeric
            className={cn(
              'mt-1 text-[28px] font-bold tabular-nums tracking-[-0.02em]',
              score >= 65 ? 'text-pos' : score <= 35 ? 'text-neg' : 'text-fg'
            )}
          >
            {formatScore(score)}
          </div>
        </div>
      </div>

      <div className="relative h-12 w-full overflow-hidden rounded-lg bg-canvas ring-1 ring-inset ring-line">
        {/* The baseline renders as part of the calculation, not as chart chrome —
            otherwise 50 + Σ = score does not close to the eye. */}
        <div
          className={cn(
            'absolute inset-y-0 left-0 bg-neu/40 transition-ui',
            active && 'opacity-40'
          )}
          style={{ width: `${baseScore}%` }}
        />

        {segments.map(({ factor, start, size }) => (
          <button
            key={factor.code}
            type="button"
            onMouseEnter={() => setActive(factor.code)}
            onMouseLeave={() => setActive(null)}
            onClick={() => {
              document
                .getElementById(`factor-${factor.code}`)
                ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }}
            title={`${factor.name} ${formatSigned(factor.contribution)}`}
            className={cn(
              'absolute inset-y-0 border-x border-canvas/60 transition-ui',
              factor.contribution > 0 ? 'bg-pos/70' : 'bg-neg/70',
              active && active !== factor.code && 'opacity-25',
              active === factor.code && 'ring-1 ring-inset ring-white/40'
            )}
            style={{ left: `${start}%`, width: `${size}%` }}
          />
        ))}

        <div
          className="absolute inset-y-0 w-px bg-fg/50"
          style={{ left: `${score}%` }}
          aria-hidden
        />
      </div>

      <div className="mt-2 flex justify-between px-0.5 text-[11px] tabular-nums text-fg-subtle" data-numeric>
        <span>0</span>
        <span>{baseScore}</span>
        <span>100</span>
      </div>

      <div className="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-6">
        {scoring
          .slice()
          .sort((a, b) => b.contribution - a.contribution)
          .map((f) => (
            <ContributionCard
              key={f.code}
              factor={f}
              dimmed={active !== null && active !== f.code}
              onHover={setActive}
            />
          ))}
      </div>
    </div>
  );
}

function ContributionCard({
  factor,
  dimmed,
  onHover,
}: {
  factor: FactorView;
  dimmed: boolean;
  onHover: (code: string | null) => void;
}) {
  const t = useTranslations();

  return (
    <button
      type="button"
      onMouseEnter={() => onHover(factor.code)}
      onMouseLeave={() => onHover(null)}
      onClick={() =>
        document
          .getElementById(`factor-${factor.code}`)
          ?.scrollIntoView({ behavior: 'smooth', block: 'center' })
      }
      className={cn(
        'rounded-[10px] border border-line bg-canvas p-3 text-left transition-ui hover:border-line-strong',
        dimmed && 'opacity-40'
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="font-mono text-[11px] font-medium uppercase tracking-wide text-fg-subtle">
          {factor.code}
        </span>
      </div>
      <p className="mt-1 truncate text-[12px] text-fg-muted" title={factor.name}>
        {factor.name}
      </p>
      <div
        data-numeric
        className={cn(
          'mt-2.5 text-[18px] font-semibold tabular-nums',
          contributionColor(factor.contribution)
        )}
      >
        {formatSigned(factor.contribution)}
      </div>
      <div data-numeric className="mt-1 text-[11px] tabular-nums text-fg-subtle">
        {formatNormalized(factor.n)} · {t('common.weight').toLowerCase()} {factor.weight}
      </div>
    </button>
  );
}
