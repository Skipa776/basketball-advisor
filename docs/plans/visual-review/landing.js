(() => {
  'use strict';
  const $ = selector => document.querySelector(selector);
  const media = matchMedia('(prefers-reduced-motion: reduce)');
  const motionButton = $('#motion');
  let manualReduced = false;
  let reduced = media.matches;
  const springs = [];
  const gestures = [];
  const effects = new Set();
  const clamp = (x, a, b) => Math.max(a, Math.min(b, x));
  const mod = (x, n) => ((x % n) + n) % n;

  // A bounded, interruptible spring. Frames run only while an object is moving.
  function spring(render, stiffness = 180, damping = 24) {
    let value = 0, velocity = 0, target = 0, frame = 0, previousTime = 0;
    const stop = () => { cancelAnimationFrame(frame); frame = 0; previousTime = 0; };
    const step = time => {
      const dt = Math.min((time - (previousTime || time - 16)) / 1000, .025);
      previousTime = time;
      velocity += ((target - value) * stiffness - velocity * damping) * dt;
      value += velocity * dt;
      render(value);
      if (Math.abs(target - value) < .015 && Math.abs(velocity) < .03) {
        value = target; velocity = 0; render(value); stop();
      } else frame = requestAnimationFrame(step);
    };
    const api = {
      get value() { return value; },
      stop,
      set(next, speed = 0) { stop(); value = next; velocity = speed; render(value); },
      to(next) {
        target = next;
        if (reduced || document.hidden) { stop(); value = target; velocity = 0; render(value); }
        else if (!frame) frame = requestAnimationFrame(step);
      },
      settle() { stop(); value = target; velocity = 0; render(value); }
    };
    springs.push(api);
    return api;
  }

  function effect(el, keyframes, options) {
    if (reduced || document.hidden) return;
    // Repeated taps replace only this object's animation; unrelated effects continue.
    for (const animation of el.getAnimations()) animation.cancel();
    const animation = el.animate(keyframes, options);
    effects.add(animation);
    animation.finished.catch(() => {}).finally(() => effects.delete(animation));
  }

  const wheel = $('#wheel');
  const panels = [...document.querySelectorAll('.wheel-panel')];
  const captions = ['Know your league.', 'See the context.', 'Make your move.'];
  let currentScene = 0;
  function renderWheel(angle) {
    $('.wheel-rotor').style.transform = `rotate(${-angle}deg)`;
    const visible = mod(Math.round(angle / 120), 3);
    for (let i = 0; i < panels.length; i++) panels[i].hidden = i !== visible;
    wheel.dataset.angle = angle.toFixed(2);
  }
  const wheelSpring = spring(renderWheel);
  function selectPerspective(step) {
    currentScene = mod(step, 3);
    $('#scene-count').textContent = `0${currentScene + 1} / 03`;
    $('#scene-caption').textContent = captions[currentScene];
    wheel.dataset.scene = String(currentScene);
    wheelSpring.to(step * 120);
  }
  function advance(direction) { selectPerspective(Math.round(wheelSpring.value / 120) + direction); }
  $('#previous').addEventListener('click', () => advance(-1));
  $('#next').addEventListener('click', () => advance(1));
  wheel.addEventListener('keydown', event => {
    if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
      event.preventDefault(); advance(event.key === 'ArrowRight' ? 1 : -1);
    }
  });

  // Horizontal gestures leave vertical touch scrolling to the browser.
  function drag(el, motion, scale, limits, release) {
    let pointer = null, lastX = 0, lastTime = 0, speed = 0, moved = 0;
    function finish(cancelled = false) {
      if (pointer === null) return;
      const id = pointer;
      pointer = null;
      if (el.hasPointerCapture(id)) el.releasePointerCapture(id);
      if (performance.now() - lastTime > 100) speed = 0;
      release(cancelled ? 0 : speed, moved, cancelled);
    }
    el.addEventListener('pointerdown', event => {
      if (event.button !== 0 || pointer !== null) return;
      pointer = event.pointerId;
      lastX = event.clientX; lastTime = performance.now(); speed = 0; moved = 0;
      motion.stop();
      el.setPointerCapture(pointer);
    });
    el.addEventListener('pointermove', event => {
      if (event.pointerId !== pointer) return;
      const now = performance.now();
      const delta = event.clientX - lastX;
      moved += Math.abs(delta);
      speed = clamp(delta * scale / Math.max((now - lastTime) / 1000, .008), -600, 600);
      if (!reduced) motion.set(clamp(motion.value + delta * scale, ...limits), speed);
      lastX = event.clientX; lastTime = now;
    });
    el.addEventListener('pointerup', () => finish());
    el.addEventListener('pointercancel', () => finish(true));
    el.addEventListener('lostpointercapture', () => finish(true));
    gestures.push(() => finish(true));
  }
  drag(wheel, wheelSpring, -.48, [-1e6, 1e6], (speed, moved, cancelled) => {
    const projected = wheelSpring.value + (cancelled ? 0 : clamp(speed * .15, -70, 70));
    selectPerspective(Math.round(projected / 120));
  });

  const sign = $('#sign');
  const signSpring = spring(value => {
    sign.style.transform = `rotate(${value}deg)`;
    sign.dataset.angle = value.toFixed(2);
  }, 90, 11);
  let suppressSignClick = false;
  drag(sign, signSpring, .12, [-18, 18], (speed, moved) => {
    suppressSignClick = moved > 5;
    signSpring.to(0);
  });
  function nudge(direction) {
    if (!reduced) signSpring.set(signSpring.value, direction * 100);
    signSpring.to(0);
  }
  sign.addEventListener('click', () => {
    if (!suppressSignClick) nudge(1);
    suppressSignClick = false;
  });
  sign.addEventListener('keydown', event => {
    if (event.key === 'ArrowRight' || event.key === 'ArrowLeft') {
      event.preventDefault(); nudge(event.key === 'ArrowRight' ? 1 : -1);
    }
  });
  $('#bounce').addEventListener('click', () => effect($('#bounce'), [
    {transform:'translateY(0) rotate(0deg)'},
    {transform:'translateY(-75px) rotate(-40deg)',offset:.32},
    {transform:'translateY(0) rotate(0deg)',offset:.7},
    {transform:'translateY(-15px) rotate(8deg)',offset:.84},
    {transform:'translateY(0) rotate(0deg)'}
  ], {duration:760,easing:'cubic-bezier(.25,.6,.4,1)'}));

  let sticker = 0;
  const stickerWords = ['League matters.', 'Your angle.', 'See the court.'];
  $('#sticker').addEventListener('click', () => {
    sticker = (sticker + 1) % stickerWords.length;
    $('#sticker > span').textContent = stickerWords[sticker];
    $('#sticker').setAttribute('aria-label', `Change the court sticker. Current sticker: ${stickerWords[sticker]}`);
    effect($('#sticker'), [
      {transform:'rotate(-12deg) scale(.88)'},
      {transform:'rotate(-5deg) scale(1.06)',offset:.6},
      {transform:'rotate(-12deg) scale(1)'}
    ], {duration:380,easing:'ease-out'});
  });

  const parallax = [...document.querySelectorAll('[data-parallax]')];
  const lines = [...document.querySelectorAll('.court-lines *')];
  const board = $('.court-board');
  const spread = $('.photo-spread');
  let scrollFrame = 0;
  function updateScroll() {
    scrollFrame = 0;
    const height = innerHeight;
    const progress = scrollY / Math.max(1, document.documentElement.scrollHeight - height);
    $('.page-progress').style.transform = `scaleX(${clamp(progress, 0, 1)})`;
    if (reduced) return;
    // Measure first, then write; no frame loop when the page is idle.
    const positions = parallax.map(el => el.getBoundingClientRect());
    const court = board.getBoundingClientRect();
    const photoBounds = spread.getBoundingClientRect();
    parallax.forEach((el, i) => {
      const fraction = clamp((height - positions[i].top) / (height + positions[i].height), 0, 1) - .5;
      el.style.setProperty('--offset', `${fraction * Number(el.dataset.parallax) * 2}px`);
    });
    const draw = clamp((height - court.top) / Math.min(court.height, height), 0, 1);
    for (const line of lines) { line.style.strokeDasharray = '1'; line.style.strokeDashoffset = String(1 - draw); }
    $('.spread-star').style.setProperty('--turn', `${clamp((height - photoBounds.top) / (height + photoBounds.height),0,1) * 100}deg`);
    $('.ribbon > div').style.transform = `translateX(${-Math.min(scrollY * .055, 200)}px)`;
  }
  function scheduleScroll() { if (!scrollFrame && !document.hidden) scrollFrame = requestAnimationFrame(updateScroll); }
  addEventListener('scroll', scheduleScroll, {passive:true});
  addEventListener('resize', scheduleScroll);
  const observer = new IntersectionObserver(entries => {
    for (const entry of entries) if (entry.isIntersecting) {
      effect(entry.target, [{opacity:.55,transform:'translateY(24px)'},{opacity:1,transform:'translateY(0)'}], {duration:550,easing:'cubic-bezier(.2,.8,.2,1)'});
      observer.unobserve(entry.target);
    }
  }, {threshold:.15});
  document.querySelectorAll('[data-reveal]').forEach(el => observer.observe(el));

  function settleAll() {
    gestures.forEach(cancel => cancel());
    springs.forEach(item => item.settle());
    effects.forEach(animation => animation.cancel());
    cancelAnimationFrame(scrollFrame); scrollFrame = 0;
  }
  function updateMotion() {
    reduced = manualReduced || media.matches;
    document.documentElement.classList.toggle('still', reduced);
    motionButton.setAttribute('aria-pressed', String(reduced));
    motionButton.textContent = media.matches ? 'Reduced motion · system' : reduced ? 'Enable motion' : 'Reduce motion';
    motionButton.disabled = media.matches;
    if (reduced) {
      settleAll();
      $('.ribbon > div').style.transform = 'none';
    }
    scheduleScroll();
  }
  motionButton.addEventListener('click', () => { manualReduced = !manualReduced; updateMotion(); });
  media.addEventListener('change', updateMotion);
  document.addEventListener('visibilitychange', () => { if (document.hidden) settleAll(); else scheduleScroll(); });
  addEventListener('blur', () => gestures.forEach(cancel => cancel()));
  addEventListener('pagehide', settleAll);
  document.querySelectorAll('a[href^="#"]').forEach(link => link.addEventListener('click', () => {
    const target = $(link.getAttribute('href'));
    if (target) { target.setAttribute('tabindex','-1'); target.focus({preventScroll:true}); }
  }));
  document.querySelectorAll('img').forEach(img => img.addEventListener('error', () => { img.style.visibility = 'hidden'; }));
  selectPerspective(0);
  updateMotion();
})();
