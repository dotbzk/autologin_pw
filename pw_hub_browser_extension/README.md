# PW Hub Session Bridge

This Chromium extension transfers PW Online cookies in both directions between Chrome or Edge and the account currently selected in PW Hub. Passwords and cookies from unrelated websites are never requested or changed.

## Install in Chrome

1. Open `chrome://extensions`.
2. Enable **Developer mode**.
3. Click **Load unpacked**.
4. Select the `PwHubBrowserExtension` folder from the Game Tools installation.

## Install in Edge

1. Open `edge://extensions`.
2. Enable **Developer mode**.
3. Click **Load unpacked**.
4. Select the `PwHubBrowserExtension` folder from the Game Tools installation.

## Connect the extension

1. Select the target account in PW Hub.
2. Open the PW Hub tray menu and click **Connect browser extension**.
3. Open the extension, paste the six-digit pairing code, and click **Connect**.

## Import a browser session into PW Hub

1. Connect the extension and select the target account in PW Hub.
2. Use Game Center to open the PW Online account page in Chrome or Edge.
3. After the page is authorized, open the extension and click **Browser → PW Hub**.

## Export a PW Hub session to the browser

1. Connect the extension and select the source account in PW Hub.
2. Open the extension and click **PW Hub → browser**.
3. Confirm replacing matching PW Online cookies in the current Chrome or Edge profile.
4. The extension opens `https://pwonline.ru/` with the exported session.

PW Hub must be running during pairing and transfer. Pairing codes expire after five minutes. Only cookies for `pwonline.ru` and `pw.mail.ru` are transferred.
