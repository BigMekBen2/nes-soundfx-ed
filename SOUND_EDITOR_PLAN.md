# NES Sound FX Editor - Development Plan

## Overview

A WPF-based sound effects editor that generates:
1. **`.wav`** files for raylib-cs Windows game (uncompressed, zero latency)
2. **`.nsf`** files for NES emulator testing
3. **C header files (`.h`)** with byte arrays for NES ROM embedding
4. **`.dat`** files for music/sound sequences (real-time synthesis in game)

## Architecture

### Core Components

#### 1. **Synthesis Engine** (`SoundFxEditor.Synth`)
Generates NES-authentic waveforms for real-time preview and export.

**Channels:**
- Pulse 1 & 2: Square waves with duty cycle, envelope, pitch sweep
- Triangle: Fixed-amplitude triangle wave, pitch sweep
- Noise: LFSR-based noise, pitch sweep (period-based)

**Per-channel state:**
```csharp
class SynthChannel
{
    public int ChannelId;              // 0-3
    public int Period;                 // APU period value
    public int PeriodSweepRate;        // frames per increment
    public int Volume;                 // 0-15
    public int VolumeDecayPerFrame;    // 0-15
    public int DutyCycle;              // Pulse only: 0-3
    public int DurationFrames;
    public bool IsActive;
}
```

**Key methods:**
```csharp
public float[] SynthesizePulse(SynthChannel ch, int sampleRate, int frameCount);
public float[] SynthesizeTriangle(SynthChannel ch, int sampleRate, int frameCount);
public float[] SynthesizeNoise(SynthChannel ch, int sampleRate, int frameCount);
public float[] MixChannels(SynthChannel[] channels, int sampleRate, int frameCount);
```

#### 2. **Sound Stamp Library** (Built-in templates)
Pre-parameterized sounds ready to stamp into editor.

**Stamps:**
- **Boing**: Rising pitch pulse, snappy decay (~100-150ms)
- **Laser Shot**: Falling pitch pulse, fast (~50-100ms)
- **Breaking Glass**: High noise burst with falling pitch, medium decay (~200-300ms)
- **Explosion**: Low noise + pulse layer, long decay (~300-600ms)

Each stamp = parameterized `SoundEffect` instance.

#### 3. **WPF UI** (`SoundFxEditor.Wpf`)

**Main Window:**
- **Sound Library Panel** (left): Stamp buttons (Boing, Laser, Glass, Explosion)
- **Editor Panel** (center): 
  - Current sound name/properties
  - Channel selector (tabs or buttons for channels 0-3)
  - Parameter sliders (period, sweep rate, volume, decay, duration)
  - Waveform preview (real-time visualization)
- **Control Panel** (bottom):
  - Preview button (play sound)
  - Stop button
  - Export button (dialog for WAV/NSF/C array)

**Data binding:**
- Sliders ↔ `SynthChannel` properties
- Preview updates in real-time as user adjusts
- Selected sound displayed in editor

#### 4. **Synthesis Playback** (Real-time preview)
Generate audio on-the-fly while user tweaks parameters.

Use **NAudio** for:
- Real-time PCM synthesis
- Play/stop control
- Latency-free preview

#### 5. **WAV Export**
Write synthesized audio to `.wav` file.

- Sample rate: 44100 Hz (standard)
- Bit depth: 16-bit PCM
- Channels: Mono (single mixed output of all 4 NES channels)
- Use NAudio's `WaveFileWriter`

#### 6. **Register Sequence Generation**
Convert sound parameters to frame-by-frame APU register writes.

**Output format (intermediate representation):**
```csharp
class RegisterFrame
{
    public Dictionary<int, byte> RegisterWrites;  // register offset → value
    public int FrameNumber;
}

List<RegisterFrame> frames = GenerateRegisterSequence(sound);
```

**Register offsets (NES APU):**
```
$4000-$4003: Pulse 1
$4004-$4007: Pulse 2
$4008-$400B: Triangle
$400C-$400F: Noise
```

#### 7. **NSF Export**
Embed register sequences in NSF container (emulator-testable).

- NSF header (6502 init routine, play routine addresses)
- Minimal NES driver code (~200 bytes)
- Register sequence data
- Playable in emulators (Nestopia, FCEUX, etc.)

Use **VGMToolbox** or hand-roll minimal NSF writer.

#### 8. **C Array Export**
Generate `.h` file with byte array for ROM embedding.

**Output format:**
```c
#define SOUND_GUNSHOT  0
#define SOUND_BOING    1
#define SOUND_LASER    2

// Metadata: [duration_frames, channels_used_bitmask]
const unsigned char sound_metadata[] = {
    60, 0x09,  // Gunshot: 60 frames, channels 0+3
    30, 0x01,  // Boing: 30 frames, channel 0
    80, 0x03,  // Laser: 80 frames, channels 0+1
};

const unsigned char sound_gunshot_data[] = {
    // 60 frames * ~4 bytes per frame
    0x00, 0x30,  // $4000 = 0x30
    0x01, 0x08,  // $4001 = 0x08
    // ...
};

const unsigned char* sound_data_table[] = {
    sound_gunshot_data,
    sound_boing_data,
    sound_laser_data,
};
```

## Data Model

### Sound Definition

```csharp
class SoundEffect
{
    public string Name;                        // "Gunshot"
    public SynthChannel[] Channels;            // 4 channels (may be inactive)
    public int DurationFrames;
    public byte[] RegisterSequence;            // Flattened register writes
    public int[] ChannelsUsed;                 // e.g., [0, 3]
}
```

### Synthesis Parameters (Per Channel)

```csharp
class SynthChannel
{
    public int ChannelId;
    public bool IsActive;
    
    // Pitch
    public int InitialPeriod;
    public int PeriodSweepRate;               // per frame: -5 to +5
    public int SweepDuration;                 // frames before sweep stops
    
    // Volume
    public int InitialVolume;                 // 0-15
    public int VolumeDecayPerFrame;           // 0-15
    
    // Pulse-specific
    public int DutyCycle;                     // 0-3 (12.5%, 25%, 50%, 75%)
    
    // Timing
    public int DurationFrames;
}
```

## Development Phases

### Phase 1: Synthesis Engine (Week 1-2)
- [ ] Implement NES waveform generators (pulse, triangle, noise)
- [ ] LFSR noise (Galois or Fibonacci configuration)
- [ ] Period-to-frequency conversion
- [ ] Real-time PCM generation
- [ ] Mix 4 channels to mono output
- [ ] Unit tests for each waveform type

**Deliverable:** Standalone synth that can generate any combination of 4 channels.

### Phase 2: WPF UI & Real-time Preview (Week 2-3)
- [ ] Main window layout (library, editor, controls)
- [ ] Parameter sliders (period, sweep, volume, decay, duration)
- [ ] Sound stamp buttons (Boing, Laser, Glass, Explosion)
- [ ] Real-time waveform visualization
- [ ] NAudio integration for preview playback
- [ ] Play/Stop/Reset buttons

**Deliverable:** Fully functional editor with live preview.

### Phase 3: WAV Export (Week 3)
- [ ] Synthesize full audio buffer (based on duration)
- [ ] Write to `.wav` file (44100 Hz, 16-bit, mono)
- [ ] File dialog for save location
- [ ] Verify output in external player (VLC, etc.)

**Deliverable:** Exportable `.wav` files for raylib game.

### Phase 4: Register Sequence Generation (Week 4)
- [ ] Convert channel parameters to frame-by-frame register writes
- [ ] Handle period sweep logic
- [ ] Handle volume decay logic
- [ ] Output intermediate representation (register frames)
- [ ] Flatten to byte array format

**Deliverable:** Verified register sequences (testable against emulator).

### Phase 5: NSF Export (Week 4)
- [ ] Research/implement NSF container format
- [ ] Embed minimal NES driver code
- [ ] Embed register sequence data
- [ ] Write NSF file
- [ ] Test in FCEUX/Nestopia emulator

**Deliverable:** Playable NSF files in emulator.

### Phase 6: C Array Export (Week 5)
- [ ] Generate `.h` file with sound metadata
- [ ] Generate `.h` file with register sequences
- [ ] Include channel usage bitmask
- [ ] Generate lookup table
- [ ] Document format for game engine

**Deliverable:** C header files ready for ROM embedding.

### Phase 7: Polish & Testing (Week 5-6)
- [ ] Save/load projects (.json with all sounds)
- [ ] Undo/redo
- [ ] Keyboard shortcuts
- [ ] Sound library docs (stamp parameters)
- [ ] Integration test: export → emulator → verify audio

**Deliverable:** Polished, documented editor.

## File Structure

```
SoundFxEditor.sln
├── SoundFxEditor.Synth/
│   ├── Synthesizer.cs           (main synth class)
│   ├── PulseGenerator.cs
│   ├── TriangleGenerator.cs
│   ├── NoiseGenerator.cs
│   ├── SynthChannel.cs          (channel state)
│   └── SoundEffect.cs           (sound definition)
├── SoundFxEditor.Export/
│   ├── WavExporter.cs           (WAV file writing)
│   ├── RegisterSequenceGen.cs   (APU register conversion)
│   ├── NsfExporter.cs           (NSF file generation)
│   └── CArrayExporter.cs        (C header generation)
├── SoundFxEditor.Wpf/
│   ├── MainWindow.xaml          (UI layout)
│   ├── MainWindow.xaml.cs       (code-behind)
│   ├── SoundStamps.cs           (built-in stamps)
│   ├── EditorViewModel.cs       (MVVM view model)
│   └── Resources/               (icons, templates)
└── SoundFxEditor.Tests/
    ├── SynthesizerTests.cs
    ├── RegisterSequenceTests.cs
    └── ExportTests.cs
```

## Key Implementation Details

### NES APU Register Map

```
$4000/$4004: Duty/Volume (--DDVVVV)
$4001/$4005: Sweep (EPPPNSSS)
$4002/$4006: Period Low
$4003/$4007: Period High / Length

$4008: Linear Counter / Period High (Triangle)
$400A: Period Low (Triangle)
$400B: Length (Triangle)

$400C: Volume / Decay (--DDVVVV) (Noise)
$400E: Period (E--PPPPP) (Noise)
$400F: Length (Noise)
```

### Frame Timing

- NES: 1.79 MHz CPU, 60 fps = ~29,830 cycles/frame
- Editor: Assume 60 fps synthesis
- Sample rate: 44100 Hz
- Samples per frame: 44100 / 60 = 735 samples

### Register Byte Format (exported)

Each sound = sequence of register writes:
```
Byte 0: Register offset (0x00-0x0F maps to $4000-$400F)
Byte 1: Value to write
Byte 2: Frame count (repeat this write for N frames, then read next)
...
0xFF:   End marker
```

Example (laser shot):
```
0x00, 0x30, 0x01,  // Frame 0: $4000 = 0x30
0x01, 0x08, 0x01,  // Frame 0: $4001 = 0x08
0x02, 0x14, 0x01,  // Frame 0: $4002 = 0x14
0x03, 0x00, 0x01,  // Frame 0: $4003 = 0x00
0x00, 0x2F, 0x01,  // Frame 1: $4000 = 0x2F (volume decayed)
0x00, 0x2E, 0x01,  // Frame 2: $4000 = 0x2E
...
0xFF               // End
```

## Integration with raylib-cs Game

The editor exports `.dat` files (register sequences) that the game engine loads and synthesizes in real-time. See `GAME_ENGINE_AUDIO_PLAN.md` for game-side integration.

## Testing Strategy

1. **Unit tests** for synthesizers (compare output to known good waveforms)
2. **Integration tests** for register sequence generation
3. **Manual testing**: Export sound → play in FCEUX → verify against editor preview
4. **Performance**: Ensure synthesis doesn't cause audio glitches

## Future Enhancements

- Reverb/echo effects (via register tricks)
- Custom duty cycle patterns
- Amplitude modulation
- Visual editor for waveform (click to draw)
- Sound library browser
- Import from FamiTracker
