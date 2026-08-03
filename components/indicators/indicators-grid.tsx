'use client';

import { useMemo, useState } from 'react';
import { useTranslations } from 'next-intl';
import { cn } from '@/lib/utils';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/sheet';
import { CategoryLabel, FactorChip } from '@/components/data/badges';
import type { FactorCategory } from '@/lib/mock/types';

export interface IndicatorView {
  code: string;
  factorCode: string;
  name: string;
  description: string;
  whyItMatters: string;
  howItAffects: string;
  category: FactorCategory;
  frequencyLabel: string;
  bandMinor: number;
  bandMajor: number;
  unit: string;
  weightRange: [number, number];
}

const CATEGORIES: (FactorCategory | 'all')[] = [
  'all',
  'policy',
  'inflation',
  'growth',
  'labour',
  'sentiment',
  'positioning',
];

export function IndicatorsGrid({ indicators }: { indicators: IndicatorView[] }) {
  const t = useTranslations();
  const [category, setCategory] = useState<FactorCategory | 'all'>('all');
  const [selected, setSelected] = useState<IndicatorView | null>(null);

  const visible = useMemo(
    () => indicators.filter((i) => (category === 'all' ? true : i.category === category)),
    [indicators, category]
  );

  return (
    <>
      <div className="flex flex-wrap gap-1.5">
        {CATEGORIES.map((c) => (
          <button
            key={c}
            type="button"
            onClick={() => setCategory(c)}
            className={cn(
              'rounded-lg px-3 py-1.5 text-[13px] font-medium transition-ui',
              category === c
                ? 'bg-accent/15 text-accent ring-1 ring-inset ring-accent/30'
                : 'border border-line text-fg-muted hover:bg-surface-2 hover:text-fg'
            )}
          >
            {c === 'all' ? t('common.all') : t(`category.${c}`)}
          </button>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
        {visible.map((i) => (
          <button
            key={i.code}
            type="button"
            onClick={() => setSelected(i)}
            className="flex flex-col rounded-[12px] border border-line bg-surface p-5 text-left transition-ui hover:border-line-strong hover:bg-surface-2"
          >
            <div className="flex items-center justify-between gap-3">
              <FactorChip code={i.factorCode} />
              <CategoryLabel category={i.category} />
            </div>

            <h3 className="mt-3 text-[14px] font-semibold leading-snug text-fg">{i.name}</h3>
            <p className="mt-2 line-clamp-3 flex-1 text-[13px] leading-relaxed text-fg-muted">
              {i.description}
            </p>

            <div className="mt-4 flex items-center justify-between border-t border-line pt-3 text-[12px] text-fg-subtle">
              <span data-numeric className="tabular-nums">
                {t('indicators.weightRange')} {i.weightRange[0]}–{i.weightRange[1]}
              </span>
              <span>{i.frequencyLabel}</span>
            </div>
          </button>
        ))}
      </div>

      <Sheet open={selected !== null} onOpenChange={(o) => !o && setSelected(null)}>
        <SheetContent>
          {selected && (
            <div className="p-6 sm:p-8">
              <div className="flex items-center gap-3">
                <FactorChip code={selected.factorCode} />
                <CategoryLabel category={selected.category} />
              </div>

              <SheetTitle className="mt-4 text-xl font-semibold tracking-tight text-fg">
                {selected.name}
              </SheetTitle>
              <p className="mt-3 text-[14px] leading-relaxed text-fg-muted">
                {selected.description}
              </p>

              <Block title={t('indicators.whyItMatters')} body={selected.whyItMatters} />
              <Block title={t('indicators.howItAffects')} body={selected.howItAffects} />

              {selected.bandMajor > 0 && (
                <div className="mt-8">
                  <h4 className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
                    {t('indicators.bands')}
                  </h4>
                  <dl className="mt-3 grid grid-cols-2 gap-3">
                    <BandCard
                      label={t('indicators.bandMinor')}
                      value={`${selected.bandMinor} ${selected.unit}`}
                    />
                    <BandCard
                      label={t('indicators.bandMajor')}
                      value={`${selected.bandMajor} ${selected.unit}`}
                    />
                  </dl>
                </div>
              )}

              <div className="mt-8 flex items-center justify-between rounded-[10px] border border-line bg-canvas px-4 py-3">
                <span className="text-[12px] text-fg-subtle">{t('indicators.weightRange')}</span>
                <span data-numeric className="text-[13px] font-medium tabular-nums text-fg">
                  {selected.weightRange[0]}–{selected.weightRange[1]}
                </span>
              </div>
            </div>
          )}
        </SheetContent>
      </Sheet>
    </>
  );
}

function Block({ title, body }: { title: string; body: string }) {
  return (
    <div className="mt-8">
      <h4 className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">{title}</h4>
      <p className="mt-2 text-[14px] leading-relaxed text-fg-muted">{body}</p>
    </div>
  );
}

function BandCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-[10px] border border-line bg-canvas p-3">
      <dt className="text-[11px] text-fg-subtle">{label}</dt>
      <dd data-numeric className="mt-1 text-[15px] font-semibold tabular-nums text-fg">
        {value}
      </dd>
    </div>
  );
}
