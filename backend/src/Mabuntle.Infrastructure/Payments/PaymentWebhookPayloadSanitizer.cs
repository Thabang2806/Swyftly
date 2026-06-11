using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mabuntle.Infrastructure.Payments;

public static class PaymentWebhookPayloadSanitizer
{
    private const int MaxStoredValueLength = 2048;
    private const string RedactedValue = "[redacted]";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly string[] SensitiveKeyFragments =
    [
        "signature",
        "token",
        "secret",
        "password",
        "passphrase",
        "card",
        "cvv",
        "cvc",
        "account_number"
    ];

    public static string Sanitize(string provider, string payload)
    {
        if (TrySanitizeJson(provider, payload, out var jsonPayload))
        {
            return jsonPayload;
        }

        if (TrySanitizeForm(provider, payload, out var formPayload))
        {
            return formPayload;
        }

        return new JsonObject
        {
            ["provider"] = provider,
            ["payloadType"] = "text",
            ["value"] = Truncate(payload)
        }.ToJsonString(JsonOptions);
    }

    private static bool TrySanitizeJson(string provider, string payload, out string sanitizedPayload)
    {
        sanitizedPayload = string.Empty;
        try
        {
            var node = JsonNode.Parse(payload);
            if (node is null)
            {
                return false;
            }

            var envelope = new JsonObject
            {
                ["provider"] = provider,
                ["payloadType"] = "json",
                ["body"] = RedactNode(node)
            };
            sanitizedPayload = envelope.ToJsonString(JsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TrySanitizeForm(string provider, string payload, out string sanitizedPayload)
    {
        sanitizedPayload = string.Empty;
        if (!payload.Contains('=', StringComparison.Ordinal))
        {
            return false;
        }

        IReadOnlyList<KeyValuePair<string, string>> pairs;
        try
        {
            pairs = PayFastFormEncoder.ParseOrderedPairs(payload);
        }
        catch (UriFormatException)
        {
            return false;
        }

        if (pairs.Count == 0)
        {
            return false;
        }

        var fields = new JsonObject();
        foreach (var pair in pairs)
        {
            fields[pair.Key] = IsSensitiveKey(pair.Key)
                ? RedactedValue
                : Truncate(pair.Value);
        }

        var envelope = new JsonObject
        {
            ["provider"] = provider,
            ["payloadType"] = "form",
            ["fields"] = fields
        };
        sanitizedPayload = envelope.ToJsonString(JsonOptions);
        return true;
    }

    private static JsonNode? RedactNode(JsonNode? node)
    {
        if (node is JsonObject jsonObject)
        {
            var sanitized = new JsonObject();
            foreach (var property in jsonObject)
            {
                sanitized[property.Key] = IsSensitiveKey(property.Key)
                    ? RedactedValue
                    : RedactNode(property.Value);
            }

            return sanitized;
        }

        if (node is JsonArray jsonArray)
        {
            var sanitized = new JsonArray();
            foreach (var item in jsonArray)
            {
                sanitized.Add(RedactNode(item));
            }

            return sanitized;
        }

        if (node is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var stringValue))
        {
            return Truncate(stringValue);
        }

        return node?.DeepClone();
    }

    private static bool IsSensitiveKey(string key) =>
        SensitiveKeyFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string value) =>
        value.Length <= MaxStoredValueLength
            ? value
            : string.Concat(value.AsSpan(0, MaxStoredValueLength), "...[truncated]");
}
