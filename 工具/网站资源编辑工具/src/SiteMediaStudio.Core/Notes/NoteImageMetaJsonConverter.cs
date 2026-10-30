using System.Text.Json;
using System.Text.Json.Serialization;

namespace PersonalWorks.SiteMediaStudio.Core;

/// <summary>
/// 兼容 index.json 里 images 的两种写法：纯路径字符串，或带 stageRel／objectKey 等字段的对象。
/// 读入时字符串只填 <see cref="NoteImageMeta.ObjectKey"/>（去掉前导斜杠）；写出时始终写成对象。
/// </summary>
public sealed class NoteImageMetaJsonConverter : JsonConverter<NoteImageMeta>
{
    /// <inheritdoc />
    public override NoteImageMeta Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new NoteImageMeta();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var path = reader.GetString() ?? "";
            return new NoteImageMeta
            {
                ObjectKey = TrimLeadingSlash(path)
            };
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("心得配图项必须是字符串路径或对象。");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var meta = new NoteImageMeta
        {
            StageRel = ReadString(root, "stageRel"),
            ObjectKey = TrimLeadingSlash(ReadString(root, "objectKey")),
            Stars = ReadInt(root, "stars"),
            DisplayName = ReadString(root, "displayName"),
            Description = ReadString(root, "description"),
            Hidden = ReadBool(root, "hidden"),
            Tags = ReadStringList(root, "tags")
        };
        return meta;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, NoteImageMeta value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("stageRel", value.StageRel ?? "");
        writer.WriteString("objectKey", value.ObjectKey ?? "");
        writer.WriteNumber("stars", value.Stars);
        writer.WritePropertyName("tags");
        JsonSerializer.Serialize(writer, value.Tags ?? new List<string>(), options);
        writer.WriteString("displayName", value.DisplayName ?? "");
        writer.WriteString("description", value.Description ?? "");
        writer.WriteBoolean("hidden", value.Hidden);
        writer.WriteEndObject();
    }

    private static string TrimLeadingSlash(string path)
    {
        return string.IsNullOrEmpty(path) ? "" : path.TrimStart('/');
    }

    private static string ReadString(JsonElement root, string name)
    {
        if (!TryGetPropertyIgnoreCase(root, name, out var property))
        {
            return "";
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() ?? "" : "";
    }

    private static int ReadInt(JsonElement root, string name)
    {
        if (!TryGetPropertyIgnoreCase(root, name, out var property))
        {
            return 0;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : 0;
    }

    private static bool ReadBool(JsonElement root, string name)
    {
        if (!TryGetPropertyIgnoreCase(root, name, out var property))
        {
            return false;
        }

        return property.ValueKind is JsonValueKind.True or JsonValueKind.False && property.GetBoolean();
    }

    private static List<string> ReadStringList(JsonElement root, string name)
    {
        var list = new List<string>();
        if (!TryGetPropertyIgnoreCase(root, name, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                list.Add(item.GetString() ?? "");
            }
        }

        return list;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string name, out JsonElement property)
    {
        if (root.TryGetProperty(name, out property))
        {
            return true;
        }

        foreach (var candidate in root.EnumerateObject())
        {
            if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}