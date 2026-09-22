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

export function DraftWorkspace({ league, draftId, onDraft }: { league: League; draftId: string; onDraft: (id: string) => void }) {
  const record = useResource<DraftRecord>(draftId ? `/api/drafts/${draftId}` : null, 5000);
  const board = useResource<Board>(draftId ? `/api/drafts/${draftId}/board?leagueId=${league.id}` : null, 5000);
  const [projectionRevision, setProjectionRevision] = useState(0);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [announcement, setAnnouncement] = useState('');
  const session = record.result?.data.leagueId === league.id ? record.result.data.session : null;
  const complete = !!session && session.currentPick > session.teamCount * session.roundCount;
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
  return <section className="draft" aria-labelledby="draft-title"><div className="section-heading"><div><p className="eyebrow">02 / PREPARE & PICK</p><h2 id="draft-title">{league.name}</h2><p>{league.teamCount} teams · {league.type === 0 ? 'Points' : 'Category'} league</p></div>{session && <div className="on-clock"><span>{complete ? 'DRAFT COMPLETE' : 'CURRENT PICK'}</span><strong>{complete ? session.picks.length : session.currentPick}</strong><button disabled={busy || record.loading || !session.picks.length} onClick={undo}>Undo last pick</button></div>}</div>
    <ErrorNotice text={error || record.error || board.error} retry={record.error || board.error ? () => { record.refresh(); board.refresh(); } : undefined} /><p className="sr-only" role="status">{announcement}</p>
    {league.type === 0 && <ProjectionControls key={league.id} leagueId={league.id} onPublished={() => { setProjectionRevision(value => value + 1); board.refresh(); }} />}
    {session && league.type === 0 && <DraftAdvice draft={session} leagueId={league.id} version={projectionRevision} disabled={busy || record.loading || !!record.error || complete} pick={pick} />}
    {!draftId && <form className="draft-start panel" onSubmit={create}><h3>Start a snake draft</h3><p>Your draft position and round count are saved with this session.</p><div className="form-row"><label>Your draft position<input type="number" name="position" required min="1" max={league.teamCount} /></label><label>Rounds<input type="number" name="rounds" required min="1" /></label><button className="primary" disabled={busy}>{busy ? 'Starting…' : 'Start draft'} ↗</button></div></form>}
    {draftId && !record.result && record.loading && <p role="status">Restoring your saved draft…</p>}
    {record.result && !session && <p className="notice">This draft belongs to a different league. Choose its league or start a new draft.</p>}
    {session && <><p className="muted">Snake draft · You pick from slot {session.userSlot} · {session.roundCount} rounds. Bookmark this page to return to this saved draft.</p>{board.result?.data.banner && <p className="notice">{board.result.data.banner}</p>}<PlayerPool leagueId={league.id} session={session} rankings={board.result?.data.rankings ?? []} pick={pick} disabled={busy || record.loading || !!record.error || complete} /><PickHistory session={session} /></>}
    {!draftId && <PlayerPool leagueId={league.id} session={null} rankings={[]} pick={pick} disabled />}
    {league.type === 0 && <RecordedPerformance key={league.id} leagueId={league.id} />}
  </section>;
}

function PlayerPool({ leagueId, session, rankings, pick, disabled }: { leagueId: string; session: Draft | null; rankings: Ranking[]; pick: (player: Player) => Promise<void>; disabled: boolean }) {
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
  return <div className="pool"><div className="pool-tools"><label>Find a player<input ref={searchInput} type="search" value={search} onChange={event => setSearch(event.target.value)} onKeyDown={event => move(event)} placeholder="Search by name" /></label><p className="muted">Type a name, ↓ to a pick button, Enter to record.<br />Rankings use your league’s saved projections.</p></div>
    <ErrorNotice text={players.error} retry={players.refresh} /><p className="loading-status">{players.loading ? 'Loading players…' : '\u00a0'}</p>
    {players.result?.data.length === 0 && <p className="notice">{query ? 'No players match this search.' : <>No players have been imported yet. <a href="/data-sources">Open data sources</a> to import your player pool.</>}</p>}
    {!!players.result?.data.length && <><div className="table-scroll"><table><caption className="sr-only">Player pool and draft actions</caption><thead><tr><th>Player</th><th>Position</th><th>Draft value</th><th>Action</th></tr></thead><tbody ref={rows}>{players.result.data.map(player => {
      const value = values.get(player.id.value); const taken = drafted.has(player.id.value);
      return <tr key={player.id.value}><th scope="row"><button className="player-name" onClick={() => setDetail(player)}>{player.fullName}</button></th><td>{player.positions.join(' / ') || '—'}</td><td>{value ? <details><summary>{number(value.total)}</summary><div className="evidence"><p>Projected season: {number(value.projectedSeasonValue)}</p>{value.evidence.map((item, i) => <p key={i}>{item.statement}</p>)}</div></details> : <span className="muted">{taken ? 'Drafted' : 'No projection'}</span>}</td><td><button className="pick" disabled={disabled || taken || players.loading} onClick={() => recordPick(player)} onKeyDown={event => { const buttons = [...(rows.current?.querySelectorAll('button.pick:not(:disabled)') ?? [])]; move(event, buttons.indexOf(event.currentTarget)); }}>{taken ? 'Drafted' : 'Pick'}<span className="sr-only"> {player.fullName}</span></button></td></tr>;
    })}</tbody></table></div><div className="pagination"><button disabled={page === 1 || players.loading} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page} · {players.result.meta?.total} players</span><button disabled={players.loading || page * (players.result.meta?.limit ?? 50) >= (players.result.meta?.total ?? 0)} onClick={() => setPage(page + 1)}>Next</button></div></>}
    {session && !rankings.length && <p className="notice">No ranked projections are available. Picks can still be recorded; draft values will appear once league projections exist.</p>}
    {detail && <PlayerDetail key={detail.id.value} player={detail} leagueId={leagueId} close={() => setDetail(null)} />}
  </div>;
}

function PickHistory({ session }: { session: Draft }) {
  return <details className="history"><summary>Pick history · {session.picks.length} recorded</summary><ol>{session.picks.map(pick => <PickName key={pick.pickNumber} id={pick.playerId.value} number={pick.pickNumber} />)}</ol></details>;
}
function PickName({ id, number: pickNumber }: { id: string; number: number }) {
  const player = useResource<Player>(`/api/players/${id}`);
  return <li value={pickNumber}>{player.result?.data.fullName ?? (player.error ? 'Player unavailable' : 'Loading player…')}</li>;
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
      {data.adjusted.hasUnverifiedContext && <p className="notice">Includes unverified context. Review it before relying on the adjusted projection.</p>}
      <p>Observed sample: {data.observed.source.gamesPlayed} games · Season ending {data.observed.source.seasonEndYear} · Calculated {new Date(data.observed.asOf).toLocaleDateString()}</p>
      <p>Baseline: {data.baseline.projectedGamesPlayed} projected games · {data.adjusted.appliedContextEventIds.length} context events applied.</p>
      <div className="table-scroll"><table><caption>Per-game projection, before and after context</caption><thead><tr><th>Stat</th><th>Baseline</th><th>Adjusted</th></tr></thead><tbody>{stats.map(stat => <tr key={stat}><th scope="row">{stat}</th><td>{number(data.baseline.projectedPerGame.values[stat] ?? 0)}</td><td>{number(data.adjusted.projectedPerGame.values[stat] ?? 0)}</td></tr>)}</tbody></table></div>
      <p>Final fantasy points: <strong>{number(data.value.perGame)} per game</strong> · {number(data.value.seasonTotal)} per season.</p>
    </>}
  </section>;
}
