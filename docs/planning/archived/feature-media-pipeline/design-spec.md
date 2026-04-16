---
id: feature-media-pipeline
title: "Media Pipeline — Audio Transcription and Extensible Media Types"
type: feature
priority: high
status: delivered
created: 2026-07-14
updated: 2026-07-14
author: nova
tags: [media, audio, transcription, extension, ddd, pipeline, whisper]
depends_on: []
ddd_types: [InboundMessage, OutboundMessage, ChannelKey, SessionEntry, IChannelAdapter, IExtensionRegistrar]
---

# Design Spec: Media Pipeline — Audio Transcription and Extensible Media Types

**Type**: Feature
**Priority**: High (architecture validation — proves extension model for non-text media)
**Status**: Delivered
**Author**: Nova (via Jon)

## Problem

BotNexus messages are text-only today. `InboundMessage.Content` is a `string`, and the entire pipeline — channels, dispatcher, agent loop, session history — assumes text. There is no concept of media attachments, binary payloads, or content type negotiation.

This means:
1. Users cannot send audio recordings to agents (voice notes, dictation)
2. Users cannot send images for visual analysis
3. Extensions cannot introduce new media types without modifying core domain types
4. The platform cannot evolve to support multimodal interactions

## Goal

Introduce an **extensible media pipeline** that allows non-text content to enter the system, be processed (transformed) server-side, and arrive at the agent as usable input. The first concrete use case is **audio transcription** — recording audio in the browser, uploading it, transcribing server-side via a local Whisper model, and injecting the transcript as text into the agent session.

This is deliberately chosen as an **architecture validation test** for the extension system and DDD model:
- It touches multiple bounded contexts (channel, gateway, agent)
- It introduces a new concept (media) that must flow through the domain
- It proves extensions can add processing stages to the message pipeline
- It establishes patterns that future media types (images, files, video) can follow

## Design Principles

1. **Messages become content envelopes** — a message carries typed content parts, not just a text string
2. **Media processing is a pipeline** — extensions register as media handlers that transform content (audio → text, image → description)
3. **Server-side processing** — transcription and media analysis happen on the gateway, not in the client
4. **Local models preferred** — use Whisper.cpp or faster-whisper for transcription, keeping it self-contained with no external API dependency
5. **Extensions add media types** — core defines the envelope; extensions define the content types they handle
6. **Agents receive what they understand** — the pipeline transforms media into agent-consumable form (text for text-only models, or native media tokens for multimodal models)

## Architecture

### Layer 1: Domain — Content Model

The `InboundMessage` currently has a single `Content` string. We introduce a **content parts** model alongside it for backwards compatibility.

```csharp
/// <summary>
/// A typed content part within a message. Messages can contain multiple parts
/// (e.g., text + image, or text transcribed from audio).
/// </summary>
public abstract record MessageContentPart
{
    /// <summary>MIME type of this content part (e.g., "text/plain", "audio/webm", "image/png").</summary>
    public required string MimeType { get; init; }
}

/// <summary>Text content part — the most common type.</summary>
public sealed record TextContentPart : MessageContentPart
{
    public required string Text { get; init; }
}

/// <summary>Binary content part — for media that hasn't been (or can't be) converted to text.</summary>
public sealed record BinaryContentPart : MessageContentPart
{
    /// <summary>Binary payload. Stored as byte array; serialized as base64 in JSON/transport.</summary>
    public required byte[] Data { get; init; }

    /// <summary>Original filename, if provided by the client.</summary>
    public string? FileName { get; init; }

    /// <summary>Duration in seconds, for audio/video content.</summary>
    public double? DurationSeconds { get; init; }
}

/// <summary>
/// Reference content part — for large media stored externally (blob storage, temp file).
/// The pipeline resolves the reference to actual data when needed.
/// </summary>
public sealed record ReferenceContentPart : MessageContentPart
{
    /// <summary>URI or path to the stored content.</summary>
    public required string Uri { get; init; }

    /// <summary>Size in bytes, if known.</summary>
    public long? SizeBytes { get; init; }
}
```

**InboundMessage extension:**

```csharp
public sealed record InboundMessage
{
    // ... existing properties unchanged ...

    /// <summary>The message text content. Primary content for text-only messages.</summary>
    public required string Content { get; init; }

    /// <summary>
    /// Typed content parts for multimodal messages. If populated, these are the
    /// authoritative content; Content may contain a text summary.
    /// </summary>
    public IReadOnlyList<MessageContentPart>? ContentParts { get; init; }
}
```

This is **additive and backwards-compatible**. Existing code reads `Content` as before. New code can check `ContentParts` for richer content.

### Layer 2: Media Processing Pipeline

A new pipeline stage sits between channel dispatch and agent routing. Media handlers are registered by extensions and invoked based on content MIME type.

```csharp
/// <summary>
/// Processes a specific media type in the inbound message pipeline.
/// Transforms binary/reference content into agent-consumable form.
/// </summary>
public interface IMediaHandler
{
    /// <summary>MIME type patterns this handler processes (e.g., "audio/*", "image/png").</summary>
    IReadOnlyList<string> SupportedMimeTypes { get; }

    /// <summary>Processing priority. Lower = earlier. Default handlers at 1000.</summary>
    int Priority { get; }

    /// <summary>
    /// Process a content part, returning zero or more replacement parts.
    /// For example, an audio handler returns a TextContentPart with the transcript.
    /// </summary>
    Task<IReadOnlyList<MessageContentPart>> ProcessAsync(
        MessageContentPart input,
        MediaProcessingContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Context provided to media handlers during processing.</summary>
public record MediaProcessingContext
{
    /// <summary>The full inbound message (for metadata access).</summary>
    public required InboundMessage Message { get; init; }

    /// <summary>The session this message belongs to.</summary>
    public string? SessionId { get; init; }

    /// <summary>The target agent (may influence processing — e.g., multimodal-capable agents skip transcription).</summary>
    public string? TargetAgentId { get; init; }

    /// <summary>Temporary storage path for intermediate files.</summary>
    public required string TempPath { get; init; }
}
```

**Pipeline orchestrator:**

```csharp
/// <summary>
/// Orchestrates media processing by routing content parts to registered handlers.
/// </summary>
public interface IMediaPipeline
{
    /// <summary>
    /// Process all content parts in a message, returning a new message with
    /// transformed content. Text-only messages pass through unchanged.
    /// </summary>
    Task<InboundMessage> ProcessAsync(
        InboundMessage message,
        CancellationToken cancellationToken = default);
}
```

The `IMediaPipeline` is invoked by the `IChannelDispatcher` before routing to the agent. This is a single well-defined integration point:

```
Channel Adapter → IChannelDispatcher → [IMediaPipeline] → IMessageRouter → IAgentSupervisor → Agent
```

If no `ContentParts` are present, the pipeline is a no-op (zero overhead for text messages).

### Layer 3: Audio Transcription Extension

The first media handler — audio transcription via local Whisper.

**Extension type**: `tools` (it's a processing capability, not a channel or provider)

**Alternatively**: This could argue for a new extension type `media-handlers` or `processors`. However, using the existing `tools` type is pragmatic for v1. The handler is registered via `IExtensionRegistrar` alongside an `IMediaHandler` registration — the extension system already supports arbitrary DI registrations.

```
extensions/
└── tools/
    └── audio-transcription/
        ├── BotNexus.Tools.AudioTranscription.dll
        ├── whisper-model/            (or configured path to model files)
        └── ...dependencies
```

**Implementation sketch:**

```csharp
public sealed class WhisperTranscriptionHandler : IMediaHandler
{
    private readonly WhisperTranscriptionConfig _config;
    private readonly ILogger _logger;

    public IReadOnlyList<string> SupportedMimeTypes => ["audio/*"];
    public int Priority => 100;

    public async Task<IReadOnlyList<MessageContentPart>> ProcessAsync(
        MessageContentPart input,
        MediaProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        if (input is not BinaryContentPart binary)
            return [input]; // pass through

        // Write audio to temp file
        var tempFile = Path.Combine(context.TempPath, $"{Guid.NewGuid()}.wav");
        await File.WriteAllBytesAsync(tempFile, binary.Data, cancellationToken);

        try
        {
            // Run whisper transcription (via CLI or native binding)
            var transcript = await TranscribeAsync(tempFile, cancellationToken);

            return
            [
                new TextContentPart
                {
                    MimeType = "text/plain",
                    Text = transcript
                }
            ];
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
```

**Configuration:**

```json
{
  "BotNexus": {
    "Extensions": {
      "tools:audio-transcription": { "enabled": true }
    },
    "Tools": {
      "Extensions": {
        "audio-transcription": {
          "backend": "whisper-cpp",
          "modelPath": "~/.botnexus/models/whisper/ggml-base.en.bin",
          "language": "en",
          "maxDurationSeconds": 300
        }
      }
    }
  }
}
```

### Layer 4: Channel Upload Support

Channels need a way to receive binary data alongside messages. Two approaches, both needed:

#### A. SignalR Binary Upload (WebUI)

SignalR supports binary frames. The `GatewayHub` gets a new method:

```csharp
/// <summary>
/// Send a message with media attachments.
/// </summary>
public async Task<object> SendMessageWithMedia(
    AgentId agentId,
    ChannelKey channelType,
    string? textContent,
    IReadOnlyList<MediaAttachment> attachments)
{
    // Build content parts from attachments
    var parts = new List<MessageContentPart>();

    if (!string.IsNullOrWhiteSpace(textContent))
        parts.Add(new TextContentPart { MimeType = "text/plain", Text = textContent });

    foreach (var attachment in attachments)
        parts.Add(new BinaryContentPart
        {
            MimeType = attachment.MimeType,
            Data = attachment.Data,
            FileName = attachment.FileName,
            DurationSeconds = attachment.DurationSeconds
        });

    // Resolve session, dispatch with content parts...
}

public record MediaAttachment(
    string MimeType,
    byte[] Data,
    string? FileName = null,
    double? DurationSeconds = null);
```

#### B. HTTP Upload Endpoint (REST API)

For clients that prefer HTTP multipart uploads (mobile apps, CLI tools, non-SignalR channels):

```
POST /api/sessions/{sessionId}/media
Content-Type: multipart/form-data

Parts:
  - file: (binary audio/image data)
  - metadata: { "agentId": "nova", "textContent": "transcribe this" }
```

The API controller creates the `InboundMessage` with `ContentParts` and dispatches normally.

### Layer 5: WebUI Client — Audio Recording

The browser client adds audio recording capability:

1. **Record button** in the message input area
2. Uses `MediaRecorder` API to capture microphone audio
3. Encodes as WebM/Opus (native browser format) or WAV
4. Sends via `SendMessageWithMedia` SignalR method
5. Shows a "transcribing..." indicator while the server processes
6. Transcript appears as the user message in the conversation

**UX flow:**
```
User clicks 🎤 → Recording starts → User clicks ⏹️ → Audio blob created
→ Sent via SignalR → Server transcribes → Transcript injected as user message
→ Agent responds to transcript text
```

### Layer 6: Session History

`SessionEntry` needs to support content parts for accurate history:

```csharp
public class SessionEntry
{
    // ... existing properties ...

    /// <summary>
    /// Original content parts before media processing.
    /// Null for plain text messages.
    /// </summary>
    public IReadOnlyList<MessageContentPart>? OriginalContentParts { get; set; }

    /// <summary>
    /// Processed content parts after media pipeline.
    /// Null for plain text messages.
    /// </summary>
    public IReadOnlyList<MessageContentPart>? ProcessedContentParts { get; set; }
}
```

This preserves the audit trail: we store both what the user sent (audio) and what the agent received (transcript). The WebUI can render both — showing a playback button for the original audio alongside the transcript text.

## Implementation Phases

### Phase 1: Domain Content Model (Foundation)
**Effort**: Small
**Risk**: Low

- Add `MessageContentPart` types to `BotNexus.Domain`
- Add `ContentParts` property to `InboundMessage` (additive, non-breaking)
- Add `OriginalContentParts` / `ProcessedContentParts` to `SessionEntry`
- Update session store serialization to handle content parts
- All existing code continues to work — `Content` string unchanged

### Phase 2: Media Pipeline Core
**Effort**: Medium
**Risk**: Low-Medium

- Define `IMediaHandler` and `IMediaPipeline` interfaces in `BotNexus.Gateway.Contracts`
- Implement `MediaPipeline` orchestrator in `BotNexus.Gateway`
- Wire into `IChannelDispatcher` (invoke pipeline before routing)
- Register `IMediaHandler` instances from extension DI
- No-op for text-only messages (zero performance impact)

### Phase 3: Audio Transcription Extension
**Effort**: Medium
**Risk**: Medium (depends on Whisper model setup)

- Create `BotNexus.Tools.AudioTranscription` extension project
- Implement `WhisperTranscriptionHandler` as `IMediaHandler`
- Support whisper.cpp CLI backend (simplest, most portable)
- Configuration for model path, language, max duration
- Extension registrar registers both `IMediaHandler` and optionally an `ITool` (for agents to request transcription of files)

### Phase 4: Channel Upload Support
**Effort**: Medium
**Risk**: Low

- Add `SendMessageWithMedia` to `GatewayHub`
- Add HTTP multipart upload endpoint
- Update `SignalRChannelAdapter` for binary content
- Size limits and validation

### Phase 5: WebUI Audio Recording
**Effort**: Small-Medium
**Risk**: Low

- Add microphone recording UI (record button, waveform indicator)
- `MediaRecorder` API integration
- Send audio via `SendMessageWithMedia`
- Transcription progress indicator
- Transcript display in conversation

### Phase 6: Future Media Types (Deferred)
- **Image analysis**: `IMediaHandler` for `image/*` → description text (via local vision model or Azure AI)
- **Document extraction**: `IMediaHandler` for `application/pdf` → extracted text
- **Video**: `IMediaHandler` for `video/*` → keyframe descriptions + audio transcript
- **Multimodal passthrough**: For agents using multimodal models (GPT-4o, Claude), skip transcription and pass native media tokens directly

## DDD Alignment

| Concept | Bounded Context | Notes |
|---------|----------------|-------|
| `MessageContentPart` | Domain (Primitives) | Core value object — part of the ubiquitous language |
| `InboundMessage.ContentParts` | Gateway (Models) | Extends existing message model |
| `IMediaHandler` | Gateway (Contracts) | Extension point for media processing |
| `IMediaPipeline` | Gateway (Core) | Orchestrator — internal to gateway bounded context |
| `WhisperTranscriptionHandler` | Extension (Tools) | Concrete handler — lives outside core |
| `SendMessageWithMedia` | Channel (SignalR) | Channel-specific transport concern |
| `SessionEntry` content parts | Session (Domain) | Preserves media audit trail |

The media pipeline sits cleanly in the **Gateway bounded context** because it's a message processing concern — it transforms inbound messages before they reach agents. Extensions provide the actual processing logic, keeping the core clean.

## Open Questions

1. **New extension type?** Should media handlers be `tools:audio-transcription` or a new `media:audio-transcription` type? Using `tools` works now but `media` would be more semantically correct as the extension system grows.

2. **Large file handling**: For audio > 10MB, should we use `ReferenceContentPart` with temp file storage instead of in-memory `BinaryContentPart`? Probably yes — the pipeline should stream to disk for large payloads.

3. **Model selection**: Which Whisper model to default? `base.en` is fast and small (~150MB). `medium.en` is more accurate but slower (~1.5GB). Config-driven, but what's the default?

4. **Agent capability negotiation**: If an agent's LLM supports native audio (GPT-4o audio mode), should the pipeline skip transcription and pass audio directly? This requires knowing the provider's capabilities at processing time.

5. **Content part storage**: SQLite blob columns for binary content parts in session history? Or store media files on disk with references in the DB? Disk references are better for large files.

## Non-Goals (v1)

- Real-time streaming transcription (separate channel type, much more complex)
- Voice response / TTS (already have `sag` for this — separate concern)
- Audio/video calling (entirely different domain)
- Client-side transcription (unreliable, inconsistent across browsers)
- Multi-language auto-detection (v1 is configured language, likely English)
