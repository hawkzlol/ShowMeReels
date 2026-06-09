using ShowMeReels.App.Models;

namespace ShowMeReels.App.Services;

public interface IWebViewScriptController
{
    string BuildApplySettingsScript(AppSettings settings);

    string BuildBootstrapScript();

    string BuildSeenDiagnosticsScript(string reason);

    string BuildPauseAndMuteScript();

    string BuildResumeScript(AppSettings settings, bool shouldResume);

    string BuildScrollScript(int direction);

    string BuildSetHostActiveScript(bool isActive);

    string BuildTogglePlayPauseScript();

    bool ParseBooleanResult(string scriptResult);
}
