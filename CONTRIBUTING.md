# Contributing

Thanks for taking a look at ShowMeReels.

## Development

1. Install the .NET 8 SDK.
2. Restore and test:

```powershell
.\scripts\test.ps1 -Configuration Release
```

3. Keep changes focused. UI changes should preserve the app's compact desktop workflow and avoid adding features that duplicate existing browser behavior.

## Pull Requests

- Include a short description of the behavior change.
- Include the validation command you ran.
- Do not commit local WebView2 profile data, settings files, logs, credentials, or screenshots containing private account data.

## Code Style

The repo uses nullable C# and implicit usings from `Directory.Build.props`. Prefer small, explicit services and tests around non-UI behavior.
