window.draftBoard = {
    restoreGeneration: 0,
    restoreObserver: null,

    capture(boardId) {
        const board = document.querySelector(`[data-draft-board="${boardId}"]`);
        const viewport = board?.querySelector("[data-player-table]");
        if (!viewport) {
            return null;
        }

        const viewportRect = viewport.getBoundingClientRect();
        const rows = Array.from(viewport.querySelectorAll("[data-player-id]"))
            .filter(row => {
                const rect = row.getBoundingClientRect();
                return rect.bottom >= viewportRect.top
                    && rect.top <= viewportRect.bottom;
            })
            .map(row => ({
                playerId: row.dataset.playerId,
                offset: row.getBoundingClientRect().top - viewportRect.top,
            }));

        return {
            scrollTop: viewport.scrollTop,
            rows,
        };
    },

    restore(boardId, snapshot) {
        if (!snapshot?.rows?.length) {
            return;
        }

        const correctAnchor = () => {
            const board = document.querySelector(
                `[data-draft-board="${boardId}"]`,
            );
            const viewport = board?.querySelector("[data-player-table]");
            if (!viewport) {
                return;
            }

            const viewportTop = viewport.getBoundingClientRect().top;
            const anchor = snapshot.rows
                .map(row => ({
                    previous: row,
                    current: viewport.querySelector(
                        `[data-player-id="${CSS.escape(row.playerId)}"]`,
                    ),
                }))
                .find(candidate => candidate.current);

            if (!anchor) {
                viewport.scrollTop = snapshot.scrollTop;
                return;
            }

            const currentOffset =
                anchor.current.getBoundingClientRect().top - viewportTop;
            const offsetDelta = currentOffset - anchor.previous.offset;
            if (Math.abs(offsetDelta) > 0.01) {
                viewport.scrollTop += offsetDelta;
            }
        };

        ++window.draftBoard.restoreGeneration;
        window.draftBoard.restoreObserver?.disconnect();

        const board = document.querySelector(
            `[data-draft-board="${boardId}"]`,
        );
        if (!board) {
            return;
        }

        const observer = new MutationObserver(() => {
            requestAnimationFrame(() => {
                const viewport = board.querySelector("[data-player-table]");
                if (!viewport) {
                    return;
                }

                if (Math.abs(viewport.scrollTop - snapshot.scrollTop) > 0.01) {
                    observer.disconnect();
                    return;
                }

                correctAnchor();
                requestAnimationFrame(() => {
                    correctAnchor();
                    observer.disconnect();
                    if (window.draftBoard.restoreObserver === observer) {
                        window.draftBoard.restoreObserver = null;
                    }
                });
            });
        });

        window.draftBoard.restoreObserver = observer;
        observer.observe(board, {
            childList: true,
            subtree: true,
        });
        correctAnchor();
    },
};
