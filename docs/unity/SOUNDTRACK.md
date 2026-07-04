# Soundtrack — Phase-Based Background Music

## Purpose
Ambient background music that shifts with the game phase, adding atmosphere and reinforcing the emotional arc of a game session.

---

## Track List

| Phase | Mood | Style Notes | Loop? |
|-------|------|-------------|-------|
| **Lobby** | Relaxed anticipation | Gentle strings, ticking clock, war room / tavern ambience. Quiet, inviting. | Yes |
| **Placement** | Strategic, deliberate | Low tempo military snare (soft), map-table feel, quill-on-parchment vibe. | Yes |
| **Reinforce** | Empowering, momentum | Brass/horns swell gently, sense of gathering strength. Slightly bolder than placement. | Yes |
| **Attack** | Tension, aggression | Driving percussion, war drums, urgent strings. Intensity without being overwhelming. | Yes |
| **Fortify** | Calm after storm | Resolving, wind-down. Softer instrumentation, gentle resolution. | Yes |
| **Game Over (win)** | Triumphant | Fanfare, full orchestra swell, celebratory. | No (play once) |
| **Game Over (lose)** | Solemn | Minor key, fading strings, dignified defeat. | No (play once) |

---

## Design Principles

- **Background, not foreground** — music sits behind SFX (dice, alerts, captures). Volume ~30-40% of SFX.
- **Crossfade between phases** — 1.5–2s crossfade so transitions are smooth, never jarring.
- **Seamless loops** — each track designed to loop without audible joins.
- **No vocals** — instrumental only. Vocals distract and compete with UI sounds.
- **Consistent tone** — all tracks share a palette (orchestral/cinematic or period/classical). Not a different genre per phase.
- **Mute option** — player preference. Remember setting across sessions.

---

## Implementation: `MusicManager.cs`

```csharp
public class MusicManager : MonoBehaviour
{
    [Header("Phase Track Pools")]
    public AudioClip[] lobbyTracks;
    public AudioClip[] placementTracks;
    public AudioClip[] reinforceTracks;
    public AudioClip[] attackTracks;
    public AudioClip[] fortifyTracks;
    public AudioClip[] victoryTracks;
    public AudioClip[] defeatTracks;

    [Header("Settings")]
    public float musicVolume = 0.3f;
    public float crossfadeDuration = 1.5f;

    AudioSource sourceA;
    AudioSource sourceB;
    bool aIsActive = true;
    string currentTrackPhase = "";

    void Start()
    {
        sourceA = gameObject.AddComponent<AudioSource>();
        sourceB = gameObject.AddComponent<AudioSource>();
        ConfigureSource(sourceA);
        ConfigureSource(sourceB);

        GameStateManager.Instance.OnStateChanged += OnStateChanged;
    }

    void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
    }

    void OnStateChanged()
    {
        var state = GameStateManager.Instance.State;
        if (state == null) return;

        string phase = GetMusicPhase(state);
        if (phase == currentTrackPhase) return;

        currentTrackPhase = phase;
        AudioClip clip = PickRandomClip(phase);
        if (clip != null) CrossfadeTo(clip, phase == "Victory" || phase == "Defeat");
    }

    string GetMusicPhase(GameStateDTO state)
    {
        if (state.phase == "Lobby") return "Lobby";
        if (state.phase == "GameOver") return "Victory"; // TODO: detect winner vs loser
        if (state.phase == "InitialPlacement") return "Placement";
        return state.turnPhase switch
        {
            "Reinforce" => "Reinforce",
            "Attack" => "Attack",
            "Fortify" => "Fortify",
            _ => currentTrackPhase // no change
        };
    }

    AudioClip PickRandomClip(string phase)
    {
        AudioClip[] pool = phase switch
        {
            "Lobby" => lobbyTracks,
            "Placement" => placementTracks,
            "Reinforce" => reinforceTracks,
            "Attack" => attackTracks,
            "Fortify" => fortifyTracks,
            "Victory" => victoryTracks,
            "Defeat" => defeatTracks,
            _ => null
        };

        if (pool == null || pool.Length == 0) return null;
        return pool[Random.Range(0, pool.Length)];
    }

    async void CrossfadeTo(AudioClip clip, bool playOnce)
    {
        var fadeOut = aIsActive ? sourceA : sourceB;
        var fadeIn = aIsActive ? sourceB : sourceA;
        aIsActive = !aIsActive;

        fadeIn.clip = clip;
        fadeIn.loop = !playOnce;
        fadeIn.Play();

        float elapsed = 0f;
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / crossfadeDuration;
            fadeOut.volume = Mathf.Lerp(musicVolume, 0f, t);
            fadeIn.volume = Mathf.Lerp(0f, musicVolume, t);
            await Awaitable.NextFrameAsync();
        }

        fadeOut.Stop();
        fadeOut.volume = 0f;
        fadeIn.volume = musicVolume;
    }
}
```

### Attach to:
Same GameObject as `UIOverlay` or a dedicated `AudioManager` object.

### Inspector:
Drag audio clips into the slots. Adjust `musicVolume` and `crossfadeDuration` to taste.

---

## Audio Source Options

### Royalty-free libraries
| Source | Notes |
|--------|-------|
| [Pixabay Music](https://pixabay.com/music/) | Free, no attribution required. Good cinematic/ambient selection. |
| [Incompetech](https://incompetech.com/) | Kevin MacLeod — huge library, attribution required (free) or paid licence. |
| [Freesound](https://freesound.org/) | Community uploads. Quality varies. Good for ambient loops. |
| [Musopen](https://musopen.org/) | Public domain classical recordings. Could suit a period/strategic feel. |

### AI-generated (custom)
| Tool | Notes |
|------|-------|
| [Suno](https://suno.ai/) | Describe mood/style, generates full tracks. Good for bespoke loops. |
| [Udio](https://udio.com/) | Similar to Suno. Experiment with both. |

#### Suno Prompts

| Phase | Prompt |
|-------|--------|
| **Lobby** | Gentle orchestral ambience, soft strings, quiet ticking clock, war room atmosphere, calm anticipation, no vocals, looping, 70bpm |
| **Placement** | Low military snare drum, quiet brass, strategic and deliberate, map table atmosphere, parchment and candlelight feel, no vocals, looping, 80bpm |
| **Reinforce** | Building orchestral swell, French horns, gathering strength, empowering, steady march tempo, cinematic, no vocals, looping, 90bpm |
| **Attack** | Intense war drums, driving percussion, urgent strings, battle tension, aggressive but not chaotic, cinematic orchestral, no vocals, looping, 120bpm |
| **Fortify** | Calm after battle, resolving strings, gentle woodwinds, peaceful, winding down, reflective, no vocals, looping, 75bpm |
| **Victory** | Triumphant fanfare, full orchestra, celebratory brass, major key, heroic, cinematic climax, no vocals, 30 seconds, not looping |
| **Defeat** | Solemn strings, minor key, fading, dignified defeat, quiet brass, mournful but respectful, no vocals, 20 seconds, not looping |

**Tips:**
- Add "seamless loop" or "loopable" to encourage loop-friendly structure.
- Specify "no vocals" every time — Suno defaults to adding vocals.
- Generate 2-3 versions of each and pick the best.
- Trim/crossfade the loop point in Audacity if Suno doesn't nail it perfectly.
- Keep tracks 60–90 seconds — long enough to not feel repetitive, short enough to loop cleanly.

### Compose your own
- **LMMS** (free, Windows) — full DAW, MIDI + samples.
- **GarageBand** (Mac/iOS) — quick loops and layering.
- **MuseScore** (free) — if you want to write notation and export audio.

---

## File Format
- **WAV or OGG** — Unity handles both. OGG for smaller file size (music tracks can be large).
- **Sample rate:** 44100 Hz.
- **Stereo.**
- **Loop point:** ensure tracks loop seamlessly (fade tail into head, or compose with a natural loop point).

---

## Volume Hierarchy

| Layer | Relative Volume | Notes |
|-------|----------------|-------|
| Music | 30% | Always behind everything else |
| Ambient SFX (dice rattle, alerts) | 70% | Clear and punchy |
| UI SFX (turn chime, capture, reinforcement click) | 100% | Immediate feedback |

Music ducks further during dice rolls (optional — add a brief volume dip when `diceRattleClip` plays).

---

## Future Enhancements
- **Intensity layers within Attack phase** — music builds as more territories are captured in a single turn.
- **Player theme motifs** — short melodic phrase when each player's turn starts (subtle, 2-3 notes over the main track).
- **Dynamic mixing** — duck music during popups/announcements, swell on captures.
- **Settings UI** — volume slider, mute toggle, persisted to PlayerPrefs.
