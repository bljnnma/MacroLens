export interface SimulatorInput {
  initialBalance: number;
  /** Percent of current balance risked per trade, e.g. 1. */
  riskPercent: number;
  /** Reward multiple of risk, e.g. 3 means a win returns 3R. */
  riskReward: number;
  /** Percent, e.g. 50. */
  winRate: number;
  targetBalance: number;
  seed: number;
  maxTrades?: number;
}

export interface SimulatedTrade {
  index: number;
  balanceBefore: number;
  riskAmount: number;
  win: boolean;
  pnl: number;
  balanceAfter: number;
  /** Drawdown from the running equity peak, as a fraction. */
  drawdown: number;
}

export type StopReason = 'target' | 'maxTrades' | 'ruin';

export interface SimulationResult {
  trades: SimulatedTrade[];
  finalBalance: number;
  peakBalance: number;
  tradesTaken: number;
  wins: number;
  losses: number;
  actualWinRate: number;
  maxDrawdown: number;
  maxDrawdownValue: number;
  longestLossStreak: number;
  longestWinStreak: number;
  stopReason: StopReason;
}

export const MAX_TRADES = 2000;
/** Below this fraction of the starting balance the account is treated as blown. */
const RUIN_FRACTION = 0.1;

/** Deterministic PRNG — the same seed always replays the same run. */
function mulberry32(seed: number) {
  let a = seed >>> 0;
  return () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Expectancy in R per trade. This is the number that decides whether the
 * target is reachable at all — a negative edge cannot be fixed by position
 * sizing, only by trading less.
 */
export function expectancyR(winRate: number, riskReward: number): number {
  const p = winRate / 100;
  return p * riskReward - (1 - p) * 1;
}

/** The win rate at which this reward ratio breaks even. */
export function breakevenWinRate(riskReward: number): number {
  return (1 / (1 + riskReward)) * 100;
}

export function runSimulation(input: SimulatorInput): SimulationResult {
  const {
    initialBalance,
    riskPercent,
    riskReward,
    winRate,
    targetBalance,
    seed,
    maxTrades = MAX_TRADES,
  } = input;

  const rand = mulberry32(seed);
  const ruinFloor = initialBalance * RUIN_FRACTION;

  const trades: SimulatedTrade[] = [];
  let balance = initialBalance;
  let peak = initialBalance;
  let wins = 0;
  let losses = 0;
  let lossStreak = 0;
  let winStreak = 0;
  let longestLossStreak = 0;
  let longestWinStreak = 0;
  let maxDrawdown = 0;
  let maxDrawdownValue = 0;
  let stopReason: StopReason = 'maxTrades';

  for (let i = 1; i <= maxTrades; i += 1) {
    if (balance >= targetBalance) {
      stopReason = 'target';
      break;
    }
    if (balance <= ruinFloor) {
      stopReason = 'ruin';
      break;
    }

    const balanceBefore = balance;
    // Fixed-fractional sizing: risk scales with the account, so losses shrink
    // as the balance falls. That is what makes ruin asymptotic rather than abrupt.
    const riskAmount = balanceBefore * (riskPercent / 100);
    const win = rand() < winRate / 100;
    const pnl = win ? riskAmount * riskReward : -riskAmount;
    balance = balanceBefore + pnl;

    if (win) {
      wins += 1;
      winStreak += 1;
      lossStreak = 0;
      longestWinStreak = Math.max(longestWinStreak, winStreak);
    } else {
      losses += 1;
      lossStreak += 1;
      winStreak = 0;
      longestLossStreak = Math.max(longestLossStreak, lossStreak);
    }

    peak = Math.max(peak, balance);
    const drawdown = peak > 0 ? (peak - balance) / peak : 0;
    if (drawdown > maxDrawdown) {
      maxDrawdown = drawdown;
      maxDrawdownValue = peak - balance;
    }

    trades.push({
      index: i,
      balanceBefore,
      riskAmount,
      win,
      pnl,
      balanceAfter: balance,
      drawdown,
    });
  }

  if (balance >= targetBalance) stopReason = 'target';
  else if (balance <= ruinFloor) stopReason = 'ruin';

  return {
    trades,
    finalBalance: balance,
    peakBalance: peak,
    tradesTaken: trades.length,
    wins,
    losses,
    actualWinRate: trades.length ? (wins / trades.length) * 100 : 0,
    maxDrawdown,
    maxDrawdownValue,
    longestLossStreak,
    longestWinStreak,
    stopReason,
  };
}

export function randomSeed(): number {
  return Math.floor(Math.random() * 1_000_000);
}
