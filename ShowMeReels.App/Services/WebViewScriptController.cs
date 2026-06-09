using System.Text.Json;
using ShowMeReels.App.Models;

namespace ShowMeReels.App.Services;

public sealed class WebViewScriptController : IWebViewScriptController
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string BuildApplySettingsScript(AppSettings settings)
    {
        string payload = JsonSerializer.Serialize(new ScriptSettings(
            settings.PlaybackSpeed,
            settings.VolumePercent,
            settings.SeekBarEnabled,
            settings.Platform == ContentPlatform.TikTok ? "tiktok" : "instagram",
            settings.SkipSeenReelsEnabled,
            settings.IgnoredTikTokVideoIds), SerializerOptions);

        return $"window.showMeReels ? window.showMeReels.setSettings({payload}) : false;";
    }

    public string BuildBootstrapScript()
    {
        return """
            (() => {
                if (window.showMeReels) {
                    return;
                }

                const state = {
                    platform: "",
                    playbackSpeed: 1,
                    volumePercent: 50,
                    seekBarEnabled: false,
                    skipSeenReelsEnabled: false
                };

                const seekBar = {
                    host: null,
                    input: null,
                    activeVideo: null,
                    isScrubbing: false
                };

                const toast = {
                    host: null,
                    hideTimeoutId: 0
                };

                const ignoreButton = {
                    host: null
                };

                const host = {
                    isActive: true,
                    observer: null,
                    activeAudioTimerId: 0,
                    maintenanceTimerId: 0,
                    applyQueued: false
                };

                const SeenReelIdsStorageKey = "showMeReels.seenReelIds.v1";
                const AdSkipCooldownMs = 1200;
                const DuplicateSkipCooldownMs = 700;
                const IgnoredTikTokSkipCooldownMs = 700;
                const ActiveAudioEnforcementIntervalMs = 250;
                const ActiveMaintenanceIntervalMs = 1200;
                const adCtaPatterns = [
                    "shop now",
                    "learn more",
                    "sign up",
                    "book now",
                    "order now",
                    "download",
                    "install now",
                    "watch now"
                ];
                const seenReelIds = loadSeenReelIds();
                const ignoredTikTokVideoIds = new Set();

                let lastAdSkipKey = "";
                let lastAdSkipAt = 0;
                let lastAdSkipDirection = 1;
                let lastActiveReelId = null;
                let lastDuplicateSkipId = null;
                let lastDuplicateSkipDirection = 1;
                let lastDuplicateSkipAt = 0;
                let lastIgnoredTikTokVideoId = null;
                let lastIgnoredTikTokSkipAt = 0;
                let lastRequestedScrollDirection = 1;
                let scrollLocked = false;

                function clamp(value, minimum, maximum) {
                    return Math.min(Math.max(value, minimum), maximum);
                }

                function scheduleApply(force = false) {
                    if ((!host.isActive && !force) || host.applyQueued) {
                        return;
                    }

                    host.applyQueued = true;
                    window.requestAnimationFrame(() => {
                        host.applyQueued = false;
                        apply();
                    });
                }

                function schedulePostScrollApply() {
                    scheduleApply(true);
                    window.setTimeout(() => scheduleApply(true), 60);
                    window.setTimeout(() => scheduleApply(true), 160);
                }

                function detectPlatform() {
                    return window.location.hostname.includes("tiktok.com") ? "tiktok" : "instagram";
                }

                function getPlatform() {
                    return state.platform || detectPlatform();
                }

                function isInstagramReelsSurface() {
                    return getPlatform() === "instagram"
                        && /\/reels(\/|$)/i.test(window.location.pathname);
                }

                function getVideos() {
                    return Array.from(document.querySelectorAll("video"))
                        .filter(video => video instanceof HTMLVideoElement);
                }

                function getVisibleVideos() {
                    return getVideos()
                        .filter(video => {
                            const rect = video.getBoundingClientRect();
                            return rect.width > 0 && rect.height > 0;
                        })
                        .sort((left, right) => left.getBoundingClientRect().top - right.getBoundingClientRect().top);
                }

                function getActiveVideo() {
                    const videos = getVisibleVideos();
                    if (!videos.length) {
                        return null;
                    }

                    let bestVideo = videos[0];
                    let bestScore = Number.NEGATIVE_INFINITY;

                    for (const video of videos) {
                        const rect = video.getBoundingClientRect();
                        const visibleHeight = Math.max(0, Math.min(rect.bottom, window.innerHeight) - Math.max(rect.top, 0));
                        const visibleWidth = Math.max(0, Math.min(rect.right, window.innerWidth) - Math.max(rect.left, 0));
                        const visibleArea = visibleHeight * visibleWidth;
                        const centerDistance = Math.abs((window.innerHeight / 2) - (rect.top + (rect.height / 2)));
                        const score = visibleArea - centerDistance;

                        if (score > bestScore) {
                            bestScore = score;
                            bestVideo = video;
                        }
                    }

                    return bestVideo;
                }

                function getScrollTarget(video) {
                    if (!video) {
                        return null;
                    }

                    if (getPlatform() === "tiktok") {
                        return video.closest("[data-e2e='recommend-list-item-container']")
                            ?? video.closest("[data-e2e='recommend-list-item']")
                            ?? video.closest("article")
                            ?? video.closest("section")
                            ?? video.parentElement
                            ?? video;
                    }

                    return video.closest("article")
                        ?? video.closest("section")
                        ?? video.parentElement
                        ?? video;
                }

                function getTikTokFeedItems(scrollContainer) {
                    const root = scrollContainer instanceof Element ? scrollContainer : document;
                    const candidates = Array.from(root.querySelectorAll(
                        "[data-e2e='recommend-list-item-container'], [data-e2e='recommend-list-item'], article, section"))
                        .filter(element => element instanceof HTMLElement
                            && element.querySelector("video")
                            && element.getBoundingClientRect().height > 160);

                    return Array.from(new Set(candidates))
                        .sort((left, right) => left.getBoundingClientRect().top - right.getBoundingClientRect().top);
                }

                function getInstagramFeedItems() {
                    const reelAnchors = Array.from(document.querySelectorAll("a[href*='/reel/']"));
                    const candidates = reelAnchors
                        .map(anchor => anchor.closest("article")
                            ?? anchor.closest("section")
                            ?? anchor.closest("main > div")
                            ?? anchor.parentElement)
                        .filter(element => element instanceof HTMLElement
                            && element.querySelector("video")
                            && element.getBoundingClientRect().height > 160);

                    return Array.from(new Set(candidates))
                        .sort((left, right) => left.getBoundingClientRect().top - right.getBoundingClientRect().top);
                }

                function isScrollableElement(element) {
                    if (!(element instanceof HTMLElement)) {
                        return false;
                    }

                    return element.scrollHeight > element.clientHeight + 4;
                }

                function getScrollableContainer(element) {
                    let current = element instanceof Element ? element.parentElement : null;
                    while (current) {
                        if (isScrollableElement(current)) {
                            return current;
                        }

                        current = current.parentElement;
                    }

                    return document.scrollingElement || document.documentElement || document.body;
                }

                function tryScrollWindow(deltaY) {
                    const before = window.scrollY || window.pageYOffset || 0;
                    window.scrollBy({
                        top: deltaY,
                        left: 0,
                        behavior: "auto"
                    });

                    const after = window.scrollY || window.pageYOffset || 0;
                    return Math.abs(after - before) > 1;
                }

                function tryScrollAncestors(element, deltaY) {
                    let current = element instanceof Element ? element : null;
                    while (current) {
                        if (current instanceof HTMLElement && isScrollableElement(current)) {
                            const before = current.scrollTop;
                            current.scrollTop = before + deltaY;
                            if (Math.abs(current.scrollTop - before) > 1) {
                                return true;
                            }
                        }

                        current = current.parentElement;
                    }

                    const scrollingElement = document.scrollingElement || document.documentElement || document.body;
                    if (scrollingElement instanceof HTMLElement) {
                        const before = scrollingElement.scrollTop;
                        scrollingElement.scrollTop = before + deltaY;
                        if (Math.abs(scrollingElement.scrollTop - before) > 1) {
                            return true;
                        }
                    }

                    return tryScrollWindow(deltaY);
                }

                function tryScrollBestContainer(seedElements, deltaY) {
                    const candidates = [];
                    const seen = new Set();

                    function pushCandidate(candidate) {
                        if (!(candidate instanceof HTMLElement) || seen.has(candidate)) {
                            return;
                        }

                        seen.add(candidate);
                        candidates.push(candidate);
                    }

                    for (const seed of seedElements) {
                        let current = seed instanceof Element ? seed : null;
                        while (current) {
                            pushCandidate(current);
                            current = current.parentElement;
                        }
                    }

                    for (const candidate of Array.from(document.querySelectorAll("main, [role='main'], div, section"))) {
                        if (candidate instanceof HTMLElement && isScrollableElement(candidate)) {
                            pushCandidate(candidate);
                        }
                    }

                    candidates.sort((left, right) => (right.scrollHeight - right.clientHeight) - (left.scrollHeight - left.clientHeight));

                    for (const candidate of candidates) {
                        if (!isScrollableElement(candidate)) {
                            continue;
                        }

                        const before = candidate.scrollTop;
                        candidate.scrollTop = before + deltaY;
                        if (Math.abs(candidate.scrollTop - before) > 1) {
                            return true;
                        }
                    }

                    return tryScrollWindow(deltaY);
                }

                function isEditableTarget(target) {
                    return target instanceof Element
                        && Boolean(target.closest("input, textarea, select, [contenteditable='true']"));
                }

                function loadSeenReelIds() {
                    try {
                        const payload = window.sessionStorage.getItem(SeenReelIdsStorageKey);
                        if (!payload) {
                            return new Set();
                        }

                        const parsed = JSON.parse(payload);
                        if (!Array.isArray(parsed)) {
                            return new Set();
                        }

                        return new Set(parsed.filter(reelId => typeof reelId === "string" && reelId.length > 0));
                    } catch {
                        return new Set();
                    }
                }

                function persistSeenReelIds() {
                    try {
                        window.sessionStorage.setItem(SeenReelIdsStorageKey, JSON.stringify(Array.from(seenReelIds)));
                    } catch {
                    }
                }

                function syncIgnoredTikTokVideoIds(videoIds) {
                    ignoredTikTokVideoIds.clear();

                    if (!Array.isArray(videoIds)) {
                        return;
                    }

                    for (const videoId of videoIds) {
                        if (typeof videoId !== "string") {
                            continue;
                        }

                        const normalizedVideoId = videoId.trim();
                        if (normalizedVideoId) {
                            ignoredTikTokVideoIds.add(normalizedVideoId);
                        }
                    }
                }

                function rememberSeenReelId(reelId) {
                    if (!reelId || seenReelIds.has(reelId)) {
                        return false;
                    }

                    seenReelIds.add(reelId);
                    persistSeenReelIds();
                    return true;
                }

                function extractReelId(candidate) {
                    if (!candidate) {
                        return null;
                    }

                    const candidateText = String(candidate).trim();
                    if (getPlatform() !== "tiktok"
                        && /^[A-Za-z0-9_-]{6,}$/.test(candidateText)
                        && !candidateText.includes("/")
                        && !candidateText.includes(".")) {
                        return `ig:${candidateText}`;
                    }

                    try {
                        const normalized = new URL(candidateText, window.location.origin);
                        const platform = getPlatform();
                        if (platform === "tiktok" || normalized.hostname.includes("tiktok.com")) {
                            const videoMatch = normalized.pathname.match(/\/video\/([^/?#]+)/i);
                            return videoMatch ? videoMatch[1] : null;
                        }

                        const match = normalized.pathname.match(/\/reel\/([^/?#]+)/i);
                        return match ? match[1] : null;
                    } catch {
                        if (getPlatform() === "tiktok") {
                            const textMatch = String(candidate).match(/(?:\/video\/|videoId["':\s=]+)([0-9]{8,})/i);
                            return textMatch ? textMatch[1] : null;
                        }

                        return null;
                    }
                }

                function collectAttributeCandidates(element, attributeNames, candidates) {
                    if (!(element instanceof Element)) {
                        return;
                    }

                    for (const attributeName of attributeNames) {
                        const attributeValue = element.getAttribute(attributeName);
                        if (attributeValue) {
                            candidates.push(attributeValue);
                        }
                    }
                }

                function collectTikTokCandidates(element, candidates) {
                    if (!(element instanceof Element)) {
                        return;
                    }

                    const attributeNames = [
                        "href",
                        "src",
                        "poster",
                        "itemid",
                        "data-id",
                        "data-video-id",
                        "data-item-id",
                        "data-aweme-id",
                        "data-e2e-video-id"
                    ];

                    collectAttributeCandidates(element, attributeNames, candidates);

                    const descendants = Array.from(element.querySelectorAll("[href], [src], [poster], [itemid], [data-id], [data-video-id], [data-item-id], [data-aweme-id], [data-e2e-video-id]"));
                    for (const descendant of descendants.slice(0, 48)) {
                        collectAttributeCandidates(descendant, attributeNames, candidates);
                    }

                    const htmlSnapshot = String(element.innerHTML || "");
                    const textMatches = htmlSnapshot.match(/(?:\/video\/|videoId["':\s=]+)([0-9]{8,})/ig) ?? [];
                    for (const match of textMatches.slice(0, 12)) {
                        candidates.push(match);
                    }
                }

                function collectInstagramCandidates(element, candidates) {
                    if (!(element instanceof Element)) {
                        return;
                    }

                    const attributeNames = [
                        "href",
                        "src",
                        "poster",
                        "itemid",
                        "data-id",
                        "data-media-id",
                        "data-reel-id",
                        "data-video-id"
                    ];

                    collectAttributeCandidates(element, attributeNames, candidates);

                    const descendants = Array.from(element.querySelectorAll("[href], [src], [poster], [itemid], [data-id], [data-media-id], [data-reel-id], [data-video-id]"));
                    for (const descendant of descendants.slice(0, 48)) {
                        collectAttributeCandidates(descendant, attributeNames, candidates);
                    }

                    const htmlSnapshot = String(element.innerHTML || "");
                    const textMatches = htmlSnapshot.match(/\/reel\/([^/?#"'<\s]+)/ig) ?? [];
                    for (const match of textMatches.slice(0, 12)) {
                        candidates.push(match);
                    }
                }

                function getCurrentPageReelId() {
                    const pageCandidates = [
                        window.location?.href,
                        document.querySelector("link[rel='canonical']")?.getAttribute("href"),
                        document.querySelector("meta[property='og:url']")?.getAttribute("content")
                    ];

                    for (const candidate of pageCandidates) {
                        const reelId = extractReelId(candidate);
                        if (reelId) {
                            return reelId;
                        }
                    }

                    return null;
                }

                function getElementRectForScoring(element) {
                    if (!(element instanceof Element)) {
                        return null;
                    }

                    const ownRect = element.getBoundingClientRect();
                    if (ownRect.width > 0 && ownRect.height > 0) {
                        return ownRect;
                    }

                    const container = element.closest("article, section, main > div");
                    if (container instanceof Element) {
                        const containerRect = container.getBoundingClientRect();
                        if (containerRect.width > 0 && containerRect.height > 0) {
                            return containerRect;
                        }
                    }

                    return null;
                }

                function getVisibleInstagramReelIds(video) {
                    if (getPlatform() === "tiktok" || !video) {
                        return [];
                    }

                    const videoRect = video.getBoundingClientRect();
                    const scored = [];
                    const seenIds = new Set();
                    for (const anchor of Array.from(document.querySelectorAll("a[href*='/reel/']"))) {
                        const reelId = extractReelId(anchor.getAttribute("href") || anchor.href);
                        if (!reelId || seenIds.has(reelId)) {
                            continue;
                        }

                        const rect = getElementRectForScoring(anchor);
                        if (!rect || rect.bottom <= 0 || rect.top >= window.innerHeight) {
                            continue;
                        }

                        const verticalOverlap = Math.max(0, Math.min(rect.bottom, videoRect.bottom) - Math.max(rect.top, videoRect.top));
                        const horizontalOverlap = Math.max(0, Math.min(rect.right, videoRect.right) - Math.max(rect.left, videoRect.left));
                        const overlapArea = verticalOverlap * horizontalOverlap;
                        const centerDistance = Math.abs((videoRect.top + (videoRect.height / 2)) - (rect.top + (rect.height / 2)));
                        const score = overlapArea - centerDistance;
                        seenIds.add(reelId);
                        scored.push({ reelId, score });
                    }

                    return scored
                        .sort((left, right) => right.score - left.score)
                        .map(candidate => candidate.reelId);
                }

                function getActiveReelId(video) {
                    if (!video) {
                        return null;
                    }

                    const candidates = [];
                    const reelSelector = getPlatform() === "tiktok" ? "a[href*='/video/']" : "a[href*='/reel/']";
                    const elements = [getScrollTarget(video), video.parentElement, video]
                        .filter(element => element instanceof Element);

                    for (const element of elements) {
                        if (getPlatform() === "tiktok") {
                            collectTikTokCandidates(element, candidates);
                        } else {
                            collectInstagramCandidates(element, candidates);
                        }

                        const parentAnchor = element.closest(reelSelector);
                        if (parentAnchor) {
                            const parentHref = parentAnchor.getAttribute("href");
                            if (parentHref) {
                                candidates.push(parentHref);
                            }

                            if (parentAnchor.href) {
                                candidates.push(parentAnchor.href);
                            }
                        }

                        const reelAnchors = Array.from(element.querySelectorAll(reelSelector)).slice(0, 8);
                        for (const anchor of reelAnchors) {
                            const href = anchor.getAttribute("href");
                            if (href) {
                                candidates.push(href);
                            }

                            if (anchor.href) {
                                candidates.push(anchor.href);
                            }
                        }
                    }

                    if (getPlatform() !== "tiktok") {
                        const pageReelId = getCurrentPageReelId();
                        if (pageReelId) {
                            return pageReelId;
                        }

                        candidates.unshift(...getVisibleInstagramReelIds(video));
                    } else {
                        const canonicalHref = document.querySelector("link[rel='canonical']")?.getAttribute("href");
                        if (canonicalHref) {
                            candidates.push(canonicalHref);
                        }

                        const ogUrl = document.querySelector("meta[property='og:url']")?.getAttribute("content");
                        if (ogUrl) {
                            candidates.push(ogUrl);
                        }
                    }

                    if (window.location?.href) {
                        candidates.push(window.location.href);
                    }

                    for (const candidate of candidates) {
                        const reelId = extractReelId(candidate);
                        if (reelId) {
                            return reelId;
                        }
                    }

                    return null;
                }

                function pushUniqueKey(keys, seen, key) {
                    if (!key || seen.has(key)) {
                        return;
                    }

                    seen.add(key);
                    keys.push(key);
                }

                function hashString(value) {
                    let hash = 2166136261;
                    for (let index = 0; index < value.length; index += 1) {
                        hash ^= value.charCodeAt(index);
                        hash = Math.imul(hash, 16777619);
                    }

                    return (hash >>> 0).toString(16).padStart(8, "0");
                }

                function normalizeAssetCandidate(candidate) {
                    if (!candidate) {
                        return null;
                    }

                    try {
                        const normalizedUrl = new URL(candidate, window.location.origin);
                        return `${normalizedUrl.host}${normalizedUrl.pathname}`.toLowerCase();
                    } catch {
                        const normalizedText = String(candidate)
                            .trim()
                            .toLowerCase()
                            .split("?")[0]
                            .split("#")[0];
                        return normalizedText || null;
                    }
                }

                function getTikTokFingerprintKey(video) {
                    if (getPlatform() !== "tiktok" || !video) {
                        return null;
                    }

                    const target = getScrollTarget(video) ?? video;
                    const creatorHref = target.querySelector("a[href^='/@']")?.getAttribute("href") ?? "";
                    const description = target.querySelector("[data-e2e='video-desc']")?.textContent
                        ?? target.querySelector("h1, h2")?.textContent
                        ?? getNormalizedTextSnapshot(target).slice(0, 240);
                    const assetCandidate = normalizeAssetCandidate(video.poster)
                        ?? normalizeAssetCandidate(video.currentSrc)
                        ?? normalizeAssetCandidate(video.getAttribute("src"))
                        ?? normalizeAssetCandidate(target.querySelector("source")?.getAttribute("src"));
                    const fingerprintSource = [creatorHref.trim().toLowerCase(), String(description || "").trim().toLowerCase(), assetCandidate ?? ""]
                        .filter(value => value.length > 0)
                        .join("|");

                    return fingerprintSource ? `fp:${hashString(fingerprintSource)}` : null;
                }

                function getTikTokIgnoreKeys(video) {
                    if (getPlatform() !== "tiktok" || !video) {
                        return [];
                    }

                    const keys = [];
                    const seen = new Set();
                    const videoId = getActiveReelId(video);
                    if (videoId) {
                        pushUniqueKey(keys, seen, videoId);
                    }

                    const assetCandidates = [
                        video.poster,
                        video.currentSrc,
                        video.getAttribute("src"),
                        video.querySelector("source")?.getAttribute("src")
                    ];

                    for (const assetCandidate of assetCandidates) {
                        const normalizedAssetCandidate = normalizeAssetCandidate(assetCandidate);
                        if (normalizedAssetCandidate) {
                            pushUniqueKey(keys, seen, `asset:${hashString(normalizedAssetCandidate)}`);
                        }
                    }

                    const fingerprintKey = getTikTokFingerprintKey(video);
                    if (fingerprintKey) {
                        pushUniqueKey(keys, seen, fingerprintKey);
                    }

                    return keys;
                }

                function getNormalizedTextSnapshot(element) {
                    if (!(element instanceof Element)) {
                        return "";
                    }

                    return String(element.innerText || element.textContent || "")
                        .replace(/\s+/g, " ")
                        .trim()
                        .toLowerCase();
                }

                function isElementInScope(element, referenceRect) {
                    if (!(element instanceof Element) || !referenceRect) {
                        return element instanceof Element;
                    }

                    const rect = element.getBoundingClientRect();
                    if (rect.width <= 0 || rect.height <= 0) {
                        return false;
                    }

                    const tolerance = 24;
                    return rect.bottom >= (referenceRect.top - tolerance)
                        && rect.top <= (referenceRect.bottom + tolerance)
                        && rect.right >= (referenceRect.left - tolerance)
                        && rect.left <= (referenceRect.right + tolerance);
                }

                function hasExactText(target, expectedText, referenceRect = null) {
                    if (!(target instanceof Element)) {
                        return false;
                    }

                    const normalizedExpectedText = String(expectedText || "").trim().toLowerCase();
                    if (!normalizedExpectedText) {
                        return false;
                    }

                    if (isElementInScope(target, referenceRect)
                        && getNormalizedTextSnapshot(target) === normalizedExpectedText) {
                        return true;
                    }

                    return Array.from(target.querySelectorAll("span, div, a, button"))
                        .some(candidate =>
                            isElementInScope(candidate, referenceRect)
                            && getNormalizedTextSnapshot(candidate) === normalizedExpectedText);
                }

                function hasSelectorInScope(target, selector, referenceRect) {
                    if (!(target instanceof Element)) {
                        return false;
                    }

                    return Array.from(target.querySelectorAll(selector))
                        .some(candidate => isElementInScope(candidate, referenceRect));
                }

                function hasButtonLabelInScope(target, labels, referenceRect) {
                    if (!(target instanceof Element)) {
                        return false;
                    }

                    return Array.from(target.querySelectorAll("button, a, div[role='button']"))
                        .some(candidate =>
                            isElementInScope(candidate, referenceRect)
                            && labels.includes(getNormalizedTextSnapshot(candidate)));
                }

                function maybeSkipTikTokAd(video) {
                    if (getPlatform() !== "tiktok" || !video) {
                        return false;
                    }

                    const target = getScrollTarget(video);
                    if (!target) {
                        return false;
                    }

                    const normalizedText = getNormalizedTextSnapshot(target);
                    const hasSponsoredLabel = normalizedText.includes("sponsored");
                    const hasAdSelector = Boolean(
                        target.querySelector("[data-e2e*='ad']")
                        || target.querySelector("[aria-label*='Sponsored' i]")
                        || target.querySelector("[class*='sponsored' i]"));
                    const hasCommerceCta = adCtaPatterns.some(pattern => normalizedText.includes(pattern));

                    if (!hasSponsoredLabel && !hasAdSelector && !hasCommerceCta) {
                        return false;
                    }

                    const adKey = getActiveReelId(video)
                        || `${normalizedText.slice(0, 120)}|${target.getBoundingClientRect().top}`;
                    const now = Date.now();
                    if (adKey === lastAdSkipKey && lastAdSkipDirection === 1 && (now - lastAdSkipAt) < AdSkipCooldownMs) {
                        return true;
                    }

                    lastAdSkipKey = adKey;
                    lastAdSkipAt = now;
                    lastAdSkipDirection = 1;
                    showToast("Skipped ad");

                    window.setTimeout(() => {
                        const activeVideo = getActiveVideo();
                        if (!activeVideo) {
                            return;
                        }

                        const activeTarget = getScrollTarget(activeVideo);
                        if (!activeTarget) {
                            return;
                        }

                        const activeSnapshot = getNormalizedTextSnapshot(activeTarget);
                        if (activeSnapshot.includes("sponsored") || activeSnapshot.includes("shop now")) {
                            scrollByDirection(1);
                        }
                    }, 0);

                    return true;
                }

                function maybeSkipInstagramAd(video) {
                    if (getPlatform() !== "instagram" || !video) {
                        return false;
                    }

                    const target = getScrollTarget(video);
                    if (!target) {
                        return false;
                    }

                    const referenceRect = video.getBoundingClientRect();
                    const normalizedText = getNormalizedTextSnapshot(target);
                    const hasAdLabel = hasExactText(target, "ad", referenceRect);
                    const hasSponsoredLabel = hasExactText(target, "sponsored", referenceRect);
                    const hasAdSelector = hasSelectorInScope(
                        target,
                        "[aria-label*='Sponsored' i], [class*='sponsored' i], [href*='ads' i]",
                        referenceRect);
                    const hasCommerceButton = hasButtonLabelInScope(target, adCtaPatterns, referenceRect);

                    if (!(hasAdLabel || ((hasSponsoredLabel || hasAdSelector) && hasCommerceButton))) {
                        return false;
                    }

                    const adKey = getActiveReelId(video)
                        || `ig-ad|${normalizedText.slice(0, 120)}`;
                    const now = Date.now();
                    const skipDirection = lastRequestedScrollDirection < 0 ? -1 : 1;
                    if (adKey === lastAdSkipKey
                        && lastAdSkipDirection === skipDirection
                        && (now - lastAdSkipAt) < AdSkipCooldownMs) {
                        return true;
                    }

                    lastAdSkipKey = adKey;
                    lastAdSkipAt = now;
                    lastAdSkipDirection = skipDirection;
                    showToast("Skipped Instagram ad");

                    window.setTimeout(() => {
                        const activeVideo = getActiveVideo();
                        if (!activeVideo) {
                            return;
                        }

                        const activeTarget = getScrollTarget(activeVideo);
                        if (!activeTarget) {
                            return;
                        }

                        const activeReferenceRect = activeVideo.getBoundingClientRect();
                        const activeHasAdLabel = hasExactText(activeTarget, "ad", activeReferenceRect);
                        const activeHasSponsoredLabel = hasExactText(activeTarget, "sponsored", activeReferenceRect);
                        const activeHasAdSelector = hasSelectorInScope(
                            activeTarget,
                            "[aria-label*='Sponsored' i], [class*='sponsored' i], [href*='ads' i]",
                            activeReferenceRect);
                        const activeHasCommerceButton = hasButtonLabelInScope(activeTarget, adCtaPatterns, activeReferenceRect);

                        if (activeHasAdLabel || ((activeHasSponsoredLabel || activeHasAdSelector) && activeHasCommerceButton)) {
                            scrollByDirection(skipDirection);
                        }
                    }, 0);

                    return true;
                }

                function ensureToast() {
                    if (toast.host) {
                        return toast.host;
                    }

                    const root = document.body || document.documentElement;
                    if (!root) {
                        return null;
                    }

                    const host = document.createElement("div");
                    host.id = "showmereels-skip-toast";
                    host.style.position = "fixed";
                    host.style.top = "18px";
                    host.style.left = "50%";
                    host.style.transform = "translate(-50%, -10px)";
                    host.style.padding = "10px 14px";
                    host.style.borderRadius = "999px";
                    host.style.background = "rgba(8, 12, 18, 0.88)";
                    host.style.border = "1px solid rgba(255, 255, 255, 0.16)";
                    host.style.color = "#F5F7FA";
                    host.style.fontSize = "12px";
                    host.style.fontWeight = "600";
                    host.style.letterSpacing = "0.02em";
                    host.style.zIndex = "2147483647";
                    host.style.opacity = "0";
                    host.style.pointerEvents = "none";
                    host.style.transition = "opacity 140ms ease, transform 140ms ease";

                    root.appendChild(host);
                    toast.host = host;
                    return host;
                }

                function showToast(message) {
                    const host = ensureToast();
                    if (!host) {
                        return;
                    }

                    host.textContent = message;
                    host.style.opacity = "1";
                    host.style.transform = "translate(-50%, 0)";

                    if (toast.hideTimeoutId) {
                        window.clearTimeout(toast.hideTimeoutId);
                    }

                    toast.hideTimeoutId = window.setTimeout(() => {
                        host.style.opacity = "0";
                        host.style.transform = "translate(-50%, -10px)";
                    }, 1200);
                }

                function ensureIgnoreButton() {
                    if (ignoreButton.host) {
                        return ignoreButton.host;
                    }

                    const root = document.body || document.documentElement;
                    if (!root) {
                        return null;
                    }

                    const button = document.createElement("button");
                    button.id = "showmereels-tiktok-ignore-button";
                    button.type = "button";
                    button.textContent = "Ignore";
                    button.style.position = "fixed";
                    button.style.right = "16px";
                    button.style.top = "120px";
                    button.style.minWidth = "84px";
                    button.style.height = "38px";
                    button.style.padding = "0 14px";
                    button.style.borderRadius = "999px";
                    button.style.border = "1px solid rgba(255, 255, 255, 0.14)";
                    button.style.background = "rgba(24, 29, 37, 0.92)";
                    button.style.color = "#f5f7fa";
                    button.style.fontSize = "13px";
                    button.style.fontWeight = "700";
                    button.style.letterSpacing = "0.02em";
                    button.style.boxShadow = "0 10px 24px rgba(0, 0, 0, 0.35)";
                    button.style.backdropFilter = "blur(8px)";
                    button.style.cursor = "pointer";
                    button.style.zIndex = "2147483647";
                    button.style.display = "none";

                    button.addEventListener("click", event => {
                        event.preventDefault();
                        event.stopPropagation();
                        ignoreCurrentTikTokVideo();
                    });

                    root.appendChild(button);
                    ignoreButton.host = button;
                    return button;
                }

                function hideIgnoreButton() {
                    if (!ignoreButton.host) {
                        return;
                    }

                    ignoreButton.host.style.display = "none";
                }

                function syncTikTokIgnoreButton(video) {
                    if (getPlatform() !== "tiktok" || !host.isActive || !video) {
                        hideIgnoreButton();
                        return;
                    }

                    const button = ensureIgnoreButton();
                    if (!button) {
                        return;
                    }

                    const ignoreKeys = getTikTokIgnoreKeys(video);
                    const rect = video.getBoundingClientRect();
                    const top = Math.max(96, Math.round(rect.top + (rect.height * 0.34)));
                    button.style.top = `${top}px`;
                    button.style.display = "block";
                    button.dataset.videoKeys = JSON.stringify(ignoreKeys);

                    const isIgnored = ignoreKeys.some(ignoreKey => ignoredTikTokVideoIds.has(ignoreKey));
                    button.textContent = isIgnored ? "Ignored" : "Ignore";
                    button.disabled = isIgnored;
                    button.style.opacity = isIgnored ? "0.72" : "1";
                    button.style.cursor = isIgnored ? "default" : "pointer";
                }

                function ignoreCurrentTikTokVideo() {
                    if (getPlatform() !== "tiktok") {
                        return false;
                    }

                    const video = getActiveVideo();
                    if (!video) {
                        return false;
                    }

                    const ignoreKeys = getTikTokIgnoreKeys(video);
                    if (!ignoreKeys.length) {
                        showToast("Couldn't identify this video");
                        return false;
                    }

                    if (ignoreKeys.some(ignoreKey => ignoredTikTokVideoIds.has(ignoreKey))) {
                        showToast("Already ignoring this video");
                        syncTikTokIgnoreButton(video);
                        return true;
                    }

                    for (const ignoreKey of ignoreKeys) {
                        ignoredTikTokVideoIds.add(ignoreKey);
                    }

                    syncTikTokIgnoreButton(video);
                    showToast("Ignoring this video");

                    try {
                        window.chrome?.webview?.postMessage({
                            type: "ignoreTikTokVideo",
                            videoIds: ignoreKeys
                        });
                    } catch {
                    }

                    window.setTimeout(() => scrollByDirection(1), 0);
                    return true;
                }

                function isVisibleOverlay(element) {
                    if (!(element instanceof HTMLElement)) {
                        return false;
                    }

                    const rect = element.getBoundingClientRect();
                    return rect.width > 120
                        && rect.height > 120
                        && rect.bottom > 0
                        && rect.right > 0
                        && rect.top < window.innerHeight
                        && rect.left < window.innerWidth;
                }

                function isTikTokInteractionOverlayOpen() {
                    if (getPlatform() !== "tiktok") {
                        return false;
                    }

                    const selectors = [
                        "[data-e2e='comment-panel']",
                        "[data-e2e='comment-modal']",
                        "[data-e2e='browse-comment-list']",
                        "[role='dialog'][aria-modal='true']"
                    ];

                    return selectors.some(selector =>
                        Array.from(document.querySelectorAll(selector)).some(candidate => isVisibleOverlay(candidate)));
                }

                function maybeSkipSeenReel(video) {
                    if (getPlatform() === "tiktok") {
                        return false;
                    }

                    const reelId = getActiveReelId(video);
                    if (!reelId) {
                        return false;
                    }

                    const seenBefore = seenReelIds.has(reelId);
                    if (reelId !== lastActiveReelId) {
                        lastActiveReelId = reelId;
                        rememberSeenReelId(reelId);
                    }

                    if (!state.skipSeenReelsEnabled || !seenBefore) {
                        return false;
                    }

                    const now = Date.now();
                    const skipDirection = lastRequestedScrollDirection < 0 ? -1 : 1;
                    if (skipDirection < 0) {
                        return false;
                    }

                    if (reelId === lastDuplicateSkipId
                        && lastDuplicateSkipDirection === skipDirection
                        && (now - lastDuplicateSkipAt) < DuplicateSkipCooldownMs) {
                        return true;
                    }

                    lastDuplicateSkipId = reelId;
                    lastDuplicateSkipDirection = skipDirection;
                    lastDuplicateSkipAt = now;
                    showToast(`Skipped seen video ${reelId}`);

                    window.setTimeout(() => {
                        const activeVideo = getActiveVideo();
                        const activeReelId = getActiveReelId(activeVideo);
                        if (!activeVideo || activeReelId !== reelId) {
                            return;
                        }

                        scrollByDirection(skipDirection);
                    }, 0);

                    return true;
                }

                function maybeSkipIgnoredTikTokVideo(video) {
                    syncTikTokIgnoreButton(video);

                    if (getPlatform() !== "tiktok" || !video) {
                        return false;
                    }

                    if (isTikTokInteractionOverlayOpen()) {
                        return false;
                    }

                    const ignoreKeys = getTikTokIgnoreKeys(video);
                    const matchingIgnoreKey = ignoreKeys.find(ignoreKey => ignoredTikTokVideoIds.has(ignoreKey));
                    if (!matchingIgnoreKey) {
                        return false;
                    }

                    const now = Date.now();
                    if (matchingIgnoreKey === lastIgnoredTikTokVideoId
                        && (now - lastIgnoredTikTokSkipAt) < IgnoredTikTokSkipCooldownMs) {
                        return true;
                    }

                    lastIgnoredTikTokVideoId = matchingIgnoreKey;
                    lastIgnoredTikTokSkipAt = now;
                    showToast("Skipped ignored video");

                    window.setTimeout(() => {
                        const activeVideo = getActiveVideo();
                        if (!activeVideo) {
                            return;
                        }

                        const activeIgnoreKeys = getTikTokIgnoreKeys(activeVideo);
                        if (!activeIgnoreKeys.includes(matchingIgnoreKey)) {
                            return;
                        }

                        scrollByDirection(1);
                    }, 0);

                    return true;
                }

                function ensureSeekBar() {
                    if (seekBar.host && seekBar.input) {
                        return seekBar;
                    }

                    const root = document.body || document.documentElement;
                    if (!root) {
                        return null;
                    }

                    const host = document.createElement("div");
                    host.id = "showmereels-seekbar-host";
                    host.style.position = "fixed";
                    host.style.zIndex = "2147483647";
                    host.style.display = "none";
                    host.style.pointerEvents = "none";
                    host.style.opacity = "0";
                    host.style.boxSizing = "border-box";
                    host.style.padding = "10px 12px";
                    host.style.borderRadius = "999px";
                    host.style.background = "rgba(8, 12, 18, 0.72)";
                    host.style.border = "1px solid rgba(255, 255, 255, 0.14)";
                    host.style.backdropFilter = "blur(8px)";
                    host.style.filter = "drop-shadow(0 10px 24px rgba(0, 0, 0, 0.35))";
                    host.style.transform = "translateY(-6px)";
                    host.style.transition = "opacity 120ms ease, transform 120ms ease";

                    const input = document.createElement("input");
                    input.type = "range";
                    input.min = "0";
                    input.max = "1";
                    input.step = "0.01";
                    input.value = "0";
                    input.disabled = true;
                    input.style.width = "100%";
                    input.style.margin = "0";
                    input.style.pointerEvents = "auto";
                    input.style.accentColor = "#f5f7fa";
                    input.style.cursor = "pointer";

                    const stopPropagation = event => {
                        event.stopPropagation();
                    };

                    input.addEventListener("pointerdown", event => {
                        seekBar.isScrubbing = true;
                        stopPropagation(event);
                    });
                    input.addEventListener("mousedown", stopPropagation);
                    input.addEventListener("click", stopPropagation);
                    input.addEventListener("keydown", stopPropagation);
                    input.addEventListener("wheel", event => {
                        event.preventDefault();
                        event.stopPropagation();
                    }, { passive: false });
                    input.addEventListener("pointerup", () => {
                        seekBar.isScrubbing = false;
                    });
                    input.addEventListener("change", () => {
                        seekBar.isScrubbing = false;
                    });
                    input.addEventListener("input", () => {
                        const video = seekBar.activeVideo;
                        if (!video || !Number.isFinite(video.duration) || video.duration <= 0) {
                            return;
                        }

                        const targetTime = clamp(Number(input.value) || 0, 0, video.duration);
                        if (Math.abs(video.currentTime - targetTime) > 0.05) {
                            video.currentTime = targetTime;
                        }
                    });

                    window.addEventListener("pointerup", () => {
                        seekBar.isScrubbing = false;
                    });

                    host.appendChild(input);
                    root.appendChild(host);

                    host.addEventListener("mouseenter", () => scheduleApply(true));
                    host.addEventListener("mouseleave", () => scheduleApply(true));

                    seekBar.host = host;
                    seekBar.input = input;
                    return seekBar;
                }

                function hideSeekBar() {
                    if (!seekBar.host || !seekBar.input) {
                        return;
                    }

                    seekBar.host.style.display = "none";
                    seekBar.host.style.opacity = "0";
                    seekBar.host.style.pointerEvents = "none";
                    seekBar.host.style.transform = "translateY(-6px)";
                    seekBar.input.disabled = true;
                    seekBar.activeVideo = null;
                }

                function syncSeekBar(video) {
                    if (!state.seekBarEnabled || !video) {
                        hideSeekBar();
                        return;
                    }

                    const ui = ensureSeekBar();
                    if (!ui) {
                        return;
                    }

                    const rect = video.getBoundingClientRect();
                    if (rect.width <= 0 || rect.height <= 0 || rect.bottom <= 0 || rect.top >= window.innerHeight) {
                        ui.host.style.display = "none";
                        ui.input.disabled = true;
                        ui.activeVideo = null;
                        return;
                    }

                    const width = Math.max(160, Math.min(Math.round(rect.width - 36), Math.max(160, window.innerWidth - 24)));
                    const left = Math.round(clamp(rect.left + 18, 12, Math.max(12, window.innerWidth - width - 12)));
                    const top = Math.round(Math.max(12, rect.top + 16));

                    ui.host.style.left = `${left}px`;
                    ui.host.style.top = `${top}px`;
                    ui.host.style.width = `${width}px`;
                    ui.host.style.display = "block";
                    ui.activeVideo = video;

                    const duration = Number.isFinite(video.duration) && video.duration > 0 ? video.duration : 0;
                    ui.input.disabled = !duration;
                    ui.input.max = duration ? String(duration) : "1";

                    if (!seekBar.isScrubbing) {
                        ui.input.value = duration ? String(clamp(video.currentTime, 0, duration)) : "0";
                    }

                    const isHovered = seekBar.isScrubbing || ui.host.matches(":hover") || video.matches(":hover");
                    ui.host.style.opacity = isHovered ? "1" : "0";
                    ui.host.style.pointerEvents = isHovered ? "auto" : "none";
                    ui.host.style.transform = isHovered ? "translateY(0)" : "translateY(-6px)";
                }

                function getAudioTargetVideos() {
                    const visibleVideos = getVisibleVideos();
                    if (visibleVideos.length) {
                        return visibleVideos;
                    }

                    const activeVideo = getActiveVideo();
                    return activeVideo ? [activeVideo] : [];
                }

                function applyAudioSettingsToVideo(video) {
                    if (!video) {
                        return;
                    }

                    const normalizedVolumePercent = clamp(Number(state.volumePercent) || 0, 0, 100);
                    const targetVolume = normalizedVolumePercent / 100;

                    if (Math.abs(video.volume - targetVolume) > 0.01) {
                        video.volume = targetVolume;
                    }

                    if (normalizedVolumePercent > 0) {
                        video.defaultMuted = false;
                        video.removeAttribute("muted");
                        if (video.muted) {
                            video.muted = false;
                        }
                    } else {
                        video.defaultMuted = true;
                        video.setAttribute("muted", "");
                        if (!video.muted) {
                            video.muted = true;
                        }
                    }
                }

                function applyAudioSettingsToTargetVideos() {
                    const targetVideos = getAudioTargetVideos();
                    for (const video of targetVideos) {
                        applyAudioSettingsToVideo(video);
                    }
                }

                function applySettingsToVideo(video) {
                    if (!video) {
                        hideSeekBar();
                        return false;
                    }

                    const normalizedPlaybackSpeed = clamp(Number(state.playbackSpeed) || 1, 0.25, 10);

                    if (video.defaultPlaybackRate !== normalizedPlaybackSpeed) {
                        video.defaultPlaybackRate = normalizedPlaybackSpeed;
                    }

                    if (video.playbackRate !== normalizedPlaybackSpeed) {
                        video.playbackRate = normalizedPlaybackSpeed;
                    }

                    applyAudioSettingsToVideo(video);

                    video.controls = false;
                    video.removeAttribute("controls");
                    syncSeekBar(video);

                    return !video.paused && !video.ended;
                }

                function attachVideoHooks(video) {
                    if (!video || video.dataset.showMeReelsHooked === "true") {
                        return;
                    }

                    video.dataset.showMeReelsHooked = "true";

                    const enforce = () => {
                        if (!host.isActive) {
                            return;
                        }

                        window.requestAnimationFrame(() => applySettingsToVideo(video));
                    };
                    video.addEventListener("loadeddata", enforce);
                    video.addEventListener("loadedmetadata", enforce);
                    video.addEventListener("mouseenter", enforce);
                    video.addEventListener("mouseleave", enforce);
                    video.addEventListener("play", enforce);
                    video.addEventListener("pause", enforce);
                    video.addEventListener("durationchange", enforce);
                    video.addEventListener("seeking", enforce);
                    video.addEventListener("volumechange", enforce);
                }

                function apply() {
                    const videos = getVideos();
                    for (const video of videos) {
                        attachVideoHooks(video);
                    }

                    applyAudioSettingsToTargetVideos();

                    const activeVideo = getActiveVideo();
                    if (maybeSkipTikTokAd(activeVideo)) {
                        return false;
                    }

                    if (maybeSkipInstagramAd(activeVideo)) {
                        return false;
                    }

                    if (maybeSkipSeenReel(activeVideo)) {
                        return false;
                    }

                    if (maybeSkipIgnoredTikTokVideo(activeVideo)) {
                        return false;
                    }

                    const isPlaying = applySettingsToVideo(activeVideo);
                    return isPlaying;
                }

                function setSettings(settings) {
                    state.platform = String(settings.platform || detectPlatform()).toLowerCase();
                    state.playbackSpeed = clamp(Number(settings.playbackSpeed) || 1, 0.25, 10);
                    state.volumePercent = clamp(Number(settings.volumePercent) || 0, 0, 100);
                    state.seekBarEnabled = Boolean(settings.seekBarEnabled);
                    state.skipSeenReelsEnabled = state.platform !== "tiktok" && Boolean(settings.skipSeenReelsEnabled);
                    syncIgnoredTikTokVideoIds(settings.ignoredTikTokVideoIds);
                    startActiveAudioEnforcement();
                    return apply();
                }

                function pauseAndMute() {
                    const video = getActiveVideo();
                    const wasPlaying = Boolean(video) && !video.paused && !video.ended;

                    if (!video) {
                        return false;
                    }

                    video.pause();
                    video.defaultMuted = true;
                    video.muted = true;
                    video.volume = 0;
                    syncSeekBar(video);
                    return wasPlaying;
                }

                function resume(shouldResume, volumePercent) {
                    state.volumePercent = clamp(Number(volumePercent) || 0, 0, 100);
                    const video = getActiveVideo();

                    if (!video) {
                        return false;
                    }

                    applySettingsToVideo(video);

                    if (shouldResume) {
                        const playback = video.play();
                        if (playback && typeof playback.catch === "function") {
                            playback.catch(() => {});
                        }
                    }

                    return true;
                }

                function togglePlayPause() {
                    const video = getActiveVideo();
                    if (!video) {
                        return false;
                    }

                    if (video.paused || video.ended) {
                        applySettingsToVideo(video);
                        const playback = video.play();
                        if (playback && typeof playback.catch === "function") {
                            playback.catch(() => {});
                        }

                        return true;
                    }

                    video.pause();
                    syncSeekBar(video);
                    return true;
                }

                function scrollTikTokByDirection(direction) {
                    const activeVideo = getActiveVideo();
                    if (!activeVideo) {
                        return false;
                    }

                    const activeTarget = getScrollTarget(activeVideo);
                    if (!activeTarget) {
                        return false;
                    }

                    const scrollContainer = getScrollableContainer(activeTarget);
                    const useWindowScroller = !scrollContainer
                        || scrollContainer === document.scrollingElement
                        || scrollContainer === document.documentElement
                        || scrollContainer === document.body;
                    const feedItems = getTikTokFeedItems(useWindowScroller ? null : scrollContainer);
                    const activeIndex = feedItems.findIndex(item => item === activeTarget || item.contains(activeTarget) || activeTarget.contains(item));
                    if (activeIndex >= 0) {
                        const targetIndex = clamp(activeIndex + direction, 0, feedItems.length - 1);
                        if (targetIndex !== activeIndex) {
                            const currentRect = activeTarget.getBoundingClientRect();
                            const targetRect = feedItems[targetIndex].getBoundingClientRect();
                            const deltaY = targetRect.top - currentRect.top;

                            if (useWindowScroller) {
                                window.scrollBy({
                                    top: deltaY,
                                    left: 0,
                                    behavior: "auto"
                                });
                            } else {
                                scrollContainer.scrollBy({
                                    top: deltaY,
                                    left: 0,
                                    behavior: "auto"
                                });
                            }

                            schedulePostScrollApply();
                            return true;
                        }
                    }

                    const activeRect = activeTarget.getBoundingClientRect();
                    const viewportHeight = useWindowScroller
                        ? window.innerHeight
                        : Math.max(scrollContainer.clientHeight || 0, 0);
                    const deltaY = direction * Math.max(activeRect.height || 0, viewportHeight * 0.92, 420);

                    if (useWindowScroller) {
                        window.scrollBy({
                            top: deltaY,
                            left: 0,
                            behavior: "auto"
                        });
                    } else {
                        scrollContainer.scrollBy({
                            top: deltaY,
                            left: 0,
                            behavior: "auto"
                        });
                    }

                    schedulePostScrollApply();
                    return true;
                }

                function scrollInstagramByDirection(direction) {
                    const activeVideo = getActiveVideo();
                    if (!activeVideo) {
                        return false;
                    }

                    const activeTarget = getScrollTarget(activeVideo);
                    if (!activeTarget) {
                        return false;
                    }

                    const activeRect = activeTarget.getBoundingClientRect();

                    if (isInstagramReelsSurface()) {
                        const deltaY = direction * Math.max(activeRect.height || 0, window.innerHeight * 0.92, 420);
                        const moved = tryScrollBestContainer([activeTarget, activeVideo], deltaY);
                        if (moved) {
                            schedulePostScrollApply();
                        }

                        return moved;
                    }

                    const feedItems = getInstagramFeedItems();

                    let scrollTarget = null;
                    if (direction > 0) {
                        scrollTarget = feedItems.find(item => {
                            const candidateRect = item.getBoundingClientRect();
                            return candidateRect.top > activeRect.top + 16;
                        }) ?? null;
                    } else {
                        const reversedFeedItems = [...feedItems].reverse();
                        scrollTarget = reversedFeedItems.find(item => {
                            const candidateRect = item.getBoundingClientRect();
                            return candidateRect.top < activeRect.top - 16;
                        }) ?? null;
                    }

                    if (scrollTarget) {
                        const scrollContainer = getScrollableContainer(scrollTarget);
                        const useWindowScroller = !scrollContainer
                            || scrollContainer === document.scrollingElement
                            || scrollContainer === document.documentElement
                            || scrollContainer === document.body;

                        if (useWindowScroller) {
                            const targetTop = Math.max(0, window.scrollY + scrollTarget.getBoundingClientRect().top);
                            window.scrollTo({
                                top: targetTop,
                                left: 0,
                                behavior: "auto"
                            });
                        } else {
                            const containerRect = scrollContainer.getBoundingClientRect();
                            const targetTop = Math.max(0, scrollContainer.scrollTop + (scrollTarget.getBoundingClientRect().top - containerRect.top));
                            scrollContainer.scrollTop = targetTop;
                        }

                        schedulePostScrollApply();
                        return true;
                    }

                    const scrollContainer = getScrollableContainer(activeTarget);
                    const useWindowScroller = !scrollContainer
                        || scrollContainer === document.scrollingElement
                        || scrollContainer === document.documentElement
                        || scrollContainer === document.body;
                    const fallbackHeight = activeRect.height || 0;
                    const fallbackDelta = direction * Math.max(
                        fallbackHeight,
                        useWindowScroller ? window.innerHeight * 0.92 : scrollContainer.clientHeight * 0.92,
                        420);

                    if (useWindowScroller) {
                        window.scrollBy({
                            top: fallbackDelta,
                            left: 0,
                            behavior: "auto"
                        });
                    } else {
                        scrollContainer.scrollBy({
                            top: fallbackDelta,
                            left: 0,
                            behavior: "auto"
                        });
                    }

                    schedulePostScrollApply();
                    return true;
                }

                function scrollByDirection(direction) {
                    const normalizedDirection = direction < 0 ? -1 : 1;
                    lastRequestedScrollDirection = normalizedDirection;

                    if (getPlatform() === "tiktok") {
                        return scrollTikTokByDirection(normalizedDirection);
                    }

                    return scrollInstagramByDirection(normalizedDirection);
                }

                function handleDirectionalInput(direction, event) {
                    if (scrollLocked) {
                        event?.preventDefault();
                        return;
                    }

                    scrollLocked = true;
                    const moved = scrollByDirection(direction);
                    if (moved) {
                        event?.preventDefault();
                    } else {
                        scrollLocked = false;
                        return;
                    }

                    window.setTimeout(() => {
                        scrollLocked = false;
                    }, 140);
                }

                function handleWheel(event) {
                    if (!host.isActive || !event.isTrusted || Math.abs(event.deltaY) < 12 || event.ctrlKey || event.metaKey) {
                        return;
                    }

                    handleDirectionalInput(event.deltaY > 0 ? 1 : -1, event);
                }

                function handleKeydown(event) {
                    if (!host.isActive || event.defaultPrevented || event.altKey || event.ctrlKey || event.metaKey || isEditableTarget(event.target)) {
                        return;
                    }

                    const normalizedKey = String(event.key || "").toLowerCase();

                    if (event.key === "ArrowDown" || event.key === "PageDown" || normalizedKey === "s") {
                        handleDirectionalInput(1, event);
                    } else if (event.key === "ArrowUp" || event.key === "PageUp" || normalizedKey === "w") {
                        handleDirectionalInput(-1, event);
                    } else if (event.code === "Space" || normalizedKey === " ") {
                        event.preventDefault();
                        togglePlayPause();
                    }
                }

                function enforceActiveVideoAudio() {
                    if (!host.isActive) {
                        return;
                    }

                    applyAudioSettingsToTargetVideos();
                }

                function stopActiveAudioEnforcement() {
                    if (!host.activeAudioTimerId) {
                        return;
                    }

                    window.clearInterval(host.activeAudioTimerId);
                    host.activeAudioTimerId = 0;
                }

                function startActiveAudioEnforcement() {
                    stopActiveAudioEnforcement();
                    host.activeAudioTimerId = window.setInterval(() => enforceActiveVideoAudio(), ActiveAudioEnforcementIntervalMs);
                    enforceActiveVideoAudio();
                }

                function stopMaintenance() {
                    if (!host.maintenanceTimerId) {
                        return;
                    }

                    window.clearInterval(host.maintenanceTimerId);
                    host.maintenanceTimerId = 0;
                }

                function startMaintenance() {
                    stopMaintenance();
                    host.maintenanceTimerId = window.setInterval(() => scheduleApply(), ActiveMaintenanceIntervalMs);
                }

                function disconnectObserver() {
                    if (!host.observer) {
                        return;
                    }

                    host.observer.disconnect();
                }

                function connectObserver() {
                    const target = document.documentElement || document.body;
                    if (!target) {
                        return false;
                    }

                    if (!host.observer) {
                        host.observer = new MutationObserver(() => scheduleApply());
                    }

                    disconnectObserver();
                    host.observer.observe(target, {
                        childList: true,
                        subtree: true
                    });
                    return true;
                }

                function setHostActive(isActive) {
                    const normalizedValue = Boolean(isActive);
                    if (host.isActive === normalizedValue) {
                        if (normalizedValue) {
                            startActiveAudioEnforcement();
                            scheduleApply(true);
                        } else {
                            stopActiveAudioEnforcement();
                            hideSeekBar();
                            hideIgnoreButton();
                        }

                        return true;
                    }

                    host.isActive = normalizedValue;

                    if (host.isActive) {
                        connectObserver();
                        startActiveAudioEnforcement();
                        startMaintenance();
                        scheduleApply(true);
                    } else {
                        stopActiveAudioEnforcement();
                        stopMaintenance();
                        disconnectObserver();
                        hideSeekBar();
                        hideIgnoreButton();
                    }

                    return true;
                }

                function startObservers() {
                    if (document.documentElement) {
                        document.documentElement.style.scrollBehavior = "auto";
                    }

                    if (document.body) {
                        document.body.style.scrollBehavior = "auto";
                    }

                    if (!connectObserver()) {
                        window.addEventListener("DOMContentLoaded", startObservers, { once: true });
                        return;
                    }

                    document.addEventListener("wheel", handleWheel, {
                        passive: false,
                        capture: true
                    });
                    document.addEventListener("keydown", handleKeydown, true);
                    document.addEventListener("scroll", () => scheduleApply(), true);
                    window.addEventListener("resize", () => scheduleApply());
                    startActiveAudioEnforcement();
                    startMaintenance();
                    scheduleApply(true);
                }

                window.showMeReels = {
                    apply,
                    pauseAndMute,
                    resume,
                    scrollByDirection,
                    setHostActive,
                    setSettings,
                    togglePlayPause
                };

                startObservers();
            })();
            """;
    }

    public string BuildPauseAndMuteScript()
    {
        return "window.showMeReels ? window.showMeReels.pauseAndMute() : false;";
    }

    public string BuildResumeScript(AppSettings settings, bool shouldResume)
    {
        string resumeFlag = shouldResume ? "true" : "false";
        return $"window.showMeReels ? window.showMeReels.resume({resumeFlag}, {settings.VolumePercent}) : false;";
    }

    public string BuildScrollScript(int direction)
    {
        int normalizedDirection = direction < 0 ? -1 : 1;
        return $"window.showMeReels ? window.showMeReels.scrollByDirection({normalizedDirection}) : false;";
    }

    public string BuildSetHostActiveScript(bool isActive)
    {
        string normalizedFlag = isActive ? "true" : "false";
        return $"window.showMeReels ? window.showMeReels.setHostActive({normalizedFlag}) : false;";
    }

    public string BuildTogglePlayPauseScript()
    {
        return "window.showMeReels ? window.showMeReels.togglePlayPause() : false;";
    }

    public bool ParseBooleanResult(string scriptResult)
    {
        if (string.IsNullOrWhiteSpace(scriptResult))
        {
            return false;
        }

        string normalized = scriptResult.Trim().Trim('"');
        return bool.TryParse(normalized, out bool parsed) && parsed;
    }

    private sealed record ScriptSettings(double PlaybackSpeed, int VolumePercent, bool SeekBarEnabled, string Platform, bool SkipSeenReelsEnabled, IReadOnlyList<string> IgnoredTikTokVideoIds);
}
