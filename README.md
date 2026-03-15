![Release](https://img.shields.io/github/v/release/DITES01/xdc-local-desktop-access-tool)
![License](https://img.shields.io/github/license/DITES01/xdc-local-desktop-access-tool)
![Last Commit](https://img.shields.io/github/last-commit/DITES01/xdc-local-desktop-access-tool)

# XDC Local Desktop Access Tool

A local-first desktop utility for interacting with the **XDC Network**.

The application provides wallet functionality and transaction tools while keeping **private keys and signing operations entirely on the user's machine**.

---

# Features

- Balance checking via XDC RPC
- Local transaction signing
- Keystore JSON generation
- BIP39 seed phrase support
- Address derivation and scanning
- Configurable gas settings
- Explorer integration using XDCScan

---

# Screenshots

## Balance Tab

<p align="center">
<img src="src/XdcLocalDesktopAccessTool.App/Assets/Screenshots/balance-tab.png" width="900">
</p>

Check wallet balances using the public XDC RPC.

---

## Send Transaction

<p align="center">
<img src="src/XdcLocalDesktopAccessTool.App/Assets/Screenshots/send-tab.png" width="900">
</p>

Create and preview transactions before sending.

---

## Authorisation Window

<p align="center">
<img src="src/XdcLocalDesktopAccessTool.App/Assets/Screenshots/authorisation-window.png" width="900">
</p>

Local signing requires wallet authorisation.

---

## Wallet Creation

<p align="center">
<img src="src/XdcLocalDesktopAccessTool.App/Assets/Screenshots/create-wallet-window.png" width="900">
</p>

Create a new wallet locally with BIP39 support.

---

## Keystore Generation

<p align="center">
<img src="src/XdcLocalDesktopAccessTool.App/Assets/Screenshots/generate-keystore-window.png" width="900">
</p>

Generate encrypted keystore files from seed phrases.

---

## About Tab

<p align="center">
<img src="src/XdcLocalDesktopAccessTool.App/Assets/Screenshots/about-tab.png" width="900">
</p>

Application information and security model overview.

---

## Support Window

<p align="center">
<img src="src/XdcLocalDesktopAccessTool.App/Assets/Screenshots/support-window.png" width="900">
</p>

The donation address displayed in the application is:

xdcD8DFc137957CaCe772021a84019E658DEFECCF43

Users can verify this inside the application.

---

# Security Philosophy

This software follows a **local-first security model**.

Private keys and seed phrases never leave the user's machine.

Always verify release files before running wallet software.

---

# Building

Requirements

.NET 8  
Windows

Build

dotnet build

Run

dotnet run

---

# License

GNU GPL v3

---

# Author

David McDougall

---

# Disclaimer

This software is provided **as is** without warranty of any kind.
