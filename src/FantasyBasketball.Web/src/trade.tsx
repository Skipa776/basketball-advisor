import { useState } from 'react';
import { message, post } from './api';
import { useResource } from './useResource';
import type { League } from './types';

type RosterTeam = { id: string; name: string; isUsersTeam: boolean; players: { playerId: string; name: string }[] };
type Side = { teamId: string; team: string; sends: string[]; receives: string[] };
type Result = { verdict: string; valueBefore: number; valueAfter: number; delta: number; confidence: string; startersBefore: string[]; startersAfter: string[]; replacementValue: number; sides: Side[]; evidence: string[] };
type Leg = { fromTeamId: string; toTeamId: string; playerId: string };

const one = (value: number) => (Math.round(value * 10) / 10).toLocaleString('en-US');
const VERDICTS: Record<string, string> = { ClearWin: 'Clear win', SlightWin: 'Slight win', Neutral: 'Neutral', SlightLoss: 'Slight loss', ClearLoss: 'Clear loss' };

/** R15, points leagues: what a trade does to your starting lineup, never whether it is fair to them. */
export function TradePage({ league, onHome }: { league?: League; onHome: () => void }) {
  const teams = useResource<RosterTeam[]>(league ? `/api/leagues/${league.id}/teams` : null);
  const [partners, setPartners] = useState<string[]>([]);
  const [sending, setSending] = useState<Record<string, string>>({});
  const [receiving, setReceiving] = useState<Record<string, string>>({});
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  if (!league) return <section className="panel"><h2>Choose a league first</h2><p>Trades are judged against your league’s scoring and lineup.</p><button onClick={onHome}>Back to the menu</button></section>;
  const all = teams.result?.data ?? [];
  const mine = all.find(team => team.isUsersTeam);
  if (teams.result && !mine) return <div className="notice" role="alert">Import your league’s rosters and mark your team first. <a href={`/app/teams?league=${encodeURIComponent(league.id)}`}>Open Teams in the league</a></div>;
  const others = all.filter(team => !team.isUsersTeam);
  const chosen = others.filter(team => partners.includes(team.id));
  function toggle<T extends Record<string, string>>(state: T, set: (value: T) => void, key: string, value: string) {
    const next = { ...state } as Record<string, string>;
    if (key in next) delete next[key]; else next[key] = value;
    set(next as T); setResult(null);
  }
  const legs: Leg[] = mine ? [
    ...Object.entries(sending).map(([playerId, toTeamId]) => ({ fromTeamId: mine.id, toTeamId, playerId })),
    ...Object.entries(receiving).map(([playerId, fromTeamId]) => ({ fromTeamId, toTeamId: mine.id, playerId })),
  ] : [];
  async function evaluate() {
    setBusy(true); setError(''); setResult(null);
    try { setResult((await post<Result>(`/api/leagues/${league!.id}/trades/evaluate`, { legs })).data); }
    catch (failure) { setError(message(failure)); }
    finally { setBusy(false); }
  }
  return <>
    {teams.loading && <p role="status">Loading rosters…</p>}
    {mine && <section className="panel trade" aria-labelledby="trade-title">
      <h2 id="trade-title">Build a trade</h2>
      <fieldset><legend>Trade with</legend><div className="trade-partners">{others.map(team =>
        <label className="check" key={team.id}><input type="checkbox" checked={partners.includes(team.id)} onChange={event => {
          setPartners(event.target.checked ? [...partners, team.id] : partners.filter(id => id !== team.id));
          setReceiving(Object.fromEntries(Object.entries(receiving).filter(([, from]) => from !== team.id)));
          setSending(Object.fromEntries(Object.entries(sending).filter(([, to]) => to !== team.id))); setResult(null);
        }} />{team.name}</label>)}</div></fieldset>
      {!!chosen.length && <div className="trade-columns">
        <fieldset><legend>You send ({mine.name})</legend>{mine.players.map(player => <div className="trade-row" key={player.playerId}>
          <label className="check"><input type="checkbox" checked={player.playerId in sending} onChange={() => toggle(sending, setSending, player.playerId, chosen[0].id)} />{player.name}</label>
          {player.playerId in sending && chosen.length > 1 && <select aria-label={`Send ${player.name} to`} value={sending[player.playerId]} onChange={event => { setSending({ ...sending, [player.playerId]: event.target.value }); setResult(null); }}>{chosen.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}</select>}
        </div>)}</fieldset>
        {chosen.map(team => <fieldset key={team.id}><legend>You receive from {team.name}</legend>{team.players.map(player =>
          <label className="check" key={player.playerId}><input type="checkbox" checked={player.playerId in receiving} onChange={() => toggle(receiving, setReceiving, player.playerId, team.id)} />{player.name}</label>)}</fieldset>)}
      </div>}
      <button className="primary" disabled={busy || !legs.length} onClick={evaluate}>{busy ? 'Evaluating…' : 'Evaluate trade'} ↗</button>
      {error && <div className="notice error" role="alert">{error}</div>}
    </section>}
    {result && <section className="panel trade-result" aria-labelledby="trade-verdict">
      <p className="eyebrow">WHAT THIS DOES TO YOUR TEAM · {result.confidence.toUpperCase()} CONFIDENCE</p>
      <h2 id="trade-verdict"><span className={`status verdict-${result.verdict}`}>{VERDICTS[result.verdict] ?? result.verdict}</span> {result.delta >= 0 ? '+' : ''}{one(result.delta)} season points</h2>
      <p>Starting lineup value {one(result.valueBefore)} → {one(result.valueAfter)} (replacement level {one(result.replacementValue)}).</p>
      <ul className="trade-sides">{result.sides.map(side => <li key={side.teamId}><strong>{side.team}</strong> sends {side.sends.join(', ') || 'nothing'} · receives {side.receives.join(', ') || 'nothing'}</li>)}</ul>
      <div className="trade-columns"><div><h3>Starters before</h3><ol>{result.startersBefore.map(name => <li key={name}>{name}</li>)}</ol></div><div><h3>Starters after</h3><ol>{result.startersAfter.map(name => <li key={name}>{name}</li>)}</ol></div></div>
      <details open><summary>Why</summary><ul>{result.evidence.map(item => <li key={item}>{item}</li>)}</ul></details>
    </section>}
  </>;
}
