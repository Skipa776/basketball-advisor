import { useEffect, useRef, useState } from 'react';
import { useResource } from './useResource';
import type { LandingDay, LandingLine, LandingRisers } from './types';
import { PLAYER_PHOTOS, photoFor } from './playerPhotos';
import { ErrorNotice } from './Workspace';

const longDate = (iso: string) => new Date(`${iso}T12:00:00`).toLocaleDateString('en-US', { weekday: 'long', month: 'short', day: 'numeric', year: 'numeric' });
const shortDate = (iso: string) => new Date(`${iso}T12:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
const one = (value: number) => (Math.round(value * 10) / 10).toLocaleString('en-US');
const initials = (name: string) => name.split(/\s+/).map(part => part[0]).slice(0, 2).join('');

function PlayerCard({ player, day, copy }: { player: LandingLine; day: string; copy?: boolean }) {
  const photo = photoFor(player.name);
  const box = `${player.line.PTS}/${player.line.REB}/${player.line.AST}`;
  return <li className="player-card" aria-hidden={copy || undefined}>
    {player.playedOn !== day && <span className="pc-date">{shortDate(player.playedOn)}</span>}
    {photo ? <img src={`/img/players/${photo.file}`} alt="" loading="lazy" width="400" height="500" /> : <span className="player-initials" aria-hidden="true">{initials(player.name)}</span>}
    <p className="pc-name">{player.name}</p>
    <p className="pc-score"><span>CAT <b data-numeric>{player.categoriesWon}/9</b></span><span>PTS <b data-numeric>{one(player.fantasyPoints)}</b></span></p>
    <p className="pc-box" data-numeric aria-label={`${player.line.PTS} points, ${player.line.REB} rebounds, ${player.line.AST} assists`}>{box} <span>pts/reb/ast</span></p>
  </li>;
}

/**
 * Every featured star's latest game as of the previous game day, in one continuously
 * rotating row. The list is rendered twice (the copy hidden from assistive tech) and
 * slides by half its width, so the loop is seamless. Hover, focus or Pause stops it;
 * reduced motion turns it into a plain horizontal scroller.
 */
export function DailyStrip() {
  const day = useResource<LandingDay>('/api/public/daily');
  const [paused, setPaused] = useState(false);
  const data = day.result?.data;
  const players = data?.players ?? [];
  return <section className="strips daily" aria-labelledby="daily-title">
    <div className="section-intro">
      <p className="eyebrow">{data?.date ? `PREVIOUS GAME DAY · ${longDate(data.date).toUpperCase()}` : 'PREVIOUS GAME DAY'}</p>
      <h2 id="daily-title">How the stars played.</h2>
      <p className="muted">Each star’s latest game (dated if it wasn’t that day). CAT: categories won of 9 against everyone who played that day. PTS: {data?.scoring ?? 'ESPN default points'}.</p>
    </div>
    <ErrorNotice text={day.error} retry={day.refresh} />
    {day.loading && <p role="status">Loading the last game day…</p>}
    {data && !players.length && <p className="notice">No featured player has a recorded game{data.date ? ` on ${longDate(data.date)}` : ' yet'}.</p>}
    {data?.date && !!players.length && <>
      <div className={`marquee${paused ? ' paused' : ''}`} tabIndex={0} role="region" aria-label="Featured players">
        <ul className="marquee-track" style={{ '--count': players.length } as React.CSSProperties}>
          {players.map(player => <PlayerCard key={player.playerId} player={player} day={data.date!} />)}
          {players.map(player => <PlayerCard key={`copy-${player.playerId}`} player={player} day={data.date!} copy />)}
        </ul>
      </div>
      <button type="button" className="marquee-toggle" aria-pressed={paused} onClick={() => setPaused(!paused)}>{paused ? 'Play rotation' : 'Pause rotation'}</button>
    </>}
    {data?.date && <p className="muted source-note">2025–26 regular-season box scores from Basketball-Reference, {data.poolSize} players that day.</p>}
    <a className="round magnetic" href="#join">Start drafting</a>
  </section>;
}

/** Waiver risers; each row rises into place as it scrolls up into view, and again on the way back down. */
export function RisersTable() {
  const risers = useResource<LandingRisers>('/api/public/risers?limit=15');
  const body = useRef<HTMLTableSectionElement>(null);
  const data = risers.result?.data;
  useEffect(() => {
    const rows = [...(body.current?.querySelectorAll<HTMLElement>('tr') ?? [])];
    if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) { rows.forEach(row => row.classList.add('in')); return; }
    const observer = new IntersectionObserver(entries => entries.forEach(entry => {
      if (entry.isIntersecting) entry.target.classList.add('in');
      else if (entry.boundingClientRect.top > 0) entry.target.classList.remove('in');
    }), { threshold: 0.1 });
    rows.forEach(row => observer.observe(row));
    return () => observer.disconnect();
  }, [data]);
  return <section className="risers" aria-labelledby="risers-title">
    <div className="section-intro">
      <p className="eyebrow">{data?.throughDate ? `ON THE WIRE · THROUGH ${shortDate(data.throughDate).toUpperCase()}` : 'ON THE WIRE'}</p>
      <h2 id="risers-title">Rising right now.</h2>
      <p className="muted">Last three games against each player’s own earlier games, in {data?.scoring ?? 'ESPN default points'}. The stars above are left out.</p>
    </div>
    <ErrorNotice text={risers.error} retry={risers.refresh} />
    {risers.loading && <p role="status">Loading risers…</p>}
    {data && !data.players.length && <p className="notice">Not enough recorded games to compare anyone yet.</p>}
    {!!data?.players.length && <div className="table-scroll" tabIndex={0} role="region" aria-label="Risers table"><table className="risers-table">
      <caption className="sr-only">Players rising above their own baseline, through {data.throughDate}</caption>
      <thead><tr><th scope="col">Player</th><th scope="col">CAT</th><th scope="col">Pts · last 3</th><th scope="col">Streak</th><th scope="col">vs baseline</th><th scope="col">Status</th></tr></thead>
      <tbody ref={body}>{data.players.map((player, index) => <tr key={player.playerId} style={{ '--i': index } as React.CSSProperties}>
        <th scope="row">{player.name}</th>
        <td data-numeric>{player.categoriesWon}/9</td>
        <td data-numeric>{one(player.recentAverage)} <span className="muted">from {one(player.baselineAverage)}</span></td>
        <td data-numeric>{player.streak}</td>
        <td data-numeric>+{one(player.percentAboveBaseline)}%</td>
        <td><span className={`status status-${player.status.toLowerCase().replace(/\s+/g, '-')}`}>{player.status}</span></td>
      </tr>)}</tbody>
    </table></div>}
  </section>;
}

export function PhotoCredits() {
  return <details className="photo-credits" id="photo-credits">
    <summary>Player photo credits</summary>
    <ul>{PLAYER_PHOTOS.map(photo => <li key={photo.file}>{photo.name}: <a href={photo.source}>{photo.author}</a>, {photo.license}, via Wikimedia Commons</li>)}</ul>
  </details>;
}
