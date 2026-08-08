# Macro Scorecard — Architecture (v2)

> Status: **design only**, no implementation code yet.
> Supersedes v1. Incorporates the 12 refinement decisions.

---

## 0. Product framing

A **global macroeconomic scoring platform for Forex traders**.

- The scoring engine is market-agnostic and globally applicable.
- The *audience* is Mongolian; the *domain* is not. No Mongolian macro data, no MNT, no BoM, no domestic indicators anywhere in the model.
- Mongolian-first shows up in exactly two places: **UI language** and **default display timezone**. Nowhere in the domain model.

Non-goals: broker, charting, AI, signals, price prediction.

### 0.1 MVP asset universe (fixed)

| Asset | Market | Asset | Market |
|---|---|---|---|
| XAUUSD | Metals | USDCHF | Forex |
| EURUSD | Forex | USDCAD | Forex |
| GBPUSD | Forex | NZDUSD | Forex |
| USDJPY | Forex | DXY | DollarIndex |
| AUDUSD | Forex | NASDAQ | Index |

Crypto and XAGUSD are out of MVP scope. Currency universe: USD, EUR, GBP, JPY, AUD, CHF, CAD, NZD.

### 0.2 Data source (MVP)

> **Superseded by C3a and C2.** USD indicators and both market series are now
> backed by FRED and refreshed automatically on a per-source cadence (§6.5). The
> seven non-USD currencies remain on fixtures until C3b. The reasoning below is
> kept because it explains why the sequencing was right, not because it still
> describes the system.

**No external provider integration.** Data arrives via seeded JSON/CSV fixtures plus an admin ingestion endpoint.

This is the right sequencing call: it decouples the two hardest things in the project. The scoring engine and the UI can each be validated against known inputs with known expected outputs — see the golden-file fixtures in `scoring-spec.md` §8 — without a provider's rate limits, schema quirks, or outages sitting in the critical path. Provider integration becomes an ingestion-slice change later, behind an interface the engine never sees.

`IndicatorRelease.Source` already carries `Manual`, so seeded rows are auditable and distinguishable from imported ones from day one — no backfill needed when TradingEconomics or FRED lands.

---

## 1. Core domain principles (non-negotiable)

Every score must be **deterministic, reproducible, explainable, versioned**.

This drives four hard rules that constrain everything below:

| Rule | Consequence |
|---|---|
| **R1 — Contributors are pure functions** | No DB access, no `DateTime.Now`, no I/O inside a contributor. All data is loaded once into `ScoringContext` before evaluation. |
| **R2 — Nothing historical is ever mutated** | `AssetScores` / `AssetFactorScores` are append-only. No `UPDATE`, no `DELETE`. |
| **R3 — Scoring profiles are immutable once used** | Tuning weights creates profile **version N+1** and deactivates N. Editing a used profile silently rewrites history. |
| **R4 — Every factor row snapshots its inputs** | `RawValue` is stored, not re-derived. A later data revision must never change a past explanation. |

---

## 2. Localization

### 2.1 Two storage strategies, deliberately different

| Data | Storage | Why |
|---|---|---|
| Reference data — `Assets.Name`, `Indicators.Name`, `Indicators.Description`, `MarketSeries.Name`, `Factors.Name` | `jsonb` via owned type `LocalizedText { Mn, En }` | Small, hand-authored, editorial. Adding a locale is a data change, not a migration. |
| Runtime explanations — `AssetFactorScores.ExplanationMn` / `ExplanationEn` | Two `text` columns | Engine-generated, never arbitrarily expanded, high row volume. Flat columns index and project cheaper than jsonb, and materialized views select them directly. |

The split is intentional and should be stated in the code comments, because it otherwise looks like an inconsistency.

### 2.2 Locale resolution (backend)

Resolution order, implemented as scoped `ILocaleContext` set by middleware:

1. Authenticated user preference (future, JWT claim `locale`)
2. Cookie `NEXT_LOCALE`
3. `Accept-Language` header
4. Default `mn`

- Supported set is a **strict allowlist** `{ mn, en }`. Anything else falls back to `mn`.
- Cookie name is `NEXT_LOCALE` on both sides — `next-intl` already writes it, so the frontend and backend share one source of truth and the API client forwards it verbatim.
- Responses set `Vary: Accept-Language, Cookie` so any CDN/proxy caches per locale.
- **The API never returns `{mn, en}` pairs.** Projection to a single string happens in the handler, at the last moment.

### 2.3 UI constraints

- Font must carry full Mongolian Cyrillic (Ө, Ү, Ё). Inter / Noto Sans do.
- Mongolian strings run ~25–35% longer. No fixed-width badges or truncated bias labels.
- Factor codes (`CPI`, `NFP`, `RATE`) stay Latin in both locales — they are ticker-like identifiers, not prose.

---

## 3. Time

- All timestamps `timestamptz`, stored UTC, no exceptions.
- API returns ISO-8601 UTC. Zero server-side formatting.
- Frontend renders `Asia/Ulaanbaatar` (UTC+8) by default, user-togglable later.
- "Last updated" is rendered **relative** (`5 минутын өмнө`) — absolute times across a 12-hour offset are a reliable source of user confusion.

---

## 4. Data model

### 4.1 The `factor_code` namespace (naming change — read this first)

v1 used `IndicatorCode` on `ProfileWeights` and `AssetFactorScores`. That breaks under decision #6: `DXY`, `US10Y`, `VIX` are **series**, not indicator releases, yet they are legitimate scoring factors and legitimate heatmap columns.

Resolution: introduce one unified **factor** namespace.

- A **factor** is a scoring dimension — a heatmap column, a detail-page row, a weight key.
- A **contributor** implements exactly one factor.
- A contributor may read `IndicatorReleases`, `SeriesObservations`, or both.

```
factor RATE   ← InterestRateContributor    reads indicator releases
factor DXY    ← DollarStrengthContributor  reads market series
factor YIELD  ← YieldContributor           reads market series (US10Y_REAL)
factor CPI    ← InflationContributor       reads indicator releases
```

`ProfileWeights.FactorCode` and `AssetFactorScores.FactorCode` replace `IndicatorCode`. This is the only deviation from the stated naming, and it is what makes #6, #7 and #8 compose instead of collide.

---

### 4.2 Reference tables

**`factors`**

| Column | Type | Notes |
|---|---|---|
| id | uuid | |
| code | varchar(20) | unique — `RATE`, `CPI`, `GDP`, `PMI`, `NFP`, `RETAIL`, `DXY`, `YIELD`, `SENTIMENT`, `COT` |
| name | jsonb | LocalizedText |
| description | jsonb | LocalizedText — the education layer |
| category | smallint | Policy / Inflation / Growth / Labour / Sentiment / Positioning |
| **scope** | smallint | **CurrencyScoped / UsdScoped** — see `scoring-spec.md` §3.1 |
| display_order | int | heatmap column order |

**`assets`**

| Column | Type | Notes |
|---|---|---|
| id | uuid | |
| symbol | varchar(20) | unique, `XAUUSD` |
| name | jsonb | LocalizedText |
| market | smallint | Metals / Forex / **DollarIndex** / Index / Crypto |
| is_active | bool | |
| display_order | int | |

**`asset_currency_exposures`** — what makes the engine generic

| asset_id | currency_code | direction |
|---|---|---|
| EURUSD | EUR | +1 |
| EURUSD | USD | −1 |
| USDJPY | USD | +1 |
| USDJPY | JPY | −1 |
| XAUUSD | USD | −1 |
| NAS100 | USD | −1 |

Without this table every new pair is a code change. With it, adding GBPJPY is two rows.

**`indicators`**

| Column | Type | Notes |
|---|---|---|
| id | uuid | |
| code | varchar(20) | `CPI_YOY`, `NFP`, `PMI_MFG`, `POLICY_RATE`, `GDP_QOQ`, `RETAIL_MOM` |
| name / description | jsonb | LocalizedText |
| category | smallint | |
| currency_direction | smallint | `+1` = higher reading strengthens the currency (CPI, RATE, NFP); `−1` = weakens |
| impact | smallint | High / Medium / Low |
| unit | smallint | Percent / Thousands / Index / Absolute |
| **band_minor** | numeric(18,6) | surprise threshold for ±1 — `scoring-spec.md` §4.1 |
| **band_major** | numeric(18,6) | surprise threshold for ±2 |
| **max_age_days** | int | beyond this the factor is unavailable, not stale |

`currency_direction` keeps normalization generic instead of per-indicator special-casing.

Surprise bands live here rather than in code or on the profile because they are **indicator-intrinsic** — a 50k NFP surprise and a 0.1pp CPI surprise are not comparable quantities. Putting them in code would mean a deploy per new indicator, contradicting the data-driven design.

**`market_series`**

| Column | Type | Notes |
|---|---|---|
| id | uuid | |
| code | varchar(30) | `DXY`, `US10Y`, `US10Y_REAL`, `US02Y`, `VIX`, `DE10Y`, `JP10Y` |
| name / description | jsonb | LocalizedText |
| unit | smallint | |
| frequency | smallint | Daily / Weekly / Intraday |
| source | smallint | |
| **max_age_days** | int | staleness gate, default 5 for daily series |

> **Derived series are stored, not computed at scoring time.** Real yield = nominal − breakeven is calculated at *ingestion* and persisted as `US10Y_REAL`. Computing it inside a contributor would violate R1 and make the value unauditable.

---

### 4.3 Fact tables

**`indicator_releases`**

| Column | Type | Notes |
|---|---|---|
| id | uuid | |
| indicator_id | uuid | |
| currency_code | char(3) | **not** `country` — scoring cares about currency. Eurozone CPI is one release for EUR. |
| period | date | which month/quarter the data *describes* |
| actual / forecast / previous | numeric(18,6) | nullable — a calendar entry exists before the number lands |
| revision | int | `0` = initial print, increments on restatement |
| source | smallint | TradingEconomics / FRED / ECB / FED / Manual |
| source_ref | varchar(200) | external id or URL — audit trail |
| released_at | timestamptz | |
| imported_at | timestamptz | |

- Unique index `(indicator_id, currency_code, period, revision)` — prevents double-ingestion, the single most common data bug in this class of app.
- Index `(currency_code, released_at DESC)`.
- "Latest release" = highest `revision` within the latest `period`.
- `numeric`, never `float`. Comparing interest rates in floating point will bite you.

**`series_observations`**

| Column | Type |
|---|---|
| id | uuid |
| series_id | uuid |
| observed_at | timestamptz |
| value | numeric(18,6) |
| source | smallint |

- Unique `(series_id, observed_at)`; index `(series_id, observed_at DESC)`.
- This table grows fastest. Candidate for monthly partitioning later; not needed at MVP.

**`scoring_profiles`**

| Column | Type | Notes |
|---|---|---|
| id | uuid | |
| name | varchar(80) | `Forex Default` |
| description | jsonb | LocalizedText |
| version | int | |
| market | smallint | |
| is_active | bool | |
| bullish_threshold | numeric(5,2) | default 65 |
| bearish_threshold | numeric(5,2) | default 35 |
| min_coverage | numeric(4,3) | default 0.600 — see §5.4 |
| created_at | timestamptz | |

- Unique `(market, version)`.
- Partial unique index `(market) WHERE is_active` — exactly one active profile per market, enforced by the database rather than by hope.
- **R3**: once a profile has produced an `AssetScore`, it is frozen. Tuning = insert version N+1, flip `is_active`. Enforced application-side, guarded by a DB trigger.

**`profile_weights`**

| Column | Type |
|---|---|
| id | uuid |
| profile_id | uuid |
| factor_code | varchar(20) |
| weight | numeric(5,2) |
| **polarity** | smallint (+1 / −1) |
| is_enabled | bool |

Unique `(profile_id, factor_code)`.

`polarity` exists because the currency-exposure abstraction **inverts the wrong way for equity indices** — strong US GDP is bullish for USD *and* bullish for NASDAQ, and no pure currency mapping gets both right. See `scoring-spec.md` §5.2. Forex and Metals profiles use `+1` throughout; the Index profile is where it earns its keep.

> **The weight rows *are* the contributor selection.** A profile with no enabled row for `GDP` simply does not run `GDPContributor`. This unifies decisions #7 and #8 — there is no second configuration surface for "which contributors participate."

**`asset_scores`** (append-only)

| Column | Type | Notes |
|---|---|---|
| id | uuid | |
| asset_id | uuid | |
| score | numeric(5,2) | 0–100 |
| bias | smallint | Bullish / Neutral / Bearish |
| coverage | numeric(4,3) | participating weight ÷ total profile weight |
| is_sufficient | bool | `coverage >= profile.min_coverage` |
| scoring_profile_id | uuid | |
| profile_version | int | denormalized snapshot |
| engine_version | varchar(20) | |
| data_as_of | timestamptz | newest input included |
| calculated_at | timestamptz | |
| calculation_duration_ms | int | profiling (#9) |

Index `(asset_id, calculated_at DESC)`.

`data_as_of` ≠ `calculated_at`: if the job runs hourly but no new release landed, the score is fresh while the *data* is three days old. Users trading the US session overnight from UTC+8 need to know which. The dashboard's "Last Updated" shows `data_as_of`.

**`asset_factor_scores`** (append-only)

| Column | Type | Notes |
|---|---|---|
| id | uuid | |
| asset_score_id | uuid | |
| factor_code | varchar(20) | **denormalized on purpose** — immutable audit record |
| raw_value | numeric(18,6) | what the data actually said (R4) |
| normalized_score | smallint | **−2 … +2 → the heatmap cell** |
| weight | numeric(5,2) | from the profile |
| contribution | numeric(6,2) | **weighted, in score points → the detail-page row** |
| explanation_mn | text | |
| explanation_en | text | |

`factor_code` is a string, not an FK, precisely so that renaming or retiring a factor cannot alter a historical explanation.

> **Spec discrepancy worth resolving now:** your heatmap shows `+2 / −1 / 0` and your detail page shows `+18 / −7`. Those are two different quantities — a normalized signal and a weighted contribution. Both are stored, computed in the same pass. If only one were stored the other page would reverse-engineer it and drift.

---

## 5. Scoring engine

### 5.1 Composition

```
IScoringStrategyResolver
        │  (by asset.Market)
        ▼
IScoringStrategy
        │
        ▼
MacroScoringStrategy ──── loads active ScoringProfile
        │
        ├─ IScoreContributor  "RATE"      InterestRateContributor
        ├─ IScoreContributor  "CPI"       InflationContributor
        ├─ IScoreContributor  "GDP"       GDPContributor
        ├─ IScoreContributor  "PMI"       PMIContributor
        ├─ IScoreContributor  "NFP"       EmploymentContributor
        ├─ IScoreContributor  "RETAIL"    RetailSalesContributor
        ├─ IScoreContributor  "DXY"       DollarStrengthContributor
        ├─ IScoreContributor  "YIELD"     YieldContributor
        └─ IScoreContributor  "SENTIMENT" SentimentContributor
```

Contributors resolve from a keyed registry `IReadOnlyDictionary<string, IScoreContributor>` built at startup by assembly scan.

**One concrete strategy in MVP.** With weights and contributor selection both in the database, Gold vs. Forex is a *data* difference, not a code difference — `MacroScoringStrategy` handles both. `IScoringStrategy` is retained as the extension seam for a future market that genuinely aggregates differently (Crypto may need on-chain/flow inputs that don't fit the release/series model). Shipping one implementation now and adding `CryptoScoringStrategy` when it's actually justified is cheaper than four near-identical classes.

### 5.2 Contract sketch

```
ScoringContext
    Asset                       (incl. currency exposures)
    ScoringProfile              (incl. enabled weights)
    LatestReleases              (factorCode, currency) → IndicatorRelease
    SeriesWindows               seriesCode → ordered SeriesObservation[]
    AsOfUtc

IScoreContributor
    string FactorCode
    bool   CanEvaluate(ScoringContext)          // false when required data is absent
    FactorContribution Evaluate(ScoringContext)

FactorContribution
    FactorCode, RawValue, NormalizedScore (−2..+2),
    Weight, Contribution, ExplanationMn, ExplanationEn
```

**One contributor emits one factor row**, even for a pair. `InterestRateContributor` on EURUSD computes the *differential* (EUR side minus USD side, via `asset_currency_exposures`) and emits a single `RATE` row — matching one heatmap cell and one detail-page row.

Data loading happens **once**, in `ScoringDataLoader`, before any contributor runs. This satisfies R1 and eliminates N+1 queries in the same stroke.

### 5.3 Normalization

Three shared normalizers in `Scoring/Normalization/`, all deterministic, all integer output in `[−2, +2]`:

| Normalizer | Input | Used by |
|---|---|---|
| `SurpriseNormalizer` | `(actual − forecast)` scaled by trailing surprise dispersion | CPI, NFP, GDP, PMI, RETAIL |
| `TrendNormalizer` | `actual` vs `previous` plus n-period direction | RATE, policy path |
| `PercentileNormalizer` | percentile rank over a trailing window of observations | DXY, YIELD, SENTIMENT |

Thresholds live in code for v1 and are documented in `scoring-spec.md`, pinned by `engine_version`. Moving thresholds into the database is a v2 option; doing it now would add a configuration surface with no consumer.

### 5.4 Aggregation math (explicit, because it must be reproducible)

```
participating = profile weights that are enabled AND whose contributor could evaluate

weightedSum   = Σ ( normalized_i × weight_i )          normalized ∈ [−2, +2]
maxAbs        = Σ ( 2 × weight_i )                     over participating only
contribution_i= normalized_i × weight_i × (50 / maxAbs)    → score points
score         = 50 + Σ contribution_i                   → 0..100
coverage      = Σ participating weight ÷ Σ all enabled weight
bias          = score ≥ bullish_threshold → Bullish
                score ≤ bearish_threshold → Bearish
                else Neutral
```

Two consequences worth stating on the detail page:

1. **Neutral baseline is 50.** The UI should render a `Baseline 50` row so the arithmetic visibly closes: `50 + 41 = 91`. Your example figures (`18+10+6+8−4−7+12+8 = 51`) were illustrative rather than arithmetic; this model makes them add up.
2. **`coverage` guards the ranking.** Because `maxAbs` counts only participating factors, an asset with two of eight factors present can post a confident-looking 88. Assets below `min_coverage` are flagged `is_sufficient = false` and excluded from Top Setups (still visible on their own detail page, with a "insufficient data" notice). Without this, the dashboard's headline ranking is quietly wrong whenever a feed lags. **This is an addition beyond the stated 12 decisions — flagged for your approval.**

---

## 6. Read model — indexed SQL now, materialized views later

**Decision: no materialized views in the MVP.** At ~10 assets × 8 factors ≈ 80 rows, an indexed query is sub-millisecond and an MV would add a staleness surface and refresh coordination for zero measurable gain.

The requirement is that introducing MVs later must not change the API contract. Three rules achieve that:

### R5 — Read queries live behind named read-model classes

```
Infrastructure\Database\ReadModel\
    LatestAssetScoresQuery.cs     → IReadOnlyList<TopSetupItem>
    HeatmapQuery.cs               → HeatmapProjection
    AssetDetailQuery.cs           → AssetDetailProjection
```

Handlers depend on these, never on ad-hoc LINQ against `DbContext`. Swapping the body from a join to `SELECT * FROM mv_heatmap_latest` touches one file each and no contract.

### R6 — Shape the query today exactly as the MV would materialize it

Both the current query and the future MV return **long format**: `(asset_id, symbol, market, factor_code, normalized_score, contribution, data_as_of)`. Pivoting into `HeatmapRow` happens in the API projection, not in SQL.

Postgres cannot pivot a dynamic column set, and the factor list *will* change. Building the pivot in the projection layer now means the MV drops in underneath without disturbing anything above it.

### R7 — Staleness semantics are already in the contract

Every read response carries `dataAsOf` and `calculatedAt`. Today they are exact; under an MV they become "as of last refresh." Because clients already consume them, MV-induced lag is expressible without a contract change — which is the actual thing that makes MVs a breaking change if you defer these fields.

### Supporting indexes (MVP)

```
asset_scores (asset_id, calculated_at DESC)              -- latest per asset, history
asset_scores (calculated_at DESC) WHERE is_sufficient    -- top setups ranking
asset_factor_scores (asset_score_id)                     -- detail + heatmap fan-out
indicator_releases (currency_code, released_at DESC)
series_observations (series_id, observed_at DESC)
```

**When to revisit:** roughly 200+ assets, or when the dashboard renders historical matrices rather than a single latest snapshot. At that point the MVs need a unique index each — without one, `REFRESH MATERIALIZED VIEW CONCURRENTLY` is unavailable and every refresh takes an `ACCESS EXCLUSIVE` lock that blocks the dashboard mid-read. Recording that here so it isn't rediscovered under load.

---

## 6.5 Ingestion scheduling — per-source cadence (C2)

**Decision: one schedule row per provider-backed source, not one nightly job.**

A single nightly job is the simple answer and it is wrong for the same reason a
single `MaxAgeDays` was wrong in C3a: sources do not share a rhythm. GDP prints
quarterly, the Fed funds target republishes every business day, and DTWEXBGS is
observed daily but *published* weekly. A nightly job either polls the quarterly
series 90 times for nothing or surfaces the daily one up to 24 hours late.

### Three numbers, three meanings

Collapsing these is the mistake the design exists to prevent.

| | Meaning | Source |
|---|---|---|
| `Cadence` | How often the provider **publishes** | `SeedData.CadenceFor` |
| `CheckInterval` | How fast we want to **notice** | `SyncCadencePolicy.CheckInterval` |
| `OverdueAfter` | How long silence is **tolerable** | `SyncCadencePolicy.OverdueAfter` |

The check interval is always far shorter than the cadence — FRED does not publish
an exact release instant, so noticing a monthly print within hours means asking
twice a day. Cadence sets patience, not polling frequency.

| Cadence | Check every | Overdue after | Sources |
|---|---|---|---|
| Daily | 6h | 4d | `US10Y_REAL`, `POLICY_RATE` |
| Weekly | 12h | 10d | `DXY` |
| Monthly | 12h | 45d | `CPI_YOY`, `NFP`, `RETAIL_MOM`, `PMI_MFG` |
| Quarterly | 24h | 120d | `GDP_QOQ` |

> `POLICY_RATE` is resampled to month-end for scoring (`LevelMonthly`) but polled
> **daily**, because cadence follows the source, not the transform. `DXY` is the
> mirror case. Both are pinned by tests.

### Where the state lives

`sync_schedules` is a separate table, not columns on `indicators` / `market_series`.
Those are reference data that barely change after seeding; a schedule row is
rewritten on every poll. Mixing them would dirty the catalogue dozens of times a
day for reasons unrelated to the catalogue.

`LastSuccessAt` and `LastChangeAt` are deliberately distinct. A monthly source
answers successfully sixty times between prints — only the gap since the last
**change** says whether a feed has gone quiet.

### The tick

```
claim due rows (push NextDueAt forward BEFORE the network call)
  └─ poll each source on its own cadence
       success → failures = 0, NextDueAt = now + CheckInterval
       change  → LastChangeAt = now
       failure → failures++, NextDueAt = now + min(5min·2^(n-1), CheckInterval)
  └─ if anything changed → recalculate ONCE
```

Claiming before the call means a process that dies mid-tick costs one interval
rather than retrying the same source on every restart. Backoff is capped at the
normal interval so a recovered source returns to its ordinary rhythm instead of
serving out a long exponential.

**At most one recalculation per tick.** Scoring reads the whole universe in a
fixed number of queries, so running it once after five sources landed costs the
same as running it after one — and running it five times would write five
near-identical score rows.

### Concurrency

Ticks are serialised by a Postgres **advisory lock** (`pg_try_advisory_lock`),
taken at the start of a tick and released in a `finally`. A tick that cannot take
the lock returns immediately rather than queueing.

This was not the original design. C2 shipped with claiming alone and a note that
it was "the cheap half of a lock" — C3b then produced the collision the note
predicted: the background worker and a manual `force=true` run overlapped, both
fetched all 21 sources, and the second violated the unique index on
`indicator_releases` after the first had inserted. Claiming stops a *later* tick
from re-picking a source; it does nothing about a tick already in flight, and
`force` skips the due check entirely.

Session-level rather than transaction-level: a tick makes minutes of HTTP calls,
and holding a transaction open across them would pin a connection and block
vacuum. Cluster-wide, so it covers multiple instances as well as one.

### Known limits
- **No release-time knowledge.** FRED stamps a period, not a publication instant,
  so this polls rather than subscribes. A provider that published a calendar
  would let the check interval collapse to a scheduled wake-up.
- `Scheduler:Enabled=false` turns the worker off entirely; `POST /admin/sync/run`
  still works, which is how the manual and scheduled paths are kept identical.

---

## 6.6 International data sources (C3b)

**Decision: three multilateral providers, not seven national statistics agencies.**

Seven agencies (ONS, StatCan, ABS, Stats NZ, SNB, e-Stat, ECB) would have meant
seven formats, seven auth schemes and seven things to keep working. BIS,
Eurostat and the OECD each carry many countries in one dataflow.

### What each provider actually supplies

| Factor | Weight | Provider | Currencies |
|---|---|---|---|
| `RATE` | 31 | **BIS** `WS_CBPOL` | EUR, GBP, JPY, AUD, CHF, CAD, NZD |
| `CPI` | 17 | **Eurostat** `prc_hicp_minr` | EUR, CHF |
| `CPI` | 17 | **OECD** `DSD_PRICES@DF_PRICES_ALL` | GBP, AUD, CAD, NZD |

One BIS dataflow made the heaviest factor in the Forex profile real for all
seven currencies at once. CPI needs two providers only because neither is
current for all six.

### Dead ends, each verified against the live API before any code was written

| Candidate | Why it was rejected |
|---|---|
| ECB `ICP` | Discontinued February 2026 (methodology change) |
| Eurostat `prc_hicp_manr` | Archived; series ends 2025-12 |
| OECD `DF_QNA` (GDP) | Ends 2024-Q1 for every area needed |
| OECD CPI for JPY | Ends **2021-06** |
| OECD CPI for EUR / CHF | Ends 2025-12 — the reason Eurostat carries those two |

Checking first is the whole lesson of C3a repeated: three integrations that look
obvious from the documentation are dead in the data.

### Labour: the unemployment rate, not payrolls

| Currency | Provider | Key |
|---|---|---|
| EUR, CHF | Eurostat `une_rt_m` | `unemployment:EA21`, `unemployment:CH` |
| GBP, JPY, AUD, CAD, USD | OECD `DSD_LFS@DF_IALFS_UNE_M` | `unemployment:GBR` … |

**USD moved off payrolls.** Payrolls carry their signal in the surprise against
consensus, which no free provider publishes — the same wall that forced engine
v2.0.0. Scoring USD on payrolls while every other currency scored on the
unemployment rate would also compare two different statistics across a pair. NFP
stays in the catalogue and on the calendar; it is not a scoring source.

Japan is current in the OECD labour dataset even though its OECD CPI died in
2021, which is why this integration is the single largest gain for USDJPY.

NZD is absent: New Zealand publishes labour data quarterly only, and a quarterly
series cannot reach the 12 readings the level component needs inside the 500-day
load window — the same structural objection as GDP.

### Deliberate gaps

- **JPY CPI** — no free current source; e-Stat requires a registered key.
- **NZD labour** — quarterly only.
- **GDP, PMI, retail** — for all seven. PMI is licensed everywhere, exactly as it
  is for USD.

### Two systemic fixes this phase forced

**Releases are stamped at the period END.** Providers give a period, never a
publication instant. Stamping the period *start* back-dates a monthly reading by
a month and a quarterly one by a quarter — enough that NZD's CPI read as four
months old while being three weeks old, and enough that every unemployment
reading would have arrived already expired. A reading covering June cannot have
been published before 30 June, so the period end is both closer to the truth and
never optimistic.

**Seed reference data is reconciled on every startup.** `SeedReferenceAsync`
returns early when the database is populated, so changes to staleness windows,
bands and display names never reached an existing instance. C3b raised CPI's
`MaxAgeDays` from 75 to 130 and the change silently did not land — the symptom
looked like a threshold that was too tight rather than one that was never
applied. Reference data is configuration and must reach existing rows; scores
snapshot their own inputs (R4), so this can never rewrite history.

### One indicator per factor per currency

Several indicators may feed one factor — that is the point of the `FactorCode`
namespace. But two indicators feeding one factor for the *same currency* makes
the loader's choice arbitrary: it picks a single release per (factor, currency),
and with equal periods the tiebreak between two different indicators is
undefined. USD carried exactly this collision when payrolls and the unemployment
rate both fed LABOUR.

Two consequences: the fixture supersede-delete is now scoped to the **factor**
rather than the indicator, and a test asserts the seed mappings contain no such
collision.

These keep their fixtures. Deletion of superseded rows is scoped to the exact
(indicator, currency) being synced, which is what allows a currency to be partly
real rather than forcing the whole currency to move at once.

### Provenance became a fraction

`IsFullyReal` alone stopped being answerable. After C3b the euro's policy rate and
CPI are genuine while its growth and sentiment factors are fixtures — a
currency-level flag would have reported EUR as fully real on the strength of two
factors out of eight.

`ProvenanceQuery` now measures the **share of an asset's profile weight** backed
by provider data, per (currency, factor), and the badge shows the percentage:

```
DXY / XAUUSD / NASDAQ   100%   fully real
six FX pairs             60%   RATE 31 + CPI 17 + DXY 7 + YIELD 5
USDJPY                   43%   no CPI — the JPY gap, visible rather than hidden
```

A factor counts only when *every* currency the asset is exposed to has it from a
provider: a differential computed from one real and one synthetic reading is not
a real number.

### Schema

`IndicatorSource` — one row per (indicator, currency) carrying provider, series
id, transform and cadence. A row rather than a column on `Indicator` because the
answer is per-currency: CPI comes from three different providers depending on who
is asking. Adding a currency is now a seed row, not a code change.

`IReleaseProvider` is deliberately one method wide. FRED, BIS, Eurostat and the
OECD disagree about formats, key encodings and which statistics they carry;
anything richer would be a lowest common denominator that fits none of them.

---

## 6.7 Scoring profile v2 — five factors

**Decision: retire every factor that could not be scored honestly.**

v1 weighted nine factors. Four are gone — not because they do not matter to a
trader, but because the model could not measure them:

| Retired | Why it could not be scored |
|---|---|
| **GDP** | Quarterly, so it can never reach the 12 readings the level component needs inside the 500-day load window. Direction-only, capped at ±1, and three months stale by construction. |
| **PMI** | ISM and S&P Global are licensed. The USD figure was a regional Fed proxy on a 0-centred scale; no free equivalent exists for the other six currencies. |
| **RETAIL** | Never sourced beyond USD. A factor present on one side of a pair cannot form a differential. |
| **COT** | Weight was already zero. The row added a heatmap column and nothing else. |
| **NFP** | Its signal is the surprise against consensus, which no free provider publishes. Replaced by `LABOUR` — the harmonised unemployment rate, comparable across countries. |

All five keep their catalogue entries: historical factor rows are stamped with
those codes and rule R2 forbids rewriting them. A retired factor is inert, never
deleted, and the heatmap now takes its columns from the **active profiles**
rather than the catalogue so retired ones do not occupy empty columns.

### The counterintuitive result

Removing four factors made the model **more** real, not less:

```
                     before v2          after v2
coverage             0.910 (best)       1.000 (every asset)
fully real assets    3 of 10            7 of 10
worst real share     0.57 (USDJPY)      0.76 (USDJPY)
```

The retired factors were precisely the ones still running on fixtures. The model
was not measuring growth and sentiment — it was measuring seed data and calling
the result 91% covered.

### Weights

| | RATE | CPI | LABOUR | DXY | YIELD |
|---|---|---|---|---|---|
| Forex | 34 | 24 | 21 | 13 | 8 |
| Metals | 26 | 14 | — | 30 | 30 |
| DollarIndex | 40 | 25 | 25 | — | 10 |
| Indices | 34 (−1) | 14 (−1) | 9 (+1) | 8 (−1) | 35 (−1) |

Weights keep v1's relative judgement, renormalised. The one deliberate deviation
is DollarIndex's RATE: a straight renormalisation gave 45, and with four factors
a score that is nearly half one input stops being a multi-factor reading.

### Inflation is scored against the mandate (engine v2.1.0)

The substantive change. v2.0.0 asked "is this inflation print high for this
country" — an own-history percentile. v2.1.0 asks "does the central bank still
have work to do":

```
gap        = CPI − target
level      = gap >  tolerance → +1   (above target, tightening bias)
             gap < −tolerance → −1   (below target, easing bias)
direction  = is |gap| widening or narrowing
n          = clamp(level + direction, −2, +2)
```

Above target is positive for the currency, because the bank is pushed toward
tightening. Direction is measured on the **gap**, not the raw change: inflation
rising from 1.0 to 1.5 against a 2% target is an undershoot closing, the opposite
of rising from 2.5 to 3.0.

The label carries the reasoning:

```
before   2.9% · 81st percentile of history (prev. 2.8%)
after    2.9% · 0.9pp above target (2.0%), diverging
```

Targets live in `currency_policies` — published mandates, seeded as
configuration, tolerance being half the official band where one exists and 0.5pp
otherwise. More objective than a percentile, not less: a percentile shifts as
history accumulates, a mandate does not.

### Real policy rate is context, not a factor

Nominal minus inflation is what a macro desk quotes, and it is displayed — but it
is **not weighted**. With RATE and CPI both in the profile, the real rate is a
linear combination of the two: it adds no independent information to the
aggregate and would only count inflation twice with opposite signs, partially
cancelling it. `TargetGapNormalizer.RealRate` computes it for display.

### Every factor records both sides

A pair's factor score is a **differential** — `(base − quote) / 2` — but the cell
only ever showed the base currency's raw value. A reader saw "6.3%" beside "−1"
and could not reconcile them, which is the one thing the product promises.

`asset_factor_scores.readings` now stores one entry per currency: its own
normalized score, raw value and label. jsonb rather than a child table — the list
is one or two entries, it is always read with its parent row, and nothing queries
inside it.

The UI writes the arithmetic out for both shapes:

```
PAIR BREAKDOWN
GBP  base   4.9% · 84th percentile of 57 readings   −1
USD  quote  4.2% · 64th percentile of 58 readings   +1
            (−1 − +1) ÷ 2                            −1

READING
USD         119.70 · 37th percentile of 1Y          −1
            −1 × exposure (−1)                      +1
```

The single-reading form needs the formula as much as the pair does: a USD-scoped
factor on a pair where the dollar is the quote currency flips sign, so the reading
shows −1 while the row shows +1.

> **A boundary lesson.** Adding the field turned the asset page into a 500 for
> every symbol whose response was still warm in Next's 60-second cache — the body
> predated the field the types declared as required. `apiAsset` now normalises at
> the boundary. A type is a compile-time claim about the *current* API; only the
> boundary can make it true at runtime.

### Shipping a new profile version

`SeedData.ProfileVersion` is the whole ceremony. Reconciliation inserts the new
version, deactivates the previous one, and never edits a used profile (rule R3) —
so every score already written keeps pointing at the profile that produced it and
stays reproducible. No migration, no manual SQL.

---

## 7. API contracts

Base: `/api/v1`. All responses already localized. All timestamps UTC ISO-8601.

| Method | Route | Purpose |
|---|---|---|
| GET | `/top-setups?market=&bias=&minScore=&limit=` | Dashboard ranked list (from `mv_latest_asset_scores`) |
| GET | `/heatmap?market=` | Matrix (from `mv_heatmap_latest`) |
| GET | `/assets/{symbol}` | Detail: overall score, bias, factor rows |
| GET | `/assets/{symbol}/history?from=&to=` | Score history |
| GET | `/factors` | Factor catalogue + localized descriptions (education layer) |
| GET | `/indicators` | Indicator catalogue |
| POST | `/admin/releases` | Ingest indicator release (idempotent on natural key) |
| POST | `/admin/series/{code}/observations` | Ingest series observations |
| POST | `/admin/scores/calculate` | Trigger a scoring run |
| GET | `/meta/profiles/{market}/active` | Active profile + weights — transparency |
| POST | `/admin/releases/sync?indicatorCode=&currency=` | Ingest releases from the mapped provider |
| GET | `/admin/sync/status` | Per-source schedule, cadence, last change, overdue flag |
| POST | `/admin/sync/run?force=` | Run one scheduler tick manually (same path as the worker) |

Representative shapes:

```
TopSetupItem      { symbol, name, market, score, bias, coverage, dataAsOf }
AssetDetail       { symbol, name, market, score, bias, baseline: 50,
                    coverage, isSufficient, engineVersion, profileVersion,
                    dataAsOf, calculatedAt, factors: FactorContribution[] }
FactorContribution{ factorCode, factorName, category, rawValue,
                    normalizedScore, weight, contribution, explanation }
HeatmapResponse   { factors: FactorSummary[], rows: HeatmapRow[] }
HeatmapRow        { symbol, name, market, cells: { factorCode, normalizedScore }[] }
```

`GET /meta/profiles/{market}/active` exists because "transparent scoring" is the product promise. Users should be able to see the exact weights that produced their number.

---

## 8. Folder structure

```
D:\ScorecardProject\
├─ backend\
│  ├─ src\Scorecard.Api\
│  │  ├─ Features\
│  │  │  ├─ TopSetups\GetTopSetups\        Endpoint, Request, Response, Handler
│  │  │  ├─ Heatmap\GetHeatmap\
│  │  │  ├─ Assets\GetAsset\ , GetAssetHistory\
│  │  │  ├─ Factors\ListFactors\
│  │  │  ├─ Indicators\ListIndicators\
│  │  │  ├─ Ingestion\IngestRelease\ , IngestObservations\
│  │  │  ├─ Profiles\GetActiveProfile\
│  │  │  └─ Scores\CalculateScores\
│  │  ├─ Scoring\                          ← domain module, NOT a slice
│  │  │  ├─ Abstractions\   IScoringStrategy, IScoreContributor,
│  │  │  │                  ScoringContext, FactorContribution
│  │  │  ├─ Contributors\   InterestRate, Inflation, GDP, PMI, Employment,
│  │  │  │                  RetailSales, DollarStrength, Yield, Sentiment
│  │  │  ├─ Normalization\  Surprise, Trend, Percentile
│  │  │  ├─ Strategies\     MacroScoringStrategy
│  │  │  ├─ ScoringDataLoader.cs
│  │  │  ├─ ScoringEngine.cs
│  │  │  └─ EngineVersion.cs
│  │  ├─ Infrastructure\
│  │  │  ├─ Database\
│  │  │  │  ├─ AppDbContext.cs, Configurations\
│  │  │  │  ├─ ReadModel\   LatestAssetScoresQuery, HeatmapQuery,
│  │  │  │  │               AssetDetailQuery      ← MV swap point (§6)
│  │  │  │  └─ Seed\        reference data + fixtures\*.json
│  │  │  ├─ Localization\ LocalizedText, LocaleContext, middleware
│  │  │  └─ Time\       IClock
│  │  ├─ Shared\        Result, IEndpoint, ValidationFilter, Paging
│  │  ├─ Migrations\
│  │  └─ Program.cs
│  └─ tests\Scorecard.Api.Tests\
│     ├─ Scoring\                          ← where the real test effort goes
│     └─ Features\
├─ frontend\                               ← TypeScript
│  ├─ app\[locale]\
│  │  ├─ layout.tsx
│  │  ├─ page.tsx                          dashboard: top setups + heatmap + filters
│  │  └─ assets\[symbol]\page.tsx
│  ├─ components\ui\ , top-setups\ , heatmap\ , asset-detail\
│  ├─ lib\ api-client.ts, query-client.ts, format.ts, tz.ts
│  ├─ types\ api.ts                        ← generated from OpenAPI
│  ├─ messages\ mn.json, en.json
│  ├─ i18n\ routing.ts, request.ts
│  └─ middleware.ts
├─ docker\ Dockerfile.api, Dockerfile.web, docker-compose.yml
└─ docs\
   ├─ architecture.md                      ← this file
   ├─ adr\
   ├─ glossary-mn.md                       ← terminology contract (you author)
   └─ scoring-spec.md                      ← the actual weights, in prose, before code
```

### Structural decisions

- **`Scoring/` is a sibling of `Features/`, not inside it.** Vertical slicing says don't share prematurely; the engine is the one thing legitimately shared (written by `CalculateScores`, read by `GetAsset` and `GetHeatmap`) and it has domain rules independent of HTTP. Forcing it into a slice would create slice-to-slice imports, which is worse than a named domain module.
- **No repository pattern.** `DbContext` is injected into handlers directly — it is already a unit of work plus repository.
- **No MediatR.** Endpoint → Handler → DbContext. Three hops, fully traceable. MediatR earns its cost through pipeline behaviors at scale, not at nine endpoints.
- **Endpoints auto-register** via an `IEndpoint` marker and assembly scan, so adding a slice never touches `Program.cs`.
- **Frontend types are generated from the OpenAPI document**, not hand-written. Hand-maintained types for nested models drift from the API within weeks; generation makes the contract enforced rather than aspirational.
- **`docs/scoring-spec.md` is written before `Scoring/*.cs`.** Weights are a product decision. Writing them in prose first prevents the classic failure where scoring logic quietly becomes whatever the code happens to do.

---

## 9. Decision log

**Approved and incorporated:** `FactorCode` namespace · single `MacroScoringStrategy` composed from contributors · `Coverage` / `IsSufficient` · immutable versioned profiles · baseline score 50 · no materialized views in MVP (read model designed for later swap, §6) · manually seeded data with admin ingestion · fixed 10-asset universe.

**Discovered while specifying the scoring model** (detail in `scoring-spec.md`, consolidated in its §11):

| Addition | Why it is not optional |
|---|---|
| `factors.scope` | Without it, `DXY` and `YIELD` are halved on every pair by the differential rule |
| `profile_weights.polarity` | Without it, equity indices score growth data backwards |
| `indicators.band_minor` / `band_major` | Surprise thresholds must be data, or every new indicator is a deploy |
| `indicators.max_age_days`, `market_series.max_age_days` | Stale data is worse than missing data — it looks authoritative |
| `Market.DollarIndex` | Prevents the DXY asset being scored by the DXY factor |

**Discovered while automating ingestion (C2):**

| Addition | Why it is not optional |
|---|---|
| `sync_schedules.cadence` | Publication rhythm differs per source; a nightly job is either wasteful or late |
| `sync_schedules.last_change_at` | Distinct from `last_success_at` — a monthly feed succeeds sixty times between prints, so only the gap since a *change* detects silence |
| Claim-before-poll | A process that dies mid-tick must cost one interval, not retry forever on restart |
| Capped backoff | Uncapped exponential leaves a recovered source stranded long after the provider is healthy |

**Discovered while integrating international sources (C3b):**

| Addition | Why it is not optional |
|---|---|
| `indicator_sources` | CPI comes from three providers depending on the currency; a column on `Indicator` cannot say that |
| Advisory lock on the tick | Claiming does not stop a tick already in flight; two overlapping runs violated the release unique index |
| `RealShare` instead of a boolean | After C3b every FX pair is partly real, and one flag cannot distinguish 60% from 5% |
| `CPI_YOY.max_age_days` 60 → 130 | One indicator now serves seven currencies and New Zealand publishes quarterly |

**Still open:**

1. **`glossary-mn.md` ownership** — the Mongolian explanation templates in `scoring-spec.md` §9 are drafts pending native review. This blocks UI copy, not backend work.
2. **Seed fixture authorship** — who supplies the ~8 currencies × 6 indicators of realistic release data, plus DXY / US10Y_REAL history (≥60 observations for the percentile window to activate).
