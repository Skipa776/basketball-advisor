import { useState } from 'react';
import type { FormEvent } from 'react';
import { message, post } from './api';
import { useResource } from './useResource';
import { ErrorNotice } from './Workspace';
import type { League } from './types';

type RosterTeam = { id: string; name: string; isUsersTeam: boolean; players: { playerId: string; name: string }[] };
type RosterImport = { teams: number; players: number; unmatchedPlayers: string[] };

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
        <ol>{team.players.map(player => <li key={player.playerId}>{player.name}</li>)}</ol>
      </article>)}</section>}
    {teams.result && !rosters.length && <p className="notice">No rosters yet. Paste them below to make the waiver list show only players nobody owns.</p>}
    <section className="panel" aria-labelledby="roster-import-title">
      <h2 id="roster-import-title">{rosters.length ? 'Replace rosters' : 'Import rosters'}</h2>
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
