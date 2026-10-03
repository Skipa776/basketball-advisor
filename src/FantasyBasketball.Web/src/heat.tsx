import { useResource } from './useResource';
import type { Player } from './types';

export type HeatLabel = { playerId: string; label: 'HOT' | 'COLD'; cause: 'Role' | 'Shooting' | 'Mixed' | null; probability: number; shiftMean: number; shiftLow: number; shiftHigh: number; opportunityShare: number | null; recentGames: number; baselineGames: number; throughDate: string };
export type HeatLabelPage = { seasonEndYear: number; throughDate: string; modelVersion: string; eligiblePlayers: number; qualifiedPlayers: number; nullLabelRate: number | null; expectedChanceLabels: number | null; disclaimer: string; labels: HeatLabel[] };

const causeText = {
  HOT: { Role: 'More minutes or touches', Shooting: 'Efficiency spike, same role', Mixed: 'Role and efficiency both up' },
  COLD: { Role: 'Fewer minutes or touches', Shooting: 'Shooting slump, same role', Mixed: 'Role and efficiency both down' },
} as const;
const signed = (value: number) => `${value > 0 ? '+' : ''}${value.toFixed(1)}`;

/** A text badge (never colour alone) whose title carries the disclaimer. */
export function HeatBadge({ label, disclaimer }: { label: HeatLabel; disclaimer: string }) {
  const cause = label.cause ? ` · ${label.cause}` : '';
  return <span className={`heat-badge ${label.label.toLowerCase()}`} title={`${disclaimer} ${Math.round(label.probability * 100)}% probability.`}>
    {label.label}{cause}
  </span>;
}

export function HeatLabels({ page }: { page: HeatLabelPage }) {
  const hot = page.labels.filter(label => label.label === 'HOT');
  const cold = page.labels.filter(label => label.label === 'COLD');
  return <section aria-label="Hot and cold labels">
    <h3>Hot & cold</h3>
    <p className="notice">{page.disclaimer}{page.expectedChanceLabels !== null && <> That is about {page.expectedChanceLabels} of the {page.labels.length} labels below.</>}</p>
    <p className="muted">{page.qualifiedPlayers} of {page.eligiblePlayers} players with 15+ games qualify (15+ baseline minutes). Games through {page.throughDate}. Model {page.modelVersion}.</p>
    {([['Running hot', hot], ['Running cold', cold]] as const).map(([title, labels]) => <div key={title}>
      <h4>{title} · {labels.length}</h4>
      {!labels.length && <p className="muted">No players clear the bar for this selection.</p>}
      <ol className="heat-list">{labels.map(label => <HeatRow key={label.playerId} label={label} disclaimer={page.disclaimer} />)}</ol>
    </div>)}
  </section>;
}

function HeatRow({ label, disclaimer }: { label: HeatLabel; disclaimer: string }) {
  const player = useResource<Player>(`/api/players/${label.playerId}`);
  return <li data-player-id={label.playerId}>
    <strong>{player.result?.data.fullName ?? (player.loading ? 'Loading player…' : 'Player unavailable')}</strong> <HeatBadge label={label} disclaimer={disclaimer} />
    <p className="muted">{label.cause ? causeText[label.label][label.cause] : 'Cause unavailable'} · Shift {signed(label.shiftMean)} pts/game (80% range {signed(label.shiftLow)} to {signed(label.shiftHigh)}) · {Math.round(label.probability * 100)}% probability · last {label.recentGames} vs prior {label.baselineGames} games</p>
  </li>;
}
