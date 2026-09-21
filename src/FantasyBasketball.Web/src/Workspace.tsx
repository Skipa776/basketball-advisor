import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { post, message } from './api';
import { useResource } from './useResource';
import type { Session, Setup, League } from './types';
import { DraftWorkspace } from './draft';

export function ErrorNotice({ text, retry }: { text: string; retry?: () => void }) {
  return text ? <div className="notice error" role="alert">{text} {retry && <button type="button" onClick={retry}>Try again</button>}</div> : null;
}

export function Workspace() {
  const session = useResource<Session>('/api/account/session');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    const expired = () => session.refresh();
    window.addEventListener('session-expired', expired);
    return () => window.removeEventListener('session-expired', expired);
  }, [session.refresh]);
  async function logout() {
    setBusy(true); setError('');
    try { await post('/api/account/logout', {}); window.location.assign('/app'); }
    catch (error) { setError(message(error)); setBusy(false); }
  }
  const user = session.result?.data.user;
  return <>
    <a className="skip" href="#workspace">Skip to workspace</a>
    <header className="masthead"><a className="wordmark" href="/app">✳ Fastbreak</a><span>YOUR DRAFT, IN PERSPECTIVE.</span><a href="/">Existing app ↗</a>{user && <button onClick={logout} disabled={busy}>Sign out</button>}</header>
    <main id="workspace" tabIndex={-1}>
      <div className="intro"><p className="eyebrow">THE WORKSPACE / DRAFT PREPARATION</p><h1>See the court.<br /><em>Make your move.</em></h1><p>Set your rules. Study the players. Keep your next pick in view.</p></div>
      <ErrorNotice text={error || session.error} retry={session.error ? session.refresh : undefined} />
      {!session.result && session.loading && <p role="status">Connecting to your workspace…</p>}
      {session.result && (user ? <LeagueWorkspace key={user.id} /> : <AccountForm session={session.result.data} onSignedIn={session.refresh} />)}
    </main>
    <footer>Fantasy basketball. With perspective. <a href="/data-sources">Data sources ↗</a></footer>
  </>;
}

function AccountForm({ session, onSignedIn }: { session: Session; onSignedIn: () => void }) {
  const [register, setRegister] = useState(false);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError('');
    const form = new FormData(event.currentTarget);
    try {
      await post(`/api/account/${register ? 'register' : 'login'}`, { email: form.get('email'), password: form.get('password'), displayName: form.get('displayName'), rememberMe: form.has('rememberMe') });
      onSignedIn();
    } catch (error) { setError(message(error)); }
    finally { setBusy(false); }
  }
  return <section className="account panel" aria-labelledby="account-title"><p className="eyebrow">YOUR PRIVATE WORKSPACE</p><h2 id="account-title">{register ? 'Create your account' : 'Welcome back'}</h2>
    <form onSubmit={submit}>
      {register && <label>Your name<input name="displayName" autoComplete="nickname" required maxLength={100} /></label>}
      <label>Email<input name="email" type="email" autoComplete="username" required /></label>
      <label>Password<input name="password" type="password" autoComplete={register ? 'new-password' : 'current-password'} minLength={register ? 12 : undefined} required /></label>
      {!register && <label className="check"><input type="checkbox" name="rememberMe" /> Keep me signed in</label>}
      <ErrorNotice text={error} /><button className="primary" disabled={busy}>{busy ? 'Connecting…' : register ? 'Create account' : 'Sign in'} <span aria-hidden="true">↗</span></button>
    </form>
    {session.registrationOpen && <button className="text-button" disabled={busy} onClick={() => { setRegister(!register); setError(''); }}>{register ? 'Already have an account? Sign in' : 'New here? Create an account'}</button>}
    {!session.registrationOpen && <p className="muted">Account creation is closed on this instance. Sign in with your existing account.</p>}
  </section>;
}

function LeagueWorkspace() {
  const leagues = useResource<League[]>('/api/leagues?limit=200');
  const setup = useResource<Setup>('/api/leagues/setup');
  const initial = new URLSearchParams(window.location.search);
  const [leagueId, setLeagueId] = useState(initial.get('league') ?? '');
  const [creating, setCreating] = useState(false);
  const [draftId, setDraftId] = useState(initial.get('draft') ?? '');
  const selected = leagues.result?.data.find(league => league.id === leagueId);
  function chooseLeague(id: string) {
    setLeagueId(id); setDraftId(''); setCreating(false);
    window.history.replaceState(null, '', `/app?league=${encodeURIComponent(id)}`);
  }
  function chooseDraft(id: string) {
    setDraftId(id);
    window.history.replaceState(null, '', `/app?league=${encodeURIComponent(leagueId)}&draft=${encodeURIComponent(id)}`);
  }
  return <>
    <section className="league-bar" aria-label="League selection"><label>Your league<select aria-label="Your league" value={leagueId} onChange={event => chooseLeague(event.target.value)}><option value="">Choose your league</option>{leagues.result?.data.map(league => <option key={league.id} value={league.id}>{league.name} · {league.teamCount} teams</option>)}</select></label><button onClick={() => setCreating(!creating)}>{creating ? 'Close setup' : 'Create a league'} <span aria-hidden="true">＋</span></button></section>
    <ErrorNotice text={leagues.error} retry={leagues.refresh} /><ErrorNotice text={setup.error} retry={setup.refresh} />
    {leagues.loading && <p role="status">Loading your leagues…</p>}
    {setup.result && (creating || leagues.result?.data.length === 0) && <LeagueForm setup={setup.result.data} onCreated={league => { leagues.refresh(); chooseLeague(league.id); }} />}
    {selected && <DraftWorkspace key={selected.id} league={selected} draftId={draftId} onDraft={chooseDraft} />}
    {leagueId && leagues.result && !leagues.loading && !selected && <p className="notice">This league is not available to your account. Choose another league above.</p>}
    {!leagueId && !!leagues.result?.data.length && !creating && <p className="notice">Choose a league to start preparing your draft.</p>}
  </>;
}

function LeagueForm({ setup, onCreated }: { setup: Setup; onCreated: (league: League) => void }) {
  const [rules, setRules] = useState(setup.pointsProfile.rules.map(rule => ({ ...rule, pointsPerUnit: String(rule.pointsPerUnit) })));
  const [slots, setSlots] = useState<string[]>([]);
  const [slot, setSlot] = useState(setup.rosterSlots[0]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError('');
    if (!slots.length) { setError('Add your league’s roster slots before saving.'); return; }
    setBusy(true);
    const form = new FormData(event.currentTarget);
    try {
      const created = await post<League>('/api/leagues', { name: form.get('name'), type: 'Points', teamCount: Number(form.get('teams')), cadence: form.get('cadence'), scoringRules: rules.map(rule => ({ ...rule, pointsPerUnit: Number(rule.pointsPerUnit) })), categories: [], rosterSlots: slots });
      onCreated(created.data);
    } catch (error) { setError(message(error)); }
    finally { setBusy(false); }
  }
  return <section className="panel" aria-labelledby="setup-title"><p className="eyebrow">01 / START WITH YOUR RULES</p><h2 id="setup-title">Your rules. Your court.</h2><p>An editable {setup.pointsProfile.name} starter. Review it against your league before saving.</p>
    <form onSubmit={submit} className="setup-form"><div className="form-row"><label>League name<input name="name" required maxLength={100} /></label><label>Teams<input name="teams" type="number" min="1" step="1" defaultValue={setup.suggestedTeamCount} required /></label><label>Lineup changes<select name="cadence"><option value="Daily">Daily</option><option value="Weekly">Weekly</option></select></label></div>
      <fieldset><legend>Scoring · points per stat</legend><div className="scoring-grid">{rules.map((rule, index) => <label key={rule.stat}>{rule.stat}<input aria-label={`${rule.stat} points`} type="number" step="any" required value={rule.pointsPerUnit} onChange={event => setRules(previous => previous.map((value, i) => i === index ? { ...value, pointsPerUnit: event.target.value } : value))} /></label>)}</div></fieldset>
      <fieldset><legend>Your roster slots</legend><p>Add each slot, including repeat positions and bench slots.</p><div className="slot-controls"><label>Slot<select value={slot} onChange={event => setSlot(event.target.value)}>{setup.rosterSlots.map(value => <option key={value}>{value}</option>)}</select></label><button type="button" onClick={() => setSlots([...slots, slot])}>Add slot</button></div><ul className="slots">{slots.map((value, index) => <li key={index}>{value} <button type="button" aria-label={`Remove ${value} slot ${index + 1}`} onClick={() => setSlots(slots.filter((_, i) => i !== index))}>×</button></li>)}</ul></fieldset>
      <label className="check"><input type="checkbox" required /> Use these reviewed scoring rules for my league.</label><ErrorNotice text={error} /><button className="primary" disabled={busy}>{busy ? 'Saving…' : 'Save league'} ↗</button>
    </form>
  </section>;
}
