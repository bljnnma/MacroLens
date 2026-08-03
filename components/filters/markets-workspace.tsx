'use client';

import { useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { Search, X } from 'lucide-react';
import { cn } from '@/lib/utils';
import { Input, Slider } from '@/components/ui/misc';
import { TopSetupsTable, type SetupRow } from '@/components/tables/top-setups-table';
import type { Bias, Market } from '@/lib/mock/types';

const MARKETS: (Market | 'all')[] = ['all', 'forex', 'metals', 'indices', 'dollarIndex'];
const BIASES: (Bias | 'all')[] = ['all', 'bullish', 'neutral', 'bearish'];

export function MarketsWorkspace({ rows }: { rows: SetupRow[] }) {
  const t = useTranslations();
  const [query, setQuery] = useState('');
  const [market, setMarket] = useState<Market | 'all'>('all');
  const [bias, setBias] = useState<Bias | 'all'>('all');
  const [minScore, setMinScore] = useState(0);
  const [compact, setCompact] = useState(false);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return rows
      .filter((r) => (market === 'all' ? true : r.market === market))
      .filter((r) => (bias === 'all' ? true : r.bias === bias))
      .filter((r) => r.score >= minScore)
      .filter((r) => !q || r.symbol.toLowerCase().includes(q) || r.name.toLowerCase().includes(q))
      .map((r, i) => ({ ...r, rank: i + 1 }));
  }, [rows, market, bias, minScore, query]);

  const hasFilters = query !== '' || market !== 'all' || bias !== 'all' || minScore > 0;

  const reset = () => {
    setQuery('');
    setMarket('all');
    setBias('all');
    setMinScore(0);
  };

  return (
    <div className="space-y-5">
      <div className="rounded-[12px] border border-line bg-surface p-4">
        <div className="flex flex-wrap items-end gap-5">
          <div className="min-w-[200px] flex-1">
            <label className="mb-1.5 block text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
              {t('common.search')}
            </label>
            <div className="relative">
              <Search
                className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-fg-subtle"
                aria-hidden
              />
              <Input
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="XAUUSD"
                className="pl-9"
              />
            </div>
          </div>

          <FilterGroup label={t('common.market')}>
            {MARKETS.map((m) => (
              <Chip key={m} active={market === m} onClick={() => setMarket(m)}>
                {m === 'all' ? t('market.all') : t(`market.${m}`)}
              </Chip>
            ))}
          </FilterGroup>

          <FilterGroup label={t('common.bias')}>
            {BIASES.map((b) => (
              <Chip key={b} active={bias === b} onClick={() => setBias(b)}>
                {b === 'all' ? t('common.all') : t(`bias.${b}Short`)}
              </Chip>
            ))}
          </FilterGroup>

          <div className="min-w-[180px]">
            <label className="mb-1.5 flex items-center justify-between text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
              {t('markets.minScore')}
              <span data-numeric className="tabular-nums text-fg-muted">
                {minScore}
              </span>
            </label>
            <div className="h-9 pt-3">
              <Slider
                value={[minScore]}
                onValueChange={([v]) => setMinScore(v)}
                min={0}
                max={100}
                step={5}
              />
            </div>
          </div>

          <FilterGroup label={t('markets.density')}>
            <Chip active={!compact} onClick={() => setCompact(false)}>
              {t('markets.comfortable')}
            </Chip>
            <Chip active={compact} onClick={() => setCompact(true)}>
              {t('markets.compact')}
            </Chip>
          </FilterGroup>
        </div>

        {hasFilters && (
          <div className="mt-4 flex items-center gap-3 border-t border-line pt-3">
            <span data-numeric className="text-[12px] tabular-nums text-fg-subtle">
              {t('markets.resultCount', { count: filtered.length })}
            </span>
            <button
              type="button"
              onClick={reset}
              className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-[12px] font-medium text-accent transition-ui hover:bg-accent/10"
            >
              <X className="size-3" aria-hidden />
              {t('common.clearFilters')}
            </button>
          </div>
        )}
      </div>

      <TopSetupsTable rows={filtered} showFilters={false} compact={compact} />
    </div>
  );
}

function FilterGroup({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <span className="mb-1.5 block text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
        {label}
      </span>
      <div className="flex flex-wrap gap-1.5">{children}</div>
    </div>
  );
}

function Chip({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'h-9 rounded-lg px-3 text-[13px] font-medium transition-ui',
        active
          ? 'bg-accent/15 text-accent ring-1 ring-inset ring-accent/30'
          : 'border border-line text-fg-muted hover:bg-surface-2 hover:text-fg'
      )}
    >
      {children}
    </button>
  );
}
