import type { League, Session } from './types';
import { useResource } from './useResource';
import { RisersTable } from './landingData';
import { RecordedPerformance } from './performance';

type HubItem = { label: string; path: string; note: string; soon?: boolean; ownerOnly?: boolean };

// The signed-in front door, after the portfolio's projects list: one oval row per destination.
const ITEMS: HubItem[] = [
  { label: 'Mock draft', path: '/app/draft', note: 'Snake draft with ranked, explained picks for your league' },
  { label: 'Teams in the league', path: '/app/teams', note: 'Every roster, and which team is yours' },
  { label: 'Waiver wire analyzer', path: '/app/waiver', note: 'Who is rising against their own baseline' },
  { label: 'Projected players', path: '/app/projections', note: 'Season values under your scoring' },
  { label: 'Your drafts', path: '/app/drafts', note: 'Reopen a saved draft' },
  { label: 'Trade analyzer', path: '/app/trade-analyzer', note: 'What a trade does to your starting lineup' },
  { label: 'Matchup analyzer', path: '/app/matchup', note: 'This week’s points against one opponent' },
  { label: 'Streaming advisor', path: '/app/streaming', note: 'Add/drops that buy usable games this week' },
  { label: 'League settings', path: '/app/league-settings', note: 'Team count, scoring and roster slots' },
  { label: 'Context review', path: '/app/context-review', note: 'Verify news before it moves a number' },
  { label: 'Account data', path: '/app/account', note: 'Export, import or delete your data' },
  { label: 'Data sources', path: '/app/data-sources', note: 'Imports and freshness', ownerOnly: true },
];

export function Hub({ session, pageHref, leagueId, onLeague }: { session: Session; pageHref: (path: string) => string; leagueId?: string; onLeague: (id: string) => void }) {
  const user = session.user!;
  const items = ITEMS.filter(item => !item.ownerOnly || user.isInstanceOwner);
  const leagues = useResource<League[]>('/api/leagues?limit=200');
  const known = leagues.result?.data.some(league => league.id === leagueId);
  return <section className="hub" aria-labelledby="hub-title">
    <p className="eyebrow">WELCOME BACK, {user.displayName.toUpperCase()}</p>
    <h1 id="hub-title">What do you want to see?</h1>
    <div className="hub-league">
      <label>Your league<select value={known ? leagueId : ''} onChange={event => onLeague(event.target.value)}>
        <option value="">{leagues.loading ? 'Loading leagues…' : 'Choose your league'}</option>
        {leagues.result?.data.map(league => <option key={league.id} value={league.id}>{league.name} · {league.teamCount} teams</option>)}
      </select></label>
      {leagues.result && !leagues.result.data.length && <a href="/app/draft">Create your first league ↗</a>}
      {leagues.result && !known && !!leagues.result.data.length && <span className="muted">Every page below uses this league.</span>}
    </div>
    <ul className="hub-list">{items.map(item => <li key={item.path}>
      <a className="hub-pill" href={pageHref(item.path)}>
        <span className="hub-label">{item.label}{item.soon && <span className="hub-soon">Soon</span>}</span>
        <span className="hub-note">{item.note}</span>
        <span className="hub-arrow" aria-hidden="true">↗</span>
      </a>
    </li>)}</ul>
  </section>;
}

export function WaiverPage({ league, onHome }: { league?: League; onHome: () => void }) {
  return <>
    <RisersTable path={league?.type === 0 ? `/api/leagues/${league.id}/waiver?limit=25` : undefined} />
    {league?.type === 0
      ? <RecordedPerformance key={league.id} leagueId={league.id} />
      : <section className="panel"><h2>Your league’s view</h2><p>Open this page from a points league to also see recorded performance under your league’s own scoring.</p><button onClick={onHome}>Back to the menu</button></section>}
  </>;
}
