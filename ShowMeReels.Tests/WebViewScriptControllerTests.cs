using ShowMeReels.App.Models;
using ShowMeReels.App.Services;

namespace ShowMeReels.Tests;

public sealed class WebViewScriptControllerTests
{
    [Fact]
    public void BuildBootstrapScript_RegistersControllerFunctions()
    {
        WebViewScriptController controller = new();

        string script = controller.BuildBootstrapScript();

        Assert.Contains("window.showMeReels", script);
        Assert.Contains("pauseAndMute", script);
        Assert.Contains("setSettings", script);
        Assert.Contains("resume", script);
        Assert.Contains("setHostActive", script);
        Assert.Contains("togglePlayPause", script);
        Assert.Contains("document.addEventListener(\"wheel\"", script);
        Assert.Contains("window.clearInterval(host.maintenanceTimerId)", script);
        Assert.Contains("host.observer.disconnect()", script);
        Assert.Contains("video.defaultMuted = false", script);
        Assert.Contains("normalizedKey === \"s\"", script);
        Assert.Contains("normalizedKey === \"w\"", script);
        Assert.Contains("normalizedKey === \" \"", script);
        Assert.Contains("showmereels-seekbar-host", script);
        Assert.Contains("input.type = \"range\"", script);
        Assert.Contains("video.matches(\":hover\")", script);
        Assert.Contains("ui.host.style.opacity = isHovered ? \"1\" : \"0\"", script);
        Assert.Contains("sessionStorage", script);
        Assert.Contains("showMeReels.seenReelIds.v1", script);
        Assert.Contains("showmereels-tiktok-ignore-button", script);
        Assert.Contains("function syncIgnoredTikTokVideoIds(videoIds)", script);
        Assert.Contains("function collectTikTokCandidates(element, candidates)", script);
        Assert.Contains("function getTikTokIgnoreKeys(video)", script);
        Assert.Contains("function getTikTokFingerprintKey(video)", script);
        Assert.Contains("function collectInstagramCandidates(element, candidates)", script);
        Assert.DoesNotContain("function getCurrentPageReelId()", script);
        Assert.Contains("function getVisibleInstagramReelIds(video)", script);
        Assert.Contains("function isInsideInstagramInteractionOverlay(element)", script);
        Assert.DoesNotContain("function normalizeStableInstagramAssetCandidate(candidate)", script);
        Assert.DoesNotContain("function getInstagramStableAssetKey(video)", script);
        Assert.DoesNotContain("ig-asset:", script);
        Assert.Contains("if (getPlatform() === \"tiktok\" && window.location?.href)", script);
        Assert.Contains("function getInstagramStrongFingerprintKey(video)", script);
        Assert.Contains("return `ig-fp:${hashString(fingerprintSource)}`;", script);
        Assert.Contains("fingerprintText.length < 40 || distinctiveWords.length < 5", script);
        Assert.Contains("function schedulePostScrollApply()", script);
        Assert.Contains("window.setTimeout(() => scheduleApply(true), 60)", script);
        Assert.Contains("window.setTimeout(() => scheduleApply(true), 160)", script);
        Assert.Contains("data-media-id", script);
        Assert.Contains("return `ig:${candidateText}`;", script);
        Assert.Contains("function ignoreCurrentTikTokVideo()", script);
        Assert.Contains("function isTikTokInteractionOverlayOpen()", script);
        Assert.Contains("function maybeSkipIgnoredTikTokVideo(video)", script);
        Assert.Contains("window.chrome?.webview?.postMessage", script);
        Assert.Contains("skipSeenReelsEnabled", script);
        Assert.Contains("Skipped seen video", script);
        Assert.Contains("function isInstagramInteractionOverlayOpen()", script);
        Assert.Contains("const overlayOpen = isInstagramInteractionOverlayOpen();", script);
        Assert.Contains("const interactionSuppressed = isSeenSkipSuppressedForVideo(video);", script);
        Assert.Contains("if (overlayOpen || interactionSuppressed)", script);
        Assert.Contains("SeenSkipInteractionSuppressionMs", script);
        Assert.Contains("SeenReelDiagnosticThrottleMs", script);
        Assert.Contains("function postSeenReelDiagnostic(eventName, details = {}, throttleMs = SeenReelDiagnosticThrottleMs)", script);
        Assert.Contains("type: \"seenReelDiagnostic\"", script);
        Assert.Contains("active-reel-id-not-found", script);
        Assert.Contains("same-active-reel", script);
        Assert.Contains("seen-reel-reappeared", script);
        Assert.Contains("window.chrome?.webview?.postMessage(payload)", script);
        Assert.Contains("function suppressSeenSkipForCurrentInteraction()", script);
        Assert.Contains("document.addEventListener(\"pointerdown\", suppressSeenSkipForCurrentInteraction, true)", script);
        Assert.Contains("const activeReelChanged = reelId !== lastActiveReelId;", script);
        Assert.Contains("if (!seenBefore)", script);
        Assert.Contains("if (!activeReelChanged)", script);
        Assert.Contains("if (maybeSkipSeenReel(activeVideo))", script);
        Assert.Contains("lastDuplicateSkipDirection === skipDirection", script);
        Assert.Contains("if (skipDirection < 0)", script);
        Assert.Contains("scrollByDirection(skipDirection)", script);
        Assert.Contains("Skipped ignored video", script);
        Assert.Contains("Ignoring this video", script);
        Assert.Contains("if (getPlatform() === \"tiktok\")", script);
        Assert.Contains("tiktok.com", script);
        Assert.Contains("\\/video\\/([^/?#]+)", script);
        Assert.Contains("videoId[\"':\\s=]+", script);
        Assert.Contains("asset:${hashString(normalizedAssetCandidate)}", script);
        Assert.Contains("fp:${hashString(fingerprintSource)}", script);
        Assert.Contains("function getScrollableContainer(element)", script);
        Assert.Contains("element.scrollHeight > element.clientHeight + 4", script);
        Assert.Contains("function getAudioTargetVideos()", script);
        Assert.Contains("function applyAudioSettingsToTargetVideos()", script);
        Assert.Contains("function scrollInstagramByDirection(direction)", script);
        Assert.Contains("document.querySelectorAll(\"a[href*='/reel/']\")", script);
        Assert.Contains("function isInstagramReelsSurface()", script);
        Assert.Contains("function tryScrollAncestors(element, deltaY)", script);
        Assert.Contains("function tryScrollBestContainer(seedElements, deltaY)", script);
        Assert.Contains("/\\/reels(\\/|$)/i.test(window.location.pathname)", script);
        Assert.Contains("candidateRect.top > activeRect.top + 16", script);
        Assert.Contains("candidateRect.top < activeRect.top - 16", script);
        Assert.Contains("function maybeSkipTikTokAd(video)", script);
        Assert.Contains("function maybeSkipInstagramAd(video)", script);
        Assert.Contains("function hasExactText(target, expectedText, referenceRect = null)", script);
        Assert.Contains("function isElementInScope(element, referenceRect)", script);
        Assert.Contains("function hasSelectorInScope(target, selector, referenceRect)", script);
        Assert.Contains("function hasButtonLabelInScope(target, labels, referenceRect)", script);
        Assert.Contains("function enforceActiveVideoAudio()", script);
        Assert.Contains("ActiveAudioEnforcementIntervalMs", script);
        Assert.Contains("video.removeAttribute(\"muted\")", script);
        Assert.Contains("video.setAttribute(\"muted\", \"\")", script);
        Assert.Contains("video.addEventListener(\"volumechange\", enforce)", script);
        Assert.Contains("Sponsored", script);
        Assert.Contains("shop now", script);
        Assert.Contains("Skipped ad", script);
        Assert.Contains("Skipped Instagram ad", script);
        Assert.Contains("ig-ad|${normalizedText.slice(0, 120)}", script);
        Assert.Contains("hasAdLabel || ((hasSponsoredLabel || hasAdSelector) && hasCommerceButton)", script);
        Assert.Contains("let lastRequestedScrollDirection = 1", script);
        Assert.Contains("const skipDirection = lastRequestedScrollDirection < 0 ? -1 : 1", script);
        Assert.Contains("lastAdSkipDirection === skipDirection", script);
        Assert.Contains("scrollByDirection(skipDirection)", script);
        Assert.Contains("function scrollTikTokByDirection(direction)", script);
        Assert.Contains("return scrollInstagramByDirection(normalizedDirection);", script);
        Assert.Contains("const moved = scrollByDirection(direction);", script);
        Assert.Contains("scrollContainer.scrollBy({", script);
        Assert.Contains("window.scrollBy({", script);
    }

    [Fact]
    public void BuildApplySettingsScript_EmbedsCurrentSettings()
    {
        WebViewScriptController controller = new();
        AppSettings settings = new()
        {
            PlaybackSpeed = 2.5,
            VolumePercent = 73,
            SeekBarEnabled = true,
            Platform = ContentPlatform.TikTok,
            IgnoredTikTokVideoIds = ["7528162394021111111"],
            SkipSeenReelsEnabled = true,
        };

        string script = controller.BuildApplySettingsScript(settings);

        Assert.Contains("setSettings", script);
        Assert.Contains("\"playbackSpeed\":2.5", script);
        Assert.Contains("\"volumePercent\":73", script);
        Assert.Contains("\"seekBarEnabled\":true", script);
        Assert.Contains("\"platform\":\"tiktok\"", script);
        Assert.Contains("\"ignoredTikTokVideoIds\":[\"7528162394021111111\"]", script);
        Assert.Contains("\"skipSeenReelsEnabled\":true", script);
    }

    [Fact]
    public void BuildPauseAndResumeScripts_TargetControllerApi()
    {
        WebViewScriptController controller = new();
        AppSettings settings = new();

        Assert.Contains("pauseAndMute", controller.BuildPauseAndMuteScript());
        Assert.Contains("resume(true", controller.BuildResumeScript(settings, shouldResume: true));
        Assert.Contains("scrollByDirection(1)", controller.BuildScrollScript(1));
        Assert.Contains("setHostActive(false)", controller.BuildSetHostActiveScript(isActive: false));
        Assert.Contains("togglePlayPause", controller.BuildTogglePlayPauseScript());
    }
}
