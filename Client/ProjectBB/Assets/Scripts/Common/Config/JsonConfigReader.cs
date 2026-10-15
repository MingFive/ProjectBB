using System;
using System.IO;
using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace ProjectBB.Common
{
    /// <summary>加载期通用 JSON 解析；拒绝重复字段、未知字段及尾随内容，不持有业务缓存。</summary>
    public static class JsonConfigReader
    {
        public static T Read<T>(string json, string source) where T : class
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException($"{source}: 配置内容为空。");
            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)))
                {
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.MaxDepth = 32;
                    var token = JToken.Load(reader, new JsonLoadSettings
                    {
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Load
                    });
                    if (token.Type != JTokenType.Object) throw new JsonException("根节点必须是对象。");
                    if (reader.Read()) throw new JsonException("根节点后存在额外内容。");
                    var serializer = JsonSerializer.Create(new JsonSerializerSettings
                    {
                        MissingMemberHandling = MissingMemberHandling.Error,
                        TypeNameHandling = TypeNameHandling.None,
                        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                        DefaultValueHandling = DefaultValueHandling.Populate,
                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                        Culture = System.Globalization.CultureInfo.InvariantCulture
                    });
                    ValidateShape(token, typeof(T), serializer);
                    return token.ToObject<T>(serializer) ?? throw new JsonException("配置为空。");
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException($"{source}: {exception.Message}", exception);
            }
        }

        // 只有明确声明 DefaultValue 的扩展字段可省略；已有必填字段及显式 null 仍严格校验。
        private static void ValidateShape(JToken token, Type type, JsonSerializer serializer)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            bool valid;
            if (type == typeof(string)) valid = token.Type == JTokenType.String;
            else if (type == typeof(bool)) valid = token.Type == JTokenType.Boolean;
            else if (type == typeof(int)) valid = token.Type == JTokenType.Integer;
            else if (type == typeof(double)) valid = token.Type == JTokenType.Integer || token.Type == JTokenType.Float;
            else if (type.IsArray)
            {
                valid = token.Type == JTokenType.Array;
                if (valid) foreach (JToken item in (JArray)token) ValidateShape(item, type.GetElementType(), serializer);
            }
            else
            {
                valid = token.Type == JTokenType.Object;
                if (valid)
                {
                    var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(type);
                    var obj = (JObject)token;
                    foreach (JsonProperty property in contract.Properties)
                    {
                        if (!obj.TryGetValue(property.PropertyName, StringComparison.Ordinal, out JToken child))
                        {
                            if (property.AttributeProvider.GetAttributes(typeof(DefaultValueAttribute), true).Count > 0) continue;
                            throw new JsonException($"{token.Path}.{property.PropertyName}: 缺失必填字段。");
                        }
                        ValidateShape(child, property.PropertyType, serializer);
                    }
                    foreach (JProperty property in obj.Properties())
                        if (contract.Properties.GetProperty(property.Name, StringComparison.Ordinal) == null)
                            throw new JsonException($"{property.Path}: 未知字段。");
                }
            }
            if (!valid) throw new JsonException($"{token.Path}: 类型错误，预期 {type.Name}，实际 {token.Type}。");
        }
    }
}
