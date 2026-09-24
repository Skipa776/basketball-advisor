import { useResource } from './useResource';
import type { League } from './types';

type Move = { day: string; addId: string; add: string; dropId: string; drop: string; gain: number; addUsableDays: string[]; dropUsableDaysLost: string[]; acquisitionsUsed: number; dropInjury?: string | null };
type Plan = { from: string; to: string; scoring: string; acquisitionLimit: number; baselineValue: number; plannedValue: number; freeAgentsConsidered: number; moves: Move[]; evidence: string[] };

const one = (value: number) => (Math.round(value * 10) / 10).toLocaleString('en-US');
const weekday = (iso: string) => new Date(`${iso}T12:00:00`).toLocaleDateString('en-US', { weekday: 'short' });
const day = (iso: string) => new Date(`${iso}T12:00:00`).toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
const days = (list: string[]) => list.length ? `(${list.map(weekday).join(', ')})` : '';

/** R14: a dated add/drop plan where every move states the usable games it buys and what it costs. */
export function StreamingPage({ league, onHome }: { league?: League; onHome: () => void }) {
  const plan = useResource<Plan>(league ? `/api/leagues/${league.id}/streaming` : null);
  if (!league) return <section className="panel"><h2>Choose a league first</h2><p>Streaming plans use your league’s roster, slots and add limit.</p><button onClick={onHome}>Back to the menu</button></section>;
  const data = plan.result?.data;
  return <>
    {plan.error && <div className="notice error" role="alert">{plan.error} <a href={`/app/teams?league=${encodeURIComponent(league.id)}`}>Open Teams in the league</a></div>}
    {plan.loading && <p role="status">Planning the rest of the week…</p>}
    {data && <section className="panel streaming" aria-labelledby="streaming-title">
      <p className="eyebrow">{day(data.from).toUpperCase()} – {day(data.to).toUpperCase()} · {data.scoring.toUpperCase()}</p>
      <h2 id="streaming-title">{data.moves.length ? `${data.moves.length} ${data.moves.length === 1 ? 'move' : 'moves'} worth +${one(data.plannedValue - data.baselineValue)}` : 'Hold your roster'}</h2>
      <p className="muted">Only games a player can actually start count. Your lineup projects {one(data.baselineValue)} usable points{data.moves.length ? `, ${one(data.plannedValue)} with this plan` : ''}; {data.freeAgentsConsidered} free agents considered; up to {data.acquisitionLimit} adds a week.</p>
      {!data.moves.length && <p>No add/drop gains usable games without dropping someone worth more over the rest of the season.</p>}
      <ol className="stream-moves">{data.moves.map(move => <li key={`${move.day}-${move.addId}`}>
        <span className="stream-day">{weekday(move.day)}</span>
        <div>
          <p><strong>Add {move.add}</strong> +{move.addUsableDays.length} usable {move.addUsableDays.length === 1 ? 'game' : 'games'} {days(move.addUsableDays)}</p>
          <p>Drop {move.drop}{move.dropInjury && <> <span className="injury">{move.dropInjury}</span></>} · {move.dropUsableDaysLost.length} usable {move.dropUsableDaysLost.length === 1 ? 'game' : 'games'} lost {days(move.dropUsableDaysLost)}</p>
          <p className="muted">net +{one(move.gain)} expected points · {move.acquisitionsUsed} of {data.acquisitionLimit} acquisitions used</p>
        </div>
      </li>)}</ol>
      <details><summary>How this plan was made</summary><ul>{data.evidence.map(item => <li key={item}>{item}</li>)}</ul></details>
    </section>}
  </>;
}
