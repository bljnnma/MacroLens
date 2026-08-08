# UX & Wireframe Specification

> Status: **design only** — wireframes and rationale. No React until approved.
> Target: 1440px desktop-first, dark theme only, MN primary / EN secondary.

---

## 1. Design foundations

### 1.1 The one job

The dashboard answers **"What should I trade today?"** — and it must answer it *without scrolling*.

That single constraint decides most of the layout below. Anything competing for above-the-fold space has to justify itself against the ranked list.

### 1.2 Colour rules

| Role | Token | Value |
|---|---|---|
| Page background | `--bg` | `#09090B` |
| Card / surface | `--surface` | `#111113` |
| Surface raised (hover, popover) | `--surface-2` | `#18181B` |
| Border | `--border` | `#27272A` |
| Border strong (sticky edges) | `--border-strong` | `#3F3F46` |
| Text primary | `--fg` | `#FAFAFA` |
| Text secondary | `--fg-muted` | `#A1A1AA` |
| Text tertiary (labels) | `--fg-subtle` | `#71717A` |
| **Positive** | `--pos` | `#10B981` emerald-500 |
| **Negative** | `--neg` | `#F43F5E` rose-500 |
| **Neutral** | `--neu` | `#52525B` zinc-600 |
| **Accent / interactive** | `--accent` | `#3B82F6` blue-500 |
| Warning (stale, low coverage) | `--warn` | `#F59E0B` amber-500 |

**The rule that keeps this readable:**

> **Semantic colour belongs to data. Accent colour belongs to interaction.**

Blue is used *only* for focus rings, selected nav, links, and active filter chips — never for a value. So blue can never be misread as a signal. Emerald and rose are used *only* for scored values — never for a button. A "Save" button is neutral, not green.

This is why Bloomberg is readable at density and most fintech dashboards are not.

### 1.3 Diverging scales, not progress bars

Scores run 0–100 with a **baseline of 50**. Every visual encoding must be anchored at the centre, never at zero:

```
   0            50            100
   ├─────────────┼─────────────┤
                 │████████▶ 91          ← fill grows FROM centre
        ◀────████│ 34                   ← not from the left edge
```

A left-anchored progress bar implies 0 is "empty" and 50 is "half full." Both are wrong. 50 is *neutral*, and 34 is a genuine bearish signal, not a weak bullish one.

### 1.4 Heatmap scale (5 steps, −2…+2)

| `n` | Fill | Text |
|---|---|---|
| `+2` | `#065F46` emerald-800 | `#6EE7B7` |
| `+1` | `#064E3B` @ 55% | `#34D399` |
| `0` | `#18181B` | `#71717A` |
| `−1` | `#4C0519` @ 55% | `#FB7185` |
| `−2` | `#881337` rose-900 | `#FDA4AF` |

**The number is always printed in the cell.** Colour-only encoding fails for the ~8% of men with red-green colour deficiency — a meaningful share of a professional trading audience — and traders want the digit regardless. Colour is the scanning aid; the number is the truth.

### 1.5 Typography

**Inter Variable.** Non-negotiable requirement: full Mongolian Cyrillic coverage including **Ө, Ү, Ё**. Many display faces marketed as "fintech" silently drop these and render tofu boxes only in Mongolian — the exact failure your primary audience would see first.

| Role | Size / Weight / Tracking |
|---|---|
| Page title | 24px / 600 / −0.02em |
| Section header | 15px / 600 |
| Section subtitle | 13px / 400 / `--fg-muted` |
| Table header | 11px / 500 / uppercase / +0.06em / `--fg-subtle` |
| Body & table cell | 14px / 400 |
| Score (table) | 15px / 600 / **tabular** |
| Score (hero) | 48px / 700 / **tabular** / −0.03em |
| Factor code chip | 11px / 500 / **JetBrains Mono** |

**`font-variant-numeric: tabular-nums` on every number in the product.** Without it, digits change width between renders and entire columns shimmer as scores update. This is the single cheapest change that separates "real terminal" from "web demo."

Factor codes (`CPI`, `NFP`, `RATE`) stay Latin monospace in both locales — they are ticker-like identifiers, not prose, and translating them would break scanning.

### 1.6 Spacing

4px base unit. Only these values: `4 · 8 · 12 · 16 · 20 · 24 · 32 · 48 · 64`.

| Element | Value |
|---|---|
| Sidebar width | 240px (rail 64px, drawer below 1024) |
| Topbar height | 56px |
| Page gutter | 32px |
| Max content width | 1280px, centred |
| Gap between sections | 48px |
| Card padding | 20px (dense tables 16px) |
| Table row height | 52px |
| Heatmap cell | 96 × 48px |
| Border radius | cards 12px · badges 6px · buttons 8px · cells 4px |

Section gap of **48px** is doing real work: it is what lets a dense table sit next to a dense heatmap without the page reading as noise. Generous vertical rhythm buys the right to be dense *inside* each block.

### 1.7 Motion

Duration ≤ 150ms, `ease-out`, and only on: hover fills, popover/tooltip entry, filter chip state, sidebar collapse.

**No** number count-ups, no chart draw-in animation, no page transitions, no skeleton shimmer sweep. A trader reloading during a CPI release needs the number *now*; a 600ms count-up is actively hostile.

---

## 2. Information architecture

### 2.1 Resolving the nav overlap

The requested sidebar has Dashboard, Markets, and Heatmap — but the Dashboard also contains a table and a heatmap. Without a rule, three pages show the same data and the user never knows which one to open.

> **The dashboard summarises. Section pages drill.**

| Surface | Scope | Affordances |
|---|---|---|
| Dashboard → Top Setups | Top 8 by score | Read-only, sorted, "View all →" |
| **Markets** | All 10 assets | Full sort, multi-filter, column chooser, CSV export |
| Dashboard → Heatmap | Top 6 assets × 8 factors | Hover only, "Open full heatmap →" |
| **Heatmap** | All assets × all factors | Crosshair, cell drill-through, factor reorder, market filter |

Each dashboard block ends with a right-aligned ghost link into its full page. The dashboard is a *briefing*; the section pages are *workspaces*.

### 2.2 Routes

```
/[locale]                         Dashboard
/[locale]/markets                 Markets
/[locale]/heatmap                 Heatmap
/[locale]/calendar                Economic Calendar
/[locale]/indicators              Indicators
/[locale]/indicators/[code]       Indicator detail (sheet on desktop, page on mobile)
/[locale]/assets/[symbol]         Asset detail
/[locale]/settings                Settings
```

`locale ∈ { mn, en }`, default `mn`. Switching language preserves the current path and scroll position — a trader mid-analysis must not be thrown back to the dashboard.

---

## 3. App shell

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│ ░░ SIDEBAR 240 ░░ │ TOPBAR 56px                                                  │
│                   │  ┌────────────────────────┐                                  │
│  ◆ MacroScore     │  │ ⌕  Хайх...        ⌘K   │        [MN|EN]  ☾   ( ) Bataa ▾ │
│                   │  └────────────────────────┘                                  │
│  ─────────────    ├──────────────────────────────────────────────────────────────┤
│  ▸ Хяналтын самбар│                                                              │
│  ▸ Зах зээл       │                    S C R O L L A B L E                       │
│  ▸ Дулааны зураг  │                     C O N T E N T                            │
│  ▸ Эдийн засгийн  │                                                              │
│    хуанли         │                  max-width 1280, gutter 32                   │
│  ▸ Үзүүлэлтүүд    │                                                              │
│  ─────────────    │                                                              │
│  ▸ Тохиргоо       │                                                              │
│                   │                                                              │
│  ┌──────────────┐ │                                                              │
│  │ ● Шинэчлэгдсэн│ │                                                             │
│  │   5 мин өмнө  │ │                                                             │
│  │   v1.0.0      │ │                                                             │
│  └──────────────┘ │                                                              │
└──────────────────────────────────────────────────────────────────────────────────┘
```

- Sidebar is **fixed**, does not scroll with content. Active item: `--surface-2` fill, 2px blue left border, `--fg` text.
- Topbar is **sticky**. Search is a `⌘K` command palette trigger, not an inline input — it jumps to assets, indicators, and pages, which is the Linear/Vercel interaction professionals expect.
- The **engine status card** pinned to the sidebar foot is deliberate: this product's credibility rests on data freshness. A green dot with "5 мин өмнө" is permanently in peripheral vision; it turns amber past 60 min and rose past 24 h. Users learn to trust the number because staleness is never hidden.

---

## 4. Dashboard

```
┌─ CONTENT ─────────────────────────────────────────────────────────────────────┐
│                                                                     32px pad   │
│  Шилдэг макро боломжууд                                    ⟳ 5 мин өмнө      │  ← 24/600
│  Хамгийн хүчтэй ба сул хөрөнгийг ил тод макро оноогоор олоорой.               │  ← 13 muted
│                                                                                │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐             │  MetricCard ×4
│  │ ХҮЧТЭЙ ВАЛЮТ│ │ СУЛ ВАЛЮТ   │ │ ЭРСДЭЛИЙН   │ │ ХАМРАЛТ     │             │  h=88
│  │             │ │             │ │ ОРЧИН       │ │             │             │
│  │  USD  ▲     │ │  JPY  ▼     │ │ Эрсдэлээс   │ │  92%        │             │
│  │  +1.8 дундаж│ │  −1.4 дундаж│ │ зайлсхийх   │ │ 10 хөрөнгө  │             │
│  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘             │
│                                                              ↕ 48px            │
│  ┌────────────────────────────────────────────────────────────────────────┐   │
│  │ ▣ Шилдэг боломжууд              [Бүгд][Форекс][Металл][Индекс]  Бүгдийг→│   │ ← sticky
│  │ ─────────────────────────────────────────────────────────────────────  │   │   filter bar
│  │  #  ХӨРӨНГӨ      ЗАХ ЗЭЭЛ   ОНОО            ХАНДЛАГА  ХАМРАЛТ  ШИНЭЧЛЭЛ│   │
│  │ ─────────────────────────────────────────────────────────────────────  │   │
│  │  1  ⬤ XAUUSD     Металл    91  ▕███▶      Өсөх      ●●●● 94%  5 мин   │   │
│  │     Алт                                                                 │   │
│  │  2  ⬤ EURUSD     Форекс    84  ▕██▶       Өсөх      ●●●● 91%  5 мин   │   │
│  │  3  ⬤ NASDAQ     Индекс    81  ▕██▶       Өсөх      ●●●○ 78%  5 мин   │   │
│  │  4  ⬤ GBPUSD     Форекс    63  ▕█▶        Өсөх      ●●●● 96%  5 мин   │   │
│  │ ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄ 50 · Төвийг сахисан ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄  │   │ ← neutral line
│  │  5  ⬤ AUDUSD     Форекс    46  ◀█▏        Саарал    ●●●○ 74%  5 мин   │   │
│  │  6  ⬤ NZDUSD     Форекс    41  ◀█▏        Саарал    ●●○○ 58% ⚠ 5 мин  │   │ ← low coverage
│  │  7  ⬤ USDCAD     Форекс    38  ◀██▏       Буурах    ●●●● 89%  5 мин   │   │
│  │  8  ⬤ USDJPY     Форекс    28  ◀███▏      Буурах    ●●●● 93%  5 мин   │   │
│  └────────────────────────────────────────────────────────────────────────┘   │
│                                                              ↕ 48px            │
│  ┌──────────────────────────────────────────┐ ┌─────────────────────────────┐ │
│  │ ▣ Дулааны зураглал        Бүтнээр нь →  │ │ ▣ Сүүлийн мэдээллүүд        │ │
│  │ ──────────────────────────────────────── │ │ ─────────────────────────── │ │
│  │        RATE CPI GDP PMI NFP DXY YLD COT │ │ ┌─────────────────────────┐ │ │
│  │ XAUUSD │ −2 │ −2 │ −2 │ +1 │ 0  │ −1 │−2│ │ │ 🇺🇸 US CPI YoY    ӨНДӨР│ │ │
│  │ EURUSD │ −1 │ +1 │ +1 │ +2 │ 0  │ −1 │ 0│ │ │ 2.7%  таам 2.8%  ө.3.0% │ │ │
│  │ NASDAQ │ −2 │ −1 │ +2 │ +2 │ +1 │ +1 │ 0│ │ │ ▼ USD-д сөрөг    2ц өмнө│ │ │
│  │ GBPUSD │ +1 │ +1 │  0 │ −1 │ +1 │ −1 │+1│ │ └─────────────────────────┘ │ │
│  │ USDJPY │ +2 │ +1 │ −1 │ −1 │ +2 │ +1 │+1│ │ ┌─────────────────────────┐ │ │
│  │ DXY    │ +2 │ +2 │ +2 │ −1 │ +2 │ ── │+1│ │ │ 🇪🇺 ECB Rate      ӨНДӨР │ │ │
│  │                                          │ │ │ 2.15%  таам 2.15%       │ │ │
│  │              ⟵ 8 багана, 6 мөр ⟶        │ │ │ ─ Өөрчлөлтгүй    1ө өмнө│ │ │
│  └──────────────────────────────────────────┘ └─────────────────────────────┘ │
│                       ~62%                                   ~38%              │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Layout
Single column of stacked blocks, `max-w-[1280px]`, 32px gutter, 48px between blocks. The bottom row is a `grid-cols-[1.6fr_1fr]` split — the heatmap needs horizontal room for 8 columns; the release feed is a narrow vertical list and would look starved of purpose at full width.

### Information hierarchy
1. **Ranked table** — the answer to the job.
2. **Market state strip** — the one-line context that makes the ranking legible.
3. **Heatmap preview** — *why* the ranking looks like that.
4. **Release feed** — *what changed* since last look.

Each layer answers the question raised by the one above it. That progression is the whole page.

### Why the hero is only two lines
A conventional marketing hero (large title, generous padding, illustration) would push the first table row below 900px and break the no-scroll requirement on a 1440×900 laptop. The title earns ~64px; the four MetricCards earn their 88px because they carry live data, not decoration.

### Why the neutral divider row exists
With a baseline-50 model, the boundary between "bullish" and "bearish" is a real, meaningful line — not an arbitrary sort position. Rendering it as a 1px dashed rule labelled `50 · Төвийг сахисан` makes the model's central concept visible on the primary screen, for free. A user learns the mental model by looking at the table.

### Why coverage is a dot meter, not a bar
Coverage is a *qualifier*, not a *value*. Four dots read as "confidence" and stay visually subordinate to the score. Below 60% the row gets an amber `⚠` and its score drops to `--fg-muted` — present, honest, and clearly de-ranked rather than silently hidden.

### Spacing
Card padding 20px · row height 52px · header row 40px · filter bar 48px, `sticky top-14` with `bg-[#111113]/95 backdrop-blur-sm` so column headers survive scrolling.

### Interaction
- Row hover → `--surface-2`, `cursor-pointer`, score bar brightens.
- Row click → `/assets/[symbol]`.
- Market tabs filter instantly, client-side, no spinner (10 rows).
- Score badge hover → tooltip: `Суурь 50 + Хувь нэмэр 41 = 91`.
- Heatmap cell hover → row + column crosshair tint; tooltip with raw value.
- Heatmap cell click → asset detail, deep-linked and scrolled to that factor row.
- `⟳ 5 мин өмнө` is a button — manual refresh, spins during fetch.

---

## 5. Asset detail — `/assets/xauusd`

The signature screen. "The calculation must be visually obvious" is the requirement, and it drives a three-density structure.

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  ← Буцах                                                                       │
│                                                                                │
│  ⬤  XAUUSD · Алт                                        Профайл: Metals v1     │
│     Металл · Үнэт металл                                Хөдөлгүүр: v1.0.0      │
│                                                                                │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────┐               │
│  │    ОНОО    │  │  ХАНДЛАГА  │  │   ХАМРАЛТ  │  │  ШИНЭЧЛЭЛ  │               │
│  │            │  │            │  │            │  │            │               │
│  │     91     │  │   Өсөх     │  │    94%     │  │  5 мин өмнө│               │
│  │  ▕███████▶ │  │   ▲        │  │  ●●●●      │  │  2026-08-03│               │
│  └────────────┘  └────────────┘  └────────────┘  └────────────┘               │
│                                                              ↕ 48px            │
│  ┌────────────────────────────────────────────────────────────────────────┐   │
│  │ ▣ Оноо хэрхэн бүрдсэн бэ                                               │   │
│  │ ─────────────────────────────────────────────────────────────────────  │   │
│  │                                                                        │   │
│  │  Суурь                                                          Эцсийн │   │
│  │   50                                                               91  │   │
│  │   │                                                                 │  │   │
│  │   ▓▓▓▓▓▓▓▓▓▓│███ +18│██ +12│█ +6│█ +11│█ +7│█ +5│▌−3│              │  │   │  ← WATERFALL
│  │   └─ суурь ─┘  RATE   CPI   PMI  DXY  YLD  GDP  RTL                   │   │
│  │                                                                        │   │
│  │  0 ──────────────── 50 ──────────────────────────────────── 91 ── 100 │   │
│  └────────────────────────────────────────────────────────────────────────┘   │
│                                                              ↕ 32px            │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────┐ │
│  │ RATE     │ │ CPI      │ │ DXY      │ │ YIELD    │ │ PMI      │ │ RETAIL │ │  ← ContributionCard
│  │ Хүү      │ │ Инфляци  │ │ Доллар   │ │ Өгөөж    │ │ PMI      │ │ Жижиглэн│ │
│  │          │ │          │ │          │ │          │ │          │ │        │ │
│  │  +18.0   │ │  +12.0   │ │  +11.0   │ │   +7.0   │ │   +6.0   │ │  −3.0  │ │
│  │  ▲ +2 ·22│ │  ▲ +2 ·12│ │  ▲ +1 ·25│ │  ▲ +1 ·25│ │  ▲ +1 ·8 │ │ ▼ −1 ·8│ │
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────┘ └────────┘ │
│                                                              ↕ 48px            │
│  ┌────────────────────────────────────────────────────────────────────────┐   │
│  │ ▣ Хүчин зүйлийн дэлгэрэнгүй                                            │   │
│  │ ───────────────────────────────────────────────────────────────────    │   │
│  │ ХҮЧИН ЗҮЙЛ  ТҮҮХИЙ УТГА  ХЭВИЙН  ЖИН   ХУВЬ НЭМЭР  ТАЙЛБАР            │   │
│  │ ───────────────────────────────────────────────────────────────────    │   │
│  │ ⬢ RATE      5.50%         +2     22     +18.00     Fed 25bp өсгөв...▸ │   │
│  │ ⬢ CPI       3.4% (т.3.1%) +2     12     +12.00     Инфляци таамга...▸ │   │
│  │ ⬢ DXY       п.72          +1     25     +11.00     Доллар 1 жилий...▸ │   │
│  │ ⬢ RETAIL    −0.4% (т.0.2%)−1      8      −3.00     Жижиглэн худал...▸ │   │
│  └────────────────────────────────────────────────────────────────────────┘   │
│                                                              ↕ 48px            │
│  ┌────────────────────────────────────────────────────────────────────────┐   │
│  │ ▣ Онооны түүх            [7 хоног][30 хоног][90 хоног]                 │   │
│  │  100 ┤                                                                 │   │
│  │      │                                        ╭──╮                     │   │
│  │   75 ┤                            ╭───────────╯  ╰─── 91              │   │
│  │      │              ╭─────────────╯                                    │   │
│  │   50 ┼┈┈┈┈┈┈╭───────╯┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈┈   │   │
│  │      │──────╯                                                          │   │
│  │   25 ┤                                                                 │   │
│  │      └────────────────────────────────────────────────────────────     │   │
│  │       7-р 5    7-р 12    7-р 19    7-р 26    8-р 2                     │   │
│  └────────────────────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────────────────┘
```

### The three-density principle

The same seven numbers are presented three times, at three densities, because three different intents arrive at this page:

| Block | Density | Intent | Time to read |
|---|---|---|---|
| **Waterfall** | Glance | "Is this bullish, and what's driving it?" | ~2s |
| **Contribution cards** | Scan | "Which factors, how big, which way?" | ~10s |
| **Factor table** | Audit | "Prove this number to me." | ~60s |

This is not redundancy. Cutting any one of them breaks a real user's workflow: the waterfall alone can't be audited, the table alone can't be glanced at.

### Why a horizontal waterfall
It is the only chart form that makes `50 + Σ contributions = 91` *literally visible*. Segments are laid end to end from the 50 baseline, positive extending right in emerald, negative pulling back left in rose, terminating at the final score. The axis beneath is a real 0–100 scale, so bar length is honest.

The baseline block itself renders in `--neu` and is labelled — it must read as *part of the calculation*, not as chart chrome, or the arithmetic doesn't close visually.

### Why the score hero is a MetricCard row, not a giant number
A 120px score with nothing beside it looks impressive and says little. Score, bias, coverage and freshness are only meaningful *together* — a 91 at 41% coverage is a different claim from a 91 at 94%. Four equal cards force them to be read as one statement.

### Interaction
- ContributionCard hover → corresponding waterfall segment brightens, others dim to 40%. Bidirectional on hover of the segment.
- Card click → scrolls to and highlights that row in the factor table.
- Table row `▸` → expands inline to the full generated explanation in the active locale, with a "Үзүүлэлт харах →" link to the indicator page.
- Table row hover shows a `⧉` copy affordance — copies raw value + normalized + weight, for users keeping their own journal.
- Chart tooltip: date, score, bias, coverage on that day. `Recharts` `LineChart`, `ReferenceLine` at 50 dashed, no dots except on hover, no area fill, no animation.
- Deep link `#factor-cpi` scrolls and flashes the row once — this is the heatmap cell click-through target.

### Spacing
Header 32px top, 24px to metric row · 48px to waterfall · 32px to cards · 48px to table · 48px to chart. Contribution cards: `grid-cols-6` at ≥1280, `grid-cols-3` at ≥768, `grid-cols-2` below.

---

## 6. Heatmap page

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  Дулааны зураглал                                                              │
│  Бүх хөрөнгө, бүх хүчин зүйл. Нүд дээр дарж дэлгэрэнгүйг харна уу.             │
│                                                                                │
│ ┌──── sticky filter bar ────────────────────────────────────────────────────┐ │
│ │ [Бүгд][Форекс][Металл][Индекс]        Эрэмбэлэх: [Оноогоор ▾]   ⬇ CSV    │ │
│ └───────────────────────────────────────────────────────────────────────────┘ │
│ ┌───────────────────────────────────────────────────────────────────────────┐ │
│ │ ХӨРӨНГӨ  │ RATE│ CPI │ GDP │ PMI │ NFP │ DXY │ YLD │ COT │  ОНОО          │ │ ← sticky top
│ │══════════╪═════╪═════╪═════╪═════╪═════╪═════╪═════╪═════╪═══════         │ │
│ │ XAUUSD   │ −2  │ −2  │ −2  │ +1  │  0  │ −1  │ −2  │ +1  │   91  ▕███▶    │ │
│ │ ────────┄┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼─────┼──────          │ │
│ │ EURUSD   │ −1  │ +1  │ +1  │ +2  │  0  │ −1  │  0  │  0  │   84  ▕██▶     │ │
│ │ NASDAQ   │ −2  │ −1  │ +2  │ +2  │ +1  │ +1  │ −2  │ ──  │   81  ▕██▶     │ │
│ │ GBPUSD   │ +1  │ +1  │  0  │ −1  │ +1  │ −1  │  0  │ +1  │   63  ▕█▶      │ │
│ │ AUDUSD   │  0  │ −1  │ +1  │  0  │ −1  │ −1  │  0  │ −1  │   46  ◀█▏      │ │
│ │ NZDUSD   │  0  │ ──  │ ──  │ −1  │ −1  │ −1  │  0  │ ──  │   41  ◀█▏ ⚠    │ │
│ │ USDCAD   │ +1  │ +1  │ −1  │ +1  │ +1  │ +1  │ +1  │  0  │   38  ◀██▏     │ │
│ │ USDCHF   │ +2  │ +1  │  0  │ +1  │ +1  │ +1  │ +1  │  0  │   34  ◀██▏     │ │
│ │ USDJPY   │ +2  │ +1  │ −1  │ −1  │ +2  │ +1  │ +1  │ +1  │   28  ◀███▏    │ │
│ │ DXY      │ +2  │ +2  │ +2  │ −1  │ +2  │ ──  │ +1  │ +1  │   88  ▕███▶    │ │
│ └───────────────────────────────────────────────────────────────────────────┘ │
│    ↑ sticky left                                                               │
│                                                                                │
│  Тайлбар:  ▪−2  ▪−1  ▫0  ▪+1  ▪+2      ── өгөгдөл байхгүй                    │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Layout
`position: sticky` on both the header row (`top-0`) and the first column (`left-0`), each with `--border-strong` on its inner edge and a matching background so content scrolling underneath never bleeds through. The score column is pinned right.

Cell 96 × 48px. At 1440 with a 240px sidebar the grid needs `160 + 8×96 + 120 = 1048px` — fits without horizontal scroll. Below 1280 the table scrolls horizontally while both sticky edges hold.

### Why the crosshair
Reading a matrix cell means answering "which row, which column" simultaneously. At 10×8 the eye loses its place constantly. Hovering tints the full row and column at 6% white — a direct lift from Bloomberg, and the single interaction that makes a matrix usable at scale.

### Why `──` for missing data
An empty cell reads as zero, and zero is a real signal meaning "neutral." Unavailable data must look categorically different from a neutral reading, or the coverage model is silently undermined at the exact moment it matters. `──` in `--fg-subtle`, tooltip: *"Өгөгдөл байхгүй эсвэл хуучирсан."*

### Interaction
Cell hover → crosshair + tooltip (factor name, raw value, normalized, weight, contribution). Cell click → `/assets/[symbol]#factor-[code]`. Column header click → sort by that factor. Row label click → asset detail.

---

## 7. Markets

Same shell as Top Setups, expanded to a workspace: all 10 assets, every column sortable, a `FilterBar` with market · bias · min score · min coverage · search, active filters shown as removable chips with a "Цэвэрлэх" reset, and a persisted density toggle (Comfortable 52px / Compact 40px).

The compact mode exists because Bloomberg-trained users measure a screen by rows visible without scrolling, and 40px rows fit all 10 assets plus header inside 480px.

Filter state lives in the URL query string — a trader can bookmark "bullish forex above 70" and share it with a colleague.

---

## 8. Economic Calendar

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  Эдийн засгийн хуанли                                                          │
│  ┌───────────────────────────────────────────────────────────────────────────┐ │
│  │ [Өнөөдөр][Маргааш][Энэ долоо хоног]    Валют:[Бүгд ▾]  Ач холбогдол:[▾]  │ │
│  └───────────────────────────────────────────────────────────────────────────┘ │
│                                                                                │
│  ӨНӨӨДӨР · 2026-08-03 · Даваа                                     3 үйл явдал │  ← sticky
│  ┌───────────────────────────────────────────────────────────────────────────┐ │
│  │ 15:30 │ 🇺🇸 USD │ ▮▮▮ │ Хэрэглээний үнийн индекс (жилээр)                │ │
│  │  UB   │         │     │ Бодит 2.7%   Таамаг 2.8%   Өмнөх 3.0%    ▼ USD  │ │
│  ├───────────────────────────────────────────────────────────────────────────┤ │
│  │ 21:00 │ 🇺🇸 USD │ ▮▮▮ │ Холбооны нөөцийн хүүгийн шийдвэр                 │ │
│  │  UB   │         │     │ Бодит ─      Таамаг 5.50%  Өмнөх 5.25%   ⏱ 4ц   │ │
│  ├───────────────────────────────────────────────────────────────────────────┤ │
│  │ 16:00 │ 🇪🇺 EUR │ ▮▮▯ │ Үйлдвэрлэлийн PMI                                │ │
│  │  UB   │         │     │ Бодит 49.2   Таамаг 49.0   Өмнөх 48.6   ▲ EUR   │ │
│  └───────────────────────────────────────────────────────────────────────────┘ │
│                                                                                │
│  МАРГААШ · 2026-08-04 · Мягмар                                    2 үйл явдал │
│  ...                                                                           │
└────────────────────────────────────────────────────────────────────────────────┘
```

### Why the time column is labelled "UB"
Every release on this calendar is published in New York, London, or Frankfurt time. The users are at **UTC+8**. Rendering times in `Asia/Ulaanbaatar` with a visible `UB` label under each time is the difference between a usable calendar and a dangerous one — and it is the strongest reason this product is built for this audience rather than localised for it. Hovering the time reveals the original timezone and UTC.

### Why released and upcoming events look different
Released events show **Actual** filled and a bias arrow. Upcoming events show `─` in the Actual slot and a live countdown (`⏱ 4ц 12м`). A trader scanning the list must never mistake a forecast for a print. Rows past their time without an actual show `⏳ Хүлээгдэж буй`.

Importance is a 3-bar glyph, not stars — bars read as magnitude, stars read as quality. Sticky date headers as the user scrolls through the week.

---

## 9. Indicators

```
┌────────────────────────────────────────────────────────────────────────────────┐
│  Үзүүлэлтүүд                                                                   │
│  Оноо тооцоход ашиглагддаг макро эдийн засгийн үзүүлэлтүүд.                     │
│                                                                                │
│  [Бүгд] [Мөнгөний бодлого] [Инфляци] [Өсөлт] [Хөдөлмөр] [Сэтгэл зүй]           │
│                                                                                │
│  ┌───────────────────────┐ ┌───────────────────────┐ ┌───────────────────────┐ │
│  │ ⬢ CPI      Инфляци    │ │ ⬢ RATE     Бодлого    │ │ ⬢ NFP      Хөдөлмөр  │ │
│  │                       │ │                       │ │                       │ │
│  │ Хэрэглээний үнийн     │ │ Бодлогын хүү          │ │ Хөдөө аж ахуйн бус   │ │
│  │ индекс                │ │                       │ │ салбарын ажлын байр   │ │
│  │                       │ │                       │ │                       │ │
│  │ Барааны үнийн ерөнхий │ │ Төв банкны тогтоодог  │ │ АНУ-ын хөдөлмөрийн   │ │
│  │ өсөлтийг хэмждэг...   │ │ суурь хүү...          │ │ зах зээлийн...       │ │
│  │                       │ │                       │ │                       │ │
│  │ ─────────────────     │ │ ─────────────────     │ │ ─────────────────     │ │
│  │ Жин  12–18   Сар бүр  │ │ Жин  22–32   Хурлаар  │ │ Жин  8–16   Сар бүр  │ │
│  └───────────────────────┘ └───────────────────────┘ └───────────────────────┘ │
└────────────────────────────────────────────────────────────────────────────────┘
```

`grid-cols-3` at ≥1280, `cols-2` at ≥768, `cols-1` below. Card click opens a **Sheet** from the right (desktop) or a full page (mobile) containing: what it measures · why traders care · how it moves currencies · normalization bands used by the engine · recent prints across all 8 currencies.

### Why this page exists
This is the section that makes the product *teach* rather than merely rank. A meaningful share of the target audience is semi-professional and will not have a working model of why a PMI miss moves EURUSD. Every explanation string the engine generates already references these concepts — this page is where a user goes when an explanation uses a term they don't know, and it is what converts a scoring tool into something people keep open.

It is also cheap: the content is reference data that already exists in `Indicators.Description`.

---

## 10. Settings

Single column, `max-w-[720px]`, grouped cards with 32px between groups — a settings page should feel calm, not dense.

**Хэл** — MN / EN segmented control, applies instantly, preserves route.
**Цагийн бүс** — `Asia/Ulaanbaatar (UTC+8)` default, with UTC and exchange-local options. Live preview line: *"CPI мэдээлэл: 2026-08-03 15:30"* updates as the selection changes, so the setting is understood before it is saved.
**Дэлгэц** — density (Comfortable / Compact), "Show factor codes in Latin" toggle.
**Тухай** — engine version `v1.0.0`, active profiles per market, last calculation timestamp, data-source attribution.

The About block is not filler. A transparency-first product should state which scoring profile version produced the numbers currently on screen.

---

## 11. Component hierarchy

```
components/
├─ layout/         AppShell · Sidebar · SidebarNavItem · Topbar
│                  CommandPalette · LocaleSwitcher · UserMenu · EngineStatusCard
├─ data/           ScoreBadge · ScoreBar · BiasBadge · CoverageBadge
│                  FactorChip · TrendArrow · ImpactBars · CountryFlag
│                  MetricCard · SectionHeader · StaleBadge
├─ tables/         DataTable · SortableHeader · DensityToggle · EmptyState
├─ heatmap/        Heatmap · HeatmapCell · HeatmapHeader · HeatmapLegend
│                  HeatmapCrosshairProvider
├─ asset/          ScoreWaterfall · ContributionCard · ContributionGrid
│                  FactorTable · FactorRow · ScoreHistoryChart
├─ calendar/       CalendarDayGroup · CalendarEvent · CountdownTimer
├─ filters/        FilterBar · MarketTabs · SearchInput · ScoreRangeFilter
│                  BiasFilter · ActiveFilterChips
└─ ui/             shadcn primitives (button, card, badge, table, tooltip,
                   sheet, popover, select, slider, tabs, skeleton, separator)
```

**Composition rule:** components in `data/` are pure presentational and accept primitives (`score: number`, `bias: Bias`), never domain objects. `ScoreBadge` must be usable in the table, the heatmap, the asset header, and the command palette without knowing what an asset is. Everything above them composes; nothing below them fetches.

---

## 12. Mock data structure

```
lib/mock/
├─ types.ts          ← mirrors the API contracts in architecture.md §7
├─ assets.ts         10 assets, symbol · localized name · market
├─ factors.ts        8 factors, localized name + description
├─ indicators.ts     9 indicators with bands, cadence, localized copy
├─ scores.ts         current AssetScore + AssetFactorScore per asset
├─ history.ts        90 days of score history per asset (seeded RNG)
├─ heatmap.ts        derived from scores.ts — never hand-authored
├─ calendar.ts       ~24 events across 7 days, mixed released/upcoming
└─ index.ts          typed accessors: getTopSetups(), getAsset(symbol)…
```

**Two rules that decide whether this prototype survives contact with the backend:**

1. **`types.ts` mirrors the real API response shapes exactly** — `TopSetupItem`, `AssetDetail`, `FactorContribution`, `HeatmapRow`. Later, swapping mocks for TanStack Query means replacing function bodies in `index.ts` and nothing else. Components never learn the difference.

2. **Derived data is derived, not authored.** `heatmap.ts` computes from `scores.ts`; totals compute from contributions. If a mock heatmap cell is typed by hand it will contradict the asset detail page, a stakeholder will spot it in the review, and the demo loses credibility over a typo. Every number on screen must reconcile — mock data has to obey `scoring-spec.md` arithmetic exactly.

Mock values are drawn from the worked examples already specified, so XAUUSD's mock breakdown reconciles to a real engine output.

> **Note:** the wireframes above show XAUUSD at **91 (Bullish)** per your brief, whereas `scoring-spec.md` §8 computes **12.25 (Bearish)** from a hawkish-USD input set. Both are internally correct — they are different macro environments. The mock set must commit to one. See open question 2.

---

## 13. States

Nothing feels production-quality until the non-happy paths are designed.

| State | Treatment |
|---|---|
| **Loading** | Skeletons matching exact row heights (52px), no shimmer sweep, no spinners. Layout must not shift when data lands. |
| **Empty (filters)** | Centred, `--fg-muted`: *"Шүүлтүүрт тохирох хөрөнгө олдсонгүй"* + "Шүүлтүүр цэвэрлэх" button. |
| **Low coverage** | Amber `⚠`, score in `--fg-muted`, tooltip: *"Хамралт 60%-иас доогуур — оноо найдвартай биш."* |
| **Missing cell** | `──` in `--fg-subtle`, never blank, never 0. |
| **Stale data** | Sidebar status dot amber >60min / rose >24h, plus a dismissible page banner past 24h. |
| **Error** | Inline card per section, not a whole-page takeover — a failed heatmap must not hide a working table. |

---

## 14. Responsive

| Breakpoint | Behaviour |
|---|---|
| **≥1440** | Full layout as specified. Content capped at 1280 and centred. |
| **1280–1439** | Heatmap preview drops to 6 factor columns; contribution cards `grid-cols-4`. |
| **1024–1279** | Sidebar → 64px icon rail with tooltips. Dashboard bottom row stacks. |
| **768–1023** | Sidebar → drawer behind a hamburger. Tables drop Market and Coverage columns; coverage moves into the score cell tooltip. |
| **<768** | Top Setups becomes a card list — rank, symbol, score badge, bias, sparkline. Heatmap keeps sticky first column and scrolls horizontally with a scroll-hint gradient. Waterfall rotates to vertical. |

The heatmap is never collapsed into cards. A matrix that isn't a matrix is worthless — horizontal scroll with a locked first column is the honest mobile answer.

---

## 15. Folder structure

```
frontend/
├─ app/
│  ├─ [locale]/
│  │  ├─ layout.tsx                  AppShell, fonts, next-intl provider
│  │  ├─ page.tsx                    Dashboard
│  │  ├─ markets/page.tsx
│  │  ├─ heatmap/page.tsx
│  │  ├─ calendar/page.tsx
│  │  ├─ indicators/page.tsx
│  │  ├─ assets/[symbol]/page.tsx
│  │  └─ settings/page.tsx
│  ├─ globals.css                    Tailwind v4 @theme tokens
│  └─ layout.tsx                     html/body, <html lang> per locale
├─ components/                       (see §11)
├─ lib/
│  ├─ mock/                          (see §12)
│  ├─ format.ts                      tabular numbers, %, relative time
│  ├─ tz.ts                          Asia/Ulaanbaatar rendering
│  └─ score.ts                       bias thresholds, colour mapping
├─ messages/  mn.json · en.json
├─ i18n/      routing.ts · request.ts
└─ middleware.ts                     locale detection
```

Tailwind v4 uses `@theme` in `globals.css` for tokens — no `tailwind.config.js`.

---

## 16. Resolved decisions

1. **Mock environment — bullish flagship, approved.** Built as a coherent *soft-landing* scenario: Fed cutting into resilient labour, US inflation undershooting, dollar and real yields near one-year lows, ECB still tightening. Every asset score is **derived** from one currency-factor table (`lib/mock/currency-scores.ts`) rather than hand-typed, so the heatmap, detail pages and calendar cannot contradict each other. `npm run verify:mock` enforces this.

   Delivered: **XAUUSD 91.0 / 94% coverage** (exact match to the brief), EURUSD 83.4, NASDAQ 81.1 — the brief's ordering. EURUSD and NASDAQ land within a point of the brief's illustrative 84/81; forcing them to exact integers would have required hand-typed contributions that break the arithmetic, which is the failure mode this whole approach exists to prevent.

2. **COT — included**, category `Positioning`, weight `0` in every v1 profile. Visible in the heatmap and the audit table, excluded from the waterfall and contribution cards (a 0.0 segment is noise). This makes the extension path visible at zero cost.

3. **Mongolian terminology — locked** to the approved glossary: `Өсөх хандлагатай` / `Буурах хандлагатай` / `Төвийг сахисан`, `Оноо`, `Хамралт`, `Нөлөөлөл`, `Хүчин зүйл`, `Хэвийн оноо`, `Эдийн засгийн хуанли`, and `Макро хүчний зураглал` with the `Macro Heatmap` English subtitle.

   Layout consequence: `Өсөх хандлагатай` is ~3× the width of "Bullish", so `BiasBadge` sizes to content and the bias column runs ~140px. No truncation anywhere.

### Discovered during implementation

**The heatmap needed a third cell state.** "Not scored by this market's profile" and "data missing" were both rendering as `——`, which is a false equivalence — XAUUSD does not score NFP at all, while its GDP is genuinely absent. Now three glyphs: `+2/-1/0` scored · `·` not in profile · `——` missing or stale. This also makes the DXY self-reference exclusion visible: the DXY row shows `·` in the DXY column.
