# Z440 GPU Upgrade Options

## Current Setup
- **Machine:** HP Z440 (E5-1650 v4, 32GB DDR4, 700W PSU)
- **Current GPU:** NVIDIA Quadro K2200 (640 CUDA cores, 4GB GDDR5, 2014)
- **Slot:** PCIe 3.0 x16
- **Use cases:** Unity 6 dev, Blender (future tokens/assets), game night TV output

## Constraints
- **PSU:** 700W — plenty for any single GPU
- **PCIe power:** Check for 6+2 pin connectors (700W unit should have them)
- **Card length:** ~267mm without removing drive bays, ~300mm with front bay removed
- **Cooling:** Single rear exhaust + front intake — long cards may restrict airflow
- **BIOS:** May need CSM/UEFI toggle for consumer GPUs (usually fine, Google "Z440 + [card]" to confirm)

## Options

### Tier 1 — Big Leap, Budget Friendly

| Card | CUDA Cores | VRAM | TDP | Used Price | Notes |
|------|-----------|------|-----|-----------|-------|
| GTX 1080 Ti | 3584 | 11GB GDDR5X | 250W | £100–130 | Massive jump from K2200. No ray tracing but raw power still holds up. Proven in Z440 builds. |
| GTX 1070 Ti | 2432 | 8GB GDDR5 | 180W | £70–90 | Cheaper, shorter card, lower power. Solid for Unity/Blender at 1080p. |

### Tier 2 — Modern Features (Ray Tracing, DLSS, NVENC)

| Card | CUDA Cores | VRAM | TDP | Used Price | Notes |
|------|-----------|------|-----|-----------|-------|
| RTX 2070 Super | 2560 | 8GB GDDR6 | 215W | £120–150 | Ray tracing + DLSS 2.0. Good all-rounder. |
| RTX 2080 | 2944 | 8GB GDDR6 | 215W | £130–160 | Slightly more grunt than 2070S. |
| RTX 3060 12GB | 3584 | 12GB GDDR6 | 170W | £150–180 | Lower TDP, huge VRAM for Blender. Dual-slot, shorter cards available. |
| RTX 3070 | 5888 | 8GB GDDR6 | 220W | £200–230 | Overkill for current needs but future-proof. DLSS 2.0. |

### Tier 3 — Overkill / Future-Proof

| Card | CUDA Cores | VRAM | TDP | Used Price | Notes |
|------|-----------|------|-----|-----------|-------|
| RTX 3080 10GB | 8704 | 10GB GDDR6X | 320W | £250–300 | Serious GPU. Check card length (~285mm+). |
| RTX 4060 Ti | 4352 | 8/16GB GDDR6 | 160W | £250–280 (new) | Current gen, DLSS 3, efficient. Compact cards available. |

## Recommendation

**Best value: RTX 3060 12GB (~£150–180 used)**
- 12GB VRAM future-proofs for Blender/Unity asset work
- Low TDP (170W) — no PSU concerns, less heat in Z440 chassis
- Shorter dual-slot cards available (fits without bay removal)
- Modern driver support, DLSS, hardware ray tracing, NVENC encoding
- ~10x the performance of the K2200

**Budget pick: GTX 1080 Ti (~£100–130 used)**
- Raw performance still excellent for 1080p
- 11GB VRAM
- Longer card — may need front drive bay removed
- No ray tracing / DLSS but irrelevant for a board game

## Before Buying

1. **Measure clearance** — open the Z440 side panel, measure from PCIe bracket to obstruction
2. **Check PSU connectors** — look for 6+2 pin PCIe power cables (should have 2x)
3. **Google "HP Z440 + [card model]"** — confirm no BIOS issues
4. **Check card dimensions** on manufacturer spec page (length × height × slots)

## Installation

1. Power off, unplug, open side panel
2. Remove K2200 (single slot, single 6-pin power)
3. Insert new GPU, connect PCIe power cables
4. Boot — may get low-res display initially
5. Download latest NVIDIA Game Ready drivers from nvidia.com
6. Uninstall old Quadro drivers first (use DDU for clean removal)
