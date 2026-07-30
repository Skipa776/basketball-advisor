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
        await evaluate(
            "document.querySelector('form button[type=submit]').click()",
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
        await waitFor(
            "document.querySelector('[role=combobox]') !== null",
            sessionId,
        );
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
                currentPick:
                    document.querySelector("section[aria-label='Draft status']")
                        ?.textContent.includes("Pick 2") === true,
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
