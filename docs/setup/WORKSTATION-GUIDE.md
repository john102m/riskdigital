# Dev Workstation — Buying Guide

## Why

Laptop struggling with Android Studio + VS 2026 + browser (Three.js / React dev) running simultaneously. Need a desktop dev machine with headroom.

## Target: HP Z440

Enterprise workstation, lease returns flood eBay. Built like a tank, quiet, designed for sustained heavy loads. DDR4, PCIe 3.0, tool-free internals.

## Ideal Spec

| Component | What to look for | Why |
|-----------|-----------------|-----|
| **CPU** | E5-1650 v3 (3.5GHz) or **E5-1650 v4** (3.6GHz) | High clock = fast builds. Avoid E5-26xx (low clocks, designed for servers) |
| **RAM** | 32GB DDR4 minimum | Multi-IDE comfort. 64GB if cheap. Max 128GB (8 slots) |
| **Storage** | SSD (SATA or NVMe via PCIe adapter) | Non-negotiable. HDD = misery. 256GB minimum, 512GB ideal |
| **GPU** | Quadro K2200 (4GB) or better | Drives 2+ monitors, hardware accel. Avoid NVS 310 (512MB, bare minimum) |
| **PSU** | 700W (standard for Z440) | Plenty for any single GPU you'd add |

## Price Guide (eBay UK, 2026)

| Config | Expected Price |
|--------|---------------|
| E5-1650 v4, 32GB, 256GB SSD, K2200 | £250–270 |
| E5-1650 v4, 32GB, HDD (no SSD), NVS 310 | £200–225 |
| E5-1650 v3, 32GB, SSD, K2200 | £180–220 |
| E5-2620 v3, 16GB, HDD (base refurb bait) | £100–130 (avoid) |

## What to Avoid

- **E5-26xx CPUs** — 2.4GHz base clock, sluggish for IDE work
- **HDD-only listings** — budget an extra £20–25 for a SATA SSD if no SSD included
- **NVS 310** — 512MB, technically works but barely. K2200 is the minimum "good" GPU
- **Refurb shops with dropdowns** — base price is always the worst spec. Calculate the full upgraded price before comparing
- **Under 32GB RAM** — not worth it for your workload

## Upgrade Path (if needed later)

| Upgrade | Cost | Effort |
|---------|------|--------|
| +32GB RAM (to 64GB total) | ~£20 | Clip in, done |
| Add 500GB SATA SSD (second drive) | ~£25 | One cable, one bay |
| Add NVMe SSD via PCIe adapter | ~£35–50 | Slot in PCIe card |
| Swap GPU (e.g. GTX 1060/1070) | ~£40–60 | One slot, may need 6-pin power |

## Windows 11

Not officially supported (CPU too old). Install via:
- Rufus USB with TPM/CPU bypass baked in
- Or registry hack during install

Works perfectly in practice. Receives updates. Microsoft just drew an arbitrary line.

## Best Listing Found So Far

**£260 delivered:**
- E5-1650 v4 (6-core, 3.6GHz, turbo 4.0GHz)
- 32GB DDR4
- 256GB SSD
- NVIDIA Quadro K2200 (4GB)
- Ready to go out of the box

## Saved Search

Set up on eBay: `HP Z440 E5-1650 32GB SSD` — Buy It Now, UK only. Wait for a private seller at £180–200 if not in a rush.

---

*Last updated: 2026-06-14*
