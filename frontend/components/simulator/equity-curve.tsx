'use client';

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

export interface EquityPoint {
  index: number;
  balance: number;
}

const compact = (v: number) =>
  v >= 1000 ? `${(v / 1000).toFixed(v >= 10000 ? 0 : 1)}k` : v.toFixed(0);

export function EquityCurve({
  points,
  initialBalance,
  targetBalance,
  reachedTarget,
}: {
  points: EquityPoint[];
  initialBalance: number;
  targetBalance: number;
  reachedTarget: boolean;
}) {
  const t = useTranslations('simulator');
  const yMax =
    Math.max(targetBalance, ...points.map((p) => p.balance), initialBalance) * 1.05;

  return (
    <div className="rounded-[12px] border border-line bg-surface p-5">
      <div className="mb-5 flex flex-wrap items-baseline justify-between gap-3">
        <span className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
          {t('equityCurve')}
        </span>
        <span className="text-[12px] text-fg-subtle">{t('equityCurveHint')}</span>
      </div>

      <div className="h-[300px] w-full">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={points} margin={{ top: 4, right: 12, bottom: 0, left: -6 }}>
            <CartesianGrid stroke="var(--line)" vertical={false} />
            <XAxis
              dataKey="index"
              tick={{ fill: 'var(--fg-subtle)', fontSize: 11 }}
              tickLine={false}
              axisLine={{ stroke: 'var(--line)' }}
              minTickGap={40}
            />
            <YAxis
              tick={{ fill: 'var(--fg-subtle)', fontSize: 11 }}
              tickLine={false}
              axisLine={false}
              width={52}
              tickFormatter={compact}
              domain={[0, yMax]}
            />
            {/* Both the starting line and the goal are drawn, so the curve is
                read against the two numbers that actually matter. */}
            <ReferenceLine y={initialBalance} stroke="var(--line-strong)" strokeDasharray="4 4" />
            <ReferenceLine
              y={targetBalance}
              stroke={reachedTarget ? 'var(--pos)' : 'var(--line-strong)'}
              strokeDasharray="4 4"
            />
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
              labelFormatter={(v) => `${t('tradeNumber')} ${v}`}
              formatter={(value: number) => [
                value.toLocaleString(undefined, { maximumFractionDigits: 0 }),
                t('balance'),
              ]}
            />
            <Line
              type="monotone"
              dataKey="balance"
              stroke={reachedTarget ? 'var(--pos)' : 'var(--accent)'}
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
