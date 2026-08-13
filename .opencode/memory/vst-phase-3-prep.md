---
name: vst-phase-3-prep
description: All VST pre-work completed; ready for C++ bridge DLL Phase 3
metadata: 
  node_type: memory
  type: project
  originSessionId: b5fb2a48-571b-4140-8518-8f92e74a5d68
---

## VST Phase 1-2 Complete (pre-work)

All C# side infrastructure for VST plugin support is done and committed (44 files, 2554+ insertions).

### Architecture layers ready:
- **EffectChain** (SignalChain/EffectChain.cs) — Flexible IEffect[] chain replacing hardcoded MixFxSource. Build() accepts UMixFx + extra IEffect[].
- **VstPluginRegistry** (Vst/VstPluginRegistry.cs) — Global singleton, scans VST3 moduleinfo.json for CID + Sub Categories + Name + Vendor. VST2 via DLL probe. Classifies effect vs instrument.
- **VstPluginSlot** (Vst/VstPluginSlot.cs) — Per-track slot: PluginUid + Bypassed. Name/Path resolved from registry at load time.
- **VstEffect** (Vst/VstEffect.cs) — Implements IEffect. Currently stub (audio passthrough). Ready for P/Invoke to C++ bridge.
- **VstEditorWindow** (Views/) — Placeholder window, Phase 3 swaps in native HWND embedding.
- **VstPluginManager** — Compatibility facade over VstPluginRegistry.
- **RenderEngine integration** — Builds VstEffect[] from track.VstSlots, feeds into EffectChain.Build().

### Key design decisions:
- Plugin identity by UID (VST3 CID / VST2 composite key), NOT by file path → cross-device compatible
- Global registry + Preferences cache → no re-scan on restart
- SubCategories parsing filters out instrument VSTs → only effects shown in plugin browser
- EffectChain doesn't cache FX output → must stop/restart playback for parameter changes to take effect (note for future: real-time IEffect parameter updates)
- .ustxp format: `ustx_version: 0.9` + `ustxp_version: 1.0`

### What's next (Phase 3):
- C++ bridge DLL (vst_bridge.dll) using Steinberg VST3 SDK v3.8+ (MIT licensed)
- 3 C exports: vst_load(path) → handle, vst_unload(handle), vst_process(handle, float* buf, int frames)
- P/Invoke wrapper following Worldline.cs pattern
- Native editor window HWND embedding
- Instance pool (LRU ≤16 hot instances)

### Why:
VST3 requires C++ COM-style interface calls. C# P/Invoke can only call flat C functions. A C++ bridge DLL that exports C functions is the standard approach.

**How to apply:** Continue with C++ bridge DLL development. Check runtimes/ directory for worldline.dll as reference for platform-specific native library placement.
