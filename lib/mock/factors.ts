import type { Factor } from './types';

/**
 * The factor namespace: one scoring dimension = one heatmap column =
 * one detail-page row = one weight key.
 *
 * COT is Positioning, not an economic release — it is displayed in the heatmap
 * but carries weight 0 in every v1 profile until the scoring spec is finalised.
 */
export const FACTORS: Factor[] = [
  {
    code: 'RATE',
    scope: 'currency',
    category: 'policy',
    displayOrder: 1,
    name: { mn: 'Бодлогын хүү', en: 'Policy rate' },
    shortName: { mn: 'Хүү', en: 'Rate' },
    description: {
      mn: 'Төв банкны бодлогын хүүгийн түвшин ба чиглэл. Валютын хамгийн хүчтэй хөдөлгөгч хүчин зүйл.',
      en: 'Central bank policy rate level and direction. The single strongest driver of currency valuation.',
    },
  },
  {
    code: 'CPI',
    scope: 'currency',
    category: 'inflation',
    displayOrder: 2,
    name: { mn: 'Инфляци', en: 'Inflation' },
    shortName: { mn: 'CPI', en: 'CPI' },
    description: {
      mn: 'Хэрэглээний үнийн индекс. Инфляци хүлээлтээс өндөр гарвал төв банк хатуу бодлого барих магадлал нэмэгдэнэ.',
      en: 'Consumer price index. Prints above forecast raise the odds of tighter policy, which supports the currency.',
    },
  },
  {
    code: 'GDP',
    scope: 'currency',
    category: 'growth',
    displayOrder: 3,
    name: { mn: 'ДНБ-ий өсөлт', en: 'GDP growth' },
    shortName: { mn: 'GDP', en: 'GDP' },
    description: {
      mn: 'Дотоодын нийт бүтээгдэхүүний улирлын өсөлт. Эдийн засгийн ерөнхий эрүүл байдлыг илэрхийлнэ.',
      en: 'Quarter-on-quarter output growth. The broadest single read on economic health.',
    },
  },
  {
    code: 'PMI',
    scope: 'currency',
    category: 'growth',
    displayOrder: 4,
    name: { mn: 'Худалдан авагчийн индекс', en: 'PMI' },
    shortName: { mn: 'PMI', en: 'PMI' },
    description: {
      mn: 'Худалдан авалтын менежерүүдийн индекс. 50-аас дээш бол тэлэлт, доош бол агшилтыг илтгэнэ.',
      en: 'Purchasing managers index. Above 50 signals expansion, below 50 contraction.',
    },
  },
  {
    code: 'NFP',
    scope: 'currency',
    category: 'labour',
    displayOrder: 5,
    name: { mn: 'Хөдөлмөр эрхлэлт', en: 'Employment' },
    shortName: { mn: 'NFP', en: 'NFP' },
    description: {
      mn: 'Хөдөө аж ахуйн бус салбарын ажлын байр (АНУ) болон бусад валютын хөдөлмөр эрхлэлтийн өөрчлөлт.',
      en: 'US non-farm payrolls, and the equivalent employment change series for other currencies.',
    },
  },
  {
    code: 'RETAIL',
    scope: 'currency',
    category: 'growth',
    displayOrder: 6,
    name: { mn: 'Жижиглэн худалдаа', en: 'Retail sales' },
    shortName: { mn: 'Retail', en: 'Retail' },
    description: {
      mn: 'Жижиглэн худалдааны сарын өөрчлөлт. Хэрэглэгчийн эрэлтийн шууд хэмжүүр.',
      en: 'Month-on-month retail sales. The most direct read on consumer demand.',
    },
  },
  {
    code: 'DXY',
    scope: 'usd',
    category: 'sentiment',
    displayOrder: 7,
    name: { mn: 'Долларын хүч', en: 'Dollar strength' },
    shortName: { mn: 'DXY', en: 'DXY' },
    description: {
      mn: 'Долларын индексийн сүүлийн 1 жилийн хуваарилалт дахь байрлал. Бодит эрэлтийг илэрхийлнэ.',
      en: 'Where the dollar index sits within its trailing one-year distribution — realised demand, not implied.',
    },
  },
  {
    code: 'YIELD',
    scope: 'usd',
    category: 'policy',
    displayOrder: 8,
    name: { mn: 'Бодит өгөөж', en: 'Real yield' },
    shortName: { mn: 'Yield', en: 'Yield' },
    description: {
      mn: 'АНУ-ын 10 жилийн бодит өгөөж. Алт болон технологийн хувьцаанд хамгийн хүчтэй нөлөөлдөг.',
      en: 'US 10-year real yield. The dominant driver for gold and long-duration equities.',
    },
  },
  {
    code: 'COT',
    scope: 'currency',
    category: 'positioning',
    displayOrder: 9,
    name: { mn: 'Позиц байрлал (COT)', en: 'Positioning (COT)' },
    shortName: { mn: 'COT', en: 'COT' },
    description: {
      mn: 'CFTC-ийн долоо хоног тутмын тайлан дахь спекулянтуудын цэвэр позиц. Эдийн засгийн мэдээлэл биш, зах зээлийн байрлалын үзүүлэлт.',
      en: 'Net speculative positioning from the weekly CFTC report. Not an economic release — a positioning signal.',
    },
  },
];

export const FACTOR_BY_CODE = new Map(FACTORS.map((f) => [f.code, f]));
