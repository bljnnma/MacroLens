# Scoring Specification (Engine v2.0.0)

> **v2.0.0 — release factors moved from surprise-vs-consensus to level + direction.**
>
> Why: FRED publishes *time series*, not calendar events with a survey consensus,
> and consensus is the one thing no free source provides. Rather than force an
> event model onto series data, release factors now score the same way `RATE`
> always has — a level percentile within the indicator's own history, plus the
> direction of the latest move.
>
> This is a major bump because it redefines what a score *means*, not merely how
> one is computed. It also matches the product's claim more honestly: the
> dashboard measures the standing macro backdrop, not repricing at the instant of
> a release. Per-indicator surprise bands (`band_minor` / `band_major`) are no
> longer used; they are retained so v1.0.0 scores stay explainable.
>
> Sections below still describing surprise normalization document **v1.0.0**.


> Status: **design only**. This document defines the scoring model in full before any implementation.
> Pinned by `engine_version = "1.0.0"`. Every `AssetScore` records this value; changing anything in this document requires a new engine version.

---

## 1. Design constraints

Restating what the model must satisfy, because every rule below is downstream of these:

| | |
|---|---|
| **Deterministic** | Same inputs → same outputs, always. No randomness, no wall-clock reads inside contributors, no floating-point accumulation order dependence. |
| **Reproducible** | A score from six months ago must be re-derivable from its stored factor rows. |
| **Explainable** | Every number on screen traces to a raw value, a normalization rule, and a weight. |
| **Versioned** | Engine version + profile version stamped on every score. |
| **Transparent** | A user can read this document and hand-compute any score on the site. |

---

## 2. Vocabulary

| Term | Meaning | Range |
|---|---|---|
| `c` | **Currency factor score** — how bullish a factor is *for a currency* | −2 … +2 (integer) |
| `s` | **Asset signal** — `c` mapped onto an asset via its exposures | −2 … +2 (real, pre-round) |
| `n` | **Normalized score** — rounded, clamped `s`. **This is the heatmap cell.** | −2 … +2 (integer) |
| `w` | **Weight** from the active profile | 0 … 100 |
| `p` | **Polarity** from the active profile | +1 or −1 |
| `contribution` | Weighted score points. **This is the detail-page row.** | signed |

Two stages, deliberately: `c` is computed **once per (factor, currency)** and reused across every asset touching that currency. `s` and `n` are per asset.

---

## 3. Factor catalogue (MVP)

| Code | Contributor | Scope | Source | Normalizer |
|---|---|---|---|---|
| `RATE` | InterestRateContributor | CurrencyScoped | releases | Trend + cross-sectional level |
| `CPI` | InflationContributor | CurrencyScoped | releases | Surprise |
| `GDP` | GDPContributor | CurrencyScoped | releases | Surprise |
| `PMI` | PMIContributor | CurrencyScoped | releases | Surprise |
| `NFP` | EmploymentContributor | CurrencyScoped | releases | Surprise |
| `RETAIL` | RetailSalesContributor | CurrencyScoped | releases | Surprise |
| `DXY` | DollarStrengthContributor | **UsdScoped** | series | Percentile |
| `YIELD` | YieldContributor | **UsdScoped** | series | Percentile |

`SENTIMENT` (VIX) and `COT` are defined in the factor catalogue but **not enabled in any v1 profile**. They exist to prove the extension path costs zero code.

### 3.1 Factor scope — required schema addition

Working the math exposed a gap. `DXY` and `YIELD` are derived from US market series; there is no "EUR real yield" in the model. Under the currency-differential rule (§5.1) a EURUSD `DXY` score would compute `(0 − c_USD)/2`, silently halving a factor that should apply at full strength.

So each factor declares a **scope**:

- **`CurrencyScoped`** — `c` is defined for every currency; pairs use the differential.
- **`UsdScoped`** — `c` is defined for USD only; the asset's USD exposure direction is applied directly, no differential.

→ **Add `factors.scope` (smallint).** Without it, USD-scoped factors are systematically under-weighted on every pair.

> Consequence to accept knowingly: a future non-USD cross (EURGBP) has no USD exposure, so `DXY` and `YIELD` become unavailable and reduce its `coverage`. That is correct behaviour — the model genuinely has less to say about EURGBP — and it surfaces honestly rather than silently.

---

## 4. Currency factor scores (`c`)

Computed per `(factor, currency)` across the full currency universe: **USD, EUR, GBP, JPY, AUD, CHF, CAD, NZD**.

### 4.1 SurpriseNormalizer — `CPI`, `GDP`, `PMI`, `NFP`, `RETAIL`

```
d = actual − forecast                          (native units of the indicator)

|d| <  band_minor                    → 0
band_minor ≤ |d| < band_major        → ±1
|d| ≥ band_major                     → ±2

c = sign(d) × magnitude × indicator.currency_direction
```

**Fixed bands, not rolling dispersion.** A z-score against trailing surprise history is more statistically elegant, but with manually seeded data the history is thin and the resulting scores would shift as the seed set grows — breaking reproducibility for reasons unrelated to the model. Fixed bands are deterministic from day one and hand-checkable by a user, which is the stated product promise.

→ **Add `indicators.band_minor`, `indicators.band_major` (numeric).** Bands are indicator-intrinsic — a 50k NFP surprise and a 0.1pp CPI surprise are not comparable — so they belong on the indicator, not in code and not on the profile.

**v1 band values**

| Indicator | Unit | `band_minor` | `band_major` | `currency_direction` |
|---|---|---|---|---|
| `CPI_YOY` | pp | 0.10 | 0.30 | +1 |
| `CORE_CPI_YOY` | pp | 0.10 | 0.30 | +1 |
| `GDP_QOQ` | pp | 0.20 | 0.60 | +1 |
| `PMI_MFG` | index pts | 0.50 | 1.50 | +1 |
| `PMI_SVC` | index pts | 0.50 | 1.50 | +1 |
| `NFP` | thousands | 25 | 75 | +1 |
| `UNEMPLOYMENT` | pp | 0.10 | 0.30 | **−1** |
| `RETAIL_MOM` | pp | 0.20 | 0.50 | +1 |
| `EMPLOY_CHANGE` | thousands | 10 | 30 | +1 |

**Forecast fallback.** If `forecast` is null, substitute `previous` and mark `usedFallback` in the explanation text. Rationale: raising coverage beats discarding a real signal, and the fallback is disclosed to the user rather than hidden.

**Non-US employment.** `NFP` is the factor code; the underlying indicator is `NFP` for USD and `EMPLOY_CHANGE` / `UNEMPLOYMENT` for other currencies. The contributor resolves indicator-by-currency; the factor code stays stable so the heatmap column is stable.

### 4.2 InterestRateContributor — `RATE`

Policy rates are a *level and a path*, not a surprise. Two components, summed then clamped:

```
direction:  Δ = actual − previous
            Δ > 0 → +1 ,  Δ < 0 → −1 ,  Δ = 0 → 0

level:      rank the currency's policy rate across the full 8-currency universe
            top    2 of 8 → +1
            bottom 2 of 8 → −1
            otherwise     →  0

c = clamp(direction + level, −2, +2)
```

Rate **differentials** are the dominant driver of FX, and the cross-sectional rank captures that directly — this is the one place the engine looks sideways across currencies rather than at one in isolation. Ties resolve by currency code ascending, so the ranking is deterministic.

### 4.3 PercentileNormalizer — `DXY`, `YIELD`

Percentile rank of the latest observation within a trailing **252-observation** window (≈1 trading year):

```
p ≥ 0.80        → +2
0.60 ≤ p < 0.80 → +1
0.40 ≤ p < 0.60 →  0
0.20 ≤ p < 0.40 → −1
p < 0.20        → −2
```

Both produce a **USD** score: a high DXY percentile and a high real-yield percentile are both USD-bullish, so `c_USD = +2`.

Series used: `DXY`, `US10Y_REAL`. Real yield is computed **at ingestion** (nominal − breakeven) and stored as its own series — never derived inside a contributor, which would violate purity and make the value unauditable.

If the window holds fewer than 60 observations, the factor is **unavailable** (contributes to coverage shortfall) rather than computed on thin data.

> Momentum blending (20-observation rate of change) is deliberately deferred to v2. Percentile alone is hand-verifiable; a blend is not.

### 4.3b Inflation — engine v2.1.0

Inflation is the one factor **not** scored on its own history. It is scored
against the central bank's published target:

```
gap        = CPI − target
level      = gap >  tolerance → +1     above target, tightening bias
             gap < −tolerance → −1     below target, easing bias
             else             →  0
direction  = sign(|gap| − |gap_prev|)  × (gap >= 0 ? +1 : −1)
n          = clamp(level + direction, −2, +2)
```

Above target is **positive** for the currency: the bank is pushed toward
tightening. `CurrencyDirection` is not applied — the gap already carries the sign,
and multiplying again would invert every reading.

Direction is measured on the **gap**, not the raw change. Inflation rising from
1.0 to 1.5 against a 2% target is an undershoot closing; rising from 2.5 to 3.0
is an overshoot widening. Scoring the raw change would call both hawkish.

> **Why not a percentile.** An own-history percentile asks "is this print high for
> this country". A target gap asks "does the bank still have work to do", which is
> the question that moves a currency — and it is more objective, not less: a
> percentile shifts as history accumulates, a mandate does not.

Targets and tolerances live in `currency_policies`, seeded as configuration.
Tolerance is half the official band where a bank publishes one, otherwise 0.5pp.

**Real policy rate** (nominal − CPI) is computed for display only. With the rate
and inflation both weighted it is a linear combination of the two and adds no
independent information to the aggregate.

### 4.3c Two windows, two purposes

The loader keeps a **staleness** window and a **history** window, and they are not
the same number:

| Window | Days | What it decides |
|---|---|---|
| `StalenessLookbackDays` | 500 | How far back a release may sit and still be found. Only has to exceed the widest `MaxAgeDays` (400). |
| `HistoryLookbackDays` | 1825 | How much history the level percentile is measured against — five years. |

These were one constant until they were separated, and the conflation was
silently wrong: a monthly series got about **16** points instead of 59. Sixteen
points make each one worth six percentile points, and a label reading "0th
percentile" then means "lowest of sixteen".

The correction changed real readings, not just precision:

```
US unemployment 4.2%
  16 readings   →  near the bottom of the range   →  USD +2
  58 readings   →  64th percentile, mid-range     →  USD +1
```

The short window only spanned 2025–2026, when unemployment was drifting up from
3.5%, so 4.2% looked like a floor. Over five years it is squarely mid-pack.

**Five years, not everything ingested (~8).** A longer window reaches the April
2020 unemployment spike, and one reading of 14.8% against a 4% norm pushes every
later print into the bottom of the distribution — the level component would read
"low" more or less permanently. That is a regime break contaminating the sample,
not extra information.

Percentile labels state their sample size (`5th percentile of 59 readings`)
because the percentile alone overstates its own precision.

### 4.3d The dollar reading names its index

`market_series.scale_note` carries a short qualifier shown beside the raw value:

```
119.70 (Fed broad) · 37th percentile of 1Y
2.41 · 96th percentile of 1Y
```

The dollar needs it because this project reads the Fed's **broad trade-weighted
index** (`DTWEXBGS`, 2006 = 100, near 120) while the DXY on a trading platform is
the **ICE index** (near 99). Different basket, different base.

Percentile normalization is scale-invariant, so the SCORE is identical either
way — but a bare "119.70" under a dollar heading reads as broken data to anyone
who checks it against their own chart. Naming the index costs nothing and removes
the doubt; converting to the ICE scale would mean another provider dependency for
a number the engine does not use.

The qualifier is empty where the unit speaks for itself. A real yield in percent
is unambiguous, and appending a note to everything would be noise.

### 4.3e Pair factors carry half steps (engine v2.2.0)

A currency's own reading is an integer by construction — level `{-1,0,1}` plus
direction `{-1,0,1}`. A pair's score is the differential:

```
s = (base − quote) / 2        →  exactly a half-integer
n = clamp(polarity × s, −2, +2) at HALF-step resolution
```

Half steps are the arithmetic's own resolution, not invented precision. Rounding
`s` to a whole number was measurably wrong on two counts:

| | |
|---|---|
| Information discarded | **10 of 21** live pair cells (48%) lost a half step |
| Systematic bias | rounding away from zero turned every 0.5 into 1.0 and 1.5 into 2.0, pushing scores **away from neutral** |

Live impact of the correction:

```
USDCHF  75.7 → 65.9   (−9.8)   the only bullish call in the universe, mostly rounding
NZDUSD  58.5 → 54.3
USDJPY  53.2 → 49.0   crossed the midpoint — the reading genuinely flipped
DXY / XAUUSD / NASDAQ  unchanged — no differential, so no rounding to correct
```

**Display.** Whole steps print bare, half steps print one decimal (`+1`, `+1.5`,
`-2`). A fixed decimal everywhere would read as spurious precision. The heatmap
palette stays at five bands — nine shades on a diverging scale stop being
distinguishable at cell size — with a half step rounding away from zero for
colour only, so `+0.5` reads positive rather than washing out to neutral. The
printed number carries the finer reading.

Single-currency factors are unaffected: with no differential there is nothing to
halve.

### 4.4 Staleness

Stale data is worse than missing data, because it looks authoritative. A factor is **unavailable** if its newest input is older than:

→ **Add `indicators.max_age_days`, `market_series.max_age_days` (int).**

| Cadence | `max_age_days` |
|---|---|
| Monthly releases (CPI, NFP, PMI, RETAIL) | 60 |
| Quarterly releases (GDP) | 130 |
| Policy rate | 400 (a rate is a standing level, not an event) |
| Daily series, daily publication (US10Y_REAL / `DFII10`) | 5 |
| Daily series, **weekly** publication (DXY / `DTWEXBGS`) | 10 |

> **Set staleness from publication cadence, not observation frequency.** Learned
> the hard way against live FRED data: `DTWEXBGS` carries daily observations but
> ships in the Fed's weekly H.10 release, so it is routinely 5–7 days behind
> through no fault of the pipeline. At `max_age_days = 5` the dollar factor
> excluded itself roughly half of every week, silently dropping 25 weight from
> the Metals profile and cutting gold's coverage from 94% to 69%. The factor was
> behaving correctly; the threshold was wrong.

Age is measured from `released_at` / `observed_at` to `ScoringContext.AsOfUtc` — never to `DateTime.UtcNow`, which would make re-runs non-reproducible.

The same publication cadence now drives the ingestion scheduler (`architecture.md`
§6.5) — `sync_schedules.cadence`, not the observation frequency. `max_age_days`
says how long a reading stays *usable*; cadence says how often to *look*. Both
are derived from the same fact about the provider, and getting either one from
observation frequency produces the same class of bug.

---

## 5. Asset signal (`s`) and normalized score (`n`)

### 5.1 Mapping `c` onto an asset

```
CurrencyScoped factor:
    two exposures (base +1, quote −1):   s = (c_base − c_quote) / 2
    one exposure  (direction d):          s = d × c

UsdScoped factor:
    s = direction_USD × c_USD

n = clamp( round( p × s ), −2, +2 )        p = polarity from the active profile
```

Rounding is **half away from zero** (`MidpointRounding.AwayFromZero`), stated explicitly so `s = 0.5` → `n = 1` is reproducible across platforms.

The `/2` on the differential is what keeps `s` in range: `c_base − c_quote` spans −4…+4.

### 5.2 Polarity — required schema addition

The currency-exposure abstraction works for FX and metals but **inverts the wrong way for equity indices**. Strong US GDP is bullish for USD *and* bullish for NASDAQ. Any pure currency mapping gets one of those two backwards.

→ **Add `profile_weights.polarity` (smallint, +1 / −1).**

With `polarity`, the Index profile reads as plain English — *"how does this index respond to USD-bullish data on this factor?"*:

| Factor | Index polarity | Reading |
|---|---|---|
| `YIELD` | −1 | higher real yields → equities down |
| `RATE` | −1 | hawkish policy → equities down |
| `CPI` | −1 | hot inflation → hawkish repricing → equities down |
| `PMI` | +1 | expansion → equities up |
| `GDP` | +1 | growth → equities up |
| `NFP` | +1 | job growth → equities up |
| `DXY` | −1 | strong dollar → weaker overseas earnings |

Forex and Metals profiles use `+1` throughout — their sign logic is already carried by exposures.

### 5.3 Asset exposures (MVP universe)

| Asset | Market | Exposures | Note |
|---|---|---|---|
| XAUUSD | Metals | USD −1 | inverse-dollar model |
| EURUSD | Forex | EUR +1, USD −1 | |
| GBPUSD | Forex | GBP +1, USD −1 | |
| USDJPY | Forex | USD +1, JPY −1 | |
| AUDUSD | Forex | AUD +1, USD −1 | |
| USDCHF | Forex | USD +1, CHF −1 | |
| USDCAD | Forex | USD +1, CAD −1 | |
| NZDUSD | Forex | NZD +1, USD −1 | |
| DXY | **DollarIndex** | USD +1 | |
| NASDAQ | Index | USD +1 | exposure means "which economy drives this", not "quote currency" |

**Two notes on DXY.** It appears twice in the model — as a tradeable `asset` (symbol `DXY`) and as a `market_series` (code `DXY`) feeding `DollarStrengthContributor`. That is intentional and not a modelling error.

But it means the DXY *asset* must not be scored using the DXY *factor* — that is circular, scoring a thing by itself. Rather than invent an asset-level exclusion mechanism, DXY gets its own market value `DollarIndex` and its own profile that simply omits the `DXY` weight row. Zero new machinery.

→ **`Market` enum gains `DollarIndex`.**

### 5.4 The gold inflation assumption — stated openly

Under `s = −c_USD`, a hot US CPI print scores **bearish for gold** (hot CPI → hawkish Fed → higher real yields → gold down). That is the correct read for the post-2022 regime but the opposite of the textbook "gold is an inflation hedge" relationship.

This is a deliberate modelling stance, not an oversight. The Metals profile therefore keeps `CPI` at a low weight (12) and lets `YIELD` and `DXY` (25 each) carry the load — they capture the transmission mechanism directly rather than through inflation's ambiguous sign. Revisit in profile v2.

---

## 6. Aggregation

```
participating = weight rows where is_enabled = true
                AND the contributor evaluated successfully

maxAbs         = Σ ( 2 × w_i )              over participating only
scale          = 50 / maxAbs
contribution_i = round1( n_i × w_i × scale )   → score points, 1 dp
score          = 50 + Σ contribution_i         → 0 … 100
coverage       = Σ w_participating / Σ w_enabled     → 3 dp
is_sufficient  = coverage ≥ profile.min_coverage
```

**Why the score is the sum of ROUNDED contributions**, rather than a rounded sum: the arithmetic a user checks by hand on the detail page must be the arithmetic the engine did. Rounding each contribution to 1 dp and summing those makes `50 + Σ` close exactly on screen, with no residual. Displayed precision and stored precision are the same number.

**Bounds check.** All `n_i = +2` → `Σ contribution = Σ 2·w_i · (50/maxAbs) = maxAbs · 50/maxAbs = 50` → `score = 100`. All `n_i = −2` → `score = 0`. The scale factor is what makes 50 the exact midpoint regardless of which factors participated.

**Bias**

```
score ≥ bullish_threshold (65)  → Bullish
score ≤ bearish_threshold (35)  → Bearish
otherwise                        → Neutral
```

**Coverage gate.** Because `maxAbs` counts participating factors only, an asset with two of eight factors present can post a confident-looking 88. Assets below `min_coverage = 0.60` are stored with `is_sufficient = false`, excluded from Top Setups ranking, and shown on their own detail page with an explicit insufficient-data notice. Without this the headline ranking is quietly wrong whenever a feed lags.

**Determinism of summation.** Contributions are summed in ascending `factor_code` order before rounding, so floating-point accumulation is order-stable. Round once, at the end.

---

## 7. Default scoring profiles (v1)

Weights sum to 100 per profile — not required by the math (`scale` normalizes any total), but it makes weights readable as percentages, which matters for a product whose promise is transparency.

### Forex Default v1 — `market = Forex`

| Factor | Weight | Polarity |
|---|---|---|
| RATE | 31 | +1 |
| CPI | 17 | +1 |
| NFP | 14 | +1 |
| PMI | 11 | +1 |
| GDP | 9 | +1 |
| DXY | 7 | +1 |
| RETAIL | 6 | +1 |
| YIELD | 5 | +1 |
| **Total** | **100** | |

`DXY` is kept light (7) because for USD pairs it partially restates the USD side already captured by `RATE` and `CPI`. It earns its place as an independent read of *realised* dollar demand versus the *implied* read from releases.

### Metals Default v1 — `market = Metals`

| Factor | Weight | Polarity |
|---|---|---|
| YIELD | 25 | +1 |
| DXY | 25 | +1 |
| RATE | 22 | +1 |
| CPI | 12 | +1 |
| PMI | 8 | +1 |
| GDP | 8 | +1 |
| **Total** | **100** | |

### Dollar Index Default v1 — `market = DollarIndex`

| Factor | Weight | Polarity |
|---|---|---|
| RATE | 32 | +1 |
| CPI | 18 | +1 |
| NFP | 16 | +1 |
| PMI | 12 | +1 |
| GDP | 10 | +1 |
| RETAIL | 7 | +1 |
| YIELD | 5 | +1 |
| **Total** | **100** | |

`DXY` factor deliberately absent — self-reference.

### Index Default v1 — `market = Index`

| Factor | Weight | Polarity |
|---|---|---|
| YIELD | 25 | −1 |
| RATE | 22 | −1 |
| PMI | 15 | +1 |
| GDP | 13 | +1 |
| CPI | 12 | −1 |
| NFP | 8 | +1 |
| DXY | 5 | −1 |
| **Total** | **100** | |

> **Most regime-sensitive assumption in this document:** `NFP` polarity `+1` for indices. In 2022–2023 strong payrolls repeatedly sold equities off (good news is bad news). The long-run relationship is positive, so v1 keeps `+1` at a low weight of 8. This is the first line to revisit in profile v2, and it is called out here rather than buried so the decision is visible.

### Profile immutability

Per architecture R3: profiles are append-only. Tuning any weight, polarity, or threshold creates **version N+1** and deactivates N. A partial unique index enforces one active profile per market. Scores stamp `scoring_profile_id` + `profile_version`, so a v1 score stays reproducible after v2 ships.

---

## 8. Worked example — XAUUSD

> **Note on scenarios.** The example below is a *hawkish-USD* environment and is
> retained because it exercises the sign conventions. The **shipped fixture** is
> the approved bullish soft-landing scenario, which puts XAUUSD at **91.0 with
> 94% coverage**. Both are pinned by tests:
> `backend/tests/Scorecard.Api.Tests/Scoring/ScoringEngineTests.cs` asserts the
> shipped fixture to the decimal, and `frontend/scripts/verify-mock.ts` asserts
> the same numbers independently in TypeScript. The two implementations agreeing
> is the real proof this spec is unambiguous.

Verifiable by hand. Any implementation must reproduce these figures exactly.

**Inputs** (`AsOfUtc = 2026-08-03T00:00:00Z`, profile *Metals Default v1*)

| Factor | Raw input | Derivation | `c_USD` |
|---|---|---|---|
| RATE | 5.50%, previous 5.25% | Δ>0 → +1; highest of 8 currencies → +1 | **+2** |
| CPI | actual 3.4, forecast 3.1 | d=+0.30 ≥ band_major 0.30 → +2 × dir +1 | **+2** |
| GDP | actual 2.8, forecast 2.0 | d=+0.80 ≥ band_major 0.60 → +2 | **+2** |
| PMI | actual 48.2, forecast 49.0 | d=−0.80, band_minor 0.50 ≤ 0.80 < 1.50 → −1 | **−1** |
| YIELD | US10Y_REAL, percentile 0.85 | p ≥ 0.80 → +2 | **+2** |
| DXY | DXY series, percentile 0.72 | 0.60 ≤ p < 0.80 → +1 | **+1** |

**Mapping** — XAUUSD has one exposure, USD with direction −1, so `s = −c_USD` for both scopes. All Metals polarities are +1.

| Factor | `c_USD` | `s` | `n` | `w` | `contribution = n × w × 0.25` |
|---|---|---|---|---|---|
| RATE | +2 | −2 | **−2** | 22 | **−11.00** |
| YIELD | +2 | −2 | **−2** | 25 | **−12.50** |
| DXY | +1 | −1 | **−1** | 25 | **−6.25** |
| CPI | +2 | −2 | **−2** | 12 | **−6.00** |
| PMI | −1 | +1 | **+1** | 8 | **+2.00** |
| GDP | +2 | −2 | **−2** | 8 | **−4.00** |

```
maxAbs        = 2 × (22+25+25+12+8+8) = 200
scale         = 50 / 200 = 0.25
Σ contribution = −37.75
score         = 50 − 37.75 = 12.25
coverage      = 100 / 100 = 1.000    →  is_sufficient = true
bias          = 12.25 ≤ 35           →  Bearish
```

**Detail page renders:** `Baseline 50.00` → six factor rows → `Total 12.25`. The arithmetic visibly closes, which is the entire point of the baseline-50 model.

### 8.1 Differential example — EURUSD, `RATE`

ECB 2.15% unchanged (Δ=0 → 0; rank 6th of 8 → 0) → `c_EUR = 0`. From above, `c_USD = +2`.

```
s = (c_EUR − c_USD) / 2 = (0 − 2) / 2 = −1     →  n = −1
contribution = −1 × 31 × scale(Forex)
```

EUR bearish on rates against a hiking Fed — the differential, not either level alone.

---

## 9. Explanations

Generated at scoring time in **both languages**, stored in `explanation_mn` / `explanation_en`, never regenerated. Templates live in resource files pinned by `engine_version` — not in the database, since they carry formatting logic.

Template `SURPRISE_PAIRED`, populated from the example above:

```
EN: US CPI (YoY) printed 3.4% against a 3.1% forecast — a +0.30pp upside
    surprise, hawkish for USD. USD is the quote currency for XAUUSD, so the
    effect on Gold is negative. Normalized −2 · weight 12 · contribution −6.00.

MN: АНУ-ын CPI (жилээр) 3.1%-ийн таамаглалтай харьцуулахад 3.4% гарлаа —
    +0.30 нэгжийн эерэг зөрүү нь USD-д хатуу мөнгөний бодлогын дохио. XAUUSD-д
    USD нь ханшийн валют тул алтад сөрөг нөлөөтэй. Хэвийн оноо −2 · жин 12 ·
    хувь нэмэр −6.00.
```

Template set: `SURPRISE_PAIRED`, `SURPRISE_SINGLE`, `RATE_DIFFERENTIAL`, `RATE_SINGLE`, `PERCENTILE`, `FALLBACK_FORECAST`, `UNAVAILABLE_STALE`, `UNAVAILABLE_MISSING`.

> The Mongolian above is a **draft pending native review**. Terminology must be reconciled against `glossary-mn.md` before implementation — in particular *хатуу мөнгөний бодлого* (hawkish), *хэвийн оноо* (normalized score), and *хувь нэмэр* (contribution) need your sign-off, since inconsistent macro terminology is exactly what would make this read as amateur to the target audience.

---

## 10. Test obligations

The scoring engine is the product; these are non-negotiable before any UI work is called done.

1. **Golden-file tests** — §8 and §8.1 encoded as fixtures, asserted to the cent.
2. **Bounds** — all `n = +2` → exactly 100.00; all `n = −2` → exactly 0.00; all `n = 0` → exactly 50.00.
3. **Determinism** — same context evaluated twice, byte-identical output; factor ordering shuffled, identical output.
4. **Coverage** — dropping factors changes `coverage` and `maxAbs` but leaves surviving contributions internally consistent.
5. **Staleness** — a release one day past `max_age_days` flips the factor to unavailable.
6. **Purity** — contributors compile without access to `DbContext` or `DateTime.Now`. Enforce by an architecture test, not by convention.
7. **Reproducibility** — replay a stored `AssetScore`'s factor rows through the aggregator and recover the stored `score`.

---

## 11. Schema additions required by this spec

Consolidated for the migration design:

| Table | Column | Type | Reason |
|---|---|---|---|
| `factors` | `scope` | smallint | CurrencyScoped vs UsdScoped (§3.1) |
| `profile_weights` | `polarity` | smallint | equity indices invert (§5.2) |
| `indicators` | `band_minor`, `band_major` | numeric(18,6) | surprise thresholds (§4.1) |
| `indicators` | `max_age_days` | int | staleness (§4.4) |
| `market_series` | `max_age_days` | int | staleness (§4.4) |
| — | `Market` enum | + `DollarIndex` | avoids DXY self-reference (§5.3) |

---

## 12. Deferred to v2 (recorded so they are decisions, not omissions)

- Momentum blending in `PercentileNormalizer`
- `SENTIMENT` (VIX) and `COT` factors — catalogued, unweighted
- Rolling-dispersion surprise normalization once release history is deep enough
- Central-bank *guidance* as distinct from realised rate moves
- Per-asset weight overrides (currently market-level only)
- Regime detection for the gold/CPI and index/NFP sign assumptions
