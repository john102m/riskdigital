# Home Server — Self-Hosted Setup

## Purpose
Replace shared hosting (WHUK) with a dedicated low-power box on the home network. Enables CI/CD, full control, zero monthly cost. Static IP already in place.

## Architecture

```
Internet ──▶ Router (port forward 80/443) ──▶ Home Server (Pi/Mini PC)
                                                  ├── Website(s)
                                                  ├── Risk game server (production)
                                                  └── CI/CD runner

Z440 (dev) ──push──▶ GitHub ──webhook/runner──▶ Home Server (auto-deploy)
```

## Hardware Options

| Option | Approx Cost | Pros | Cons |
|--------|-------------|------|------|
| Raspberry Pi 5 (8GB) | £75–90 | Tiny, silent, low power (~5W) | ARM — most .NET works fine, occasional quirks |
| Beelink Mini S12 (N95) | £100–130 | x86, 8GB RAM, silent, NVMe | Slightly bigger |
| MeLE Quieter 4C | £130–150 | Fanless x86, good thermals | Pricier |
| Old laptop/desktop | Free | Already own it | Bigger, louder, more power draw |

**Recommendation:** Beelink or similar N95 mini PC. x86 avoids any ARM compatibility questions with .NET, plenty of power for low-traffic sites, silent, cheap to run.

## Software Stack

| Layer | Choice | Notes |
|-------|--------|-------|
| OS | Ubuntu Server 24.04 LTS | Headless, lightweight, .NET 8 supported |
| Runtime | .NET 8 | Same as dev |
| Reverse proxy | Caddy | Auto HTTPS via Let's Encrypt, zero config |
| CI/CD | GitHub Actions self-hosted runner | Or simple webhook + deploy script |
| Process manager | systemd | Auto-restart on crash, start on boot |
| Firewall | ufw | Allow 80, 443, 22 only |

## Network Setup

1. Static IP already sorted (ISP).
2. Router: port forward 80 → server, 443 → server.
3. DNS: A record for domain(s) → static IP.
4. Caddy handles TLS termination (Let's Encrypt auto-renew).

## Deploy Workflow

```
Developer (Z440)
    │
    ├── git push to GitHub
    │
    ▼
GitHub Actions (self-hosted runner on home server)
    │
    ├── dotnet publish -c Release
    ├── Copy output to /var/www/<site>
    ├── systemctl restart <site>
    │
    ▼
Live in seconds
```

### Alternative (simple, no runner)
- Webhook listener on server watches for GitHub push events.
- On trigger: `git pull && dotnet publish && restart service`.

## Caddy Config Example

```
mysite.com {
    reverse_proxy localhost:5000
}

risk.mysite.com {
    reverse_proxy localhost:5001
}
```

That's it. Caddy auto-provisions HTTPS certificates.

## What This Enables (vs shared hosting)

- ✅ CI/CD pipelines
- ✅ Multiple sites/services on one box
- ✅ Background services, scheduled jobs
- ✅ Docker if needed later
- ✅ Full SSH access
- ✅ WebSocket support (SignalR) without host restrictions
- ✅ No monthly hosting fees
- ✅ No upload size limits or timeout restrictions

## Migration Path

1. Buy mini PC, install Ubuntu Server.
2. Install .NET 8 runtime, Caddy, ufw.
3. Set up systemd service for first site.
4. Point DNS, configure port forwards.
5. Verify HTTPS working.
6. Set up GitHub Actions self-hosted runner.
7. Migrate Risk server from WHUK (optional — or run both).

## Costs

| Item | Cost |
|------|------|
| Mini PC (one-off) | ~£100–130 |
| Electricity (~10W, 24/7) | ~£25/year |
| Domain renewal | Already paying |
| **Total ongoing** | **~£2/month electricity** |

vs WHUK shared hosting: £5–10/month with less control.

## Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| Power cut = site down | UPS (optional, £40). Acceptable for low-traffic personal site. |
| Router reboot = brief outage | Auto-reconnect, minimal downtime |
| Security exposure | ufw, fail2ban, unattended-upgrades, Caddy handles TLS |
| ISP blocks port 80/443 | Unlikely with static IP contract — verify with ISP |
