# Game Engine - Audio System Plan

## Overview

This document describes how the raylib-cs Windows game and NES ROM integrate audio from the Sound FX Editor.

The game engine synthesizes NES sounds in real-time using the same synthesis engine as the editor. This provides:
- Identical audio between Windows and NES versions
- Seamless channel arbitration (music vs. sound effects)
- Real-time synthesis (low CPU, small footprint)

## Architecture

### Audio System Components

```
Game Code
├── AudioManager (channel arbitration, playback state)
├── MusicPlayer (loads .dat, manages music synth)
├── SoundEffectPlayer (plays effects, steals channels)
└── Synthesizer (same synth engine as editor)
```

### Data Flow

```
.dat file (register sequences)
    ↓
[Load into memory]
    ↓
MusicPlayer / SoundEffectPlayer
    ├── Frame 0: Read register writes from .dat
    ├── Write to SynthChannel state
    ├── Synthesizer.GenerateFrame() → PCM
    ├── Frame 1: Next register writes
    └── ...
    ↓
[Mix all 4 channels]
    ↓
NAudio / raylib audio output
    ↓
Speakers
```

## .DAT File Format (Register Sequences)

Export from Sound FX Editor.

**Structure:**
```
Byte 0:     Number of sounds (N)
Bytes 1-N:  Sound metadata (duration, channels used)
Bytes N+1+: Register sequence data for each sound
```

**Sound Metadata (4 bytes per sound):**
```
Byte 0: Duration (in frames)
Byte 1: Channels used bitmask (bit 0=CH0, bit 1=CH1, bit 2=CH2, bit 3=CH3)
Byte 2-3: Offset (little-endian) to register sequence data
```

**Register Sequence Format:**
```
Register offset, Value, Frame count, Register offset, Value, Frame count, ...
0xFF: End marker

Example (Laser Shot, 60 frames):
0x00, 0x30, 0x01,   // Frame 0: $4000 = 0x30
0x01, 0x08, 0x01,   // Frame 0: $4001 = 0x08
0x02, 0x14, 0x01,   // Frame 0: $4002 = 0x14
0x03, 0x00, 0x01,   // Frame 0: $4003 = 0x00
0x00, 0x2F, 0x01,   // Frame 1: $4000 = 0x2F (volume decayed)
0x00, 0x2E, 0x01,   // Frame 2: $4000 = 0x2E
...
0xFF
```

## C# Audio System Implementation

### 1. Synthesizer (Reuse from editor)

```csharp
// Copy SoundFxEditor.Synth to game project
// OR: Compile as shared library

public class Synthesizer
{
    public const int SampleRate = 44100;
    public const int FramesPerSecond = 60;
    public const int SamplesPerFrame = SampleRate / FramesPerSecond;  // 735
    
    public SynthChannel[] Channels { get; private set; }  // 4 channels
    
    public Synthesizer()
    {
        Channels = new SynthChannel[4];
        for (int i = 0; i < 4; i++)
        {
            Channels[i] = new SynthChannel { ChannelId = i };
        }
    }
    
    public void GenerateFrame(float[] buffer, int offset)
    {
        // Generate 735 samples for this frame
        // Apply current channel state (period, volume, etc.)
        // Mix all 4 channels
        // Write to buffer
    }
}

public class SynthChannel
{
    public int ChannelId;
    public int Period;                   // APU period value
    public int PeriodSweepRate;          // per frame: -5 to +5
    public int Volume;                   // 0-15
    public int VolumeDecayPerFrame;      // 0-15
    public int DutyCycle;                // Pulse only
    public bool IsActive;
    
    public void UpdateFrame()
    {
        // Apply period sweep
        Period += PeriodSweepRate;
        
        // Apply volume decay
        Volume = Math.Max(0, Volume - VolumeDecayPerFrame);
        
        if (Volume == 0)
            IsActive = false;
    }
}
```

### 2. Audio Data Loader

```csharp
public class SoundData
{
    public string Name;
    public int DurationFrames;
    public int ChannelsMask;             // bits 0-3
    public byte[] RegisterSequence;      // raw register data
}

public class SoundLibrary
{
    private SoundData[] sounds;
    
    public SoundLibrary(byte[] datFileBytes)
    {
        // Parse .dat file
        int soundCount = datFileBytes[0];
        sounds = new SoundData[soundCount];
        
        int metadataOffset = 1;
        int dataOffset = 1 + (soundCount * 4);
        
        for (int i = 0; i < soundCount; i++)
        {
            int duration = datFileBytes[metadataOffset];
            int channels = datFileBytes[metadataOffset + 1];
            int seqOffset = BitConverter.ToUInt16(datFileBytes, metadataOffset + 2);
            
            sounds[i] = new SoundData
            {
                DurationFrames = duration,
                ChannelsMask = channels,
                RegisterSequence = ExtractSequence(datFileBytes, dataOffset + seqOffset)
            };
            
            metadataOffset += 4;
        }
    }
    
    public SoundData GetSound(int soundId) => sounds[soundId];
    
    private byte[] ExtractSequence(byte[] data, int offset)
    {
        // Read from offset until 0xFF
        List<byte> seq = new();
        int pos = offset;
        while (data[pos] != 0xFF)
        {
            seq.Add(data[pos++]);
        }
        seq.Add(0xFF);
        return seq.ToArray();
    }
}
```

### 3. Music Player

```csharp
public class MusicPlayer
{
    private Synthesizer synth;
    private SoundData currentMusic;
    private int frameIndex = 0;
    private int[] mutedChannels = new int[4];  // mute counter per channel
    
    public MusicPlayer(Synthesizer synth)
    {
        this.synth = synth;
    }
    
    public void LoadMusic(SoundData musicData)
    {
        currentMusic = musicData;
        frameIndex = 0;
        Array.Clear(mutedChannels);
    }
    
    public void Stop()
    {
        currentMusic = null;
        frameIndex = 0;
    }
    
    public void MuteChannel(int channel)
    {
        mutedChannels[channel]++;
    }
    
    public void UnmuteChannel(int channel)
    {
        mutedChannels[channel] = Math.Max(0, mutedChannels[channel] - 1);
    }
    
    public void UpdateFrame()
    {
        if (currentMusic == null)
            return;
        
        // Parse register writes for this frame from currentMusic.RegisterSequence
        int seqIndex = FindFrameOffset(frameIndex);
        
        while (seqIndex < currentMusic.RegisterSequence.Length && 
               currentMusic.RegisterSequence[seqIndex] != 0xFF)
        {
            byte regOffset = currentMusic.RegisterSequence[seqIndex++];
            byte value = currentMusic.RegisterSequence[seqIndex++];
            byte frameCount = currentMusic.RegisterSequence[seqIndex++];
            
            int channel = RegOffsetToChannel(regOffset);
            
            if (mutedChannels[channel] == 0)  // Not muted
            {
                ApplyRegisterWrite(channel, regOffset, value);
            }
        }
        
        frameIndex++;
        if (frameIndex >= currentMusic.DurationFrames)
            frameIndex = 0;  // Loop
    }
    
    private int FindFrameOffset(int targetFrame)
    {
        // Linear scan or indexed lookup
        // For now, linear scan is fine for typical music lengths
        
        int currentFrame = 0;
        int seqIndex = 0;
        
        while (seqIndex < currentMusic.RegisterSequence.Length && 
               currentMusic.RegisterSequence[seqIndex] != 0xFF)
        {
            byte regOffset = currentMusic.RegisterSequence[seqIndex];
            byte value = currentMusic.RegisterSequence[seqIndex + 1];
            byte frameCount = currentMusic.RegisterSequence[seqIndex + 2];
            
            if (currentFrame + frameCount > targetFrame)
                return seqIndex;
            
            currentFrame += frameCount;
            seqIndex += 3;
        }
        
        return seqIndex;
    }
    
    private void ApplyRegisterWrite(int channel, byte regOffset, byte value)
    {
        // Map register offset to SynthChannel fields
        // regOffset: 0x00-0x03 = Pulse1, 0x04-0x07 = Pulse2, etc.
        
        int relOffset = regOffset % 4;
        
        switch (relOffset)
        {
            case 0:  // Volume/Duty
                synth.Channels[channel].Volume = value & 0x0F;
                synth.Channels[channel].DutyCycle = (value >> 6) & 0x03;
                break;
            case 1:  // Sweep (ignore for now)
                break;
            case 2:  // Period Low
                synth.Channels[channel].Period = (synth.Channels[channel].Period & 0xFF00) | value;
                break;
            case 3:  // Period High / Length
                synth.Channels[channel].Period = (synth.Channels[channel].Period & 0x00FF) | ((value & 0x07) << 8);
                break;
        }
    }
    
    private int RegOffsetToChannel(byte regOffset)
    {
        if (regOffset < 0x04) return 0;      // Pulse 1
        if (regOffset < 0x08) return 1;      // Pulse 2
        if (regOffset < 0x0C) return 2;      // Triangle
        return 3;                             // Noise
    }
}
```

### 4. Sound Effect Player

```csharp
public class SoundEffectPlayer
{
    private Synthesizer synth;
    private MusicPlayer musicPlayer;
    private SoundLibrary soundLibrary;
    
    private class ActiveEffect
    {
        public SoundData Sound;
        public int[] Channels;
        public int FramesRemaining;
        public int SequenceIndex;
    }
    
    private List<ActiveEffect> activeEffects = new();
    
    public SoundEffectPlayer(Synthesizer synth, MusicPlayer musicPlayer, SoundLibrary soundLibrary)
    {
        this.synth = synth;
        this.musicPlayer = musicPlayer;
        this.soundLibrary = soundLibrary;
    }
    
    public void Play(int soundId)
    {
        var sound = soundLibrary.GetSound(soundId);
        var channels = ExtractChannels(sound.ChannelsMask);
        
        // Mute music on these channels
        foreach (int ch in channels)
        {
            musicPlayer.MuteChannel(ch);
        }
        
        activeEffects.Add(new ActiveEffect
        {
            Sound = sound,
            Channels = channels,
            FramesRemaining = sound.DurationFrames,
            SequenceIndex = 0
        });
    }
    
    public void UpdateFrame()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            
            // Parse and apply register writes for this frame
            int seqIndex = effect.SequenceIndex;
            while (seqIndex < effect.Sound.RegisterSequence.Length && 
                   effect.Sound.RegisterSequence[seqIndex] != 0xFF)
            {
                byte regOffset = effect.Sound.RegisterSequence[seqIndex++];
                byte value = effect.Sound.RegisterSequence[seqIndex++];
                byte frameCount = effect.Sound.RegisterSequence[seqIndex++];
                
                int channel = RegOffsetToChannel(regOffset);
                if (effect.Channels.Contains(channel))
                {
                    ApplyRegisterWrite(channel, regOffset, value);
                }
            }
            
            effect.SequenceIndex = seqIndex;
            effect.FramesRemaining--;
            
            if (effect.FramesRemaining <= 0)
            {
                // Effect finished, unmute music
                foreach (int ch in effect.Channels)
                {
                    musicPlayer.UnmuteChannel(ch);
                }
                activeEffects.RemoveAt(i);
            }
        }
    }
    
    private int[] ExtractChannels(int channelsMask)
    {
        var channels = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            if ((channelsMask & (1 << i)) != 0)
                channels.Add(i);
        }
        return channels.ToArray();
    }
    
    private void ApplyRegisterWrite(int channel, byte regOffset, byte value)
    {
        // Same as MusicPlayer
        int relOffset = regOffset % 4;
        switch (relOffset)
        {
            case 0:
                synth.Channels[channel].Volume = value & 0x0F;
                synth.Channels[channel].DutyCycle = (value >> 6) & 0x03;
                break;
            case 1:
                break;
            case 2:
                synth.Channels[channel].Period = (synth.Channels[channel].Period & 0xFF00) | value;
                break;
            case 3:
                synth.Channels[channel].Period = (synth.Channels[channel].Period & 0x00FF) | ((value & 0x07) << 8);
                break;
        }
    }
    
    private int RegOffsetToChannel(byte regOffset)
    {
        if (regOffset < 0x04) return 0;
        if (regOffset < 0x08) return 1;
        if (regOffset < 0x0C) return 2;
        return 3;
    }
}
```

### 5. Audio Manager (Coordinator)

```csharp
public class AudioManager
{
    private Synthesizer synth;
    private MusicPlayer musicPlayer;
    private SoundEffectPlayer effectPlayer;
    private SoundLibrary soundLibrary;
    private AudioStream audioStream;
    
    private float[] frameBuffer = new float[Synthesizer.SamplesPerFrame];
    
    public AudioManager(SoundLibrary soundLibrary)
    {
        this.soundLibrary = soundLibrary;
        
        synth = new Synthesizer();
        musicPlayer = new MusicPlayer(synth);
        effectPlayer = new SoundEffectPlayer(synth, musicPlayer, soundLibrary);
        
        // Initialize raylib audio
        Raylib.InitAudioDevice();
        audioStream = Raylib.LoadAudioStream(
            Synthesizer.SampleRate,
            16,      // bits
            1        // mono
        );
        Raylib.PlayAudioStream(audioStream);
    }
    
    public void Update()
    {
        // Called once per game frame
        
        musicPlayer.UpdateFrame();
        effectPlayer.UpdateFrame();
        
        synth.GenerateFrame(frameBuffer, 0);
        
        // Update all channel states for next frame
        for (int i = 0; i < 4; i++)
        {
            synth.Channels[i].UpdateFrame();
        }
        
        // Stream audio to raylib
        Raylib.UpdateAudioStream(audioStream, frameBuffer, frameBuffer.Length);
    }
    
    public void LoadMusic(int musicId)
    {
        var musicData = soundLibrary.GetSound(musicId);
        musicPlayer.LoadMusic(musicData);
    }
    
    public void PlaySoundEffect(int soundId)
    {
        effectPlayer.Play(soundId);
    }
    
    public void StopMusic()
    {
        musicPlayer.Stop();
    }
    
    public void Shutdown()
    {
        Raylib.UnloadAudioStream(audioStream);
        Raylib.CloseAudioDevice();
    }
}
```

### 6. Game Integration

```csharp
// In your main game class
public class GameEngine
{
    private AudioManager audio;
    
    public void Initialize()
    {
        // Load sound data from editor export
        var datBytes = File.ReadAllBytes("assets/sounds.dat");
        var soundLibrary = new SoundLibrary(datBytes);
        
        audio = new AudioManager(soundLibrary);
        audio.LoadMusic(0);  // SOUND_OVERWORLD or whatever ID
    }
    
    public void Update()
    {
        audio.Update();  // Call every frame
        
        // Your game logic
        if (playerJumped)
        {
            audio.PlaySoundEffect(SOUND_JUMP);
        }
        if (playerShot)
        {
            audio.PlaySoundEffect(SOUND_GUNSHOT);
        }
    }
    
    public void Shutdown()
    {
        audio.Shutdown();
    }
}
```

## NES ROM Integration

The NES ROM uses a similar architecture, but with 6502 assembly instead of C#.

### NES Sound Driver

```asm
; sound_driver.s (minimal sound driver for NES ROM)

.segment "ZEROPAGE"
channel_ptr:         .res 8    ; 2 bytes * 4 channels
channel_duration:    .res 4
channel_priority:    .res 4
frame_counter:       .res 1

.segment "CODE"

; PlaySoundEffect(A=sound_id, X=primary_channel, Y=secondary_channel)
PlaySoundEffect:
    ; Look up sound data from table
    ; Set channel state
    ; Set priority to SFX
    rts

; UpdateAudio (called once per frame from NMI)
UpdateAudio:
    ; For each channel:
    ;   If active, write next frame of register data
    ;   Decrement duration
    ;   If duration=0, reset to music priority
    rts

; PlayRegisterFrame(A=channel, X=register_offset, Y=value)
PlayRegisterFrame:
    ; Write to APU register
    ; Updates synth channel state
    rts
```

### NES Embed Structure

ROM structure (excerpt):
```
Bank 0:
├── NES driver code (500 bytes)
├── Sound data (gunshot, laser, etc.)
├── Sound lookup table
└── Sound metadata

Bank 1+:
├── Game code
└── Game data
```

## Testing Checklist

- [ ] Synthesizer generates correct waveforms
- [ ] .dat file loads correctly
- [ ] Music plays and loops
- [ ] Sound effect mutes music on correct channels
- [ ] Sound effect unmutes after completion
- [ ] No audio glitches or pops
- [ ] Audio sync is tight (no drift)
- [ ] Windows version matches NES sound (subjectively)

## Performance Notes

**CPU cost:**
- Synthesis: ~2-3% CPU (single frame generation)
- Register parsing: negligible
- Total: <5% per frame (on modern CPU)

**Memory:**
- Synth state: ~100 bytes
- Active effects: ~50 bytes * 4 channels = 200 bytes
- .dat file in RAM: varies (typically 10-50 KB for full game)

## File Locations (Windows Game)

```
assets/
├── sounds.dat          (exported from editor)
├── music.dat           (or separate files per music)
└── ...
```

## Next Steps

1. Copy `Synthesizer` from editor to game project
2. Implement `SoundLibrary` to parse .dat files
3. Implement `MusicPlayer` and `SoundEffectPlayer`
4. Integrate with raylib audio system
5. Test with exported sounds from editor
6. Add to game code (PlaySoundEffect calls in response to game events)
