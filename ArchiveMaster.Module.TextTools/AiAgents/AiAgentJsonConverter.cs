using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using ArchiveMaster.AiAgents;
using ArchiveMaster.Attributes;

namespace ArchiveMaster.AiAgents;

public class AiAgentListJsonConverter : JsonConverter<List<AiAgentBase>>
{
    public const string TypeFieldName = "Type";

    public override List<AiAgentBase> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException();

        var list = new List<AiAgentBase>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var jsonObject = doc.RootElement;

            if (!jsonObject.TryGetProperty(TypeFieldName, out var typeProp))
                throw new JsonException("找不到AiAgentBase的类型标记");

            string typeName = typeProp.GetString()!;

            // 通过反射找到具体类型
            Type type = Assembly.GetExecutingAssembly().GetTypes()
                .FirstOrDefault(t => typeof(AiAgentBase).IsAssignableFrom(t) && t.Name == typeName);

            if (type == null)
            {
                throw new JsonException($"找不到类型名为{typeName}的AiAgentBase派生类");
            }

            var agent = (AiAgentBase)JsonSerializer.Deserialize(jsonObject.GetRawText(), type, options)!;
            list.Add(agent);
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<AiAgentBase> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var agent in value)
        {
            writer.WriteStartObject();

            // 写入类型标记
            writer.WriteString(TypeFieldName, agent.GetType().Name);

            // 写入属性
            var props = agent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (!prop.CanRead || prop.GetMethod == null) continue;

                // 只序列化基类属性或者带 AiAgentConfigAttribute 的派生属性
                bool hasAttribute = prop.GetCustomAttribute<AiAgentConfigAttribute>() != null;
                if (prop.DeclaringType != typeof(AiAgentBase) || hasAttribute ||
                    prop.Name == nameof(AiAgentBase.ExtraPrompt))
                {
                    object propValue = prop.GetValue(agent);
                    writer.WritePropertyName(prop.Name);
                    JsonSerializer.Serialize(writer, propValue, prop.PropertyType, options);
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}