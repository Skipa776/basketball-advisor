import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, message, post } from './api';
import { useResource } from './useResource';
import { ErrorNotice } from './Workspace';
import type { League } from './types';

type RosterTeam = { id: string; name: string; isUsersTeam: boolean; players: { playerId: string; name: string; injury?: string | null }[] };
type RosterImport = { teams: number; players: number; unmatchedPlayers: string[] };
type Preview = { name: string; teamCount: number; teams: { externalTeamId: string; name: string; ownerName: string | null; players: number }[]; notes: string[] };
type ProviderImport = { teams: number; players: number; pendingMatches: number; notes: string[] };

/** Sleeper: read-only public API. Preview first (shows every setting difference), then import rosters and eligibility. */
function SleeperImport({ leagueId, onImported }: { leagueId: string; onImported: () => void }) {
  const [input, setInput] = useState('');
  const [preview, setPreview] = useState<Preview | null>(null);
  const [mine, setMine] = useState('');
  const [result, setResult] = useState<ProviderImport | null>(null);
  const [busy, setBusy] = useState('');
  const [error, setError] = useState('');
  const sleeperId = input.match(/\d{10,25}/)?.[0] ?? '';
  async function load(event: FormEvent) {
    event.preventDefault(); setBusy('preview'); setError(''); setPreview(null); setResult(null);
    try {
      const data = (await api<Preview>(`/api/leagues/${leagueId}/providers/sleeper/${sleeperId}`)).data;
      setPreview(data); setMine(data.teams.find(team => team.ownerName)?.externalTeamId ?? data.teams[0]?.externalTeamId ?? '');
    } catch (failure) { setError(message(failure)); }
    finally { setBusy(''); }
  }
  async function run() {
    setBusy('import'); setError('');
    try { setResult((await post<ProviderImport>(`/api/leagues/${leagueId}/providers/sleeper/import`, { sleeperLeagueId: sleeperId, myTeamId: mine })).data); onImported(); }
    catch (failure) { setError(message(failure)); }
    finally { setBusy(''); }
  }
  return <section className="panel" aria-labelledby="sleeper-title">
    <h2 id="sleeper-title">Import from Sleeper</h2>
    <p>Paste your Sleeper league link or ID. Rosters and each player’s Sleeper positions are imported; your league settings are compared but never changed. Sleeper is read at the app’s polite rate, so the first preview takes about 30–40 seconds.</p>
    <form className="form-row" onSubmit={load}>
      <label>Sleeper league link or ID<input value={input} onChange={event => setInput(event.target.value)} placeholder="https://sleeper.com/leagues/…" /></label>
      <button disabled={!!busy || !sleeperId}>{busy === 'preview' ? 'Reading Sleeper…' : 'Preview league'}</button>
    </form>
    {busy === 'preview' && <p role="status">Reading the league from Sleeper…</p>}
    {error && <div className="notice error" role="alert">{error}</div>}
    {preview && <>
      <fieldset><legend>Which team is yours? ({preview.name}, {preview.teamCount} teams)</legend><div className="provider-teams">{preview.teams.map(team =>
        <label className="check" key={team.externalTeamId}><input type="radio" name="my-sleeper-team" checked={mine === team.externalTeamId} onChange={() => setMine(team.externalTeamId)} />{team.name} <span className="muted">{team.players} players</span></label>)}</div></fieldset>
      {!!preview.notes.length && <ul className="provider-notes">{preview.notes.map(note => <li key={note}>{note}</li>)}</ul>}
      <button className="primary" disabled={!!busy || !mine} onClick={run}>{busy === 'import' ? 'Importing…' : 'Import rosters'} ↗</button>
    </>}
    {result && <div className="notice" role="status">Imported {result.teams} teams and {result.players} players from Sleeper.{result.pendingMatches > 0 && <> {result.pendingMatches} players need a person to confirm who they are (Context review → identities).</>}</div>}
  </section>;
}

const TEMPLATE = 'Team,Player,Mine,Positions\nMy Team,Nikola Jokic,yes,C\nMy Team,Stephen Curry,yes,PG/SG\nRival Team,Luka Doncic,,PG/SG\n';

/** League rosters from a pasted CSV (ESPN, Yahoo or Sleeper rosters copied into Team,Player rows). */
export function TeamsPage({ league, onHome }: { league?: League; onHome: () => void }) {
  const teams = useResource<RosterTeam[]>(league ? `/api/leagues/${league.id}/teams` : null);
  const [csv, setCsv] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<RosterImport | null>(null);
  if (!league) return <section className="panel"><h2>Choose a league first</h2><p>Rosters belong to a league.</p><button onClick={onHome}>Back to the menu</button></section>;
  async function submit(event: FormEvent) {
    event.preventDefault(); setBusy(true); setError(''); setResult(null);
    try { setResult((await post<RosterImport>(`/api/leagues/${league!.id}/teams/csv`, { csv })).data); setCsv(''); teams.refresh(); }
    catch (failure) { setError(message(failure)); }
    finally { setBusy(false); }
  }
  const rosters = teams.result?.data ?? [];
  return <>
    <ErrorNotice text={teams.error} retry={teams.refresh} />
    {!!rosters.length && <section className="team-grid" aria-label="League rosters">{rosters.map(team =>
      <article className={`team-card${team.isUsersTeam ? ' mine' : ''}`} key={team.id} aria-labelledby={`team-${team.id}`}>
        <header><h3 id={`team-${team.id}`}>{team.name}</h3>{team.isUsersTeam && <span className="status status-add">Your team</span>}</header>
        <p className="muted">{team.players.length} {team.players.length === 1 ? "player" : "players"}</p>
        <ol>{team.players.map(player => <li key={player.playerId}>{player.name}{player.injury && <> <span className="injury">{player.injury}</span></>}</li>)}</ol>
      </article>)}</section>}
    {teams.result && !rosters.length && <p className="notice">No rosters yet. Paste them below to make the waiver list show only players nobody owns.</p>}
    <SleeperImport leagueId={league.id} onImported={teams.refresh} />
    <section className="panel" aria-labelledby="roster-import-title">
      <h2 id="roster-import-title">{rosters.length ? 'Or replace rosters from a CSV' : 'Or import rosters from a CSV'}</h2>
      <p>One row per rostered player: <code>Team,Player</code>, plus <code>Mine</code> = yes on your team’s rows and optional <code>Positions</code> (e.g. PG/SG) as your platform lists them; positions are used by the draft for this league. Copy rosters from ESPN, Yahoo or Sleeper into this shape; names are matched without accents. An import replaces the league’s rosters.</p>
      <form onSubmit={submit} className="roster-form">
        <label>Roster CSV<textarea value={csv} onChange={event => setCsv(event.target.value)} rows={8} required spellCheck={false} placeholder={TEMPLATE} /></label>
        <div className="form-row">
          <a className="button-link" href={`data:text/csv;charset=utf-8,${encodeURIComponent(TEMPLATE)}`} download="rosters-template.csv">Download template</a>
          <button className="primary" disabled={busy || !csv.trim()}>{busy ? 'Importing…' : 'Import rosters'} ↗</button>
        </div>
      </form>
      <ErrorNotice text={error} />
      {result && <div className="notice" role="status">Imported {result.teams} teams and {result.players} players.{!!result.unmatchedPlayers.length && <> Not matched (check spelling or add them later): {result.unmatchedPlayers.join(', ')}.</>}</div>}
    </section>
  </>;
}
