type Point = { playedOn: string; fantasyPoints: number };

/** Game-by-game points with the baseline average as a line and the recent games shaded. */
export function GameChart({ games, baseline, recentCount }: { games: Point[]; baseline: number | null; recentCount: number }) {
  if (games.length < 2) return null;
  const width = 320, height = 96, pad = 6;
  const values = games.map(game => game.fantasyPoints);
  const low = Math.min(0, ...values), high = Math.max(...values, baseline ?? 0, 1);
  const x = (index: number) => pad + (index / (games.length - 1)) * (width - 2 * pad);
  const y = (value: number) => height - pad - ((value - low) / (high - low)) * (height - 2 * pad);
  const path = values.map((value, index) => `${index ? 'L' : 'M'}${x(index).toFixed(1)} ${y(value).toFixed(1)}`).join('');
  const recentStart = x(Math.max(0, games.length - recentCount)) - 3;
  return <svg className="game-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`Fantasy points in the last ${games.length} games; the last ${recentCount} are shaded${baseline === null ? '' : `, baseline ${baseline.toFixed(1)}`}. The table below lists every game.`}>
    <rect x={recentStart} y={0} width={width - recentStart} height={height} className="game-chart-recent" />
    {baseline !== null && <line x1={pad} x2={width - pad} y1={y(baseline)} y2={y(baseline)} className="game-chart-baseline" />}
    <path d={path} className="game-chart-line" />
  </svg>;
}
