import type { Indicator } from './types';

export const INDICATORS: Indicator[] = [
  {
    code: 'POLICY_RATE',
    factorCode: 'RATE',
    category: 'policy',
    frequency: 'perMeeting',
    bandMinor: 0.1,
    bandMajor: 0.25,
    unit: '%',
    weightRange: [22, 32],
    name: { mn: 'Бодлогын хүү', en: 'Policy rate' },
    description: {
      mn: 'Төв банк арилжааны банкуудад зээл олгох суурь хүү. Мөнгөний бодлогын хамгийн шууд хэрэгсэл.',
      en: 'The benchmark rate a central bank charges commercial banks — the most direct instrument of monetary policy.',
    },
    whyItMatters: {
      mn: 'Хүүгийн зөрүү нь валютын урт хугацааны чиглэлийг тодорхойлдог хамгийн хүчтэй хүчин зүйл. Хөрөнгө өндөр өгөөжтэй валют руу шилждэг.',
      en: 'Rate differentials are the strongest long-run driver of currency direction. Capital flows toward the higher-yielding currency.',
    },
    howItAffects: {
      mn: 'Хүү өсөх буюу өсөх хүлээлт нэмэгдэх нь тухайн валютыг чангаруулна. Бууруулах мөчлөг эсрэгээр сулруулна. Оноололтын хөдөлгүүр хүүгийн түвшин болон чиглэл хоёуланг тооцдог.',
      en: 'Hikes, or rising expectations of hikes, strengthen the currency; cutting cycles weaken it. The engine scores both the level and the direction of travel.',
    },
  },
  {
    code: 'CPI_YOY',
    factorCode: 'CPI',
    category: 'inflation',
    frequency: 'monthly',
    bandMinor: 0.1,
    bandMajor: 0.3,
    unit: 'pp',
    weightRange: [12, 18],
    name: { mn: 'Хэрэглээний үнийн индекс (жилээр)', en: 'Consumer Price Index (YoY)' },
    description: {
      mn: 'Өрхийн худалдан авдаг барааны сагсны үнийн жилийн өөрчлөлт.',
      en: 'Year-on-year change in the price of a representative basket of household goods.',
    },
    whyItMatters: {
      mn: 'Инфляци нь төв банкны шийдвэрийн гол оролт. Хүлээлтээс өндөр гарвал хүү өсгөх магадлал нэмэгдэнэ.',
      en: 'Inflation is the primary input to central bank decisions. A print above forecast raises the odds of tighter policy.',
    },
    howItAffects: {
      mn: 'Таамгаас өндөр CPI нь валютыг чангаруулдаг. Гэхдээ алтны хувьд урвуу: хатуу бодлого нь бодит өгөөжийг өсгөж алтны эрэлтийг бууруулна.',
      en: 'A hot CPI supports the currency. For gold the sign inverts: tighter policy lifts real yields, which weighs on gold.',
    },
  },
  {
    code: 'GDP_QOQ',
    factorCode: 'GDP',
    category: 'growth',
    frequency: 'quarterly',
    bandMinor: 0.2,
    bandMajor: 0.6,
    unit: 'pp',
    weightRange: [6, 13],
    name: { mn: 'ДНБ-ий улирлын өсөлт', en: 'GDP (QoQ)' },
    description: {
      mn: 'Дотоодын нийт бүтээгдэхүүний өмнөх улиралтай харьцуулсан өөрчлөлт.',
      en: 'Change in total output against the previous quarter.',
    },
    whyItMatters: {
      mn: 'Эдийн засгийн өргөн хүрээний эрүүл байдлыг илэрхийлдэг хамгийн цогц үзүүлэлт.',
      en: 'The single broadest measure of whether an economy is expanding or contracting.',
    },
    howItAffects: {
      mn: 'Хүчтэй өсөлт нь валютад эерэг, учир нь хатуу бодлогын орон зайг нэмэгдүүлнэ. Хувьцааны индекст мөн эерэг.',
      en: 'Strong growth supports the currency by widening room for tighter policy, and supports equity indices directly.',
    },
  },
  {
    code: 'PMI_MFG',
    factorCode: 'PMI',
    category: 'growth',
    frequency: 'monthly',
    bandMinor: 0.5,
    bandMajor: 1.5,
    unit: 'index',
    weightRange: [5, 15],
    name: { mn: 'Үйлдвэрлэлийн PMI', en: 'Manufacturing PMI' },
    description: {
      mn: 'Худалдан авалтын менежерүүдийн санал асуулгад суурилсан индекс. 50 нь тэлэлт ба агшилтын хил.',
      en: 'A diffusion index built from purchasing manager surveys. 50 is the line between expansion and contraction.',
    },
    whyItMatters: {
      mn: 'ДНБ-ээс хамаагүй эрт гардаг тул эдийн засгийн эргэлтийн цэгийг хамгийн түрүүнд илрүүлдэг.',
      en: 'It lands far earlier than GDP, which makes it the first credible signal of a turning point.',
    },
    howItAffects: {
      mn: '50-аас дээш ба таамгаас өндөр бол валютад эерэг. Хувьцааны индекст өсөлтийн дохио тул мөн эерэг.',
      en: 'Above 50 and above forecast is positive for the currency, and positive for equity indices as a growth signal.',
    },
  },
  {
    code: 'NFP',
    factorCode: 'NFP',
    category: 'labour',
    frequency: 'monthly',
    bandMinor: 25,
    bandMajor: 75,
    unit: 'K',
    weightRange: [7, 16],
    name: { mn: 'Хөдөө аж ахуйн бус ажлын байр', en: 'Non-Farm Payrolls' },
    description: {
      mn: 'АНУ-д хөдөө аж ахуйн бус салбарт сард нэмэгдсэн ажлын байрны тоо.',
      en: 'Monthly change in US payroll employment outside agriculture.',
    },
    whyItMatters: {
      mn: 'Сарын хамгийн их хөдөлгөөн үүсгэдэг мэдээлэл. Холбооны нөөцийн хоёр үүргийн нэг нь хөдөлмөр эрхлэлт.',
      en: 'The highest-volatility release of the month. Employment is one half of the Federal Reserve dual mandate.',
    },
    howItAffects: {
      mn: 'Хүчтэй тоо нь долларыг дэмжинэ. Бусад валютын хувьд ажил эрхлэлтийн өөрчлөлт болон ажилгүйдлийн түвшинг ашиглана.',
      en: 'A strong print supports the dollar. Other currencies are scored on their employment change or unemployment rate.',
    },
  },
  {
    // NFP is US-specific. Every other currency is scored on the same factor
    // through its own employment series, so the calendar must not label an
    // Australian release "Non-Farm Payrolls".
    code: 'EMPLOY_CHANGE',
    factorCode: 'NFP',
    category: 'labour',
    frequency: 'monthly',
    bandMinor: 10,
    bandMajor: 30,
    unit: 'K',
    weightRange: [7, 16],
    name: { mn: 'Хөдөлмөр эрхлэлтийн өөрчлөлт', en: 'Employment Change' },
    description: {
      mn: 'АНУ-аас бусад валютын хувьд сард нэмэгдсэн ажлын байрны тоо буюу ажилгүйдлийн түвшин.',
      en: 'Monthly change in employment, or the unemployment rate, for currencies outside the US.',
    },
    whyItMatters: {
      mn: 'Төв банк бүр хөдөлмөрийн зах зээлийн байдлыг бодлогын шийдвэртээ тусгадаг.',
      en: 'Every central bank weighs labour market slack when setting policy.',
    },
    howItAffects: {
      mn: 'Ажлын байр таамгаас илүү нэмэгдвэл валютад эерэг. Ажилгүйдлийн түвшний хувьд эсрэгээр тооцно.',
      en: 'Job gains above forecast support the currency. For unemployment rate series the sign inverts.',
    },
  },
  {
    code: 'RETAIL_MOM',
    factorCode: 'RETAIL',
    category: 'growth',
    frequency: 'monthly',
    bandMinor: 0.2,
    bandMajor: 0.5,
    unit: 'pp',
    weightRange: [6, 7],
    name: { mn: 'Жижиглэн худалдаа (сараар)', en: 'Retail Sales (MoM)' },
    description: {
      mn: 'Жижиглэн худалдааны борлуулалтын сарын өөрчлөлт.',
      en: 'Month-on-month change in retail sales volume.',
    },
    whyItMatters: {
      mn: 'Хөгжингүй эдийн засгийн 60-70 хувийг хэрэглээ бүрдүүлдэг тул эрэлтийн шууд хэмжүүр.',
      en: 'Consumption is 60–70% of a developed economy, making this the most direct read on demand.',
    },
    howItAffects: {
      mn: 'Хүчтэй хэрэглээ нь инфляцийн даралт үүсгэж, хатуу бодлогын магадлалыг нэмснээр валютыг дэмжинэ.',
      en: 'Strong consumption feeds inflation pressure, raising the odds of tighter policy and supporting the currency.',
    },
  },
  {
    code: 'DXY_INDEX',
    factorCode: 'DXY',
    category: 'sentiment',
    frequency: 'daily',
    bandMinor: 0,
    bandMajor: 0,
    unit: 'index',
    weightRange: [5, 25],
    name: { mn: 'АНУ долларын индекс', en: 'US Dollar Index' },
    description: {
      mn: 'Долларын сагсны валютуудтай харьцуулсан жинлэсэн ханш. Оноололтод сүүлийн 252 ажиглалтын хувиарлалтаар хэвийн болгоно.',
      en: 'The trade-weighted dollar against a basket. Scored as a percentile of its trailing 252 observations.',
    },
    whyItMatters: {
      mn: 'Мэдээллээс таамагласан долларын хүч биш, бодитоор биелсэн эрэлтийг харуулдаг тул бие даасан баталгаа болно.',
      en: 'It shows realised dollar demand rather than demand implied by releases, so it independently corroborates the rest.',
    },
    howItAffects: {
      mn: 'Индекс өндөр байх нь доллартай хосолсон бүх хөрөнгөд сөрөг. Алтанд хамгийн хүчтэй нөлөөтэй.',
      en: 'A high index reads negative for everything quoted against the dollar, and weighs most heavily on gold.',
    },
  },
  {
    code: 'US10Y_REAL',
    factorCode: 'YIELD',
    category: 'policy',
    frequency: 'daily',
    bandMinor: 0,
    bandMajor: 0,
    unit: '%',
    weightRange: [5, 30],
    name: { mn: 'АНУ-ын 10 жилийн бодит өгөөж', en: 'US 10Y Real Yield' },
    description: {
      mn: 'Нэрлэсэн өгөөжөөс инфляцийн хүлээлтийг хассан утга. Импортын үед тооцоолж, тусдаа цуваа болгон хадгална.',
      en: 'Nominal yield minus breakeven inflation. Computed at ingestion and stored as its own series, never derived at scoring time.',
    },
    whyItMatters: {
      mn: 'Өгөөжгүй хөрөнгө болох алт барих боломжийн өртгийг тодорхойлдог. Технологийн хувьцааны үнэлгээнд мөн шийдвэрлэх нөлөөтэй.',
      en: 'It sets the opportunity cost of holding a non-yielding asset like gold, and drives the valuation of long-duration equities.',
    },
    howItAffects: {
      mn: 'Бодит өгөөж буурах нь алт болон технологийн индекст эерэг, доллартай урвуу хамааралтай.',
      en: 'Falling real yields are positive for gold and for technology indices, and negative for the dollar.',
    },
  },
  {
    code: 'COT_NET',
    factorCode: 'COT',
    category: 'positioning',
    frequency: 'weekly',
    bandMinor: 0,
    bandMajor: 0,
    unit: 'K',
    weightRange: [0, 0],
    name: { mn: 'COT цэвэр позиц', en: 'COT Net Positioning' },
    description: {
      mn: 'CFTC-ийн долоо хоног тутмын тайлан дахь спекулянтуудын цэвэр байрлал. Эдийн засгийн мэдээлэл биш.',
      en: 'Net speculative positioning from the weekly CFTC Commitment of Traders report. Not an economic release.',
    },
    whyItMatters: {
      mn: 'Зах зээл аль хэдийн ямар байрлалд орсныг харуулдаг. Хэт нэг талыг барьсан позиц нь эргэлтийн эрсдэлийг илтгэнэ.',
      en: 'It shows how the market is already positioned. Crowded positioning flags reversal risk that releases alone will not.',
    },
    howItAffects: {
      mn: 'MVP-д зөвхөн харагдана — оноололтын жин 0. Оноололтын тодорхойлолт эцэслэгдсэний дараа жин олгоно.',
      en: 'Displayed only in the MVP, with weight 0. It will be weighted once the scoring specification is finalised.',
    },
  },
];

export const INDICATOR_BY_CODE = new Map(INDICATORS.map((i) => [i.code, i]));

/**
 * First match wins: several indicators can feed one factor (NFP and
 * EMPLOY_CHANGE both feed NFP), and the first is the canonical one to link to.
 */
export const INDICATOR_BY_FACTOR = INDICATORS.reduce((map, i) => {
  if (!map.has(i.factorCode)) map.set(i.factorCode, i);
  return map;
}, new Map<string, Indicator>());
