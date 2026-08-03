import { spawn } from "node:child_process";
import { once } from "node:events";
import {
    existsSync,
    mkdtempSync,
    readFileSync,
    rmSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const chromeCandidates = [
    process.env.CHROME_PATH,
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    "/usr/bin/google-chrome",
    "/usr/bin/google-chrome-stable",
    "/usr/bin/chromium",
    "/usr/bin/chromium-browser",
].filter(Boolean);
const chromePath = chromeCandidates.find(existsSync);
if (!chromePath) {
    throw new Error(
        "D-14 requires a Chromium browser. Set CHROME_PATH to Chrome or Chromium.",
    );
}

const tokenCss = readFileSync(
    "src/FantasyBasketball.Api/Components/Design/Tokens.razor.css",
    "utf8",
);
const rowHeightMatch = tokenCss.match(/--board-row-height:\s*(\d+(?:\.\d+)?)px/);
if (!rowHeightMatch) {
    throw new Error("The canonical --board-row-height token is missing.");
}

const rowHeight = Number(rowHeightMatch[1]);
const boardScript = readFileSync(
    "src/FantasyBasketball.Api/wwwroot/draft-board.js",
    "utf8",
);
const profileDirectory = mkdtempSync(join(tmpdir(), "fantasy-ui-gate-"));
const browser = spawn(
    chromePath,
    [
        "--headless=new",
        "--disable-gpu",
        "--no-first-run",
        "--no-default-browser-check",
        "--remote-debugging-pipe",
        `--user-data-dir=${profileDirectory}`,
        "about:blank",
    ],
    {
        stdio: ["ignore", "ignore", "pipe", "pipe", "pipe"],
    },
);

let nextId = 1;
let incoming = Buffer.alloc(0);
const pending = new Map();
browser.stdio[4].on("data", chunk => {
    incoming = Buffer.concat([incoming, chunk]);
    let separator = incoming.indexOf(0);
    while (separator >= 0) {
        const payload = incoming.subarray(0, separator).toString("utf8");
        incoming = incoming.subarray(separator + 1);
        if (payload) {
            const message = JSON.parse(payload);
            if (message.id && pending.has(message.id)) {
                const waiter = pending.get(message.id);
                pending.delete(message.id);
                if (message.error) {
                    waiter.reject(new Error(message.error.message));
                } else {
                    waiter.resolve(message.result);
                }
            }
        }

        separator = incoming.indexOf(0);
    }
});

function command(method, params = {}, sessionId = undefined) {
    const id = nextId++;
    const message = { id, method, params };
    if (sessionId) {
        message.sessionId = sessionId;
    }

    return new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
            pending.delete(id);
            const detail = method === "Runtime.evaluate"
                ? ` (${String(params.expression).replaceAll(/\s+/g, " ").slice(0, 120)})`
                : "";
            reject(new Error(
                `Chrome DevTools command timed out: ${method}${detail}`,
            ));
        }, 10000);
        pending.set(id, {
            resolve(value) {
                clearTimeout(timeout);
                resolve(value);
            },
            reject(error) {
                clearTimeout(timeout);
                reject(error);
            },
        });
        browser.stdio[3].write(`${JSON.stringify(message)}\0`);
    });
}

async function evaluate(expression, sessionId) {
    const response = await command(
        "Runtime.evaluate",
        {
            expression,
            awaitPromise: true,
            returnByValue: true,
        },
        sessionId,
    );
    if (response.exceptionDetails) {
        throw new Error(
            response.exceptionDetails.exception?.description
            ?? response.exceptionDetails.text,
        );
    }

    return response.result.value;
}

async function waitFor(expression, sessionId, timeoutMilliseconds = 15000) {
    const deadline = Date.now() + timeoutMilliseconds;
    while (Date.now() < deadline) {
        if (await evaluate(expression, sessionId)) {
            return;
        }

        await new Promise(resolve => setTimeout(resolve, 100));
    }

    throw new Error(`Browser condition timed out: ${expression}`);
}

async function pressKey(sessionId, key, code, text = undefined) {
    await command(
        "Input.dispatchKeyEvent",
        {
            type: "keyDown",
            key,
            code,
            text,
        },
        sessionId,
    );
    await command(
        "Input.dispatchKeyEvent",
        {
            type: "keyUp",
            key,
            code,
        },
        sessionId,
    );
}

try {
    const targets = await command("Target.getTargets");
    const pageTarget = targets.targetInfos.find(target => target.type === "page");
    if (!pageTarget) {
        throw new Error("Chrome did not expose a page target.");
    }

    const attached = await command("Target.attachToTarget", {
        targetId: pageTarget.targetId,
        flatten: true,
    });
    const sessionId = attached.sessionId;
    await command("Runtime.enable", {}, sessionId);
    await command("Page.enable", {}, sessionId);
    await evaluate(boardScript, sessionId);

    const measurement = await evaluate(
        `(async () => {
            const rowHeight = ${JSON.stringify(rowHeight)};
            const playerIds = Array.from(
                { length: 50 },
                (_, index) => "player-" + index,
            );
            const rows = ids => ids.map(id =>
                '<div data-player-id="' + id + '">' + id + '</div>'
            ).join("");
            document.body.innerHTML =
                '<style>' +
                '* { box-sizing: border-box; }' +
                '[data-player-table] { height: ' + (rowHeight * 5) +
                'px; overflow: auto; }' +
                '[data-player-id] { height: ' + rowHeight +
                'px; line-height: ' + rowHeight + 'px; }' +
                '</style>' +
                '<section data-draft-board="live-draft-board">' +
                '<div data-player-table>' + rows(playerIds) + '</div>' +
                '</section>';

            const viewport = document.querySelector("[data-player-table]");
            viewport.scrollTop = rowHeight * 10;
            await new Promise(resolve =>
                requestAnimationFrame(() => requestAnimationFrame(resolve)));
            const before = window.draftBoard.capture("live-draft-board");

            const afterPick = playerIds.filter(id => id !== "player-2");
            viewport.innerHTML = rows(afterPick);
            window.draftBoard.restore("live-draft-board", before);
            await new Promise(resolve =>
                requestAnimationFrame(() => requestAnimationFrame(resolve)));
            const after = window.draftBoard.capture("live-draft-board");
            const afterById = new Map(
                after.rows.map(row => [row.playerId, row.offset]),
            );
            const survivingOffsets = before.rows
                .filter(row => afterById.has(row.playerId))
                .map(row => Math.abs(
                    afterById.get(row.playerId) - row.offset,
                ));
            return {
                beforeScrollTop: before.scrollTop,
                afterScrollTop: after.scrollTop,
                measuredRows: survivingOffsets.length,
                maximumOffsetDelta: Math.max(0, ...survivingOffsets),
            };
        })()`,
        sessionId,
    );

    if (measurement.measuredRows === 0) {
        throw new Error("D-14 measured no surviving visible rows.");
    }

    if (measurement.maximumOffsetDelta > 0.01) {
        throw new Error(
            `D-14 layout shift: ${measurement.maximumOffsetDelta} CSS px`,
        );
    }

    process.stdout.write(
        "D-14 browser measurement: "
        + `${measurement.measuredRows} visible rows, `
        + `${measurement.maximumOffsetDelta.toFixed(3)} CSS px maximum offset delta\n`,
    );

    const baseUrl = process.env.UI_BASE_URL;
    const leagueId = process.env.UI_LEAGUE_ID;

    // D-30: no horizontal overflow at 390 CSS px. Needs a laid-out page, which
    // is why it lives here rather than in the AngleSharp suite -- that never
    // runs layout at all.
    //
    // The workspace routes are the ones that can actually break it: the public
    // pages are one column by construction, while the instrument is built from
    // two-column grids that have to collapse. Checking only the front door was
    // checking the half that was never at risk.
    //
    // Decorative backdrops are allowed past the viewport by name: CourtBackdrop
    // is a full-bleed SVG and its figures are meant to bleed. Content and
    // controls are not, and the document itself must never scroll sideways.
    if (baseUrl) {
        const narrowRoutes = [
            "/",
            "/account/login",
            "/account/register",
            "/draft",
            "/players",
            "/league",
            "/leagues",
            "/trade",
            "/free-agents",
            "/leaderboard",
            "/context-review",
            "/data-sources",
            "/welcome",
        ];
        for (const route of narrowRoutes) {
            await command(
                "Emulation.setDeviceMetricsOverride",
                { width: 390, height: 844, deviceScaleFactor: 1, mobile: true },
                sessionId,
            );
            await command(
                "Page.navigate",
                { url: new URL(route, baseUrl).toString() },
                sessionId,
            );
            await waitFor("document.readyState === 'complete'", sessionId);
            await new Promise(resolve => setTimeout(resolve, 750));

            const overflow = await evaluate(
                `(() => {
                    const viewport = document.documentElement.clientWidth;
                    const decorative = new Set(["svg", "g", "path", "circle", "line", "rect", "polygon"]);
                    const offenders = [];
                    for (const element of document.querySelectorAll("*")) {
                        if (decorative.has(element.tagName.toLowerCase())) continue;
                        if (element.closest("[data-decorative], .court-backdrop")) continue;
                        const box = element.getBoundingClientRect();
                        if (box.width === 0 && box.height === 0) continue;
                        if (box.right > viewport + 1 || box.left < -1) {
                            offenders.push(
                                element.tagName.toLowerCase()
                                + "." + (element.className || "").toString().trim().split(/\\s+/)[0]
                                + " right=" + Math.round(box.right),
                            );
                        }
                    }
                    // An element can be inside the viewport and still push the
                    // document wide, when something it clips or scrolls is
                    // wider than it is. The rect check above cannot see that,
                    // so overflowing containers are collected separately.
                    const overflowing = [];
                    for (const element of document.querySelectorAll("*")) {
                        if (decorative.has(element.tagName.toLowerCase())) continue;
                        if (element.scrollWidth > element.clientWidth + 1
                            && element.clientWidth > 0) {
                            overflowing.push(
                                element.tagName.toLowerCase()
                                + "." + (element.className || "").toString().trim().split(/\\s+/)[0]
                                + " scroll=" + element.scrollWidth
                                + "/" + element.clientWidth,
                            );
                        }
                    }
                    return JSON.stringify({
                        viewport,
                        documentScrollWidth: document.documentElement.scrollWidth,
                        offenders: offenders.slice(0, 8),
                        overflowing: overflowing.slice(0, 8),
                    });
                })()`,
                sessionId,
            );

            const result = JSON.parse(overflow);
            if (result.documentScrollWidth > result.viewport + 1) {
                throw new Error(
                    `D-30 ${route}: the document scrolls sideways at 390 CSS px `
                    + `(scrollWidth ${result.documentScrollWidth} > viewport ${result.viewport}). `
                    // Naming what is over the line, not just that something is:
                    // the width alone leaves whoever reads this walking the DOM
                    // by hand to find it.
                    + `Past the viewport: ${result.offenders.join(", ") || "nothing"}. `
                    + `Overflowing its own box: ${result.overflowing.join(", ") || "nothing"}`,
                );
            }
            if (result.offenders.length > 0) {
                throw new Error(
                    `D-30 ${route}: content past the viewport at 390 CSS px: `
                    + result.offenders.join(", "),
                );
            }
            process.stdout.write(
                `D-30 ${route}: no horizontal overflow at 390 CSS px\n`,
            );
        }

        await command("Emulation.clearDeviceMetricsOverride", {}, sessionId);
    }

    if (baseUrl && leagueId) {
        await command(
            "Page.navigate",
            { url: new URL("/draft", baseUrl).toString() },
            sessionId,
        );
        await waitFor(
            "document.querySelector('#draft-league') !== null",
            sessionId,
        );
        await new Promise(resolve => setTimeout(resolve, 2000));
        await evaluate(
            "document.querySelector('#draft-league').focus()",
            sessionId,
        );
        await command(
            "Input.insertText",
            { text: leagueId },
            sessionId,
        );
        await evaluate(
            "document.querySelector('#draft-slot').focus()",
            sessionId,
        );
        await new Promise(resolve => setTimeout(resolve, 500));
        // Scoped to the draft form. `form button[type=submit]` matched the
        // league switcher in the utility bar instead -- that form sits above
        // <main> in the DOM, so it wins document order. Clicking it posted a
        // cookie change and redirected back to /draft, which looks identical to
        // a draft that never started.
        await evaluate(
            "document.querySelector('main .setup-form button[type=submit]').click()",
            sessionId,
        );
        await new Promise(resolve => setTimeout(resolve, 2000));
        const startState = await evaluate(
            `(() => ({
                url: location.href,
                error: document.querySelector(".error-state")?.textContent.trim(),
                leagueValue: document.querySelector("#draft-league")?.value,
                heading: document.querySelector("h1")?.textContent.trim(),
            }))()`,
            sessionId,
        );
        if (startState.error) {
            throw new Error(
                `Draft session did not start: ${JSON.stringify(startState)}`,
            );
        }
        // The session can also fail to start without rendering an error state --
        // a render exception tears the circuit down and leaves the page as it
        // was. A bare "condition timed out" for the combobox says nothing about
        // which of those happened, so the page's own state goes in the message.
        try {
            await waitFor(
                "document.querySelector('[role=combobox]') !== null",
                sessionId,
            );
        } catch (cause) {
            const afterState = await evaluate(
                `(() => ({
                    url: location.href,
                    heading: document.querySelector("h1")?.textContent.trim(),
                    hasStatus:
                        document.querySelector("section[aria-label='Draft status']")
                            !== null,
                    disconnected:
                        document.querySelector("#components-reconnect-modal")
                            !== null,
                    body: document.body.innerText.slice(0, 400),
                }))()`,
                sessionId,
            );
            throw new Error(
                `D-13: the pick entry never rendered after starting a draft. `
                + `Page state: ${JSON.stringify(afterState)}`,
                { cause },
            );
        }
        await evaluate(
            "document.querySelector('[role=combobox]').focus()",
            sessionId,
        );
        await pressKey(sessionId, "z", "KeyZ", "z");
        await waitFor(
            "document.querySelectorAll('[role=option]').length === 1",
            sessionId,
        );
        await pressKey(sessionId, "ArrowDown", "ArrowDown");
        await pressKey(sessionId, "Enter", "Enter");
        await waitFor(
            "document.querySelector('[data-live-region=pick]')?.textContent.includes('Zed UI Player') === true",
            sessionId,
        );
        const keyboardResult = await evaluate(
            `(() => ({
                focusReturned:
                    document.activeElement?.getAttribute("role") === "combobox",
                // The number itself, not a substring of the rendered line. The
                // status bar puts the label, the pick and the total in separate
                // elements, so its textContent has no spaces to match on.
                currentPick:
                    document.querySelector(
                        "section[aria-label='Draft status'] [data-numeric]",
                    )?.textContent.trim() === "2",
            }))()`,
            sessionId,
        );
        if (!keyboardResult.focusReturned || !keyboardResult.currentPick) {
            throw new Error(
                "D-13 keyboard commit did not advance the pick and restore focus.",
            );
        }

        process.stdout.write(
            "D-13 browser interaction: 3 keystrokes, pick committed, focus restored\n",
        );

        const topName = await evaluate(
            `document.querySelector(
                "[data-draft-board] [data-player-id] th"
            )?.textContent.trim()`,
            sessionId,
        );
        if (!topName) {
            throw new Error("D-14 could not identify the top ranked player.");
        }

        await evaluate(
            `(() => {
                const viewport = document.querySelector(
                    "[data-draft-board] [data-player-table]"
                );
                viewport.scrollTop = ${JSON.stringify(rowHeight)} * 10;
            })()`,
            sessionId,
        );
        await waitFor(
            "document.querySelector('[data-player-table]').scrollTop > 0",
            sessionId,
        );
        await evaluate(
            "new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)))",
            sessionId,
        );
        const beforeLivePick = await evaluate(
            "window.draftBoard.capture('live-draft-board')",
            sessionId,
        );
        await evaluate(
            `(() => {
                const input = document.querySelector("[role=combobox]");
                input.value = ${JSON.stringify(topName)};
                input.dispatchEvent(new Event("input", { bubbles: true }));
                input.focus();
            })()`,
            sessionId,
        );
        await waitFor(
            "document.querySelectorAll('[role=option]').length === 1",
            sessionId,
        );
        await pressKey(sessionId, "Enter", "Enter");
        await waitFor(
            `document.querySelector('[data-live-region=pick]')
                ?.textContent.includes(${JSON.stringify(topName)}) === true`,
            sessionId,
        );
        await waitFor(
            `(() => {
                const after = window.draftBoard.capture("live-draft-board");
                const afterById = new Map(
                    after.rows.map(row => [row.playerId, row.offset])
                );
                const deltas = ${JSON.stringify(beforeLivePick.rows)}
                    .filter(row => afterById.has(row.playerId))
                    .map(row => Math.abs(
                        afterById.get(row.playerId) - row.offset
                    ));
                return deltas.length > 0 && Math.max(...deltas) <= 0.01;
            })()`,
            sessionId,
        );
        const liveMeasurement = await evaluate(
            `(() => {
                const after = window.draftBoard.capture("live-draft-board");
                const afterById = new Map(
                    after.rows.map(row => [row.playerId, row.offset])
                );
                const deltas = ${JSON.stringify(beforeLivePick.rows)}
                    .filter(row => afterById.has(row.playerId))
                    .map(row => Math.abs(
                        afterById.get(row.playerId) - row.offset
                    ));
                return {
                    measuredRows: deltas.length,
                    maximumOffsetDelta: Math.max(0, ...deltas),
                };
            })()`,
            sessionId,
        );
        process.stdout.write(
            "D-14 live Blazor measurement: "
            + `${liveMeasurement.measuredRows} visible rows, `
            + `${liveMeasurement.maximumOffsetDelta.toFixed(3)} CSS px maximum offset delta\n`,
        );
    }

    command("Browser.close").catch(() => {});
    await new Promise(resolve => setTimeout(resolve, 500));
} finally {
    if (browser.exitCode === null) {
        browser.kill();
        await Promise.race([
            once(browser, "exit"),
            new Promise(resolve => setTimeout(resolve, 2000)),
        ]);
    }

    rmSync(profileDirectory, {
        recursive: true,
        force: true,
        maxRetries: 10,
        retryDelay: 100,
    });
}
