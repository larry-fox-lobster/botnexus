# Research: Media Pipeline — Audio Transcription and Extensible Media Types

## Current State Analysis

### InboundMessage (as of 2026-07-14)
- Location: `src/domain/BotNexus.Domain/Gateway/Models/Messages.cs`
- Pure text: `Content` is a `required string`, no media/attachment properties
- `Metadata` dictionary exists but is untyped — not suitable for structured media data
- `OutboundMessage` is similarly text-only

### Message Flow
- `ChannelAdapterBase.DispatchInboundAsync()` → `IChannelDispatcher.DispatchAsync()` → `IMessageRouter` → `IAgentSupervisor` → Agent
- No middleware/pipeline hooks between dispatch and routing today
- The `IChannelDispatcher` is the natural injection point for media processing

### GatewayHub
- `SendMessage(AgentId, ChannelKey, string content)` — text only
- `DispatchMessageAsync()` constructs `InboundMessage` with string content
- No binary upload support
- SignalR itself supports binary frames via `byte[]` parameters and streaming

### Session History
- `SessionEntry` stores conversation entries
- Text-only content model
- SQLite backing store — would need schema evolution for media columns

### Extension System
- Three extension types: `channels`, `providers`, `tools`
- Extensions register via `IExtensionRegistrar` into DI
- No `IMediaHandler` or pipeline processing concept exists
- Extensions can register arbitrary services — `IMediaHandler` registration is compatible with existing `IExtensionRegistrar`

## Local Whisper Options for Windows/.NET

### Option A: Whisper.net (Recommended for v1)
- **NuGet**: `Whisper.net` + `Whisper.net.Runtime.Cublas` (GPU) or `Whisper.net.Runtime` (CPU)
- **What**: Managed .NET bindings for whisper.cpp
- **Pros**: Native .NET, no subprocess, NuGet distribution, GPU acceleration available
- **Cons**: Native binary dependencies, model files needed separately
- **Models**: GGML format from huggingface.co/ggerganov/whisper.cpp
- **Sizes**: tiny.en ~75MB, base.en ~150MB, small.en ~500MB, medium.en ~1.5GB
- **Performance**: base.en processes ~1min audio in ~3-5 seconds on modern CPU

### Option B: whisper.cpp CLI
- **What**: Standalone C++ binary, called via `Process.Start()`
- **Pros**: No managed dependencies, easy to update independently
- **Cons**: Subprocess overhead, PATH management, less integrated error handling
- **Distribution**: Pre-built binaries from GitHub releases or self-compiled

### Option C: faster-whisper (Python)
- **What**: CTranslate2-based Python implementation
- **Pros**: Fastest option, excellent accuracy, streaming support
- **Cons**: Python dependency, subprocess overhead, more complex deployment
- **Not recommended for v1**: Adds Python as a runtime dependency

### Recommendation
**Whisper.net for v1** — native .NET integration, no subprocess, clean DI-compatible design. Fall back to whisper.cpp CLI as a config option for environments where managed bindings are problematic.

## Browser Audio Recording

### MediaRecorder API
- Supported in all modern browsers (Chrome, Firefox, Safari, Edge)
- Default codec: WebM/Opus (Chrome/Firefox), MP4/AAC (Safari)
- Whisper handles WebM/Opus natively — no client-side conversion needed
- Alternative: Use `AudioContext` + `ScriptProcessorNode` for WAV output (more control, larger files)

### Recommended Client Flow
1. `navigator.mediaDevices.getUserMedia({ audio: true })` — request mic permission
2. `new MediaRecorder(stream, { mimeType: 'audio/webm;codecs=opus' })` — or fallback to default
3. Collect chunks in `ondataavailable`
4. On stop: create `Blob`, convert to `ArrayBuffer`
5. Send via SignalR as `byte[]` parameter

### Size Considerations
- WebM/Opus: ~16KB/sec at default quality → 1 min ≈ 1MB
- WAV: ~176KB/sec (16-bit, 44.1kHz) → 1 min ≈ 10MB
- WebM/Opus is strongly preferred for network transfer
- 5-minute limit at WebM/Opus ≈ 5MB — well within SignalR capabilities

## SignalR Binary Transfer

### Capabilities
- SignalR supports `byte[]` parameters natively
- MessagePack protocol preferred for binary (vs JSON with base64 encoding)
- Default message size limit: 32KB → must increase for audio
- Configure: `MaximumReceiveMessageSize` in hub options (set to 10-50MB)
- Streaming: `IAsyncEnumerable<byte[]>` for chunked upload (optional, for very large files)

### Alternative: HTTP Upload + SignalR Notification
- Upload audio via `POST /api/media` (multipart form)
- Server stores file, returns reference ID
- Client sends regular SignalR message with reference ID in metadata
- Server resolves reference in media pipeline
- **Better for large files** but more complex client code

## Content Parts Model — Prior Art

### OpenAI Chat API
```json
{
  "role": "user",
  "content": [
    { "type": "text", "text": "What's in this image?" },
    { "type": "image_url", "image_url": { "url": "data:image/png;base64,..." } }
  ]
}
```

### Anthropic Messages API
```json
{
  "role": "user",
  "content": [
    { "type": "text", "text": "Describe this audio" },
    { "type": "image", "source": { "type": "base64", "media_type": "image/png", "data": "..." } }
  ]
}
```

### Pattern
Both major LLM APIs use a **content parts array** with typed parts. Our `MessageContentPart` hierarchy aligns with this pattern, making future multimodal passthrough natural.

## Storage Considerations

### Session History Storage
- **Small media (<1MB)**: Store as base64 in JSON `SessionEntry` (SQLite TEXT column)
- **Large media (>1MB)**: Store file on disk, reference in `SessionEntry`
- **Recommended path**: `~/.botnexus/media/{sessionId}/{entryId}.{ext}`
- **Cleanup**: Media files cleaned up when session is sealed + retention period expires

### Temp Files During Processing
- Pipeline writes incoming binary to temp dir for processing
- Location: `Path.GetTempPath()` or configured `~/.botnexus/tmp/`
- Cleaned up immediately after processing (in `finally` block)
- Max concurrent processing should be bounded to prevent disk exhaustion

## Performance Estimates

| Operation | Duration | Notes |
|-----------|----------|-------|
| Browser recording + encode | Real-time | WebM/Opus is real-time |
| SignalR upload (1MB) | <1 sec | Local network |
| Whisper base.en (1 min audio) | 3-5 sec | CPU, modern hardware |
| Whisper base.en (1 min audio) | <1 sec | GPU (CUDA) |
| Total round-trip (1 min voice) | 5-8 sec | CPU path |
| Total round-trip (1 min voice) | 2-3 sec | GPU path |

Acceptable for push-to-talk UX. User records, gets transcript back in a few seconds.

## Risks

1. **Model download/setup**: First-time setup requires downloading Whisper model (~150MB for base.en). Need clear setup docs or auto-download.
2. **GPU support**: CUDA bindings add complexity. CPU-only is fine for v1 — base.en on CPU is fast enough for short recordings.
3. **Audio format compatibility**: Whisper expects specific formats. May need FFmpeg for conversion of exotic formats. WebM/Opus from browsers works natively.
4. **Memory pressure**: Loading Whisper model into memory (~300MB for base.en). Keep model loaded as singleton, not per-request.
5. **Concurrent transcription**: Multiple users sending audio simultaneously. Queue or semaphore to prevent memory exhaustion. For single-user (Jon's setup), not an issue.
