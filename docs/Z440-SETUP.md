# Z440 Setup Checklist

## Hardware
- [ ] Install 1TB SATA drive (tool-less bay)
- [ ] Verify Win 11 Pro boots, drivers OK
- [ ] Check NVIDIA K2200 driver (latest from nvidia.com, not Windows Update)
- [ ] Confirm 32GB RAM showing in BIOS/Task Manager

## Core Tools
- [ ] Git for Windows (include Git Bash) — https://git-scm.com
- [ ] GitHub CLI (`winget install GitHub.cli`)
- [ ] Windows Terminal (from MS Store)
- [ ] 7-Zip

## .NET / C#
- [ ] .NET 8 SDK — https://dot.net
- [ ] Visual Studio 2022 (Community/Pro) — workloads: ASP.NET, .NET desktop
- [ ] NuGet cache will rebuild on first restore

## Node / Web
- [ ] Node.js LTS (v20+) — https://nodejs.org
- [ ] VS Code + extensions: Tailwind IntelliSense, ESLint, Prettier, GitLens
- [ ] npm cache rebuilds on first install

## Android / Fire TV
- [ ] Android Studio — https://developer.android.com/studio
- [ ] SDK: API 34 platform only (skip emulator system images — deploy to real Fire Stick)
- [ ] ADB — comes with Android Studio, add to PATH
- [ ] Kotlin plugin (bundled with AS)

## Unity / 3D
- [ ] Unity Hub → Unity 2022 LTS or 6000 LTS
- [ ] Blender (latest) — https://blender.org
- [ ] VS Code or Rider as Unity script editor

## Docker
- [ ] Docker Desktop — https://docker.com
- [ ] Move data/images to 1TB: Settings → Resources → Disk image location
- [ ] Pull images as needed (don't migrate old vhdx)

## Python
- [ ] Python 3.12+ — https://python.org (tick "Add to PATH")
- [ ] pip install whatever you need per-project

## Config & Credentials
- [ ] Generate new SSH key: `ssh-keygen -t ed25519`
- [ ] Add public key to GitHub: https://github.com/settings/keys
- [ ] `git config --global user.name "John McKinney"`
- [ ] `git config --global user.email "your@email"`
- [ ] Clone repos from GitHub (fresh, no old cruft)
- [ ] Browser: sign into Chrome for bookmarks/passwords sync
- [ ] Postman — export collections first (when it lets you back in)

## Drive Layout
- **C: (256GB SSD)** — Windows, tools, SDKs
- **D: (1TB SATA)** — Projects, repos, Docker data, Blender assets, Unity projects

## Optional / Later
- [ ] GPU upgrade (GTX 1070/2060 — ~£80-100 used)
- [ ] Second monitor
- [ ] WSL2 if you want Linux tooling
