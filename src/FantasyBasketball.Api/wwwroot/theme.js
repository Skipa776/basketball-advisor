(() => {
    const storageKey = "fantasy-basketball-theme";
    const themes = ["system", "dark", "light"];

    function apply(theme) {
        const root = document.querySelector("[data-theme-root]");
        if (!root) {
            return;
        }

        if (theme === "system") {
            root.removeAttribute("data-theme");
        } else {
            root.dataset.theme = theme;
        }

        document.querySelectorAll("[data-theme-toggle]").forEach(button => {
            const label = button.querySelector("[data-theme-label]");
            const icon = button.querySelector("[data-theme-icon]");
            const display = theme[0].toUpperCase() + theme.slice(1);
            button.setAttribute("aria-label", `Theme: ${display.toLowerCase()}`);
            if (label) {
                label.textContent = display;
            }

            if (icon) {
                icon.textContent = theme === "dark"
                    ? "●"
                    : theme === "light"
                        ? "○"
                        : "◐";
            }
        });
    }

    function current() {
        const stored = localStorage.getItem(storageKey);
        return themes.includes(stored) ? stored : "system";
    }

    document.addEventListener("click", event => {
        const button = event.target.closest("[data-theme-toggle]");
        if (!button) {
            return;
        }

        const theme = current();
        const next = themes[(themes.indexOf(theme) + 1) % themes.length];
        localStorage.setItem(storageKey, next);
        apply(next);
    });

    document.addEventListener("DOMContentLoaded", () => apply(current()));
    new MutationObserver(() => apply(current())).observe(
        document.documentElement,
        { childList: true, subtree: true });
})();
