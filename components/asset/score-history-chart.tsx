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
  const stroke = last >= 65 ? 'var(--color-pos)' : last <= 35 ? 'var(--color-neg)' : '#a1a1aa';

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
            <CartesianGrid stroke="#27272a" strokeDasharray="0" vertical={false} />
            <XAxis
              dataKey="label"
              tick={{ fill: '#71717a', fontSize: 11 }}
              tickLine={false}
              axisLine={{ stroke: '#27272a' }}
              minTickGap={28}
            />
            <YAxis
              domain={[0, 100]}
              ticks={[0, 25, 50, 75, 100]}
              tick={{ fill: '#71717a', fontSize: 11 }}
              tickLine={false}
              axisLine={false}
              width={44}
            />
            {/* The neutral baseline is a real boundary, so it is drawn, not implied. */}
            <ReferenceLine y={50} stroke="#3f3f46" strokeDasharray="4 4" />
            <Tooltip
              cursor={{ stroke: '#3f3f46', strokeWidth: 1 }}
              contentStyle={{
                background: '#18181b',
                border: '1px solid #3f3f46',
                borderRadius: 8,
                fontSize: 13,
                padding: '8px 12px',
              }}
              labelStyle={{ color: '#a1a1aa', marginBottom: 2 }}
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
