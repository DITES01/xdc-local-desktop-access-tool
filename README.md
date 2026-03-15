# XDC Local Desktop Access Tool

A local-first desktop utility for interacting with the **XDC Network**.

The application provides wallet functionality and transaction tools while keeping **private keys and signing operations entirely on the user's machine**.

This tool is designed for users who prefer **local signing and transparent open-source software** instead of web-based wallets.

---

## Features

- Balance checking
- Local transaction signing
- Keystore file generation
- BIP39 seed phrase support
- XDC network interaction via RPC
- Address derivation support
- Local wallet creation and recovery

All sensitive operations are performed **locally on the user's device**.

---

## Security Philosophy

This software follows a **local-first security model**.

Private keys and seed phrases **never leave the user's machine**.

The tool does not transmit private keys, seed phrases, or keystore passwords over the network.

However, users should understand:

- A compromised computer can compromise any wallet software.
- Always run wallet software on a **trusted system**.
- Never share your **seed phrase** or **private keys**.

---

## Project Status

The project is currently in **active development**.

Features and improvements are being added incrementally.

---

## Building the Application

### Requirements

- .NET 8
- Windows environment

### Build

dotnet build

### Run

dotnet run

---

## Planned Improvements

- Additional validation improvements
- UI polish
- Expanded network interaction tools
- Further security hardening

---

## License

This project is licensed under the **GNU GPL v3**.

See the LICENSE file for full details.

---

## Author

David McDougall

---

## Disclaimer

This software is provided **as is**, without warranty of any kind.

Users are responsible for securely managing their private keys, seed phrases, and wallet backups.
