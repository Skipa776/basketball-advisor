import { useState } from 'react';
import { useResource } from './useResource';
import type { League } from './types';

type Row = { playerId: string; name: string; scoredSoFar: number; gamesLeft: number; perGame: number | null; projectedRest: number };
type Side = { teamId: string; name: string; scoredSoFar: number; projectedRest: number; projectedTotal: number; players: Row[] };
type Week = { weekStart: string; weekEnd: string; today: string; scoring: string; recentGames: number; you: Side; opponent: Side; opponents: { id: string; name: string }[] };

const one = (value: number) => (Math.round(value * 10) / 10).toLocaleString('en-US');
const day = (iso: string) => new Date(`${iso}T12:00:00`).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });

function SideTable({ side, recentGames }: { side: Side; recentGames: number }) {
  return <section className="matchup-side" aria-labelledby={`side-${side.teamId}`}>
    <h3 id={`side-${side.teamId}`}>{side.name}</h3>
    <div className="table-scroll" tabIndex={0} role="region" aria-label={`${side.name} players`}><table>
      <caption className="sr-only">{side.name}: points so far and projected rest of week</caption>
      <thead><tr><th scope="col">Player</th><th scope="col">So far</th><th scope="col">Games left</th><th scope="col">Avg · last {recentGames}</th><th scope="col">Projected rest</th></tr></thead>
      <tbody>{side.players.map(player => <tr key={player.playerId}>
        <th scope="row">{player.name}</th><td data-numeric>{one(player.scoredSoFar)}</td><td data-numeric>{player.gamesLeft}</td>
        <td data-numeric>{player.perGame === null ? <span className="muted">no games yet</span> : one(player.perGame)}</td><td data-numeric>{one(player.projectedRest)}</td>
      </tr>)}</tbody>
    </table></div>
  </section>;
}

/** Weekly points matchup: your team against one opponent, scored so far plus the rest of the week projected. */
export function MatchupPage({ league, onHome }: { league?: League; onHome: () => void }) {
  const [opponent, setOpponent] = useState('');
  const week = useResource<Week>(league ? `/api/leagues/${league.id}/matchup${opponent ? `?opponent=${encodeURIComponent(opponent)}` : ''}` : null);
  if (!league) return <section className="panel"><h2>Choose a league first</h2><p>A matchup compares two rosters in one league.</p><button onClick={onHome}>Back to the menu</button></section>;
  const data = week.result?.data;
  const lead = data ? data.you.projectedTotal - data.opponent.projectedTotal : 0;
  const share = data && data.you.projectedTotal + data.opponent.projectedTotal > 0 ? (data.you.projectedTotal / (data.you.projectedTotal + data.opponent.projectedTotal)) * 100 : 50;
  return <>
    {week.error && <div className="notice error" role="alert">{week.error} <a href={`/app/teams?league=${encodeURIComponent(league.id)}`}>Open Teams in the league</a></div>}
    {week.loading && <p role="status">Projecting the week…</p>}
    {data && <>
      <section className="panel matchup" aria-labelledby="matchup-title">
        <p className="eyebrow">WEEK OF {day(data.weekStart).toUpperCase()} – {day(data.weekEnd).toUpperCase()} · {data.scoring.toUpperCase()}</p>
        <div className="matchup-head">
          <h2 id="matchup-title">{data.you.name} <span className="muted">vs</span> {data.opponent.name}</h2>
          <label>Opponent<select value={data.opponent.teamId} onChange={event => setOpponent(event.target.value)}>{data.opponents.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}</select></label>
        </div>
        <div className="matchup-score">
          {[data.you, data.opponent].map(side => <div key={side.teamId}>
            <p className="muted">{side.name}</p>
            <p className="matchup-total" data-numeric>{one(side.projectedTotal)}</p>
            <p className="muted" data-numeric>{one(side.scoredSoFar)} so far + {one(side.projectedRest)} projected</p>
          </div>)}
        </div>
        <div className="matchup-bar" role="img" aria-label={`Projected share: ${data.you.name} ${Math.round(share)} percent`}><span style={{ width: `${share}%` }} /></div>
        <p>{lead === 0 ? 'Dead even on projection.' : `${lead > 0 ? data.you.name : data.opponent.name} is projected to win by ${one(Math.abs(lead))}.`} Projections: games left from {day(data.today)} through Sunday × each player’s average over their last {data.recentGames} games.</p>
      </section>
      <div className="matchup-sides"><SideTable side={data.you} recentGames={data.recentGames} /><SideTable side={data.opponent} recentGames={data.recentGames} /></div>
    </>}
  </>;
}
