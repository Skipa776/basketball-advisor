import { useState } from 'react';
import type { FormEvent } from 'react';
import { useResource } from './useResource';
import { ErrorNotice } from './Workspace';
import type { Player } from './types';

type Pool = { seasonEndYear: number; source: string; gameCount: number; latestGameDate: string; latestFetchedAt: string };
type Appearance = { gameId: string; playedOn: string; fantasyPoints: number; provenance: { source: string; fetchedAt: string; parserVersion: string; rawRecordHash: string } };
type Heat = { playerId: { value: string }; seasonAppearances: number; latestAppearance: string | null; currentAverage: number | null; baselineAverage: number | null; recentAverage: number | null; pointsAboveBaseline: number | null; relativeLift: number | null; recentGamesAboveBaseline: number | null; hasComparison: boolean; currentWindow: Appearance[]; baselineWindow: Appearance[]; recentWindow: Appearance[] };
type Performance = { scoringRules: { stat: string; pointsPerUnit: number }[]; seasonEndYear: number; source: string; throughDate: string; view: string; modelVersion: string; policy: { recentGames: number; minimumBaselineGames: number; maximumBaselineGames: number }; observedPlayers: number; bestQualifiedPlayers: number; comparisonQualifiedPlayers: number; latestAppearance: string | null; latestFetchedAt: string | null; players: Heat[] };
type View = 'best' | 'hot' | 'all';
const format = (value: number | null) => value === null ? '—' : value.toLocaleString(undefined, { maximumFractionDigits: 1 });
const lift = (value: number | null) => value === null ? '—' : `${value > 0 ? '+' : ''}${format(value)}`;

export function RecordedPerformance({ leagueId }: { leagueId: string }) {
  const [open, setOpen] = useState(false);
  const pools = useResource<Pool[]>(open ? `/api/leagues/${leagueId}/performance-pools` : null);
  const [pool, setPool] = useState('');
  const [date, setDate] = useState('');
  const [selection, setSelection] = useState<{ seasonEndYear: number; source: string; throughDate: string } | null>(null);
  const [view, setView] = useState<View>('best');
  const [page, setPage] = useState(1);
  const query = selection ? new URLSearchParams({ ...selection, seasonEndYear: String(selection.seasonEndYear), view, page: String(page), limit: '10' }) : null;
  const performance = useResource<Performance>(open && query ? `/api/leagues/${leagueId}/performance?${query}` : null);
  const data = performance.result?.data;
  function submit(event: FormEvent) {
    event.preventDefault();
    const selected = pools.result?.data.find(item => `${item.seasonEndYear}:${item.source}` === pool);
    if (!selected) return;
    setSelection({ seasonEndYear: selected.seasonEndYear, source: selected.source, throughDate: date });
    setPage(1); performance.refresh();
  }
  return <details className="performance panel" onToggle={event => setOpen(event.currentTarget.open)}>
    <summary>Recorded performance · best & above baseline</summary>
    <p>Compare observed fantasy points under your league’s saved scoring rules. These dated results are not projections or a verified live streak. Import completeness and freshness are unverified.</p>
    <ErrorNotice text={pools.error} retry={pools.refresh} />
    {pools.loading && <p role="status">Loading recorded seasons…</p>}
    {pools.result?.data.length === 0 && <p className="notice">No regular-season game observations have been imported. Season projections cannot replace game logs. <a href="/data-sources">View data sources</a> · <button onClick={pools.refresh}>Reload recorded seasons</button></p>}
    {!!pools.result?.data.length && <form className="form-row" onSubmit={submit}>
      <label>Recorded season and source<select aria-label="Recorded season and source" required value={pool} onChange={event => { const value = event.target.value; setPool(value); const selected = pools.result?.data.find(item => `${item.seasonEndYear}:${item.source}` === value); setDate(selected?.latestGameDate ?? ''); }}><option value="">Choose recorded data</option>{pools.result.data.map(item => <option key={`${item.seasonEndYear}:${item.source}`} value={`${item.seasonEndYear}:${item.source}`}>{item.seasonEndYear - 1}–{item.seasonEndYear} · {item.source} · {item.gameCount} recorded games</option>)}</select></label>
      <label>Games through<input type="date" required value={date} max={new Date().toISOString().slice(0, 10)} onChange={event => setDate(event.target.value)} /></label>
      <button disabled={!pool || !date || pools.loading}>View performance</button>
    </form>}
    {selection && <><div className="performance-views" role="group" aria-label="Performance view">{([['best', 'Best performing'], ['hot', 'Above baseline'], ['all', 'All observed players']] as const).map(([key, label]) => <button key={key} aria-pressed={view === key} onClick={() => { setView(key); setPage(1); }}>{label}</button>)}<button onClick={performance.refresh} disabled={performance.loading}>Refresh performance</button></div>
      <ErrorNotice text={performance.error} retry={performance.refresh} />
      <p className="loading-status" role="status">{performance.loading ? 'Loading recorded performance…' : '\u00a0'}</p>
    </>}
    {data && <section aria-label="Recorded performance results" aria-busy={performance.loading}>
      <h3>{data.view === 'best' ? 'Best performing' : data.view === 'hot' ? 'Above baseline' : 'All observed players'}</h3>
      <p>{data.seasonEndYear - 1}–{data.seasonEndYear} · {data.source} · Games through {data.throughDate}. {data.observedPlayers} players observed; {data.bestQualifiedPlayers} qualify for a current average; {data.comparisonQualifiedPlayers} qualify for a comparison.</p>
      <p className="muted">Latest appearance: {data.latestAppearance ?? 'none'} · Latest source retrieval: {data.latestFetchedAt ? new Date(data.latestFetchedAt).toLocaleString() : 'none'}. Refresh after scoring edits. This is corrected recorded history, not an as-known-at backtest.</p>
      <details className="performance-scoring"><summary>Scoring used for these results</summary><ul>{data.scoringRules.map(rule => <li key={rule.stat}>{rule.stat}: {rule.pointsPerUnit} points per unit</li>)}</ul></details>
      <p className="muted">Current average: up to {data.policy.maximumBaselineGames} appearances, including the recent {data.policy.recentGames}. Comparison: the preceding {data.policy.minimumBaselineGames}–{data.policy.maximumBaselineGames}, excluding those recent appearances. Best sorts by current average; above baseline sorts by positive point lift.</p>
      {!data.players.length && <p className="notice">{!data.observedPlayers ? 'No game observations match this source, season and date.' : data.view === 'hot' ? 'No players have a qualified positive lift for this selection. Check all observed players for sample sizes.' : data.view === 'best' ? 'No players have enough appearances for a current average. Check all observed players for sample sizes.' : 'No players on this page.'}</p>}
      <ol className="performance-list" start={(page - 1) * 10 + 1}>{data.players.map(item => <PerformanceRow key={`${item.playerId.value}:${data.throughDate}:${data.source}:${data.view}`} item={item} />)}</ol>
      {!!performance.result?.meta?.total && <div className="pagination"><button disabled={page === 1 || performance.loading} onClick={() => setPage(value => value - 1)}>Previous results</button><span>Page {page} · {performance.result.meta.total} players</span><button disabled={page * 10 >= performance.result.meta.total || performance.loading} onClick={() => setPage(value => value + 1)}>Next results</button></div>}
      <p className="muted">Calculation model: {data.modelVersion}. DNPs are excluded from appearances; played zero and negative fantasy scores count.</p>
    </section>}
  </details>;
}

function PerformanceRow({ item }: { item: Heat }) {
  const player = useResource<Player>(`/api/players/${item.playerId.value}`);
  return <li data-player-id={item.playerId.value}>
    <h4>{player.result?.data.fullName ?? (player.loading ? 'Loading player…' : 'Player unavailable')}</h4>
    <ErrorNotice text={player.error} retry={player.refresh} />
    <p>{item.seasonAppearances} appearances · Latest {item.latestAppearance ?? 'none'}</p>
    <dl className="performance-values"><div><dt>Current average</dt><dd>{format(item.currentAverage)}</dd></div><div><dt>Recent average</dt><dd>{format(item.recentAverage)}</dd></div><div><dt>Comparison average</dt><dd>{format(item.baselineAverage)}</dd></div><div><dt>Points above baseline</dt><dd>{lift(item.pointsAboveBaseline)}</dd></div></dl>
    {!item.hasComparison && <p className="muted">Insufficient history for a heat comparison. {item.baselineWindow.length} preceding and {item.recentWindow.length} recent appearances recorded.</p>}
    {item.hasComparison && <p className="muted">Relative lift: {item.relativeLift === null ? 'undefined at a zero baseline' : `${lift(item.relativeLift * 100)}%`} · {item.recentGamesAboveBaseline} recent appearances above baseline.</p>}
    <details><summary>Games behind this result</summary>{([['Current sample', item.currentWindow], ['Comparison baseline', item.baselineWindow], ['Recent sample', item.recentWindow]] as const).map(([label, games]) => <div key={label} className="table-scroll"><table><caption>{label} · {games.length} appearances</caption><thead><tr><th>Date</th><th>Fantasy points</th><th>Source evidence</th></tr></thead><tbody>{games.map(game => <tr key={game.gameId}><td>{game.playedOn}</td><td>{format(game.fantasyPoints)}</td><td><details><summary>{game.provenance.source}</summary><div className="performance-provenance">Retrieved {new Date(game.provenance.fetchedAt).toLocaleString()}<br />Parser {game.provenance.parserVersion}<br />Game {game.gameId}<br />SHA-256 {game.provenance.rawRecordHash}</div></details></td></tr>)}</tbody></table></div>)}</details>
  </li>;
}
