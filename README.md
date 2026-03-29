# 🛡️ XDC Local Desktop Access Tool

A local-first desktop utility for interacting with the XDC Network securely.

All wallet operations are performed entirely on your machine — no accounts, no telemetry, no remote key handling.

---

## 🔐 Key Features

- Balance checking via public XDC RPC
- Local transaction signing (no browser wallets required)
- Secure authorisation (keystore, seed phrase, or private key)
- Keystore (JSON) generation for encrypted wallet storage
- BIP39 seed phrase support (12 / 24 words + optional passphrase)
- Multiple address derivation and scanning
- XDCScan integration

---

## 🧠 Security Model

- No accounts
- No background services
- No telemetry
- No remote storage of secrets

All sensitive operations:
- occur locally
- remain in memory only
- are cleared on deauthorise or app close

---

## 🖥️ Screenshots

### Balance & Send
![Balance Send](screenshots/balance-send-result.png)

### Authorisation (Keystore)
![Auth Keystore](screenshots/auth-keystore.png)

### Seed Phrase + Address Scan
![Auth Seed](screenshots/auth-seed-xdc-scan.png)

### Create Wallet
![Create Wallet](screenshots/create-wallet-12-generated.png)

### Generate Keystore (Locked)
![Generate Keystore](screenshots/generate-keystore-locked.png)

### About
![About](screenshots/about-tab.png)

---

## 🚀 Usage

### Check Balance
Enter an XDC address and click **Check Balance**

### Authorise
Use one of:
- Keystore (recommended)
- Seed phrase (advanced)
- Private key (advanced)

### Send Transaction
- Enter recipient
- Enter amount
- Preview
- Send

### Create Wallet
- Generate seed phrase
- Store securely
- Optionally create keystore

---

## ⚠️ Important

- Lost keys = lost funds
- Never share your seed phrase
- Always verify addresses before sending

---

## 📦 Run

No install required.

Run:
XDC Desktop Access Tool.exe

---

## ☕ Support

xdc8DfC137957CaCe772021a84019E658DEFECF43

---

## 👤 Author

David McDougall

