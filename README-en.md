# OpenUTAU Plus

An enhanced fork of **OpenUTAU** — a vocal synthesis workstation with DAW-style mixer and VST3 plugin support.

Based on [OpenUTAU](https://github.com/openutau/OpenUtau) (MIT License)

---

## What's New

### 🎚️ DAW-Style Mixer
- Vertical faders, -24dB ~ +12dB
- 30fps real-time peak meters (LevelTracker)
- Mute / Solo buttons with color indicators
- Double-click numeric editing (volume / pan)
- **Ctrl+M** shortcut to toggle

### 🔌 VST3 Audio Effect Support
- Load any VST3 audio effect (compressors, EQs, reverbs, delays, etc.)
- **Native GUI popup** — standalone Win32 window embedding the plugin interface
- **Real-time parameter sync** — GUI knob changes immediately affect audio output
- **Instrument filtering** — automatically excludes synths/samplers (no audio input plugins)
- Up to 8 plugin slots per track, processed in series
- 3 built-in effects: EQ / Compressor / Reverb (based on upstream "Audition Effect", redesigned with collapsible panels and bypass toggles)

### 📦 .ustxp Project Format
- Plus-native format, `ustxpVersion: 1.0`
- VST plugin parameter persistence — re-open a project and all plugin settings are restored
- Backward-compatible with `.ustx`

---

## Design Goals

OpenUTAU Plus aims to evolve OpenUTAU from a vocal synthesis editor into a **vocal-centric DAW workstation**, enabling the full mixing, mastering, and effects workflow without leaving the application.

### Near-term (v1.x)

- ✅ Mixer console (per-track faders, pan, mute/solo, meters)
- ✅ VST3 audio effect support (loading, GUI, real-time parameters)
- ✅ `.ustxp` project format (VST parameter persistence)
- 🚧 Export/render with VST effects (bounce/render with effects)
- 🚧 VST instrument support (loading synths/samplers as sound sources)
- 🚧 macOS / Linux cross-platform support

### Long-term Vision

- Send tracks / Aux buses / Sidechain compression
- Plugin delay compensation (PDC)
- Track groups and VCA faders
- MIDI control surface mapping
- Built-in sampler and drum machine

---

## Quick Start

```bash
git clone https://github.com/XKLMY-hi/OpenUTAU-Plus.git
cd OpenUTAU-Plus
dotnet restore
dotnet run --project OpenUtau
```

Requires [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).

> **Platform Support**: OpenUTAU Plus is built with C# / Avalonia and runs on Windows / macOS / Linux. However, **the VST3 bridge DLL is currently only compiled for Windows x64**. VST features are unavailable on macOS and Linux for now.

---

## Limitations & Known Gaps

These are design decisions or work-in-progress items. **Please read before using**:

| Limitation | Details |
|------------|---------|
| **Export excludes VST effects** | VST effects are not rendered into exported audio files. Currently only works during real-time playback |
| **No VST instrument support** | VST synths/samplers (instrument plugins) cannot be loaded as sound sources. The scanner automatically filters them out |
| **Windows x64 only** | The bridge DLL is compiled for Windows x64 only. macOS / Linux users cannot use VST features |
| **No send/Aux buses** | Aux buses and sidechain compression are not yet implemented |
| **Single-threaded rendering** | The render engine processes tracks serially; parallelization is not yet introduced |

Solutions for all of the above are planned in the [Design Goals](#design-goals) roadmap.

---

## Building the VST Bridge DLL (Optional)

Only needed when modifying bridge C++ code:

```bat
cd runtimes\vst_bridge
build.bat
```

Requires **Visual Studio 2022** (Community Edition is fine) with the "Desktop development with C++" workload.

---

## Architecture

```
C# (Avalonia UI)                         C++ (VST3 Bridge)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

VstPluginRegistry  ─── scans system VST3 directories
       │
VstPluginManager   ─── per-track instance management (UI loads, audio reads-only)
       │
VstEffect : IEffect ─── inserted into EffectChain
       │                    │
       │ [P/Invoke]         │ EffectChain.Mix()
       ▼                    ▼
  VstBridge.cs          fx.Process(scratch)
       │
       ▼ [DllImport]
  vst_bridge.dll (Steinberg VST3 SDK v3.8.0)
       │
       ├─ IComponent / IAudioProcessor ── audio processing
       ├─ IEditController / createView ── native GUI
       └─ performEdit → atomic queue → inputParameterChanges
```

### Parameter Flow

```
Plugin GUI knob turn
  → Controller::setParamNormalized()
    → BridgeCompHandler::performEdit()
      → atomic ring buffer (lock-free, SPSC)
        → consumed by vst_process()
          → ProcessData::inputParameterChanges
            → IAudioProcessor::process() → real-time audio change ✅
```

---

## Project Structure

```
OpenUtau.sln
├── OpenUtau/               # Main app UI (Avalonia)
│   ├── Views/              # MainWindow, MixerWindow, VstEditorWindow, etc.
│   ├── ViewModels/         # MVVM
│   └── Controls/           # Custom controls (MixerTrackStrip, etc.)
├── OpenUtau.Core/          # Core logic
│   ├── Ustx/               # Data model
│   ├── Render/             # Render engine
│   ├── SignalChain/        # Audio signal chain (EffectChain, LevelTracker, Fader)
│   └── Vst/                # VST host (VstEffect, VstBridge, VstPluginManager)
├── OpenUtau.Plugin.Builtin/
├── OpenUtau.Test/
├── runtimes/               # Native platform libraries
│   ├── vst_bridge/         # C++ bridge project (CMake)
│   └── win-x64/native/     # vst_bridge.dll + worldline.dll
└── VstTest/                # VST compatibility test suite
```

---

## Development

```bash
dotnet build                                  # Build all
dotnet run --project OpenUtau                 # Run
dotnet test                                   # Unit tests
dotnet run --project VstTest                  # VST compatibility tests
```

See [CLAUDE.md](./CLAUDE.md) for the full architecture and development guide.

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| UI | Avalonia 11.x + ReactiveUI MVVM |
| Core | .NET 8.0 / C# 12 |
| Audio | NAudio + custom ISignalSource signal chain |
| VST3 Host | C++ / Steinberg VST3 SDK v3.8.0 (MIT) |
| Serialization | YamlDotNet |
| Testing | xUnit |

---

## License

Based on [OpenUTAU](https://github.com/openutau/OpenUtau), MIT License.

The VST3 bridge uses [Steinberg VST3 SDK v3.8.0](https://github.com/steinbergmedia/vst3sdk), also MIT-licensed.

---

## Acknowledgments

- [OpenUTAU](https://github.com/openutau/OpenUtau) original project and all contributors
- Steinberg for the VST3 SDK
- The vocal synthesis community
- This project was developed through **Vibe Coding** — using DeepSeek V4 Pro AI to assist with architecture design, code generation, and debugging
