import { useState } from 'react';
import type { FormEvent } from 'react';
import { post, message } from './api';
import { useResource } from './useResource';
import { ErrorNotice } from './Workspace';

type Pool = { seasonEndYear: number; source: string; playerCount: number };
type Publication = Pool & { computedAt: string };

export function ProjectionControls({ leagueId, onPublished }: { leagueId: string; onPublished: () => void }) {
  const pools = useResource<Pool[]>(`/api/leagues/${leagueId}/projection-pools`);
  const [selected, setSelected] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [published, setPublished] = useState<Publication | null>(null);
  async function calculate(event: FormEvent) {
    event.preventDefault();
    const pool = pools.result?.data.find(item => `${item.seasonEndYear}:${item.source}` === selected);
    if (!pool || busy) return;
    setBusy(true); setError(''); setPublished(null);
    try {
      const result = await post<Publication>(`/api/leagues/${leagueId}/projections`, { seasonEndYear: pool.seasonEndYear, source: pool.source });
      setPublished(result.data); onPublished();
    } catch (error) { setError(message(error)); }
    finally { setBusy(false); }
  }
  return <details className="projection-controls panel"><summary>Prepare league projections</summary>
    <p>Choose the imported season behind your draft estimates. Recalculate after changing scoring or reviewing context. Manual stat corrections take precedence.</p>
    <ErrorNotice text={error || pools.error} retry={pools.error ? pools.refresh : undefined} />
    {pools.loading && <p>Loading imported seasons…</p>}
    {pools.result?.data.length === 0 && <p>No season statistics have been imported. <a href="/data-sources">Open data sources</a>, then <button onClick={pools.refresh}>Reload seasons</button>.</p>}
    {!!pools.result?.data.length && <form onSubmit={calculate} className="form-row"><label>Projection season and source<select aria-label="Projection season and source" required value={selected} disabled={busy} onChange={event => setSelected(event.target.value)}><option value="">Choose imported data</option>{pools.result.data.map(pool => <option key={`${pool.seasonEndYear}:${pool.source}`} value={`${pool.seasonEndYear}:${pool.source}`}>{pool.seasonEndYear - 1}–{pool.seasonEndYear} · {pool.source} · {pool.playerCount} source players</option>)}</select></label><button disabled={!selected || busy || pools.loading}>{busy ? 'Calculating…' : 'Calculate projections'}</button></form>}
    {published && <p>Saved estimates for {published.playerCount} players from {published.seasonEndYear - 1}–{published.seasonEndYear} · {published.source}. Calculated {new Date(published.computedAt).toLocaleString()}.</p>}
    <p className="muted">Season projections are estimates. Recent form and hot-streak rankings are not connected yet.</p>
  </details>;
}
