"use client";

import { useMemo, useState } from "react";
import { useTranslations } from "next-intl";
import {
  AlertTriangle,
  CheckCircle2,
  Play,
  Shuffle,
  TrendingDown,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { Input } from "@/components/ui/misc";
import {
  MAX_TRADES,
  breakevenWinRate,
  expectancyR,
  randomSeed,
  runSimulation,
  type SimulationResult,
} from "@/lib/simulator";
import { EquityCurve } from "./equity-curve";

const DEFAULTS = {
  initialBalance: "5000",
  riskPercent: "1",
  riskReward: "3",
  winRate: "50",
  targetBalance: "100000",
  seed: "42",
};

type FormState = typeof DEFAULTS;

const money = (v: number) =>
  v.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

export function SimulatorView() {
  const t = useTranslations("simulator");
  const [form, setForm] = useState<FormState>(DEFAULTS);
  const [result, setResult] = useState<SimulationResult | null>(null);

  const nums = useMemo(
    () => ({
      initialBalance: Number(form.initialBalance),
      riskPercent: Number(form.riskPercent),
      riskReward: Number(form.riskReward),
      winRate: Number(form.winRate),
      targetBalance: Number(form.targetBalance),
      seed: Number(form.seed),
    }),
    [form],
  );

  const errors = useMemo(() => {
    const e: string[] = [];
    if (!(nums.initialBalance > 0)) e.push(t("errInitial"));
    if (!(nums.riskPercent > 0 && nums.riskPercent <= 100))
      e.push(t("errRisk"));
    if (!(nums.riskReward > 0)) e.push(t("errRr"));
    if (!(nums.winRate > 0 && nums.winRate < 100)) e.push(t("errWinRate"));
    if (!(nums.targetBalance > nums.initialBalance)) e.push(t("errTarget"));
    if (!Number.isFinite(nums.seed)) e.push(t("errSeed"));
    return e;
  }, [nums, t]);

  const valid = errors.length === 0;

  // Expectancy updates as you type, before anything is run. It is the single
  // number that decides whether the target is reachable at all — position
  // sizing cannot rescue a negative edge.
  const expectancy = useMemo(
    () =>
      Number.isFinite(nums.winRate) && Number.isFinite(nums.riskReward)
        ? expectancyR(nums.winRate, nums.riskReward)
        : 0,
    [nums.winRate, nums.riskReward],
  );
  const breakeven = useMemo(
    () => (nums.riskReward > 0 ? breakevenWinRate(nums.riskReward) : 0),
    [nums.riskReward],
  );

  const set = (key: keyof FormState) => (value: string) =>
    setForm((f) => ({ ...f, [key]: value }));

  const run = () => {
    if (!valid) return;
    setResult(runSimulation(nums));
  };

  const equityPoints = useMemo(() => {
    if (!result) return [];
    return [
      { index: 0, balance: nums.initialBalance },
      ...result.trades.map((tr) => ({
        index: tr.index,
        balance: tr.balanceAfter,
      })),
    ];
  }, [result, nums.initialBalance]);

  return (
    <div className="space-y-8">
      <section className="rounded-[12px] border border-line bg-surface p-5 sm:p-6">
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 lg:grid-cols-2">
          <Field
            label={t("initialBalance")}
            prefix="$"
            value={form.initialBalance}
            onChange={set("initialBalance")}
          />
          <Field
            label={t("targetBalance")}
            prefix="$"
            value={form.targetBalance}
            onChange={set("targetBalance")}
          />
          <Field
            label={t("riskPercent")}
            suffix="%"
            value={form.riskPercent}
            onChange={set("riskPercent")}
            step="0.1"
          />
          <Field
            label={t("riskReward")}
            suffix="R"
            value={form.riskReward}
            onChange={set("riskReward")}
            step="0.1"
          />
          <Field
            label={t("winRate")}
            suffix="%"
            value={form.winRate}
            onChange={set("winRate")}
          />

          <div>
            <label className="mb-1.5 block text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
              {t("seed")}
            </label>
            <div className="flex gap-2">
              <Input
                type="number"
                value={form.seed}
                onChange={(e) => set("seed")(e.target.value)}
                className="tabular-nums"
              />
              <button
                type="button"
                onClick={() => set("seed")(String(randomSeed()))}
                title={t("newSeed")}
                className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-line px-3 text-[13px] font-medium text-fg-muted transition-ui hover:bg-surface-2 hover:text-fg"
              >
                <Shuffle className="size-3.5" aria-hidden />
                {t("newSeed")}
              </button>
            </div>
            <p className="mt-1.5 text-[11px] text-fg-subtle">{t("seedHint")}</p>
          </div>
        </div>

        {/* Edge read-out — visible before running, because the answer to
            "is this system viable" does not require a simulation. */}
        <div className="mt-6 grid grid-cols-1 gap-4 border-t border-line pt-5 sm:grid-cols-3">
          <Stat
            label={t("expectancy")}
            value={`${expectancy > 0 ? "+" : ""}${expectancy.toFixed(2)}R`}
            tone={expectancy > 0 ? "pos" : expectancy < 0 ? "neg" : "neutral"}
            hint={t("expectancyHint")}
          />
          <Stat
            label={t("breakevenWinRate")}
            value={`${breakeven.toFixed(1)}%`}
            hint={t("breakevenHint", { rr: nums.riskReward || 0 })}
          />
          <div className="flex items-end">
            <button
              type="button"
              onClick={run}
              disabled={!valid}
              className="inline-flex h-10 w-full items-center justify-center gap-2 rounded-lg bg-accent px-5 text-[14px] font-medium text-white transition-ui hover:bg-accent/90 disabled:cursor-not-allowed disabled:opacity-40"
            >
              <Play className="size-4" aria-hidden />
              {t("run")}
            </button>
          </div>
        </div>

        {!valid && (
          <ul className="mt-4 space-y-1">
            {errors.map((e) => (
              <li key={e} className="text-[12px] text-neg">
                {e}
              </li>
            ))}
          </ul>
        )}
      </section>

      {result && (
        <>
          <StatusBanner result={result} target={nums.targetBalance} />

          <section className="grid grid-cols-2 gap-4 xl:grid-cols-5">
            <Summary
              label={t("finalBalance")}
              value={`$${money(result.finalBalance)}`}
            />
            <Summary
              label={t("tradesTaken")}
              value={String(result.tradesTaken)}
            />
            <Summary
              label={t("actualWinRate")}
              value={`${result.actualWinRate.toFixed(1)}%`}
              hint={t("ofTarget", { target: nums.winRate })}
            />
            <Summary
              label={t("maxDrawdown")}
              value={`${(result.maxDrawdown * 100).toFixed(1)}%`}
              tone="neg"
              hint={`-$${money(result.maxDrawdownValue)}`}
            />
            <Summary
              label={t("longestLossStreak")}
              value={String(result.longestLossStreak)}
              hint={t("consecutive")}
            />
          </section>

          <EquityCurve
            points={equityPoints}
            initialBalance={nums.initialBalance}
            targetBalance={nums.targetBalance}
            reachedTarget={result.stopReason === "target"}
          />

          <section>
            <div className="mb-3 flex flex-wrap items-baseline justify-between gap-3">
              <span className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
                {t("tradeLog")}
              </span>
              <span className="text-[12px] text-fg-subtle">
                {t("tradeLogCount", { count: result.trades.length })}
              </span>
            </div>

            <div className="overflow-hidden rounded-[12px] border border-line bg-surface">
              <div className="max-h-[520px] overflow-auto">
                <table className="w-full border-collapse text-left">
                  <thead className="sticky top-0 z-10 bg-surface">
                    <tr className="border-b border-line">
                      <Th className="pl-4">{t("tradeNumber")}</Th>
                      <Th className="text-right">{t("balanceBefore")}</Th>
                      <Th className="text-right">{t("riskAmount")}</Th>
                      <Th>{t("outcome")}</Th>
                      <Th className="text-right">{t("profitLoss")}</Th>
                      <Th className="pr-4 text-right">{t("balanceAfter")}</Th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.trades.map((tr) => (
                      <tr
                        key={tr.index}
                        className="border-b border-line transition-ui last:border-0 hover:bg-surface-2"
                      >
                        <Td className="pl-4 text-fg-subtle">{tr.index}</Td>
                        <Td className="text-right text-fg-muted">
                          {money(tr.balanceBefore)}
                        </Td>
                        <Td className="text-right text-fg-muted">
                          {money(tr.riskAmount)}
                        </Td>
                        <Td>
                          <span
                            className={cn(
                              "text-[13px] font-medium",
                              tr.win ? "text-pos" : "text-neg",
                            )}
                          >
                            {tr.win ? t("win") : t("loss")}
                          </span>
                        </Td>
                        <Td
                          className={cn(
                            "text-right font-medium",
                            tr.win ? "text-pos" : "text-neg",
                          )}
                        >
                          {tr.pnl > 0 ? "+" : ""}
                          {money(tr.pnl)}
                        </Td>
                        <Td className="pr-4 text-right text-fg">
                          {money(tr.balanceAfter)}
                        </Td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        </>
      )}

      <p className="text-[12px] leading-relaxed text-fg-subtle">
        {t("disclaimer")}
      </p>
    </div>
  );
}

function StatusBanner({
  result,
  target,
}: {
  result: SimulationResult;
  target: number;
}) {
  const t = useTranslations("simulator");

  const config = {
    target: {
      icon: CheckCircle2,
      className: "border-pos/30 bg-pos/5 text-pos",
      text: t("reachedTarget", {
        count: result.tradesTaken,
        target: money(target),
      }),
    },
    maxTrades: {
      icon: AlertTriangle,
      className: "border-warn/30 bg-warn/5 text-warn",
      text: t("hitMaxTrades", { max: MAX_TRADES }),
    },
    ruin: {
      icon: TrendingDown,
      className: "border-neg/30 bg-neg/5 text-neg",
      text: t("ruined", { count: result.tradesTaken }),
    },
  }[result.stopReason];

  const Icon = config.icon;

  return (
    <div
      className={cn(
        "flex items-start gap-3 rounded-[12px] border px-5 py-4 text-[13px] leading-relaxed",
        config.className,
      )}
    >
      <Icon className="mt-0.5 size-4 shrink-0" aria-hidden />
      <span>{config.text}</span>
    </div>
  );
}

function Field({
  label,
  value,
  onChange,
  prefix,
  suffix,
  step,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  prefix?: string;
  suffix?: string;
  step?: string;
}) {
  return (
    <div>
      <label className="mb-1.5 block text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
        {label}
      </label>
      <div className="relative">
        {prefix && (
          <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[13px] text-fg-subtle">
            {prefix}
          </span>
        )}
        <Input
          type="number"
          inputMode="decimal"
          step={step}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className={cn("tabular-nums", prefix && "pl-7", suffix && "pr-8")}
        />
        {suffix && (
          <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-[13px] text-fg-subtle">
            {suffix}
          </span>
        )}
      </div>
    </div>
  );
}

function Stat({
  label,
  value,
  hint,
  tone = "neutral",
}: {
  label: string;
  value: string;
  hint?: string;
  tone?: "pos" | "neg" | "neutral";
}) {
  return (
    <div>
      <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
        {label}
      </div>
      <div
        data-numeric
        className={cn(
          "mt-1.5 text-[20px] font-semibold tabular-nums",
          tone === "pos" ? "text-pos" : tone === "neg" ? "text-neg" : "text-fg",
        )}
      >
        {value}
      </div>
      {hint && <p className="mt-1 text-[11px] text-fg-subtle">{hint}</p>}
    </div>
  );
}

function Summary({
  label,
  value,
  hint,
  tone = "neutral",
}: {
  label: string;
  value: string;
  hint?: string;
  tone?: "pos" | "neg" | "neutral";
}) {
  return (
    <div className="rounded-[12px] border border-line bg-surface p-5">
      <div className="text-[11px] font-medium uppercase tracking-wider text-fg-subtle">
        {label}
      </div>
      <div
        data-numeric
        className={cn(
          "mt-3 text-[20px] font-semibold tabular-nums",
          tone === "neg" ? "text-neg" : "text-fg",
        )}
      >
        {value}
      </div>
      {hint && <p className="mt-1.5 text-[11px] text-fg-subtle">{hint}</p>}
    </div>
  );
}

function Th({
  children,
  className,
}: {
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <th
      className={cn(
        "px-3 py-2.5 text-[11px] font-medium uppercase tracking-wider text-fg-subtle",
        className,
      )}
    >
      {children}
    </th>
  );
}

function Td({
  children,
  className,
}: {
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <td
      data-numeric
      className={cn("px-3 py-2.5 text-[13px] tabular-nums", className)}
    >
      {children}
    </td>
  );
}
