// Auto-advancing carousels are a WCAG 2.2.2 hazard: anything that moves for
// more than five seconds needs a way to pause it. This module supplies that,
// and refuses to start at all when the user has asked for reduced motion --
// checked here rather than in CSS because a media query cannot stop a timer.
(() => {
    const reduceMotion = () =>
        window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    function setup(root) {
        if (root.dataset.carouselReady === "true") {
            return;
        }

        root.dataset.carouselReady = "true";

        const slides = [...root.querySelectorAll("[data-carousel-slide]")];
        const dots = [...root.querySelectorAll("[data-carousel-dot]")];
        const toggle = root.querySelector("[data-carousel-toggle]");
        if (slides.length < 2) {
            if (toggle) {
                toggle.hidden = true;
            }
            return;
        }

        const interval = Number(root.dataset.carouselInterval) || 6000;
        let index = slides.findIndex(slide => !slide.hidden);
        let paused = reduceMotion();
        let hovered = false;
        let timer = null;

        function show(next) {
            index = (next + slides.length) % slides.length;
            slides.forEach((slide, i) => {
                slide.hidden = i !== index;
            });
            dots.forEach((dot, i) => {
                dot.setAttribute("aria-current", String(i === index));
            });
        }

        function stop() {
            if (timer !== null) {
                clearInterval(timer);
                timer = null;
            }
        }

        function start() {
            stop();
            // Every reason to stay still is checked in one place: the user asked
            // for less motion, the user pressed pause, or the pointer or the
            // keyboard is currently inside the carousel.
            if (reduceMotion() || paused || hovered) {
                return;
            }

            timer = setInterval(() => show(index + 1), interval);
        }

        function setPaused(value) {
            paused = value;
            if (toggle) {
                toggle.setAttribute("aria-pressed", String(paused));
                const label = toggle.querySelector("[data-carousel-toggle-label]");
                if (label) {
                    label.textContent = paused ? "Play" : "Pause";
                }
            }

            start();
        }

        if (toggle) {
            toggle.hidden = false;
            toggle.addEventListener("click", () => setPaused(!paused));
        }

        dots.forEach((dot, i) => dot.addEventListener("click", () => {
            show(i);
            start();
        }));

        // Hover and focus both suspend it: a pointer resting on a slide and a
        // keyboard tabbing through its links are the same intent to read.
        root.addEventListener("mouseenter", () => { hovered = true; stop(); });
        root.addEventListener("mouseleave", () => { hovered = false; start(); });
        root.addEventListener("focusin", () => { hovered = true; stop(); });
        root.addEventListener("focusout", event => {
            if (!root.contains(event.relatedTarget)) {
                hovered = false;
                start();
            }
        });

        show(index < 0 ? 0 : index);
        setPaused(paused);
    }

    function scan() {
        document.querySelectorAll("[data-carousel]").forEach(setup);
    }

    document.addEventListener("DOMContentLoaded", scan);
    new MutationObserver(scan).observe(
        document.documentElement,
        { childList: true, subtree: true });
})();
