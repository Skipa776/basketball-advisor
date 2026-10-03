import { useResource } from './useResource';
import { ErrorNotice } from './Workspace';
import type { Draft, Player, SimulatedBoard, SimulatedCandidate, Simulation } from './types';

const points = (value: number) => Math.round(value).toLocaleString();
const signed = (value: number) => `${value >= 0 ? '+' : '−'}${points(Math.abs(value))}`;
const percent = (value: number) => `${Math.round(value * 100)}%`;

/**
 * The Monte Carlo pick (draft_simulation_contract): a pick card for the top candidate and the
 * simulated board with 80% interval bars and survival odds. While a new pick re-simulates, the
 * last board stays up and the heuristic shortlist below answers instantly.
 */
export function SimulatedPick({ draft, disabled, pick }: { draft: Draft; disabled: boolean; pick: (player: Player) => Promise<void> }) {
  const revision = draft.picks.map(item => item.playerId.value).join(',');
  const simulation = useResource<Simulation>(`/api/drafts/${draft.id}/board/simulation`, 0, revision);
  const board = simulation.result?.data.board ?? null;
  const unavailable = simulation.result?.data.unavailable;
  return <section className="panel simulation" aria-labelledby="simulation-title">
    <div className="advice-heading"><h3 id="simulation-title">Simulated pick</h3></div>
    <p className="muted">Each option is drafted {board?.rollouts ?? 500} times against simulated opponents; numbers are your final roster’s season points.</p>
    <p className="loading-status" role="status">{simulation.loading ? 'Simulating drafts…' : ' '}</p>
    <ErrorNotice text={simulation.error} retry={simulation.refresh} />
    {unavailable && <p className="notice">{unavailable}</p>}
    {board && board.candidates.length > 0 && <><PickCard board={board} disabled={disabled || simulation.loading} pick={pick} /><SimulatedList board={board} disabled={disabled || simulation.loading} pick={pick} /></>}
  </section>;
}

function usePlayer(candidate?: SimulatedCandidate) {
  return useResource<Player>(candidate ? `/api/players/${candidate.playerId.value}` : null);
}

function reason(top: SimulatedCandidate, nextPick: number | null) {
  if (top.survivalToNextPick === null || nextPick === null) return 'Best expected final roster for your last pick.';
  return top.survivalToNextPick < 0.5
    ? `Likely gone before your pick ${nextPick}: about ${percent(top.survivalToNextPick)} chance he lasts.`
    : `Best expected roster, and about ${percent(top.survivalToNextPick)} chance he would last to pick ${nextPick}.`;
}

function PickCard({ board, disabled, pick }: { board: SimulatedBoard; disabled: boolean; pick: (player: Player) => Promise<void> }) {
  const [top, runnerUp] = board.candidates;
  const player = usePlayer(top);
  const next = usePlayer(runnerUp);
  const value = player.result?.data;
  return <article className="pick-card" aria-label="Top simulated pick">
    <p className="eyebrow">TOP PICK</p>
    <h4>{value?.fullName ?? (player.loading ? 'Loading player…' : 'Player unavailable')}</h4>
    <p className="pick-card-numbers"><strong>{points(top.mean)}</strong> ± {points(top.sd)} roster points{runnerUp && <> · <strong>{signed(-runnerUp.edge)}</strong> ± {points(runnerUp.edgeSe)} over {next.result?.data.fullName ?? 'the next option'}</>}</p>
    <p>{reason(top, board.nextUserPick)}</p>
    <ErrorNotice text={player.error} retry={player.refresh} />
    <button className="primary" disabled={disabled || !value} onClick={() => value && pick(value)}>Draft {value?.fullName ?? 'top pick'}</button>
  </article>;
}

function SimulatedList({ board, disabled, pick }: { board: SimulatedBoard; disabled: boolean; pick: (player: Player) => Promise<void> }) {
  const low = Math.min(...board.candidates.map(candidate => candidate.p10));
  const high = Math.max(...board.candidates.map(candidate => candidate.p90));
  const span = Math.max(high - low, 1);
  const at = (value: number) => `${((value - low) / span) * 100}%`;
  return <ol className="sim-board" aria-label="Simulated options">{board.candidates.map((candidate, index) =>
    <SimulatedRow key={candidate.playerId.value} candidate={candidate} first={index === 0} nextPick={board.nextUserPick} at={at} disabled={disabled} pick={pick} />)}</ol>;
}

function SimulatedRow({ candidate, first, nextPick, at, disabled, pick }: { candidate: SimulatedCandidate; first: boolean; nextPick: number | null; at: (value: number) => string; disabled: boolean; pick: (player: Player) => Promise<void> }) {
  const player = usePlayer(candidate);
  const value = player.result?.data;
  return <li data-player-id={candidate.playerId.value}>
    <span className="sim-name">{value?.fullName ?? (player.loading ? 'Loading…' : 'Unavailable')}</span>
    <span className="interval" aria-hidden="true"><span className="interval-range" style={{ left: at(candidate.p10), width: `calc(${at(candidate.p90)} - ${at(candidate.p10)})` }} /><span className="interval-mean" style={{ left: at(candidate.mean) }} /></span>
    <span className="sim-numbers">{points(candidate.mean)} <span className="muted">(80%: {points(candidate.p10)}–{points(candidate.p90)})</span></span>
    <span className="sim-edge">{first ? 'Top pick' : `${signed(candidate.edge)} ± ${points(candidate.edgeSe)}`}</span>
    <span className="sim-survival">{candidate.survivalToNextPick === null || nextPick === null ? '—' : `${percent(candidate.survivalToNextPick)} at pick ${nextPick}`}</span>
    <button disabled={disabled || !value} onClick={() => value && pick(value)}>Draft<span className="sr-only"> {value?.fullName}</span></button>
  </li>;
}
