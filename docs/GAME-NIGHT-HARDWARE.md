# Game Night Hardware

What each household/player needs to play Risk Digital — current setup and future options.

---

## Per Player
| Item | Required? | Notes |
|------|-----------|-------|
| Any smartphone/tablet with a browser | **Yes** | The React handset — no install, just open the URL. |

That's it. Every player needs a phone. Nothing else.

---

## Per Household (TV Board)

One screen per household displays the shared map. Two options:

| Option | What you need | Experience |
|--------|---------------|------------|
| **Web board** | Any device with a browser (laptop, tablet, smart TV browser, Fire Stick Silk) | Functional — dots on map, info overlays, activity feed, sounds |
| **Unity board** | Windows PC/laptop (runs the .exe) + TV/monitor via HDMI | Premium — 3D dice physics, normal map relief, camera flypath, lighting, sound design |

### Web board setup
1. Open `http://<server>/tv.html?game=XXXX` in any browser.
2. Done.

### Unity board setup
1. Install via Inno Setup installer (one-time).
2. Run the app, enter game code.
3. Connect PC to TV via HDMI (or just use the monitor).

---

## Server

| Scenario | Where it runs | Who sets it up |
|----------|---------------|----------------|
| Same house (LAN) | Z440 or home server | You |
| Remote (cross-country) | WHUK / home server with static IP | You |

Players don't need to think about the server — they just get a URL and game code.

---

## Example Setups

### Scotland household (you)
- Z440 running the server (dev) or connecting to WHUK (prod)
- Unity board on Z440 → HDMI to TV
- Your phone: React handset

### England household (kids)
- Any desktop/laptop running Unity board → HDMI to their TV
- Each player's phone: React handset
- Connects to your server (WHUK or static IP)

### Minimal setup (no Unity)
- Open `tv.html` on a laptop or smart TV browser
- Each player's phone: React handset
- Works anywhere, zero install

### Self-contained LAN (no internet needed)
One household plays locally — server, TV board, and handsets all on the same WiFi. No internet connection required.

- **One PC** runs both the server AND the Unity board (or web board)
- That PC connects to TV via HDMI
- Players join from phones on the same WiFi → `http://<pc-ip>:5000`
- Game code entered on handsets + TV board

**What's needed:**
| Item | Notes |
|------|-------|
| 1 Windows PC/laptop | Runs server + Unity board. Any modern machine handles both. |
| WiFi router | Doesn't need internet — just local network for phones to reach the PC. |
| Phones | One per player, connected to same WiFi, open browser. |
| TV/monitor | HDMI from the PC. Or just use the PC screen. |

**Setup steps:**
1. Run the server on the PC (launches on `http://0.0.0.0:5000`).
2. Run the Unity board on the same PC (connects to `localhost:5000`).
3. Players open `http://<pc-local-ip>:5000` on their phones.
4. Play. No internet, no external server, no accounts.

This is the "take it to someone's house" scenario — bring a laptop, plug into their TV, everyone joins from phones. Fully portable.

### Smart Installer / Launcher Concept

The Inno Setup installer bundles both the Unity board AND the .NET server. On launch, the app handles both scenarios automatically:

**Install time:**
- Installer asks: "Do you want to be able to host games locally?" 
- If yes → installs the .NET server alongside the Unity board.
- If no → installs Unity board only (lighter, connect to remote server).

**Launch time (if server is bundled):**
1. App tries to reach the configured remote server (e.g. WHUK / Scotland).
2. If reachable → connects normally. No local hosting needed.
3. If unreachable (no internet, or remote server is down) → prompts: "Host a game locally?"
4. If yes → starts the server in the background on port 5000.
5. Unity board connects to `localhost:5000`.
6. Displays: "Players join at: `http://192.168.x.x:5000`" + game code on screen.

**Result:**
- One installer, one app, covers remote play and LAN party.
- No technical knowledge needed — it just works.
- The person in England can play with Scotland (remote) or play with mates in their living room (local) with the same install.

---

## Potential TV Board Hardware (Future Options)

Devices that could display the board on a TV without needing a full desktop PC.

### Android-based (Unity APK)

| Device | Approx Cost | GPU | RAM | Pros | Cons |
|--------|-------------|-----|-----|------|------|
| NVIDIA Shield TV Pro | £180–200 | Tegra X1+ | 3GB | Best Android TV GPU, proper gaming device, native ADB | Expensive for one use case |
| NVIDIA Shield TV (tube) | £130–150 | Tegra X1+ | 2GB | Same GPU as Pro, smaller form factor | Only 2GB RAM |
| Fire TV Stick 4K Max | £55–70 | Mali-G52 | 2GB | Cheap, already tested | Underpowered for Unity 3D, poor thermals, sideload hassle. Tried and rejected. |
| Fire TV Cube (3rd gen) | £120–140 | Mali-G52 MP2 | 2GB | Better thermals than Stick | Same weak GPU |
| Samsung A25 5G (phone) | ~£200 | Mali-G68 | 6GB | Better GPU than any stick/box, USB-C to HDMI out | It's a phone — awkward as a TV device |
| Cheap Android TV box (e.g. Mecool) | £40–60 | Varies (Amlogic) | 2–4GB | Cheap | Unreliable hardware, poor driver support, risky |

**Verdict:** NVIDIA Shield is the only Android device worth considering for Unity 3D. Everything else is underpowered or unreliable. But a £100 mini PC running Windows beats them all.

### Windows mini PCs (Unity .exe — recommended)

| Device | Approx Cost | CPU | RAM | Pros | Cons |
|--------|-------------|-----|-----|------|------|
| Beelink Mini S12 (N95) | £100–130 | Intel N95 | 8GB | x86, silent, NVMe, runs Unity natively | Integrated GPU only (Intel UHD) |
| Beelink SER5 (Ryzen 5) | £200–250 | Ryzen 5 5560U | 16GB | Much better GPU (Vega 7), handles 3D well | Pricier |
| Intel NUC (used) | £80–150 | Varies | 8–16GB | Compact, reliable, good used market | Discontinued line — parts drying up |
| Any old laptop | Free | Varies | Varies | Already own it, HDMI out built in | Bulky, may be slow |

**Verdict:** Beelink Mini S12 for budget, SER5 for comfort. A used laptop with HDMI costs nothing if you have one spare.

### Raspberry Pi

| Device | Approx Cost | Viability | Notes |
|--------|-------------|-----------|-------|
| Raspberry Pi 5 (8GB) | £75–90 | Web board only | Can't run Unity. Perfect for hosting the server or showing tv.html in a kiosk browser (Chromium fullscreen). Tiny, silent, ~5W. |
| Raspberry Pi 4 (4/8GB) | £50–70 | Web board only | Same as above, slightly less powerful. |

### Other options

| Device | Viability | Notes |
|--------|-----------|-------|
| Chromecast / Google TV | Web board only | Cast a browser tab showing tv.html. No native app without significant work. |
| Apple TV | Not viable | No Unity sideloading without Apple dev account + tvOS build. Not worth it. |
| Steam Deck | Works | Runs Windows/Linux Unity builds. Overkill and awkward form factor for a TV display. |
| Old gaming PC | Best option if available | Any PC with a dedicated GPU (even old GTX 750+) runs the Unity board perfectly. Free. |

---

## Recommendation Matrix

| Priority | Best choice | Cost |
|----------|-------------|------|
| **Free / use what you have** | Old laptop or desktop → HDMI to TV | £0 |
| **Cheap dedicated box** | Beelink Mini S12 | ~£110 |
| **Best compact experience** | Beelink SER5 or used gaming mini PC | ~£200 |
| **Android TV (if you insist)** | NVIDIA Shield TV Pro | ~£190 |
| **No install at all** | Any browser → tv.html | £0 |

---

## Summary

| Role | Minimum requirement |
|------|-------------------|
| Player | Phone with a browser |
| TV display (basic) | Any browser on any screen |
| TV display (premium) | Windows PC + monitor/TV |
| Server | Already hosted — players don't worry about this |

---

## Cost to a new household

| Item | Cost |
|------|------|
| React handset | Free (just a URL) |
| Web board | Free (just a URL) |
| Unity board | Free (installer from shared link) |
| **Total** | **£0** — assuming they have a phone and a computer/TV already |
