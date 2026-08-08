import type { ReactNode } from 'react';
import { getLocale } from 'next-intl/server';
import { fetchMarketSnapshot, fetchRankedAssets } from '@/lib/data';
import { formatRelativeTime } from '@/lib/format';
import type { Locale } from '@/lib/mock/types';
import { ShellFrame } from './shell-frame';

const ENGINE_VERSION = '1.0.0';

export async function AppShell({ children }: { children: ReactNode }) {
  const locale = (await getLocale()) as Locale;

  const [snapshot, assets] = await Promise.all([
    fetchMarketSnapshot(locale),
    fetchRankedAssets(locale),
  ]);

  const now = new Date();

  // Freshness drives this product's credibility, so it is computed once here
  // and pinned into the sidebar rather than fetched per page.
  const ageMinutes = Math.round((now.getTime() - new Date(snapshot.dataAsOf).getTime()) / 60_000);
  const freshness = ageMinutes > 24 * 60 ? 'stale' : ageMinutes > 60 ? 'aging' : 'fresh';

  return (
    <ShellFrame
      engine={{
        updatedLabel: formatRelativeTime(snapshot.dataAsOf, now, locale),
        version: ENGINE_VERSION,
        freshness,
      }}
      searchItems={assets.map((a) => ({
        symbol: a.symbol,
        name: a.name,
        score: a.score,
        bias: a.bias,
      }))}
    >
      {children}
    </ShellFrame>
  );
}
