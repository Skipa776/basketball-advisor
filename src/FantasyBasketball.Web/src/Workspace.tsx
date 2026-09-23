import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { post, message } from './api';
import { useResource } from './useResource';
import type { Session, Setup, League } from './types';
import { DraftWorkspace } from './draft';
import { Landing } from './Landing';
import { FunctionalPage } from './functionalPages';
import { Hub } from './hub';

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
  const pageNames = { '/app/projections': 'Projected players', '/app/data-sources': 'Data sources', '/app/drafts': 'Your drafts', '/app/league-settings': 'League settings', '/app/context-review': 'Context review', '/app/account': 'Account data', '/app/trade-analyzer': 'Trade analyzer', '/app/streaming': 'Streaming advisor', '/app/standings': 'Standings', '/app/waiver': 'Waiver wire analyzer', '/app/matchup': 'Matchup analyzer' } as Record<string, string>;
  const path = window.location.pathname.replace(/\/+$/, '');
  const params = new URLSearchParams(window.location.search);
  // /app is the menu; the draft lives at /app/draft. Old /app?…&draft= links still open the draft.
  const isDraft = path === '/app/draft' || (path === '/app' && params.has('draft'));
  const page = path === '/app' || isDraft ? null : pageNames[path] ?? 'Page not found';
  const leagueId = params.get('league') ?? undefined;
  const pageHref = (path: string) => `${path}${leagueId ? `?league=${encodeURIComponent(leagueId)}` : ''}`;
  function goHome() { window.location.assign(pageHref('/app')); }
  return <>
    <a className="skip" href="#workspace">Skip to workspace</a>
    <header className="masthead"><a className="wordmark" href="/app"><span aria-hidden="true">◉</span> Fastbreak</a><nav aria-label="Site">{user && <><a href={pageHref('/app')}>Menu</a><button onClick={logout} disabled={busy}>Sign out</button></>}</nav></header>
    <main id="workspace" tabIndex={-1} className={session.result && !user ? 'landing' : 'workspace'}>
      <ErrorNotice text={error || session.error} retry={session.error ? session.refresh : undefined} />
      {!session.result && session.loading && <p role="status">Connecting to your workspace…</p>}
      {session.result && (user
        ? isDraft
          ? <><div className="intro intro-compact"><p className="eyebrow">THE WORKSPACE / MOCK DRAFT</p><h1>See the court. <em>Make your move.</em></h1></div><LeagueWorkspace key={user.id} /></>
          : page
            ? <FunctionalPage name={page} session={session.result.data} leagueId={leagueId} onHome={goHome} onLeagueUpdated={() => window.location.reload()} />
            : <Hub session={session.result.data} pageHref={pageHref} />
        : <Landing><AccountForm session={session.result.data} onSignedIn={session.refresh} /></Landing>)}
    </main>
    {user && <footer className="site-footer"><span>Fantasy basketball. With perspective.</span><a href="/app">Workspace</a><span>2026 © Edition</span></footer>}
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
  return <section className="account" aria-labelledby="account-title"><p className="eyebrow">YOUR PRIVATE WORKSPACE</p><h2 id="account-title">{register ? 'Create your account' : 'Welcome back'}</h2>
    <form onSubmit={submit}>
      {register && <label>Your name<input name="displayName" autoComplete="nickname" required maxLength={100} /></label>}
      <label>Email<input name="email" type="email" autoComplete="username" required /></label>
      <label>Password<input name="password" type="password" autoComplete={register ? 'new-password' : 'current-password'} minLength={register ? 12 : undefined} required /></label>
      {!register && <label className="check"><input type="checkbox" name="rememberMe" /> Keep me signed in</label>}
      <ErrorNotice text={error} /><button className="primary" disabled={busy}>{busy ? 'Connecting…' : register ? 'Create account' : 'Sign in'} <span aria-hidden="true">↗</span></button>
    </form>
    {session.registrationOpen && <button className="text-button" disabled={busy} onClick={() => { setRegister(!register); setError(''); }}>{register ? 'Already have an account? Sign in' : 'New here? Create an account'}</button>}
    {!session.registrationOpen && <p className="muted">Sign-ups are closed. Sign in to continue.</p>}
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
    window.history.replaceState(null, '', `/app/draft?league=${encodeURIComponent(id)}`);
  }
  function chooseDraft(id: string) {
    setDraftId(id);
    window.history.replaceState(null, '', `/app/draft?league=${encodeURIComponent(leagueId)}&draft=${encodeURIComponent(id)}`);
  }
  return <>
    <section className="league-bar" aria-label="League selection"><label>Your league<select aria-label="Your league" value={leagueId} onChange={event => chooseLeague(event.target.value)}><option value="">Choose your league</option>{leagues.result?.data.map(league => <option key={league.id} value={league.id}>{league.name} · {league.teamCount} teams</option>)}</select></label><button onClick={() => setCreating(!creating)}>{creating ? 'Close setup' : 'Create a league'} <span aria-hidden="true">＋</span></button></section>
    <ErrorNotice text={leagues.error} retry={leagues.refresh} /><ErrorNotice text={setup.error} retry={setup.refresh} />
    {leagues.loading && <p role="status">Loading your leagues…</p>}
    {setup.result && (creating || leagues.result?.data.length === 0) && <LeagueForm setup={setup.result.data} onCreated={league => { leagues.refresh(); chooseLeague(league.id); }} />}
    {selected && selected.type === 0 && <DraftWorkspace key={selected.id} league={selected} draftId={draftId} onDraft={chooseDraft} />}
    {selected && selected.type !== 0 && <section className="panel"><p className="eyebrow">CATEGORY LEAGUE</p><h2>{selected.name}</h2><p>Category-based player valuation, draft rankings, and performance analysis are not implemented in React yet, so this workspace will not show points-based recommendations as category advice.</p></section>}
    {leagueId && leagues.result && !leagues.loading && !selected && <p className="notice">League not found. Pick another above.</p>}
    {!leagueId && !!leagues.result?.data.length && !creating && <p className="notice">Pick a league to start drafting.</p>}
  </>;
}

function LeagueForm({ setup, onCreated }: { setup: Setup; onCreated: (league: League) => void }) {
  const [rules, setRules] = useState(setup.pointsProfile.rules.map(rule => ({ ...rule, pointsPerUnit: String(rule.pointsPerUnit) })));
  const [leagueType, setLeagueType] = useState<'Points' | 'Categories'>('Points');
  const [categories, setCategories] = useState<string[]>([]);
  const [slots, setSlots] = useState<string[]>([]);
  const [slot, setSlot] = useState(setup.rosterSlots[0]);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError('');
    if (!slots.length) { setError('Add your league’s roster slots before saving.'); return; }
    if (leagueType === 'Categories' && !categories.length) { setError('Choose at least one category before saving.'); return; }
    setBusy(true);
    const form = new FormData(event.currentTarget);
    try {
      const created = await post<League>('/api/leagues', { name: form.get('name'), type: leagueType, teamCount: Number(form.get('teams')), cadence: form.get('cadence'), scoringRules: leagueType === 'Points' ? rules.map(rule => ({ ...rule, pointsPerUnit: Number(rule.pointsPerUnit) })) : [], categories: leagueType === 'Categories' ? categories : [], rosterSlots: slots });
      onCreated(created.data);
    } catch (error) { setError(message(error)); }
    finally { setBusy(false); }
  }
  return <section className="panel" aria-labelledby="setup-title"><p className="eyebrow">01 / START WITH YOUR RULES</p><h2 id="setup-title">Your rules. Your court.</h2><p>Set the rules your league uses, then save.</p>
    <form onSubmit={submit} className="setup-form"><div className="form-row"><label>League name<input name="name" required maxLength={100} /></label><label>Teams<input name="teams" type="number" min="1" step="1" defaultValue={setup.suggestedTeamCount} required /></label><label>Lineup changes<select name="cadence"><option value="Daily">Daily</option><option value="Weekly">Weekly</option></select></label></div>
      <fieldset><legend>League scoring</legend><div className="form-row"><label className="check"><input type="radio" name="leagueType" checked={leagueType === 'Points'} onChange={() => setLeagueType('Points')} />Points</label><label className="check"><input type="radio" name="leagueType" checked={leagueType === 'Categories'} onChange={() => setLeagueType('Categories')} />Categories</label></div>{leagueType === 'Points' ? <><p>{setup.pointsProfile.name} starter. Review or edit every value.</p><div className="scoring-grid">{rules.map((rule, index) => <label key={rule.stat}>{rule.stat}<input aria-label={`${rule.stat} points`} type="number" step="any" required value={rule.pointsPerUnit} onChange={event => setRules(previous => previous.map((value, i) => i === index ? { ...value, pointsPerUnit: event.target.value } : value))} /></label>)}</div></> : <div className="scoring-grid">{setup.stats.map(stat => <label className="check" key={stat.name}><input type="checkbox" checked={categories.includes(stat.name)} onChange={event => setCategories(previous => event.target.checked ? [...previous, stat.name] : previous.filter(value => value !== stat.name))} />{stat.name}</label>)}</div>}</fieldset>
      <fieldset><legend>Your roster slots</legend><p>Add every slot, bench included.</p><div className="slot-controls"><label>Slot<select value={slot} onChange={event => setSlot(event.target.value)}>{setup.rosterSlots.map(value => <option key={value}>{value}</option>)}</select></label><button type="button" onClick={() => setSlots([...slots, slot])}>Add slot</button></div><ul className="slots">{slots.map((value, index) => <li key={index}>{value} <button type="button" aria-label={`Remove ${value} slot ${index + 1}`} onClick={() => setSlots(slots.filter((_, i) => i !== index))}>×</button></li>)}</ul></fieldset>
      <label className="check"><input type="checkbox" required /> Use these reviewed {leagueType === 'Points' ? 'scoring rules' : 'categories'} for my league.</label><ErrorNotice text={error} /><button className="primary" disabled={busy}>{busy ? 'Saving…' : 'Save league'} ↗</button>
    </form>
  </section>;
}
