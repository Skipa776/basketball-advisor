# Landing, signed-in hub, and last-season box-score test — plan

Owner request 2026-09-23. Executed phase by phase; **each phase ends with its
tests passing and one conventional commit**. Never commit `CLAUDE.md` (it is
`skip-worktree`, owner-local).

## Owner decisions (2026-09-23)

| Question | Decision |
|---|---|
| Box-score source | Basketball-Reference **game pages** `/boxscores/{yyyyMMdd}0{TEAM}.html`, owner-authorized. Schedule (which games, home team) comes from balldontlie `/v1/games` (free tier, verified 200). balldontlie `/v1/stats` is 401 on the free tier — that is the recorded blocker. |
| Test window | Random season day **2025-11-16**; test month **2025-11-16 → 2025-12-16** (2025-26 season). |
| Thresholds | For this test only, ignore minimum-history thresholds (e.g. 10-game baseline) via an explicit, default-off setting. Production defaults unchanged. |
| CAT figure | **Categories won**: of the 9 default cats (PTS, REB, AST, STL, BLK, 3PM, FG%, FT%, TO — TO lower is better), how many the player beat that day's pool average in. Shown `CAT 7/9`. |
| Points figure | ESPN default points profile already in the app (`/api/leagues/setup` points profile). |
| Waiver % | **% above own baseline** from the existing heat calculator; streak = consecutive recent appearances above baseline. |
| Trade / Matchup analyzers | "Coming soon" tiles → honest not-built page naming the requirement (R15 for trade; matchup is unspecified). |
| Frontend | React 19 + TypeScript + Vite. Node is build/dev only; ASP.NET Core serves the bundle and the API. |

## Phases

0. **Commit pending WIP.** Run `bash scripts/gate.sh`; if green commit the
   existing working-tree changes (projected-player service, live blank-page
   repair) as their own commit. If red, stop and report.
1. **Portfolio style reference.** `docs/design/portfolio-style-reference.html`
   (single self-contained HTML/CSS/JS) cataloguing buttons, fields, scroll
   animations, magnetic/rounded buttons, preloader, text reveal, parallax,
   gallery, carousel, modal, contact form, curve section, colors, type.
   Credit: original **bettinasosa/portfolio** by Bettina Sosa
   (bettinasosa.com), MIT © 2023 m6v3l9; forked as **Skipa776/portfolio**.
   Re-implemented in plain CSS/JS (no GSAP/framer), nothing copied beyond MIT
   terms. Commit `docs:`.
2. **Box-score import.** `BbrefUrlBuilder` allows exactly
   `^/boxscores/\d{8}0[A-Z]{3}\.html$`; scraping-policy table gains that row
   with "owner-authorized 2026-09-23"; balldontlie→bbref team-code map (BKN→BRK,
   CHA→CHO, PHX→PHO, others equal); an owner-only import command/endpoint for a
   date range that reads the balldontlie schedule, fetches each final game page
   through the existing rate limiter, parses with `BoxScoreParser`, stores via
   `IBoxScoreRepository`, is resumable (skips stored games) and reports progress.
   Offline tests with fixtures (URL allow/deny, team map, skip-stored). Commit.
   Then run it live for the test window (background, policy rate) and record
   counts in `implementation-progress.md`.
3. **Remove Blazor.** Delete `Components/`, Razor/interactive-server
   registration, Blazor-only `wwwroot` scripts; `/` serves the React app (or
   redirects to `/app`). Update/retire tests and OKF concepts that reference
   Blazor in the same commit. Gate green. Commit `refactor:`.
4. **Public landing data API** (anonymous, read-only, no-store, rate-limited):
   `GET /api/public/daily?date=YYYY-MM-DD` → featured players' lines for that
   date with ESPN-default points and CAT categories-won; `GET
   /api/public/risers?through=YYYY-MM-DD` → players not in the featured list,
   ranked by heat under default points: name, CAT, points, streak,
   % above baseline, status (`Must add` / `Add` / `Hold` / `Watch` — thresholds
   recorded in `assumptions.md`). Default date is the latest stored date.
   Unit + HTTP tests. Commit `feat:`.
5. **Landing page.** Keep the falling-letter "See the court." hero and join
   section. Replace the two photo strips with a strip of small rectangular
   player headshot cards (30 current top fantasy players): photo, name, and
   `CAT 7/9 · PTS 65.5` for the previous day. Below, the risers table whose
   rows rise into place as it scrolls into view (reduced-motion: static).
   Headshots only from Wikimedia Commons with a free license verified via the
   Commons API; stored in `wwwroot/img/players/`, each credited (author,
   license, source URL) in `ASSETS.md` and a visible credits line; players
   without a free image use `resources/nophotoforplayer.jpg`. Playwright +
   axe pass. Commit.
6. **Signed-in hub.** After sign-in, a plain white page, same type as the
   landing, oval buttons asking "What do you want to see?": Mock draft,
   Teams in the league, Waiver wire analyzer, Projected players, Trade
   analyzer (soon), Matchup analyzer (soon), Context review, Account data,
   Data sources (owner). Layout borrows the portfolio *projects* list (rows
   with hover preview). Existing feature pages stay reachable. Playwright +
   axe pass. Commit.
7. **End-to-end run.** Start the app against the imported window with the
   threshold override on and "today" = 2025-11-17; screenshot landing, hub,
   waiver view; record the dates exercised and results in
   `implementation-progress.md`. Commit `docs:`.
