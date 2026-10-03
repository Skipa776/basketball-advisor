import { useResource } from './useResource';
import { ErrorNotice } from './Workspace';
import type { Draft, Player, SimulatedBoard, SimulatedCandidate, Simulation } from './types';

const points = (value: number) => Math.round(value).toLocaleString();
const signed = (value: number) => `${value >= 0 ? '+' : '−'}${points(Math.abs(value))}`;
const percent = (value: number) => `${Math.round(value * 100)}%`;

/**
 * The Monte Carlo pick (draft_simulation_contract): a pick card for the top candidate, with its
 * expected final roster, its edge over the next option and a one-line reason.
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
    {board && board.candidates.length > 0 && <PickCard board={board} disabled={disabled || simulation.loading} pick={pick} />}
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
