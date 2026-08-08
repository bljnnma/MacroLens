/**
 * Guards the rule that makes the prototype credible: every number on screen
 * must reconcile. Run with `npm run verify:mock`.
 */
import { getAllScores, getHeatmap, getMarketSnapshot } from '../lib/mock';

let failures = 0;
const fail = (msg: string) => {
  failures += 1;
  console.error(`  FAIL  ${msg}`);
};

const scores = getAllScores();

console.log('SYMBOL   MARKET       SCORE  BIAS      COV   SUFFICIENT');
for (const s of scores) {
  console.log(
    `${s.symbol.padEnd(8)} ${s.market.padEnd(12)} ${s.score.toFixed(1).padStart(5)}  ${s.bias.padEnd(8)} ${(s.coverage * 100).toFixed(0).padStart(3)}%  ${s.isSufficient ? 'yes' : 'NO'}`
  );
}

console.log('\nChecking 50 + Σcontributions = score ...');
for (const s of scores) {
  const sum = Math.round(s.factors.reduce((a, f) => a + f.contribution, 0) * 10) / 10;
  if (Math.abs(50 + sum - s.score) > 0.001) {
    fail(`${s.symbol}: 50 + ${sum} = ${50 + sum}, but score is ${s.score}`);
  }
}

console.log('Checking bias matches score thresholds ...');
for (const s of scores) {
  const expected = s.score >= 65 ? 'bullish' : s.score <= 35 ? 'bearish' : 'neutral';
  if (s.bias !== expected) fail(`${s.symbol}: score ${s.score} implies ${expected}, got ${s.bias}`);
}

console.log('Checking heatmap cells match asset factor rows ...');
const heatmap = getHeatmap();
for (const row of heatmap.rows) {
  const detail = scores.find((s) => s.symbol === row.symbol)!;
  for (const cell of row.cells) {
    const source = detail.factors.find((f) => f.factorCode === cell.factorCode);
    if ((source?.normalizedScore ?? null) !== cell.normalizedScore) {
      fail(`${row.symbol}/${cell.factorCode}: heatmap ${cell.normalizedScore} vs detail ${source?.normalizedScore}`);
    }
  }
}

console.log('Checking normalized scores stay inside -2..+2 ...');
for (const s of scores) {
  for (const f of s.factors) {
    if (f.normalizedScore !== null && (f.normalizedScore < -2 || f.normalizedScore > 2)) {
      fail(`${s.symbol}/${f.factorCode}: normalized ${f.normalizedScore} out of range`);
    }
  }
}

console.log('\nXAUUSD breakdown');
const gold = scores.find((s) => s.symbol === 'XAUUSD')!;
for (const f of gold.factors) {
  console.log(
    `  ${f.factorCode.padEnd(7)} n=${String(f.normalizedScore ?? '—').padStart(2)}  w=${String(f.weight).padStart(2)}  c=${f.contribution.toFixed(1).padStart(6)}${f.available ? '' : '   (unavailable)'}`
  );
}

const snap = getMarketSnapshot();
console.log(
  `\nSnapshot  strongest=${snap.strongestCurrency} (${snap.strongestAvg})  weakest=${snap.weakestCurrency} (${snap.weakestAvg})  risk=${snap.riskRegime}  avgCoverage=${(snap.avgCoverage * 100).toFixed(0)}%`
);

console.log(failures === 0 ? '\nAll reconciliation checks passed.' : `\n${failures} check(s) failed.`);
process.exit(failures === 0 ? 0 : 1);
