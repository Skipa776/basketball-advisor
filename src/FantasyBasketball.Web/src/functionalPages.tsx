import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, message, post } from './api';
import { useResource } from './useResource';
import type { League, Player, Session, Setup } from './types';
import { PlayerDetail } from './draft';
import { WaiverPage } from './hub';
import { MatchupPage } from './matchup';
import { TeamsPage } from './teams';

type PageProps = { onHome: () => void; onLeagueUpdated: () => void };
type Health = { source: string; lastSuccess: string | null; lastFailure: string | null; isStale: boolean; isDegraded: boolean };
type ImportRun = { id: string; source: string; status: number | string; startedAt: string; finishedAt: string | null; rowsWritten: number; pendingIdentityMatches: number; failureDetail: string | null };
type ContextEvent = { id: string; type: number; typeName: string; summary: string; verification: number; verificationName: string; effectiveFrom: string; expectedExpiration: string | null; affectedPlayerIds: { value: string }[] };
type ProjectedPlayer = { rank: number; playerId: { value: string }; fullName: string; positions: string[]; projectedSeasonValue: number; averageDraftPosition: number | null; hasUnverifiedContext: boolean };
type DraftRecord = { leagueId: string; session: { id: string; teamCount: number; roundCount: number; userSlot: number; currentPick: number; picks: { pickNumber: number }[] } };

function ErrorNotice({ text, retry }: { text: string; retry?: () => void }) {
  return text ? <div className="notice error" role="alert">{text} {retry && <button type="button" onClick={retry}>Try again</button>}</div> : null;
}

export function FunctionalPage({ name, session, onHome, onLeagueUpdated, leagueId }: PageProps & { name: string; session: Session; leagueId?: string }) {
  const leagues = useResource<League[]>(leagueId ? '/api/leagues?limit=200' : null);
  const selectedLeague = leagues.result?.data.find(item => item.id === leagueId);
  return <>
    <div className="intro intro-compact"><p className="eyebrow">THE WORKSPACE / {name.toUpperCase()}</p><h1>{name}.</h1></div>
    {name === 'Data sources' && <DataSourcesPage session={session} onHome={onHome} />}
    {name === 'Projected players' && <ProjectedPlayersPage league={selectedLeague} loading={leagues.loading} onHome={onHome} />}
    {name === 'Your drafts' && <DraftListPage league={selectedLeague} onHome={onHome} />}
    {name === 'League settings' && <LeagueSettingsPage league={selectedLeague} onHome={onHome} onLeagueUpdated={onLeagueUpdated} />}
    {name === 'Context review' && <ContextReviewPage onHome={onHome} />}
    {name === 'Account data' && <AccountDataPage onHome={onHome} />}
    {name === 'Waiver wire analyzer' && <WaiverPage league={selectedLeague} onHome={onHome} />}
    {name === 'Matchup analyzer' && <MatchupPage league={selectedLeague} onHome={onHome} />}
    {name === 'Teams in the league' && <TeamsPage league={selectedLeague} onHome={onHome} />}
    {['Trade analyzer', 'Streaming advisor', 'Standings'].includes(name) && <section className="panel"><h2>Not built yet</h2><p>{name === 'Trade analyzer' ? 'R15 is not implemented; no trade results are available.' : name === 'Streaming advisor' ? 'R14 is not implemented; no streaming recommendations are available.' : 'Standings are deferred in the post-MVP roadmap; no leaderboard data is available.'}</p><button onClick={onHome}>Back to workspace</button></section>}
    {name === 'Page not found' && <section className="panel"><h2>Page not found</h2><p>This app page does not exist.</p><button onClick={onHome}>Back to workspace</button></section>}
  </>;
}

function DataSourcesPage({ session, onHome }: { session: Session; onHome: () => void }) {
  const owner = !!session.user?.isInstanceOwner;
  const health = useResource<Health[]>(owner ? '/api/health/data-sources' : null, 15000);
  const runs = useResource<ImportRun[]>(owner ? '/api/imports/runs?page=1&limit=20' : null, 5000);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const [seasonEndYear, setSeasonEndYear] = useState(new Date().getUTCFullYear());
  const [scheduleFrom, setScheduleFrom] = useState(`${new Date().getUTCFullYear()}-01-01`);
  const [scheduleTo, setScheduleTo] = useState(`${new Date().getUTCFullYear()}-12-31`);
  const [adpCsv, setAdpCsv] = useState('');
  const yesterday = new Date(Date.now() - 86400000).toISOString().slice(0, 10);
  const [boxFrom, setBoxFrom] = useState(yesterday);
  const [boxTo, setBoxTo] = useState(yesterday);
  if (!owner) return <section className="panel"><h2>Owner access required</h2><p>Data import actions are available to the instance owner.</p><button onClick={onHome}>Back to workspace</button></section>;
  async function start(kind: string, body: unknown = {}) {
    setBusy(kind); setError('');
    try { await post(`/api/imports/${kind}`, body); runs.refresh(); health.refresh(); }
    catch (reason) { setError(message(reason)); }
    finally { setBusy(''); }
  }
  return <section className="panel functional-page" aria-labelledby="sources-title"><div className="section-heading"><div><p className="eyebrow">OWNER / REFERENCE DATA</p><h2 id="sources-title">Data sources</h2></div><button onClick={onHome}>Workspace</button></div>
    <p>Imports run in the background. This page shows recorded status and source freshness; it never starts an import automatically.</p>
    <ErrorNotice text={health.error || runs.error || error} retry={() => { health.refresh(); runs.refresh(); }} />
    <div className="import-actions"><button disabled={!!busy} onClick={() => start('players')}>{busy === 'players' ? 'Queueing…' : 'Import players'}</button><div className="form-row"><label>Season ending<input type="number" min="1947" max={new Date().getUTCFullYear() + 1} value={seasonEndYear} onChange={event => setSeasonEndYear(Number(event.target.value))} /></label><button disabled={!!busy || !seasonEndYear} onClick={() => start('season-stats', { seasonEndYear })}>{busy === 'season-stats' ? 'Queueing…' : 'Import season stats'}</button></div><div className="form-row"><label>Schedule from<input type="date" value={scheduleFrom} onChange={event => setScheduleFrom(event.target.value)} /></label><label>Schedule to<input type="date" min={scheduleFrom} value={scheduleTo} onChange={event => setScheduleTo(event.target.value)} /></label><button disabled={!!busy || !scheduleFrom || !scheduleTo || scheduleFrom > scheduleTo} onClick={() => start('schedule', { from: scheduleFrom, to: scheduleTo })}>{busy === 'schedule' ? 'Queueing…' : 'Import schedule'}</button></div><label>ADP CSV, optional<textarea value={adpCsv} onChange={event => setAdpCsv(event.target.value)} rows={3} placeholder="Paste a CSV or leave blank to use FantasyPros" /></label><button disabled={!!busy} onClick={() => start('adp', { csv: adpCsv || null })}>{busy === 'adp' ? 'Queueing…' : 'Import ADP'}</button></div>
    <fieldset><legend>Box scores (Basketball-Reference)</legend>
      <p>Regular-season dates only: the stored schedule cannot tell preseason, play-in or playoff games apart. One page per final game at the polite rate (about 20 seconds each); games already stored are skipped. Import the schedule for these dates first.</p>
      <div className="form-row"><label>From<input type="date" value={boxFrom} onChange={event => setBoxFrom(event.target.value)} /></label><label>To<input type="date" value={boxTo} onChange={event => setBoxTo(event.target.value)} /></label>
        <button disabled={!!busy || !boxFrom || !boxTo || boxTo < boxFrom} onClick={() => start('box-scores', { from: boxFrom, to: boxTo })}>{busy === 'box-scores' ? 'Queueing…' : 'Import box scores'}</button></div>
    </fieldset>
    <h3>Source freshness</h3>{health.loading && !health.result && <p role="status">Loading source health…</p>}
    <ul className="data-list">{health.result?.data.map(source => {
      const latest = runs.result?.data.find(run => run.source === source.source);
      const runStatus = latest?.status === 0 || latest?.status === 'Running' ? 'Import running' : latest?.status === 2 || latest?.status === 'Failed' ? 'Latest import failed' : latest && latest.rowsWritten === 0 ? 'Import completed with no rows' : !source.lastSuccess ? 'No data imported' : source.isStale || source.isDegraded ? 'Stale or degraded' : 'Ready';
      return <li key={source.source}><strong>{source.source}</strong><span>{source.lastSuccess ? `Last success ${new Date(source.lastSuccess).toLocaleString()}` : 'No successful import recorded'}</span><span>{runStatus}</span></li>;
    })}</ul>
    <h3>Recent import runs</h3>{runs.loading && !runs.result && <p role="status">Loading import history…</p>}
    {runs.result?.data.length ? <ul className="data-list">{runs.result.data.map(run => <li key={run.id}><strong>{run.source} · {typeof run.status === 'number' ? ['Running', 'Succeeded', 'Failed'][run.status] ?? 'Unknown' : run.status}</strong><span>{run.rowsWritten} rows · started {new Date(run.startedAt).toLocaleString()}</span>{run.failureDetail && <span role="status">{run.failureDetail}</span>}</li>)}</ul> : runs.result && <p>No imports have run yet.</p>}
  </section>;
}

const PAGE_SIZE = 50;
const points = (value: number) => value.toLocaleString(undefined, { maximumFractionDigits: 1 });

function ProjectedPlayersPage({ league, loading, onHome }: { league?: League; loading: boolean; onHome: () => void }) {
  const [page, setPage] = useState(1);
  const [detail, setDetail] = useState<Player | null>(null);
  const resource = useResource<ProjectedPlayer[]>(league?.type === 0 ? `/api/leagues/${league.id}/projected-players?page=${page}&limit=${PAGE_SIZE}` : null);
  if (!league) return loading ? <p role="status">Loading your league…</p> : <section className="panel"><h2>Choose a league first</h2><p>Projections are calculated per league, under its scoring.</p><button onClick={onHome}>Choose a league</button></section>;
  if (league.type !== 0) return <section className="panel"><h2>Points leagues only</h2><p>Projected values are calculated for points scoring. Category leagues have no ranked list yet.</p><button onClick={onHome}>Back to workspace</button></section>;
  const total = resource.result?.meta?.total ?? 0;
  return <section className="panel functional-page" aria-labelledby="projected-title"><div className="section-heading"><div><p className="eyebrow">{league.name}</p><h2 id="projected-title">Projected players</h2></div><button onClick={onHome}>Workspace</button></div>
    <p>Ranked by projected season points under this league’s scoring. Estimates, not results. Tap a name to see how it was built.</p>
    <ErrorNotice text={resource.error} retry={resource.refresh} />
    <p className="loading-status" role="status">{resource.loading ? 'Loading projections…' : '\u00a0'}</p>
    {resource.result && !total && <p className="notice">No projections for this league yet. Import players and season stats, then open the workspace and use “Prepare league projections”. <a href={`/app/draft?league=${encodeURIComponent(league.id)}`}>Go to workspace ↗</a></p>}
    {!!resource.result?.data.length && <><div className="table-scroll"><table><caption className="sr-only">Players ranked by projected season points</caption><thead><tr><th>Rank</th><th>Player</th><th>Position</th><th>Projected season points</th><th>ADP</th></tr></thead><tbody>{resource.result.data.map(row => <tr key={row.playerId.value}><td>{row.rank}</td><th scope="row"><button className="player-name" onClick={() => setDetail({ id: row.playerId, fullName: row.fullName, positions: row.positions })}>{row.fullName}</button>{row.hasUnverifiedContext && <span className="muted"> · unverified context</span>}</th><td>{row.positions.join(' / ') || '—'}</td><td>{points(row.projectedSeasonValue)}</td><td>{row.averageDraftPosition === null ? '—' : points(row.averageDraftPosition)}</td></tr>)}</tbody></table></div>
      <div className="pagination"><button disabled={page === 1 || resource.loading} onClick={() => setPage(page - 1)}>Previous</button><span>Page {page} of {Math.max(1, Math.ceil(total / PAGE_SIZE))} · {total} players</span><button disabled={resource.loading || page * PAGE_SIZE >= total} onClick={() => setPage(page + 1)}>Next</button></div></>}
    {detail && <PlayerDetail key={detail.id.value} player={detail} leagueId={league.id} close={() => setDetail(null)} />}
  </section>;
}

function DraftListPage({ league, onHome }: { league?: League; onHome: () => void }) {
  const [page, setPage] = useState(1);
  const resource = useResource<DraftRecord[]>(league ? `/api/drafts?leagueId=${encodeURIComponent(league.id)}&page=${page}&limit=50` : null);
  if (!league) return <section className="panel"><h2>Choose a league first</h2><p>Your saved drafts are listed under a league.</p><button onClick={onHome}>Choose a league</button></section>;
  return <section className="panel functional-page"><div className="section-heading"><div><p className="eyebrow">{league.name}</p><h2>Your drafts</h2></div><button onClick={onHome}>Workspace</button></div>
    <ErrorNotice text={resource.error} retry={resource.refresh} />{resource.loading && !resource.result && <p role="status">Loading saved drafts…</p>}
    {resource.result?.data.length ? <ul className="data-list">{resource.result.data.map(({ session }) => <li key={session.id}><div><strong>{session.picks.length === session.roundCount * session.teamCount ? 'Complete draft' : 'Draft in progress'}</strong><span>{session.picks.length} of {session.roundCount * session.teamCount} picks · seat {session.userSlot} of {session.teamCount}</span></div><a className="button-link" href={`/app/draft?league=${encodeURIComponent(league.id)}&draft=${encodeURIComponent(session.id)}`}>Reopen draft</a></li>)}</ul> : resource.result && <p>No saved drafts for this league yet.</p>}
    {!!resource.result?.meta && resource.result.meta.total > 50 && <div className="form-row"><button disabled={page === 1 || resource.loading} onClick={() => setPage(page - 1)}>Previous page</button><span>Page {page} of {Math.ceil(resource.result.meta!.total / 50)}</span><button disabled={page * 50 >= resource.result.meta.total || resource.loading} onClick={() => setPage(page + 1)}>Next page</button></div>}
  </section>;
}

function LeagueSettingsPage({ league, onHome, onLeagueUpdated }: { league?: League; onHome: () => void; onLeagueUpdated: () => void }) {
  const setup = useResource<Setup>(league ? '/api/leagues/setup' : null);
  const values = setup.result?.data;
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [showRecalculate, setShowRecalculate] = useState(false);
  const [savedScoring, setSavedScoring] = useState<{ stat: string; pointsPerUnit: number }[] | null>(null);
  const pools = useResource<{ seasonEndYear: number; source: string; playerCount: number }[]>(league ? `/api/leagues/${league.id}/projection-pools` : null);
  const [selectedPool, setSelectedPool] = useState('');
  const ruleRows = savedScoring ?? (league?.scoringRules ?? []).map(rule => ({ stat: values?.stats.find(item => item.value === rule.stat)?.name ?? String(rule.stat), pointsPerUnit: rule.pointsPerUnit }));
  if (!league) return <section className="panel"><h2>Choose a league first</h2><button onClick={onHome}>Choose a league</button></section>;
  const selected = league;
  async function saveSettings(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('');
    const form = new FormData(event.currentTarget);
    try {
      const rosterSlots = values?.rosterSlots.flatMap(name => Array<string>(Number(form.get(`slot:${name}`))).fill(name)) ?? [];
      await api(`/api/leagues/${selected.id}/settings`, { method: 'PUT', body: JSON.stringify({ name: form.get('name'), teamCount: Number(form.get('teamCount')), cadence: form.get('cadence'), rosterSlots }) });
      onLeagueUpdated();
    } catch (reason) { setError(message(reason)); }
    finally { setBusy(false); }
  }
  async function saveScoring(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('');
    const form = new FormData(event.currentTarget);
    const nextRules = ruleRows.map(rule => ({ stat: rule.stat, pointsPerUnit: Number(form.get(rule.stat)) }));
    try {
      await api(`/api/leagues/${selected.id}/scoring`, { method: 'PUT', body: JSON.stringify({ scoringRules: nextRules }) });
      setSavedScoring(nextRules);
      setShowRecalculate(true); pools.refresh();
    } catch (reason) { setError(message(reason)); }
    finally { setBusy(false); }
  }
  async function recalculate() {
    if (!selectedPool) return;
    const [seasonEndYear, source] = selectedPool.split('|'); setBusy(true); setError('');
    try { await post(`/api/leagues/${selected.id}/projections`, { seasonEndYear: Number(seasonEndYear), source }); setShowRecalculate(false); onLeagueUpdated(); }
    catch (reason) { setError(message(reason)); }
    finally { setBusy(false); }
  }
  return <section className="panel functional-page"><div className="section-heading"><div><p className="eyebrow">LEAGUE / {league.type === 0 ? 'POINTS' : 'CATEGORIES'}</p><h2>League settings</h2></div><button onClick={onHome}>Workspace</button></div>
    <ErrorNotice text={setup.error || pools.error || error} retry={() => { setup.refresh(); pools.refresh(); }} />
    {setup.loading && <p role="status">Loading league rules…</p>}
    {values && <><form className="setup-form" onSubmit={saveSettings}><div className="form-row"><label>League name<input name="name" defaultValue={league.name} maxLength={100} required /></label><label>Teams<input name="teamCount" type="number" min="1" defaultValue={league.teamCount} required /></label><label>Lineup changes<select name="cadence" defaultValue={league.cadence === 1 ? 'Weekly' : 'Daily'}><option>Daily</option><option>Weekly</option></select></label></div>
      <fieldset><legend>Roster slots</legend><div className="scoring-grid">{values.rosterSlots.map((name, ordinal) => <label key={name}>{name}<input type="number" name={`slot:${name}`} min="0" max="20" step="1" defaultValue={league.rosterSlots.filter(slot => slot.kind === ordinal).length} required /></label>)}</div><p>Team count and roster slots cannot change after the first draft.</p></fieldset><button className="primary" disabled={busy}>Save league settings</button></form>
      {league.type === 0 && <><form className="setup-form" onSubmit={saveScoring}><fieldset><legend>Points per stat</legend><div className="scoring-grid">{ruleRows.map(rule => <label key={rule.stat}>{rule.stat}<input name={rule.stat} type="number" step="any" defaultValue={rule.pointsPerUnit} required /></label>)}</div></fieldset><button disabled={busy}>Save scoring rules</button></form>{showRecalculate && <div className="notice" role="status"><p>Scoring changed. Recalculate projections to refresh player values.</p><label>Imported season<select value={selectedPool} onChange={event => setSelectedPool(event.target.value)}><option value="">Choose a season and source</option>{pools.result?.data.map(item => <option key={`${item.seasonEndYear}|${item.source}`} value={`${item.seasonEndYear}|${item.source}`}>{item.seasonEndYear} · {item.source} · {item.playerCount} players</option>)}</select></label><button disabled={!selectedPool || busy} onClick={recalculate}>Recalculate projections</button></div>}</>}</>}
  </section>;
}

function ContextReviewPage({ onHome }: { onHome: () => void }) {
  const [page, setPage] = useState(1);
  const events = useResource<ContextEvent[]>(`/api/context-events?page=${page}&limit=50`);
  const [search, setSearch] = useState('');
  const players = useResource<Player[]>(search.trim() ? `/api/players?search=${encodeURIComponent(search.trim())}&page=1&limit=10` : null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(''); setBusy(true);
    const element = event.currentTarget;
    const form = new FormData(element);
    const playerId = String(form.get('playerId') ?? '');
    try {
      await post('/api/context-events', { type: form.get('type'), primaryPlayerId: playerId, affectedPlayerIds: [playerId], effectiveFrom: new Date(`${form.get('effectiveFrom')}T00:00:00Z`).toISOString(), expectedExpiration: form.get('expiration') ? new Date(`${form.get('expiration')}T00:00:00Z`).toISOString() : null, direction: form.get('direction'), magnitude: Number(form.get('magnitude')), confidence: form.get('confidence'), sourceName: 'manual', summary: form.get('summary') });
      events.refresh(); element.reset();
    } catch (reason) { setError(message(reason)); }
    finally { setBusy(false); }
  }
  async function review(id: string, action: 'verify' | 'reject') {
    setError('');
    try { await post(`/api/context-events/${id}/${action}`, {}); events.refresh(); }
    catch (reason) { setError(message(reason)); }
  }
  return <section className="panel functional-page"><div className="section-heading"><div><p className="eyebrow">HUMAN REVIEW</p><h2>Context review</h2></div><button onClick={onHome}>Workspace</button></div><p>Proposed context does not affect projections as verified truth until a person reviews it.</p><ErrorNotice text={events.error || players.error || error} retry={() => events.refresh()} />
    <form className="setup-form" onSubmit={submit}><fieldset><legend>Add an injury or news event</legend><label>Find a player<input value={search} onChange={event => setSearch(event.target.value)} autoComplete="off" /></label><label htmlFor="context-player">Player</label><select id="context-player" name="playerId" required defaultValue=""><option value="">Choose a player</option>{players.result?.data.map(player => <option key={player.id.value} value={player.id.value}>{player.fullName}</option>)}</select><div className="form-row"><label>Type<select name="type"><option>Injury</option><option>CoachStatement</option></select></label><label>Direction<select name="direction"><option>Negative</option><option>Neutral</option><option>Positive</option></select></label><label>Confidence<select name="confidence"><option>Low</option><option>Moderate</option><option>High</option><option>Speculative</option></select></label></div><div className="form-row"><label>Effective from<input name="effectiveFrom" type="date" required /></label><label>Expected expiration<input name="expiration" type="date" /></label><label>Magnitude<input name="magnitude" type="number" min="0" max="1" step="any" defaultValue="0.5" required /></label></div><label>Summary<input name="summary" maxLength={500} required /></label></fieldset><button className="primary" disabled={busy}>Add proposal</button></form>
    <h3>Review queue</h3>{events.loading && !events.result && <p role="status">Loading context events…</p>}{events.result?.data.length ? <ul className="data-list">{events.result.data.map(item => <li key={item.id}><div><strong>{item.typeName} · {item.verificationName}</strong><span>{item.summary}</span><span>Effective {new Date(item.effectiveFrom).toLocaleDateString()}</span></div>{item.verificationName === 'Proposed' && <div className="form-row"><button onClick={() => review(item.id, 'verify')}>Verify</button><button onClick={() => review(item.id, 'reject')}>Reject</button></div>}</li>)}</ul> : events.result && <p>No context events recorded.</p>}
    {!!events.result?.meta && events.result.meta.total > 50 && <div className="form-row"><button disabled={page === 1 || events.loading} onClick={() => setPage(page - 1)}>Previous page</button><span>Page {page} of {Math.ceil(events.result.meta!.total / 50)}</span><button disabled={page * 50 >= events.result.meta.total || events.loading} onClick={() => setPage(page + 1)}>Next page</button></div>}
  </section>;
}

function AccountDataPage({ onHome }: { onHome: () => void }) {
  const [error, setError] = useState('');
  const [status, setStatus] = useState('');
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState('');
  async function exportData() {
    setBusy(true); setError('');
    try { const result = await api<unknown>('/api/account/export'); const file = new Blob([JSON.stringify(result.data, null, 2)], { type: 'application/json' }); const url = URL.createObjectURL(file); const anchor = document.createElement('a'); anchor.href = url; anchor.download = 'fantasy-basketball-account.json'; anchor.click(); URL.revokeObjectURL(url); setStatus('Account archive downloaded.'); }
    catch (reason) { setError(message(reason)); }
    finally { setBusy(false); }
  }
  async function importData(event: FormEvent<HTMLInputElement>) {
    const input = event.currentTarget;
    const file = input.files?.[0]; if (!file) return;
    setBusy(true); setError(''); setStatus('');
    try { const archive = JSON.parse(await file.text()); await post('/api/account/import', archive); setStatus('Account archive imported.'); }
    catch (reason) { setError(message(reason)); }
    finally { setBusy(false); input.value = ''; }
  }
  async function deleteAccount() {
    if (confirm !== 'DELETE') return;
    setBusy(true); setError('');
    try { await api('/api/account', { method: 'DELETE' }); window.location.assign('/app'); }
    catch (reason) { setError(message(reason)); setBusy(false); }
  }
  return <section className="panel functional-page"><div className="section-heading"><div><p className="eyebrow">YOUR DATA</p><h2>Account data</h2></div><button onClick={onHome}>Workspace</button></div><p>Download a portable copy, restore an archive, or delete this account and its owned data.</p><ErrorNotice text={error} /><p role="status">{status}</p><div className="form-row"><button disabled={busy} onClick={exportData}>Export account archive</button><label className="button-link">Import account archive<input type="file" accept="application/json,.json" disabled={busy} onChange={importData} /></label></div><fieldset><legend>Delete my account</legend><p>This permanently deletes your account and the leagues, drafts, picks, and context events you own.</p><label>Type DELETE to confirm<input value={confirm} onChange={event => setConfirm(event.target.value)} autoComplete="off" /></label><button className="danger" disabled={busy || confirm !== 'DELETE'} onClick={deleteAccount}>Delete account</button></fieldset></section>;
}
