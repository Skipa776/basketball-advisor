import { useEffect, useRef } from 'react';
import type { ReactNode } from 'react';
import { DailyStrip, PhotoCredits, RisersTable } from './landingData';

// Signed-out landing, after the owner's portfolio (Skipa776/portfolio, forked from
// bettinasosa/portfolio): falling headline letters, word slide-up, two scroll-drifting
// rows of player cards, rising table rows, magnetic round buttons, and a dark curved
// closing section. Plain CSS + one scroll listener; still under prefers-reduced-motion.
const HEADLINE = ['See the', 'court.', 'make your move.'];
const PITCH = 'League-aware fantasy basketball. Your scoring, your roster, your pick.';

const clamp = (value: number) => Math.min(1, Math.max(0, value));

export function Landing({ children }: { children: ReactNode }) {
  const root = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const page = root.current!;
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const pitch = page.querySelector('.pitch')!;
    const reveal = new IntersectionObserver(([entry]) => pitch.classList.toggle('in-view', entry.isIntersecting), { threshold: 0.25 });
    reveal.observe(pitch);
    if (reduced) { pitch.classList.add('in-view'); reveal.disconnect(); return; }

    const letters = [...page.querySelectorAll<HTMLElement>('.letter')].map(element => ({ element, speed: 0.8 + Math.random() * 0.7, spin: Math.random() * 60 - 30 }));
    const strips = page.querySelector<HTMLElement>('.strips')!;
    const curve = page.querySelector<HTMLElement>('.curve')!;
    const hint = page.querySelector<HTMLElement>('.scroll-hint')!;
    let frame = 0;
    const update = () => {
      frame = 0;
      const fall = clamp(scrollY / innerHeight);
      for (const { element, speed, spin } of letters) element.style.transform = `translateY(${(1 - speed) * fall * innerHeight * 1.2}px) rotate(${spin * fall}deg)`;
      const box = strips.getBoundingClientRect();
      const drift = clamp((innerHeight - box.top) / (innerHeight + box.height)) * 150;
      strips.style.setProperty('--drift', `${drift}px`);
      curve.style.height = `${clamp(curve.getBoundingClientRect().top / innerHeight) * 60}px`;
      hint.hidden = scrollY > 0;
    };
    const onScroll = () => { frame ||= requestAnimationFrame(update); };
    update();
    addEventListener('scroll', onScroll, { passive: true });

    // Magnetic buttons: fine pointers only.
    const fine = window.matchMedia('(pointer: fine)').matches;
    const magnets = fine ? [...page.querySelectorAll<HTMLElement>('.magnetic')] : [];
    const pull = (event: PointerEvent) => {
      const target = event.currentTarget as HTMLElement, box = target.getBoundingClientRect();
      target.style.transform = `translate(${(event.clientX - box.left - box.width / 2) * 0.3}px, ${(event.clientY - box.top - box.height / 2) * 0.3}px)`;
    };
    const release = (event: PointerEvent) => { (event.currentTarget as HTMLElement).style.transform = ''; };
    magnets.forEach(magnet => { magnet.addEventListener('pointermove', pull); magnet.addEventListener('pointerleave', release); });
    return () => {
      reveal.disconnect(); removeEventListener('scroll', onScroll); cancelAnimationFrame(frame);
      magnets.forEach(magnet => { magnet.removeEventListener('pointermove', pull); magnet.removeEventListener('pointerleave', release); });
    };
  }, []);

  return <div className="landing-page" ref={root}>
    <section className="letters" aria-labelledby="headline">
      <h1 id="headline" aria-label={HEADLINE.join(' ')}>{HEADLINE.map(line => <span className="line" key={line} aria-hidden="true">{[...line].map((character, index) => <span className="letter" key={index}>{character === ' ' ? ' ' : character}</span>)}</span>)}</h1>
    </section>
    <a className="scroll-hint" href="#pitch">Scroll <span aria-hidden="true">↘</span></a>

    <section className="pitch" id="pitch" aria-label="What Fastbreak does">
      <p className="pitch-lead">{PITCH.split(' ').map((word, index) => <span className="word" key={index}><span style={{ transitionDelay: `${index * 15}ms` }}>{word}</span></span>)}</p>
      <div className="pitch-side">
        <p>Projections tuned to your rules. Recent form checked against a real baseline.</p>
        <a className="round round-red magnetic" href="#join">Get started</a>
      </div>
    </section>

    <DailyStrip />
    <RisersTable />
    <div className="curve" aria-hidden="true"><div /></div>

    <section className="join" id="join" aria-label="Sign in">
      <div className="join-head"><img src="/img/ball-through-hoop.jpg" alt="" width="900" height="600" /><h2>Let’s win your league.</h2></div>
      <div className="join-body"><p>Free. Private. Built around your scoring.</p>{children}</div>
      <dl className="join-meta">
        <div><dt>Version</dt><dd>2026 © Edition</dd></div>
        <div><dt>Data</dt><dd>Owner managed sources</dd></div>
        <div><dt>Photos</dt><dd>Wikimedia Commons contributors · <a href="#photo-credits">credits</a></dd></div>
      </dl>
      <PhotoCredits />
    </section>
  </div>;
}
