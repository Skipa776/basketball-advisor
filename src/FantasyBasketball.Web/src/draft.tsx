import { useEffect, useRef, useState } from 'react';
import type { FormEvent, KeyboardEvent } from 'react';
import { api, post, message } from './api';
import { useResource } from './useResource';
import { ErrorNotice } from './Workspace';
import { ProjectionControls } from './projections';
import { DraftAdvice } from './advice';
import { RecordedPerformance } from './performance';
import type { Board, Draft, DraftRecord, League, Player, Ranking } from './types';

const number = (value: number) => value.toLocaleString(undefined, { maximumFractionDigits: 1 });
/** Snake order: odd rounds run 1→N, even rounds N→1. */
export const slotOnClock = (pick: number, teams: number) => { const index = (pick - 1) % teams; return Math.ceil(pick / teams) % 2 === 1 ? index + 1 : teams - index; };
type Taken = { recorded: number; currentPick: number; stoppedAt: string | null; reason: string | null };

// The Sleeper league id is remembered per browser (storage may be blocked), never stored on the server.
const sleeperKey = (leagueId: string) => `fb.sleeperLeague.${leagueId}`;
const rememberSleeperLeague = (leagueId: string, value: string) => { try { if (value) localStorage.setItem(sleeperKey(leagueId), value); else localStorage.removeItem(sleeperKey(leagueId)); } catch { /* per-browser convenience only */ } };
const recalledSleeperLeague = (leagueId: string) => { try { return localStorage.getItem(sleeperKey(leagueId)) ?? ''; } catch { return ''; } };

export function DraftWorkspace({ league, draftId, onDraft }: { league: League; draftId: string; onDraft: (id: string) => void }) {
  const record = useResource<DraftRecord>(draftId ? `/api/drafts/${draftId}` : null, 5000);
  const board = useResource<Board>(draftId ? `/api/drafts/${draftId}/board?leagueId=${league.id}` : null, 5000);
  const [projectionRevision, setProjectionRevision] = useState(0);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [announcement, setAnnouncement] = useState('');
  const session = record.result?.data.leagueId === league.id ? record.result.data.session : null;
  const complete = !!session && session.currentPick > session.teamCount * session.roundCount;
  const onClock = session && !complete ? slotOnClock(session.currentPick, session.teamCount) : 0;
  const myTurn = !!session && onClock === session.userSlot;
  const [takenText, setTakenText] = useState('');
  const [sleeperInput, setSleeperInput] = useState(() => recalledSleeperLeague(league.id));
  const sleeperId = sleeperInput.match(/\d{10,25}/)?.[0] ?? '';
  async function syncPicks(event: FormEvent) {
    event.preventDefault();
    if (!session || busy || !sleeperId) return;
    setBusy(true); setError('');
    try {
      const result = (await post<Taken>(`/api/drafts/${draftId}/picks/sync`, { sleeperLeagueId: sleeperId })).data;
      rememberSleeperLeague(league.id, sleeperId);
      setAnnouncement(`Synced ${result.recorded} ${result.recorded === 1 ? 'pick' : 'picks'} from Sleeper.`);
      if (result.stoppedAt) setError(`Recorded ${result.recorded}, then stopped at “${result.stoppedAt}”: ${result.reason} Add him to your player data, or enter the rest below.`);
    } catch (error) { setError(message(error)); }
    finally { record.refresh(); board.refresh(); setBusy(false); }
  }
  async function simulate() {
    if (!session || busy || complete || myTurn) return;
    setBusy(true); setError('');
    try { const made = (await post<{ picks: number }>(`/api/drafts/${draftId}/picks/simulate`, {})).data.picks; setAnnouncement(`Other teams made ${made} ${made === 1 ? 'pick' : 'picks'}. You are on the clock.`); }
    catch (error) { setError(message(error)); }
    finally { record.refresh(); board.refresh(); setBusy(false); }
  }
  async function recordTaken(event: FormEvent) {
    event.preventDefault();
    if (!session || busy) return;
    setBusy(true); setError('');
    try {
      const result = (await post<Taken>(`/api/drafts/${draftId}/picks/taken`, { names: takenText.split(/\r?\n|,/) })).data;
      const lines = takenText.split(/\r?\n|,/).map(line => line.trim()).filter(Boolean);
      const stopped = result.stoppedAt ? lines.indexOf(result.stoppedAt.trim()) : -1;
      setTakenText(stopped >= 0 ? lines.slice(stopped).join('\n') : '');
      setAnnouncement(`Recorded ${result.recorded} ${result.recorded === 1 ? 'pick' : 'picks'}.`);
      if (result.stoppedAt) setError(`Recorded ${result.recorded}, then stopped at “${result.stoppedAt}”: ${result.reason} Fix that line and record again, or pick him from the list below.`);
    } catch (error) { setError(message(error)); }
    finally { record.refresh(); board.refresh(); setBusy(false); }
  }
  async function create(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('');
    const form = new FormData(event.currentTarget);
    try {
      const draft = await post<Draft>('/api/drafts', { leagueId: league.id, draftPosition: Number(form.get('position')), roundCount: Number(form.get('rounds')) });
      onDraft(draft.data.id);
    } catch (error) { setError(message(error)); }
    finally { setBusy(false); }
  }
  async function pick(player: Player) {
    if (!session || busy || complete) return;
    setBusy(true); setError('');
    try {
      await post(`/api/drafts/${draftId}/picks`, { pickNumber: session.currentPick, playerId: player.id.value });
      setAnnouncement(`${player.fullName} recorded at pick ${session.currentPick}.`);
    } catch (error) { setError(message(error)); }
    finally { record.refresh(); board.refresh(); setBusy(false); }
  }
  async function undo() {
    if (!session || !session.picks.length || busy) return;
    setBusy(true); setError('');
    try {
      const last = session.picks[session.picks.length - 1];
      await api(`/api/drafts/${draftId}/picks/${last.pickNumber}`, { method: 'DELETE' });
      setAnnouncement(`Pick ${last.pickNumber} undone.`);
    } catch (error) { setError(message(error)); }
    finally { record.refresh(); board.refresh(); setBusy(false); }
  }
  return <section className="draft" aria-labelledby="draft-title"><div className="section-heading"><div><p className="eyebrow">02 / PREPARE & PICK</p><h2 id="draft-title">{league.name}</h2><p>{league.teamCount} teams · {league.type === 0 ? 'Points' : 'Category'} league</p></div>{session && <div className="on-clock"><span>{complete ? 'DRAFT COMPLETE' : myTurn ? 'YOUR PICK' : `TEAM ${onClock} PICKING`}</span><strong>{complete ? session.picks.length : session.currentPick}</strong><button disabled={busy || record.loading || !session.picks.length} onClick={undo}>Undo last pick</button></div>}</div>
    <ErrorNotice text={error || record.error || board.error} retry={record.error || board.error ? () => { record.refresh(); board.refresh(); } : undefined} /><p className="sr-only" role="status">{announcement}</p>
    {league.type === 0 && <ProjectionControls key={league.id} leagueId={league.id} onPublished={() => { setProjectionRevision(value => value + 1); board.refresh(); }} />}
    {session && !complete && <section className="panel draft-others" aria-labelledby="others-title">
      <h3 id="others-title">Other teams’ picks</h3>
      <p className="muted">{myTurn ? 'You are on the clock. Draft from the shortlist or the list below.' : `Pick ${session.currentPick} belongs to team ${onClock}. Any player you pick now is recorded for them.`}</p>
      <div className="form-row"><button onClick={simulate} disabled={busy || myTurn}>Sim other teams to my pick</button><span className="muted">Solo mock: each team drafts near ADP, with some variance, and fills its starting slots.</span></div>
      <form className="form-row" onSubmit={syncPicks}><label>Sleeper league link or ID<input value={sleeperInput} onChange={event => setSleeperInput(event.target.value)} placeholder="https://sleeper.com/leagues/…" /></label><button disabled={busy || !sleeperId}>Sync picks from Sleeper</button></form>
      <form onSubmit={recordTaken}><label>Picks made in your draft room, in order<textarea rows={3} value={takenText} onChange={event => setTakenText(event.target.value)} placeholder={'One player per line, e.g.\nNikola Jokić\nShai Gilgeous-Alexander'} /></label><button disabled={busy || !takenText.trim()}>Record these picks</button></form>
    </section>}
    {session && league.type === 0 && <DraftAdvice draft={session} leagueId={league.id} version={projectionRevision} disabled={busy || record.loading || !!record.error || complete} pick={pick} />}
    {!draftId && <form className="draft-start panel" onSubmit={create}><h3>Start a snake draft</h3><p>Choose your slot and rounds. We’ll save it.</p><div className="form-row"><label>Your draft position<input type="number" name="position" required min="1" max={league.teamCount} /></label><label>Rounds<input type="number" name="rounds" required min="1" /></label><button className="primary" disabled={busy}>{busy ? 'Starting…' : 'Start draft'} ↗</button></div></form>}
    {draftId && !record.result && record.loading && <p role="status">Restoring your saved draft…</p>}
    {record.result && !session && <p className="notice">This draft is from another league. Switch leagues or start fresh.</p>}
    {session && <><p className="muted">Snake · Slot {session.userSlot} · {session.roundCount} rounds. Bookmark to come back.</p>{board.result?.data.banner && <p className="notice">{board.result.data.banner}</p>}<PlayerPool leagueId={league.id} session={session} rankings={board.result?.data.rankings ?? []} pick={pick} disabled={busy || record.loading || !!record.error || complete} actionLabel={myTurn ? 'Pick' : `Taken by ${onClock}`} /><PickHistory session={session} /></>}
    {!draftId && <PlayerPool leagueId={league.id} session={null} rankings={[]} pick={pick} disabled />}
    {league.type === 0 && <RecordedPerformance key={league.id} leagueId={league.id} />}
  </section>;
}

function PlayerPool({ leagueId, session, rankings, pick, disabled, actionLabel = 'Pick' }: { leagueId: string; session: Draft | null; rankings: Ranking[]; pick: (player: Player) => Promise<void>; disabled: boolean; actionLabel?: string }) {
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [page, setPage] = useState(1);
  const [detail, setDetail] = useState<Player | null>(null);
  const rows = useRef<HTMLTableSectionElement>(null);
  const searchInput = useRef<HTMLInputElement>(null);
  async function recordPick(player: Player) {
    await pick(player);
    searchInput.current?.focus();
    searchInput.current?.select();
  }
  useEffect(() => { const timer = setTimeout(() => { setQuery(search); setPage(1); }, 200); return () => clearTimeout(timer); }, [search]);
  const players = useResource<Player[]>(`/api/players?search=${encodeURIComponent(query)}&page=${page}`);
  const drafted = new Set(session?.picks.map(value => value.playerId.value));
  const values = new Map(rankings.map(value => [value.playerId.value, value]));
  function move(event: KeyboardEvent, current = -1) {
    if (!['ArrowDown', 'ArrowUp'].includes(event.key)) return;
    const buttons = rows.current?.querySelectorAll<HTMLButtonElement>('button.pick:not(:disabled)');
    if (!buttons?.length) return;
    event.preventDefault();
    buttons[Math.max(0, Math.min(buttons.length - 1, current + (event.key === 'ArrowDown' ? 1 : -1)))].focus();
  }
  return <div className="pool"><div className="pool-tools"><label>Find a player<input ref={searchInput} type="search" value={search} onChange={event => setSearch(event.target.value)} onKeyDown={event => move(event)} placeholder="Search by name" /></label><p className="muted">Type a name. ↓ to Pick. Enter to draft.</p></div>
    <ErrorNotice text={players.error} retry={players.refresh} /><p className="loading-status">{players.loading ? 'Loading players…' : '\u00a0'}</p>
    {players.result?.data.length === 0 && <p className="notice">{query ? 'No players match this search.' : <>No players yet. <a href="/app/data-sources">View data sources ↗</a></>}</p>}
    {!!players.result?.data.length && <><div className="table-scroll"><table><caption className="sr-only">Player pool and draft actions</caption><thead><tr><th>Player</th><th>Position</th><th>Draft value</th><th>Action</th></tr></thead><tbody ref={rows}>{players.result.data.map(player => {
      const value = values.get(player.id.value); const taken = drafted.has(player.id.value);
      return <tr key={player.id.value}><th scope="row"><button className="player-name" onClick={() => setDetail(player)}>{player.fullName}</button></th><td>{player.positions.join(' / ') || '—'}</td><td>{value ? <details><summary>{number(value.total)}</summary><div className="evidence"><p>Projected season: {number(value.projectedSeasonValue)}</p>{value.evidence.map((item, i) => <p key={i}>{item.statement}</p>)}</div></details> : <span className="muted">{taken ? 'Drafted' : session ? 'No projection' : 'Ranked once a draft starts'}</span>}</td><td><button className="pick" disabled={disabled || taken || players.loading} onClick={() => recordPick(player)} onKeyDown={event => { const buttons = [...(rows.current?.querySelectorAll('button.pick:not(:disabled)') ?? [])]; move(event, buttons.indexOf(event.currentTarget)); }}>{taken ? 'Drafted' : actionLabel}<span className="sr-only"> {player.fullName}</span></button></td></tr>;
    })}</tbody></table></div><div className="pagination"><button disabled={page === 1 || players.loading} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page} · {players.result.meta?.total} players</span><button disabled={players.loading || page * (players.result.meta?.limit ?? 50) >= (players.result.meta?.total ?? 0)} onClick={() => setPage(page + 1)}>Next</button></div></>}
    {session && !rankings.length && <p className="notice">No projections yet. Keep picking, or calculate projections above.</p>}
    {!session && <p className="muted">Draft value depends on the pick you are on, so it appears once a draft starts. <a href={`/app/projections?league=${encodeURIComponent(leagueId)}`}>See projected players ranked by season value ↗</a></p>}
    {detail && <PlayerDetail key={detail.id.value} player={detail} leagueId={leagueId} close={() => setDetail(null)} />}
  </div>;
}

function PickHistory({ session }: { session: Draft }) {
  return <details className="history"><summary>Pick history · {session.picks.length} recorded</summary><ol>{session.picks.map(pick => <PickName key={pick.pickNumber} id={pick.playerId.value} number={pick.pickNumber} team={slotOnClock(pick.pickNumber, session.teamCount) === session.userSlot ? 'You' : `Team ${slotOnClock(pick.pickNumber, session.teamCount)}`} />)}</ol></details>;
}
function PickName({ id, number: pickNumber, team }: { id: string; number: number; team: string }) {
  const player = useResource<Player>(`/api/players/${id}`);
  return <li value={pickNumber}>{player.result?.data.fullName ?? (player.error ? 'Player unavailable' : 'Loading player…')} <span className="muted">· {team}</span></li>;
}

type StatValues = { values: Record<string, number> };
type Projection = {
  observed: { asOf: string; source: { gamesPlayed: number; seasonEndYear: number } };
  baseline: { computedAt: string; projectedPerGame: StatValues; projectedGamesPlayed: number };
  adjusted: { projectedPerGame: StatValues; hasUnverifiedContext: boolean; appliedContextEventIds: string[] };
  value: { perGame: number; seasonTotal: number };
};
export function PlayerDetail({ player, leagueId, close }: { player: Player; leagueId: string; close: () => void }) {
  const projection = useResource<Projection>(`/api/players/${player.id.value}/projection?leagueId=${leagueId}`);
  const data = projection.result?.data;
  const stats = data ? [...new Set([...Object.keys(data.baseline.projectedPerGame.values), ...Object.keys(data.adjusted.projectedPerGame.values)])] : [];
  return <section className="player-detail panel" aria-label={`${player.fullName} projection`}><button onClick={close}>Close player details ×</button><h3>{player.fullName}</h3>
    {projection.loading && <p role="status">Loading projection…</p>}<ErrorNotice text={projection.error} retry={projection.refresh} />
    {data && <>
      {data.adjusted.hasUnverifiedContext && <p className="notice">Includes unverified context. Review before trusting.</p>}
      <p>Observed sample: {data.observed.source.gamesPlayed} games · Season ending {data.observed.source.seasonEndYear} · Calculated {new Date(data.observed.asOf).toLocaleDateString()}</p>
      <p>Baseline: {data.baseline.projectedGamesPlayed} projected games · {data.adjusted.appliedContextEventIds.length} context events applied.</p>
      <div className="table-scroll"><table><caption>Per-game projection, before and after context</caption><thead><tr><th>Stat</th><th>Baseline</th><th>Adjusted</th></tr></thead><tbody>{stats.map(stat => <tr key={stat}><th scope="row">{stat}</th><td>{number(data.baseline.projectedPerGame.values[stat] ?? 0)}</td><td>{number(data.adjusted.projectedPerGame.values[stat] ?? 0)}</td></tr>)}</tbody></table></div>
      <p>Final fantasy points: <strong>{number(data.value.perGame)} per game</strong> · {number(data.value.seasonTotal)} per season.</p>
    </>}
  </section>;
}
