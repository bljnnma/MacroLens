import type { ReactNode } from 'react';
import { getLocale } from 'next-intl/server';
import { getAllScores, getEngineVersion, getMarketSnapshot, getReferenceTime } from '@/lib/mock';
import { formatRelativeTime } from '@/lib/format';
import { t } from '@/lib/localized';
import type { Locale } from '@/lib/mock/types';
import { ShellFrame } from './shell-frame';

export async function AppShell({ children }: { children: ReactNode }) {
  const locale = (await getLocale()) as Locale;
  const snapshot = getMarketSnapshot();
  const now = getReferenceTime();

  // Freshness drives this product's credibility, so it is computed once here
  // and pinned into the sidebar rather than fetched per page.
  const ageMinutes = Math.round((now.getTime() - new Date(snapshot.dataAsOf).getTime()) / 60_000);
  const freshness = ageMinutes > 24 * 60 ? 'stale' : ageMinutes > 60 ? 'aging' : 'fresh';

  const searchItems = getAllScores().map((s) => ({
    symbol: s.symbol,
    name: t(s.name, locale),
    score: s.score,
    bias: s.bias,
  }));

  return (
    <ShellFrame
      engine={{
        updatedLabel: formatRelativeTime(snapshot.dataAsOf, now, locale),
        version: getEngineVersion(),
        freshness,
      }}
      searchItems={searchItems}
    >
      {children}
    </ShellFrame>
  );
}
