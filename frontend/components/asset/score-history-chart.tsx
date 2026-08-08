'use client';

import { useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import {
  CartesianGrid,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { cn } from '@/lib/utils';
import { formatScore } from '@/lib/format';

export interface HistoryPointView {
  date: string;
  label: string;
  score: number;
}

const RANGES = [
  { key: 'days7', days: 7 },
  { key: 'days30', days: 30 },
  { key: 'days90', days: 90 },
] as const;

export function ScoreHistoryChart({ points }: { points: HistoryPointView[] }) {
  const t = useTranslations();
  const [days, setDays] = useState(30);

  const data = useMemo(() => points.slice(-days), [points, days]);
  const last = data[data.length - 1]?.score ?? 50;
  // Raw theme variables, not literals: Recharts takes these straight through to
  // SVG attributes, so the chart repaints with the rest of the product.
  const stroke = last >= 65 ? 'var(--pos)' : last <= 35 ? 'var(--neg)' : 'var(--fg-muted)';

  return (
    <div className="rounded-[12px] border border-line bg-surface p-5">
      <div className="mb-5 flex flex-wrap items-center justify-end gap-2">
        {RANGES.map((r) => (
          <button
            key={r.key}
            type="button"
            onClick={() => setDays(r.days)}
            className={cn(
              'rounded-lg px-2.5 py-1 text-[12px] font-medium transition-ui',
              days === r.days
                ? 'bg-accent/15 text-accent ring-1 ring-inset ring-accent/30'
                : 'text-fg-muted hover:bg-surface-2 hover:text-fg'
            )}
          >
            {t(`asset.${r.key}`)}
          </button>
        ))}
      </div>

      <div className="h-[260px] w-full">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data} margin={{ top: 4, right: 8, bottom: 0, left: -18 }}>
            <CartesianGrid stroke="var(--line)" strokeDasharray="0" vertical={false} />
            <XAxis
              dataKey="label"
              tick={{ fill: 'var(--fg-subtle)', fontSize: 11 }}
              tickLine={false}
              axisLine={{ stroke: 'var(--line)' }}
              minTickGap={28}
            />
            <YAxis
              domain={[0, 100]}
              ticks={[0, 25, 50, 75, 100]}
              tick={{ fill: 'var(--fg-subtle)', fontSize: 11 }}
              tickLine={false}
              axisLine={false}
              width={44}
            />
            {/* The neutral baseline is a real boundary, so it is drawn, not implied. */}
            <ReferenceLine y={50} stroke="var(--line-strong)" strokeDasharray="4 4" />
            <Tooltip
              cursor={{ stroke: 'var(--line-strong)', strokeWidth: 1 }}
              contentStyle={{
                background: 'var(--surface-2)',
                border: '1px solid var(--line-strong)',
                borderRadius: 8,
                fontSize: 13,
                padding: '8px 12px',
                color: 'var(--fg)',
              }}
              labelStyle={{ color: 'var(--fg-muted)', marginBottom: 2 }}
              formatter={(value: number) => [formatScore(value), t('common.score')]}
            />
            <Line
              type="monotone"
              dataKey="score"
              stroke={stroke}
              strokeWidth={2}
              dot={false}
              activeDot={{ r: 3, strokeWidth: 0 }}
              isAnimationActive={false}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}
