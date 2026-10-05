import { useState } from 'react';
import { useResource } from './useResource';
import { ErrorNotice } from './Workspace';
import { PlayerDetail } from './draft';
import type { Advice, Draft, EvidenceCatalog, Player } from './types';

/** EvidenceKind.DataQuality: about the data behind every pick, so it is said once, not per row. */
const DATA_QUALITY = 11;

export function DraftAdvice({ draft, leagueId, version, disabled, pick }: { draft: Draft; leagueId: string; version: number; disabled: boolean; pick: (player: Player) => Promise<void> }) {
  const revision = `${version}:${draft.picks.map(item => item.playerId.value).join(',')}`;
  const recommendations = useResource<Advice[]>(`/api/drafts/${draft.id}/recommendations`, 0, revision);
  const catalog = useResource<EvidenceCatalog>('/api/leagues/setup');
  const [detail, setDetail] = useState<Player | null>(null);
  const drafted = new Set(draft.picks.map(item => item.playerId.value));
  const available = recommendations.result?.data.filter(item => !drafted.has(item.subjectPlayerId.value)).slice(0, 5) ?? [];
  const complete = draft.currentPick > draft.teamCount * draft.roundCount;
  return <section className="advice" aria-labelledby="advice-title"><div className="advice-heading"><h3 id="advice-title" tabIndex={-1}>{complete ? 'Draft complete' : 'Next-pick shortlist'}</h3><button onClick={recommendations.refresh} disabled={recommendations.loading || disabled}>Refresh advice</button></div>
    <p className="muted">Ranked by draft value and roster fit. Tap a name for details.</p>
    <ErrorNotice text={recommendations.error || catalog.error} retry={() => { recommendations.refresh(); catalog.refresh(); }} />
    <p className="loading-status">{recommendations.loading ? 'Updating advice…' : '\u00a0'}</p>
    {available.some(item => item.evidence.some(evidence => evidence.kind === DATA_QUALITY)) && <p className="notice">Some data sources behind these picks are stale or failed, so treat close calls with care.</p>}
    {!recommendations.loading && recommendations.result && !available.length && <p>No suggestions yet. Calculate projections or keep picking.</p>}
    {!complete && <ol className="advice-list">{available.map(item => <AdviceRow key={item.subjectPlayerId.value} item={item} catalog={catalog.result?.data} disabled={disabled || recommendations.loading} pick={pick} detail={setDetail} />)}</ol>}
    {detail && <PlayerDetail key={`${detail.id.value}:${version}`} player={detail} leagueId={leagueId} close={() => setDetail(null)} />}
  </section>;
}

function AdviceRow({ item, catalog, disabled, pick, detail }: { item: Advice; catalog?: EvidenceCatalog; disabled: boolean; pick: (player: Player) => Promise<void>; detail: (player: Player) => void }) {
  const player = useResource<Player>(`/api/players/${item.subjectPlayerId.value}`);
  const value = player.result?.data;
  const confidence = catalog?.confidenceLevels.find(level => level.value === item.confidence)?.name;
  const availability = item.evidence.find(evidence => evidence.statement.startsWith('About '))?.statement;
  return <li data-player-id={item.subjectPlayerId.value}>
    <div className="advice-heading"><div>{value ? <button className="player-name" onClick={() => detail(value)}>{value.fullName}</button> : <span>{player.loading ? 'Loading player…' : 'Player unavailable'}</span>}<p className="muted">Draft value {item.score.toLocaleString(undefined, { maximumFractionDigits: 1 })}{confidence && ` · ${confidence} confidence`}{availability && ` · ${availability}`}</p></div><button disabled={disabled || !value} onClick={async () => { if (value) { await pick(value); document.getElementById('advice-title')?.focus(); } }}>Draft<span className="sr-only"> {value?.fullName}</span></button></div>
    {item.evidence.filter(evidence => evidence.kind !== DATA_QUALITY && catalog?.evidencePolarities.find(polarity => polarity.value === evidence.polarity)?.name === 'Risk').map((evidence, index) => <p className="advice-risk" key={index}><strong>Risk: </strong>{evidence.statement}</p>)}
    <ErrorNotice text={player.error} retry={player.refresh} /><details><summary>Why this pick</summary><ul>{item.evidence.map((evidence, index) => <li key={index}><strong>{catalog?.evidencePolarities.find(polarity => polarity.value === evidence.polarity)?.name ?? 'Evidence'}: </strong>{evidence.statement}</li>)}</ul></details>
  </li>;
}
