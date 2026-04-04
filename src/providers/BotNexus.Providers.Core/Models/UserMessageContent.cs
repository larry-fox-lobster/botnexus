using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotNexus.Providers.Core.Models;

/// <summary>
/// Represents user message content that can be either a plain string
/// or a list of content blocks (TextContent | ImageContent).
/// Mirrors pi-mono's <c>string | (TextContent | ImageContent)[]</c> union.
/// </summary>
[JsonConverter(typeof(UserMessageContentConverter))]
public sealed class UserMessageContent
{
    public string? Text { get; }
    public IReadOnlyList<ContentBlock>? Blocks { get; }

    public bool IsText => Text is not null;

    public UserMessageContent(string text) => Text = text;
    public UserMessageContent(IReadOnlyList<ContentBlock> blocks) => Blocks = blocks;

    public static implicit operator UserMessageContent(string text) => new(text);
}

internal sealed class UserMessageContentConverter : JsonConverter<UserMessageContent>
{
    public override UserMessageContent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new UserMessageContent(reader.GetString()!);

        var blocks = JsonSerializer.Deserialize<List<ContentBlock>>(ref reader, options)
                     ?? [];
        return new UserMessageContent(blocks);
    }

    public override void Write(Utf8JsonWriter writer, UserMessageContent value, JsonSerializerOptions options)
    {
        if (value.IsText)
            writer.WriteStringValue(value.Text);
        else
            JsonSerializer.Serialize(writer, value.Blocks, options);
    }
}
