import { USE_API } from './client';
import * as api from './api';
import * as mock from './mock-adapter';
import type { Locale, Market } from '@/lib/mock/types';
import type {
  AssetDetailView,
  CalendarEventView,
  HeatmapView,
  IndicatorView,
  MarketSnapshotView,
  MetaView,
  ScoreHistoryView,
  TopSetupView,
} from './types';

/**
 * The single seam between the UI and its data.
 *
 * Set API_BASE_URL and every page reads the live .NET API; leave it unset and
 * the whole app runs on local fixtures. Deliberately no silent fallback from API
 * to mocks: a stakeholder demo that quietly serves stale fixtures while
 * appearing live is worse than one that fails visibly.
 */

export { USE_API };
export * from './types';

export async function fetchTopSetups(
  locale: Locale,
  limit = 8,
  market?: Market
): Promise<TopSetupView[]> {
  return USE_API ? api.apiTopSetups(locale, limit, market) : mock.mockTopSetups(locale, limit, market);
}

export async function fetchRankedAssets(locale: Locale): Promise<TopSetupView[]> {
  return USE_API ? api.apiRankedAssets(locale) : mock.mockRankedAssets(locale);
}

export async function fetchMeta(locale: Locale): Promise<MetaView> {
  return USE_API ? api.apiMeta(locale) : mock.mockMeta();
}

export async function fetchHeatmap(
  locale: Locale,
  limit?: number,
  market?: Market
): Promise<HeatmapView> {
  return USE_API ? api.apiHeatmap(locale, limit, market) : mock.mockHeatmap(locale, limit, market);
}

export async function fetchAsset(locale: Locale, symbol: string): Promise<AssetDetailView | null> {
  return USE_API ? api.apiAsset(locale, symbol) : mock.mockAsset(locale, symbol);
}

export async function fetchAssetHistory(
  locale: Locale,
  symbol: string,
  days = 30
): Promise<ScoreHistoryView[]> {
  return USE_API ? api.apiAssetHistory(locale, symbol, days) : mock.mockAssetHistory(symbol, days);
}

export async function fetchCalendar(locale: Locale): Promise<CalendarEventView[]> {
  return USE_API ? api.apiCalendar(locale) : mock.mockCalendar(locale);
}

export async function fetchIndicators(locale: Locale): Promise<IndicatorView[]> {
  return USE_API ? api.apiIndicators(locale) : mock.mockIndicators(locale);
}

export async function fetchMarketSnapshot(locale: Locale): Promise<MarketSnapshotView> {
  return USE_API ? api.apiMarketSnapshot(locale) : mock.mockMarketSnapshot();
}

export async function fetchRecentReleases(
  locale: Locale,
  limit = 4
): Promise<CalendarEventView[]> {
  const events = await fetchCalendar(locale);
  return events
    .filter((e) => e.actual !== null)
    .sort((a, b) => b.releasedAt.localeCompare(a.releasedAt))
    .slice(0, limit);
}
