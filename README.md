# GuessWho

A networked **"Guess Who?"** party game for two players, built with WPF (.NET 10).

One player hosts the session, the other joins as a client. Each player is randomly assigned a character — the goal is to guess the opponent's character.

The application is distributed as a **standalone** (self-contained) executable — no .NET installation required on the user's machine.

---

## 📌 How It Works

1. **Start screen** — the player chooses the role of **Host** or **Client**.
2. **Connecting** — the host starts a TCP server and broadcasts its presence on the local network via UDP. The client automatically discovers the host and connects — **the IP address is detected automatically**, no manual input needed.
3. **Character selection** — once connected, both players see a grid of 15 characters. Each player clicks a face and confirms their choice.
4. **Game start** — the host randomly assigns characters so that each player gets a different one to guess. The assignments are sent over the network.
5. **Gameplay** — players take turns:
   - They can **cross out** characters (left-click to toggle).
   - When confident, they click the **Guess** button and pick the opponent's character.
6. **Result** — after a guess, the end screen is shown (win/loss) with an option to **rematch**, which resets the game without disconnecting.

---

## 🔌 Networking & Host Discovery

The application uses **automatic host discovery on the local network** (UDP broadcast on port 5001). This means:

- The host **does not need to share** their IP with the other player.
- The client **finds the host automatically** — the IP address adjusts on its own.
- If discovery fails, the client falls back to the address in `config.txt`, and ultimately tries `127.0.0.1` (localhost).

---

## 🪵 Logs — Debug Window (F12)

During the game, press **F12** to open/hide the **log window** — it displays application events in real time (network connections, character selections, guess results, restarts).

| Shortcut | Action |
|----------|--------|
| **F12** | Show / hide log window |

The log window is always on top (`Topmost`) and does not steal focus — you can use it without interrupting the game.

---

## ▶️ How to Run

### Standalone version (recommended)

1. Download the installer or published application folder for your architecture (`win-x64`, `win-x86`, or `win-arm64`).
2. Run `GuessWho.exe` — no .NET installation required.

### Playing over LAN

1. Launch the application on **two computers** in the same network (or twice on the same machine).
2. On the first one, choose **Host** — the server will start listening and broadcasting its presence.
3. On the second one, choose **Client** — the application will automatically find the host.
4. Once connected, both players select their characters and the game begins.

### Building from Source

1. Open `GuessWho.slnx` in **Visual Studio 2022** or later.
2. Make sure the **.NET Desktop Development** workload and **.NET 10 SDK** are installed.
3. Press **F5** (with debugging) or **Ctrl + F5** (without).

### Publishing Standalone

```powershell
.\publish-all.ps1
```

The script publishes a self-contained `.exe` for x64, x86, and arm64 architectures to `bin\publish\`, and optionally builds Inno Setup installers.

---

## ⚙️ Configuration

`config.txt` file (next to the `.exe`):

```
host_ip=127.0.0.1
port=5000
```

| Key | Description | Default |
|-----|-------------|---------|
| `host_ip` | Host IP address (fallback when auto-detection fails) | `127.0.0.1` |
| `port` | TCP connection port | `5000` |

> In most cases **you don't need to change** these values — UDP auto-detection resolves the IP and port automatically.

---

## 📋 Requirements

- Windows 10 / 11
- The standalone version has no additional dependencies
