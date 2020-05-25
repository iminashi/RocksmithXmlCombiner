using RSXmlCombinerGUI.Extensions;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RSXmlCombinerGUI.Models
{
    public sealed class ArrangementJsonConverter : JsonConverter<Arrangement>
    {
        public override bool CanConvert(Type typeToConvert) =>
            typeof(Arrangement).IsAssignableFrom(typeToConvert);

        public override Arrangement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException();

            reader.Read();
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException();

            string propertyName = reader.GetString();
            if (propertyName != "ArrangementType") throw new JsonException();

            reader.Read();
            if (reader.TokenType != JsonTokenType.Number) throw new JsonException();

            ArrangementType arrangementType = (ArrangementType)reader.GetInt32();
            Arrangement arrangement = arrangementType switch
            {
                var a when a.IsInstrumental() => new InstrumentalArrangement(arrangementType),
                var a when a.IsVocals() => new VocalsArrangement(arrangementType),
                ArrangementType.ShowLights => new ShowLightsArrangement(),
                _ => throw new JsonException()
            };

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return arrangement;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    propertyName = reader.GetString();
                    reader.Read();
                    switch (propertyName)
                    {
                        case "FileName":
                            arrangement.FileName = reader.GetString();
                            break;
                        case "BaseTone":
                            ((InstrumentalArrangement)arrangement).BaseTone = reader.GetString();
                            break;
                        case "ToneNames":
                            ((InstrumentalArrangement)arrangement).ToneNames = JsonSerializer.Deserialize<List<string>>(ref reader, options);
                            break;
                        case "ToneReplacements":
                            ((InstrumentalArrangement)arrangement).ToneReplacements = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options);
                            break;
                    }
                }
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, Arrangement arrangement, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("ArrangementType", (int)arrangement.ArrangementType);
            writer.WriteString("FileName", arrangement.FileName);

            if (arrangement is InstrumentalArrangement instArr)
            {
                writer.WriteString("BaseTone", instArr.BaseTone);
                if (instArr.ToneNames != null)
                {
                    writer.WriteStartArray("ToneNames");
                    foreach (var tone in instArr.ToneNames)
                    {
                        writer.WriteStringValue(tone);
                    }
                    writer.WriteEndArray();
                }

                writer.WritePropertyName("ToneReplacements");
                JsonSerializer.Serialize(writer, instArr.ToneReplacements, options);
            }

            writer.WriteEndObject();
        }
    }
}
