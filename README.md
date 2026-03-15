XDC Local Desktop Access Tool

A local-first desktop utility for interacting with the XDC Network.

The application provides wallet functionality and transaction tools while keeping private keys and signing operations entirely on the user's machine.

This tool is designed for users who prefer local signing and transparent open-source software instead of web-based wallets.

Features

Balance checking via XDC RPC

Local transaction signing (no external wallet required)

Secure authorisation workflow before transactions

Keystore JSON generation for encrypted wallet storage

BIP39 seed phrase wallet creation and recovery

Multiple address derivation and scanning

Configurable gas settings (Auto / Custom)

Direct explorer access via XDCScan

All sensitive operations are performed locally on the user's device.

Screenshots
Balance Tab

Check wallet balances using the public XDC RPC.

Send Transaction

Create and preview transactions before sending.

Authorisation Window

Local signing requires wallet authorisation using either:

Keystore JSON

Seed phrase

Private key

Wallet Creation

Create a new wallet locally with BIP39 seed phrase support.

Keystore Generation

Generate encrypted keystore files from a seed phrase.

About

Application information and security model overview.

Support This Tool

If the project is useful to you, you can support development.

The donation address displayed in the application is:

xdcD8DFc137957CaCe772021a84019E658DEFECCF43

Users can verify this address inside the application interface.

Security Philosophy

This software follows a local-first security model.

Private keys and seed phrases never leave the user's machine.

The tool does not transmit private keys, seed phrases, or keystore passwords over the network.

However, users should understand:

A compromised computer can compromise any wallet software

Always run wallet software on a trusted system

Never share your seed phrase or private keys

Project Status

The project is currently in active development.

Features and improvements are being added incrementally.

Building the Application
Requirements

.NET 8

Windows environment

Build
dotnet build
Run
dotnet run
Planned Improvements

Additional validation improvements

UI polish

Expanded network interaction tools

Further security hardening

License

This project is licensed under the GNU GPL v3.

See the LICENSE file for full details.

Author

David McDougall

Disclaimer

This software is provided as is, without warranty of any kind.

Users are responsible for securely managing their private keys, seed phrases, and wallet backups.