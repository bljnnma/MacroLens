import type { Asset } from './types';

/**
 * Exposures are what make the engine generic: without them every new pair
 * would be a code change. For indices the exposure means "which economy drives
 * this asset", not "quote currency" — the profile's polarity carries the sign.
 */
export const ASSETS: Asset[] = [
  {
    symbol: 'XAUUSD',
    market: 'metals',
    displayOrder: 1,
    name: { mn: 'Алт', en: 'Gold' },
    exposures: [{ currency: 'USD', direction: -1 }],
  },
  {
    symbol: 'EURUSD',
    market: 'forex',
    displayOrder: 2,
    name: { mn: 'Евро / АНУ доллар', en: 'Euro / US Dollar' },
    exposures: [
      { currency: 'EUR', direction: 1 },
      { currency: 'USD', direction: -1 },
    ],
  },
  {
    symbol: 'GBPUSD',
    market: 'forex',
    displayOrder: 3,
    name: { mn: 'Фунт / АНУ доллар', en: 'British Pound / US Dollar' },
    exposures: [
      { currency: 'GBP', direction: 1 },
      { currency: 'USD', direction: -1 },
    ],
  },
  {
    symbol: 'USDJPY',
    market: 'forex',
    displayOrder: 4,
    name: { mn: 'АНУ доллар / Иен', en: 'US Dollar / Japanese Yen' },
    exposures: [
      { currency: 'USD', direction: 1 },
      { currency: 'JPY', direction: -1 },
    ],
  },
  {
    symbol: 'AUDUSD',
    market: 'forex',
    displayOrder: 5,
    name: { mn: 'Австрали доллар / АНУ доллар', en: 'Australian Dollar / US Dollar' },
    exposures: [
      { currency: 'AUD', direction: 1 },
      { currency: 'USD', direction: -1 },
    ],
  },
  {
    symbol: 'USDCHF',
    market: 'forex',
    displayOrder: 6,
    name: { mn: 'АНУ доллар / Швейцар франк', en: 'US Dollar / Swiss Franc' },
    exposures: [
      { currency: 'USD', direction: 1 },
      { currency: 'CHF', direction: -1 },
    ],
  },
  {
    symbol: 'USDCAD',
    market: 'forex',
    displayOrder: 7,
    name: { mn: 'АНУ доллар / Канад доллар', en: 'US Dollar / Canadian Dollar' },
    exposures: [
      { currency: 'USD', direction: 1 },
      { currency: 'CAD', direction: -1 },
    ],
  },
  {
    symbol: 'NZDUSD',
    market: 'forex',
    displayOrder: 8,
    name: { mn: 'Шинэ Зеланд доллар / АНУ доллар', en: 'New Zealand Dollar / US Dollar' },
    exposures: [
      { currency: 'NZD', direction: 1 },
      { currency: 'USD', direction: -1 },
    ],
  },
  {
    symbol: 'DXY',
    market: 'dollarIndex',
    displayOrder: 9,
    name: { mn: 'АНУ долларын индекс', en: 'US Dollar Index' },
    exposures: [{ currency: 'USD', direction: 1 }],
  },
  {
    symbol: 'NASDAQ',
    market: 'indices',
    displayOrder: 10,
    name: { mn: 'Nasdaq 100', en: 'Nasdaq 100' },
    exposures: [{ currency: 'USD', direction: 1 }],
  },
];

export const ASSET_BY_SYMBOL = new Map(ASSETS.map((a) => [a.symbol, a]));
