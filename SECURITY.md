# Security Policy

## Sensitive Data

ShowMeReels stores browser login state outside the repo under `%LOCALAPPDATA%\ShowMeReels\WebView2`. App settings are stored under `%LOCALAPPDATA%\ShowMeReels\settings.json`.

Never commit browser profile folders, cookies, local settings, `.env` files, keys, certificates, logs, or screenshots that expose account data.

## Reporting

Please open a private security advisory on GitHub if this repository enables them. If not, open an issue with a minimal description and avoid posting credentials, cookies, tokens, or private account screenshots.
