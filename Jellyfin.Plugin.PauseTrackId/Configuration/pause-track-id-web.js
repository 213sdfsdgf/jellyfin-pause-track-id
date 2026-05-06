(() => {
    if (window.__pauseTrackIdWebClientLoaded) {
        return;
    }

    window.__pauseTrackIdWebClientLoaded = true;

    const STYLE_ID = "pause-track-id-web-style";
    const ROOT_ID = "pause-track-id-root";
    const TITLE_BUTTON_CLASS = "pause-track-id__title";
    const CLOSE_BUTTON_CLASS = "pause-track-id__close";

    const state = {
        root: null,
        titleButton: null,
        closeButton: null,
        hideTimer: null,
        lastDisplayText: null,
        inFlight: false,
        copiedTimer: null,
    };

    function injectStyle() {
        if (document.getElementById(STYLE_ID)) {
            return;
        }

        const style = document.createElement("style");
        style.id = STYLE_ID;
        style.textContent = `
#${ROOT_ID} {
    position: fixed;
    top: calc(env(safe-area-inset-top, 0px) + 1rem);
    right: calc(env(safe-area-inset-right, 0px) + 1rem);
    z-index: 10000;
    display: none;
    align-items: center;
    gap: 0.5rem;
    max-width: min(32rem, calc(100vw - 2rem));
    padding: 0.65rem 0.8rem;
    border-radius: 999px;
    background: rgba(20, 20, 25, 0.88);
    backdrop-filter: blur(10px);
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.28);
    border: 1px solid rgba(255, 255, 255, 0.12);
    color: #fff;
    font: inherit;
}

#${ROOT_ID}.is-visible {
    display: inline-flex;
}

#${ROOT_ID} .${TITLE_BUTTON_CLASS},
#${ROOT_ID} .${CLOSE_BUTTON_CLASS} {
    appearance: none;
    border: 0;
    color: inherit;
    background: transparent;
    font: inherit;
}

#${ROOT_ID} .${TITLE_BUTTON_CLASS} {
    cursor: pointer;
    font-weight: 600;
    text-align: left;
    max-width: 100%;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

#${ROOT_ID} .${TITLE_BUTTON_CLASS}::before {
    content: "🎵 ";
}

#${ROOT_ID} .${CLOSE_BUTTON_CLASS} {
    cursor: pointer;
    width: 2rem;
    height: 2rem;
    border-radius: 999px;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    background: rgba(255, 255, 255, 0.12);
    flex: 0 0 auto;
}

#${ROOT_ID} .${CLOSE_BUTTON_CLASS}:hover,
#${ROOT_ID} .${CLOSE_BUTTON_CLASS}:focus-visible,
#${ROOT_ID} .${TITLE_BUTTON_CLASS}:hover,
#${ROOT_ID} .${TITLE_BUTTON_CLASS}:focus-visible {
    opacity: 0.92;
}

@media (max-width: 640px) {
    #${ROOT_ID} {
        left: 0.75rem;
        right: 0.75rem;
        max-width: none;
    }
}
`;

        document.head.appendChild(style);
    }

    function clearTimers() {
        if (state.hideTimer) {
            window.clearTimeout(state.hideTimer);
            state.hideTimer = null;
        }

        if (state.copiedTimer) {
            window.clearTimeout(state.copiedTimer);
            state.copiedTimer = null;
        }
    }

    function hideButton() {
        clearTimers();
        if (state.root) {
            state.root.classList.remove("is-visible");
        }
    }

    async function copyCurrentText() {
        if (!state.titleButton) {
            return;
        }

        const text = state.lastDisplayText;
        if (!text) {
            return;
        }

        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
            }
        } catch (error) {
            console.warn("[pause-track-id] clipboard copy failed", error);
        }

        const previous = state.titleButton.textContent;
        state.titleButton.textContent = "Copied";
        state.copiedTimer = window.setTimeout(() => {
            if (state.titleButton) {
                state.titleButton.textContent = previous;
            }
            state.copiedTimer = null;
        }, 1400);
    }

    function ensureUi() {
        injectStyle();

        if (state.root && document.body.contains(state.root)) {
            return state.root;
        }

        const root = document.createElement("div");
        root.id = ROOT_ID;
        root.setAttribute("role", "status");
        root.setAttribute("aria-live", "polite");

        const titleButton = document.createElement("button");
        titleButton.type = "button";
        titleButton.className = TITLE_BUTTON_CLASS;
        titleButton.addEventListener("click", () => {
            copyCurrentText().catch((error) => console.warn("[pause-track-id] copy handler failed", error));
        });

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = CLOSE_BUTTON_CLASS;
        closeButton.textContent = "×";
        closeButton.setAttribute("aria-label", "Dismiss recognized track");
        closeButton.addEventListener("click", hideButton);

        root.append(titleButton, closeButton);
        document.body.appendChild(root);

        state.root = root;
        state.titleButton = titleButton;
        state.closeButton = closeButton;

        return root;
    }

    function isPlaybackPaused() {
        const video = document.querySelector("video");
        return Boolean(video && !video.ended && video.paused);
    }

    function getApiClient() {
        return window.ApiClient || null;
    }

    function getDeviceId(apiClient) {
        if (!apiClient) {
            return "";
        }

        if (typeof apiClient.deviceId === "function") {
            return apiClient.deviceId() || "";
        }

        return apiClient._deviceId || "";
    }

    function getAccessToken(apiClient) {
        if (!apiClient) {
            return "";
        }

        if (typeof apiClient.accessToken === "function") {
            return apiClient.accessToken() || "";
        }

        return apiClient._serverInfo?.AccessToken || "";
    }

    function buildUrl(apiClient, path) {
        if (apiClient && typeof apiClient.getUrl === "function") {
            return apiClient.getUrl(path);
        }

        return `/${path}`;
    }

    function showButton(displayText, autoHideSeconds) {
        const root = ensureUi();
        state.lastDisplayText = displayText;
        state.titleButton.textContent = displayText;
        root.classList.add("is-visible");

        clearTimers();
        state.hideTimer = window.setTimeout(() => {
            hideButton();
        }, Math.max(3, autoHideSeconds || 12) * 1000);
    }

    async function pollForRecognition() {
        if (state.inFlight || document.hidden || !isPlaybackPaused()) {
            if (!isPlaybackPaused()) {
                hideButton();
            }
            return;
        }

        const apiClient = getApiClient();
        const deviceId = getDeviceId(apiClient);
        if (!deviceId) {
            return;
        }

        state.inFlight = true;
        try {
            const url = `${buildUrl(apiClient, "PauseTrackId/Active")}?deviceId=${encodeURIComponent(deviceId)}`;
            const headers = {};
            const token = getAccessToken(apiClient);
            if (token) {
                headers["X-Emby-Token"] = token;
            }

            const response = await fetch(url, {
                credentials: "same-origin",
                headers,
            });

            if (response.status === 204) {
                return;
            }

            if (!response.ok) {
                console.warn("[pause-track-id] polling failed", response.status);
                return;
            }

            const data = await response.json();
            const displayText = data?.displayText || data?.DisplayText;
            const autoHideSeconds = data?.autoHideSeconds || data?.AutoHideSeconds;
            if (!displayText) {
                return;
            }

            showButton(displayText, autoHideSeconds);
        } catch (error) {
            console.warn("[pause-track-id] polling exception", error);
        } finally {
            state.inFlight = false;
        }
    }

    ensureUi();
    window.setInterval(() => {
        pollForRecognition().catch((error) => console.warn("[pause-track-id] poll loop failed", error));
    }, 1500);

    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) {
            pollForRecognition().catch((error) => console.warn("[pause-track-id] visibility poll failed", error));
        }
    });
})();
