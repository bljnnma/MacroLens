import { apiGet } from './client';
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
import type { Market } from '@/lib/mock/types';

/** Presentation-only, so it stays on the client rather than bloating the API. */
const FLAG: Record<string, string> = {
  USD: '🇺🇸', EUR: '🇪🇺', GBP: '🇬🇧', JPY: '🇯🇵',
  AUD: '🇦🇺', CHF: '🇨🇭', CAD: '🇨🇦', NZD: '🇳🇿',
};

const ORIGIN_TZ: Record<string, string> = {
  USD: 'America/New_York', EUR: 'Europe/Frankfurt', GBP: 'Europe/London', JPY: 'Asia/Tokyo',
  AUD: 'Australia/Sydney', CHF: 'Europe/Zurich', CAD: 'America/Toronto', NZD: 'Pacific/Auckland',
};

/** Maps the API's IndicatorUnit onto the token `formatIndicatorValue` expects. */
const UNIT: Record<string, string> = {
  percent: '%',
  percentagePoints: 'pp',
  thousands: 'K',
  index: 'index',
  absolute: '',
};

export async function apiTopSetups(locale: string, limit = 8, market?: Market) {
  return apiGet<TopSetupView[]>('/api/v1/top-setups', {
    locale,
    query: { limit, market },
  });
}

export async function apiRankedAssets(locale: string) {
  // Its own endpoint, not the heatmap projection: Markets must show real
  // coverage, including for assets below the sufficiency floor.
  return apiGet<TopSetupView[]>('/api/v1/assets', { locale });
}

export async function apiMeta(locale: string) {
  return apiGet<MetaView>('/api/v1/meta', { locale, revalidate: 30 });
}

export async function apiHeatmap(locale: string, limit?: number, market?: Market) {
  return apiGet<HeatmapView>('/api/v1/heatmap', { locale, query: { limit, market } });
}

export async function apiAsset(locale: string, symbol: string) {
  try {
    const asset = await apiGet<AssetDetailView>(`/api/v1/assets/${symbol.toUpperCase()}`, {
      locale,
    });

    // Responses are cached for `revalidate` seconds, so a cached body can predate
    // a field the current types declare as required — which is exactly how adding
    // `readings` turned the asset page into a 500 for every symbol whose response
    // was already warm. The type is a compile-time claim about the CURRENT API;
    // only the boundary can make it true at runtime.
    return {
      ...asset,
      factors: asset.factors.map((f) => ({ ...f, readings: f.readings ?? [] })),
    };
  } catch {
    return null;
  }
}

export async function apiAssetHistory(locale: string, symbol: string, days = 30) {
  return apiGet<ScoreHistoryView[]>(`/api/v1/assets/${symbol.toUpperCase()}/history`, {
    locale,
    query: { days },
  });
}

export async function apiCalendar(locale: string): Promise<CalendarEventView[]> {
  const raw = await apiGet<
    Array<Omit<CalendarEventView, 'flag' | 'originTimeZone' | 'unit'> & { unit: string }>
  >('/api/v1/calendar', { locale, query: { daysBack: 3, daysForward: 7 }, revalidate: 30 });

  return raw.map((e) => ({
    ...e,
    unit: UNIT[e.unit] ?? '',
    flag: FLAG[e.currency] ?? '',
    originTimeZone: ORIGIN_TZ[e.currency] ?? 'UTC',
  }));
}

export async function apiIndicators(locale: string) {
  return apiGet<IndicatorView[]>('/api/v1/indicators', { locale, revalidate: 3600 });
}

export async function apiMarketSnapshot(locale: string) {
  return apiGet<MarketSnapshotView>('/api/v1/market-snapshot', { locale });
}
