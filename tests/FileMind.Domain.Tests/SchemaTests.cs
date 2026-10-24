using System.Collections.Concurrent;
using Json.Schema;
using Xunit;

namespace FileMind.Domain.Tests;

public class SchemaTests
{
    private static readonly ConcurrentDictionary<string, JsonSchema> Cache = new();
    private static readonly object LoadLock = new();

    private static JsonSchema LoadSchema(string relative)
    {
        lock (LoadLock)
        {
            return Cache.GetOrAdd(relative, r =>
            {
                var name = "FileMind.Domain.Tests." + r.Replace('/', '.');
                using var stream = typeof(SchemaTests).Assembly.GetManifestResourceStream(name)
                    ?? throw new FileNotFoundException($"Embedded schema '{name}' not found.");
                using var reader = new StreamReader(stream);
                return JsonSchema.FromText(reader.ReadToEnd());
            });
        }
    }

    private const string ValidRule = """
        {
          "schemaVersion": 1,
          "id": "rule-school-it421",
          "name": "Route IT421 to School",
          "enabled": true,
          "priority": 10,
          "conditionRoot": {
            "operator": "And",
            "conditions": [
              { "field": "Subject", "operator": "Equals", "value": "IT421" }
            ]
          },
          "actions": [
            { "kind": "Move", "destinationTemplate": "School/{Subject}/{YYYY}", "conflictPolicy": "KeepBoth" }
          ]
        }
        """;

    [Fact]
    public void RuleSchema_ValidRulePasses()
    {
        var schema = LoadSchema("schemas/rules/rule.schema.json");
        var result = schema.Evaluate(System.Text.Json.JsonDocument.Parse(ValidRule).RootElement);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RuleSchema_MissingSchemaVersionFails()
    {
        var schema = LoadSchema("schemas/rules/rule.schema.json");
        var doc = System.Text.Json.JsonDocument.Parse(ValidRule.Replace("\"schemaVersion\": 1,", "")).RootElement;
        Assert.False(schema.Evaluate(doc).IsValid);
    }

    [Fact]
    public void RuleSchema_UnknownFieldFails()
    {
        var schema = LoadSchema("schemas/rules/rule.schema.json");
        var doc = System.Text.Json.JsonDocument.Parse(ValidRule.Replace("\"id\":", "\"bogus\": 1, \"id\":")).RootElement;
        Assert.False(schema.Evaluate(doc).IsValid);
    }

    [Fact]
    public void ProfileSchema_MinimalProfilePasses()
    {
        var schema = LoadSchema("schemas/profiles/organizer-profile.schema.json");
        var doc = System.Text.Json.JsonDocument.Parse("""{ "schemaVersion": 1, "subjects": [ { "id": "IT421", "name": "IT421 Networks" } ] }""").RootElement;
        Assert.True(schema.Evaluate(doc).IsValid);
    }

    [Fact]
    public void ProfileSchema_MissingVersionFails()
    {
        var schema = LoadSchema("schemas/profiles/organizer-profile.schema.json");
        Assert.False(schema.Evaluate(System.Text.Json.JsonDocument.Parse("{}").RootElement).IsValid);
    }

    [Fact]
    public void AiRequestSchema_MinimizedPayloadPasses()
    {
        var schema = LoadSchema("schemas/ai/ai-request.schema.json");
        var doc = System.Text.Json.JsonDocument.Parse("""
            { "schemaVersion": 1, "name": "report.pdf", "ext": "pdf",
              "metadata": { "author": "Jane" }, "snippet": "first lines" }
            """).RootElement;
        Assert.True(schema.Evaluate(doc).IsValid);
    }

    [Fact]
    public void AiRequestSchema_UnexpectedFieldFails()
    {
        var schema = LoadSchema("schemas/ai/ai-request.schema.json");
        var doc = System.Text.Json.JsonDocument.Parse("""
            { "schemaVersion": 1, "name": "a.pdf", "ext": "pdf", "rawContents": "leak" }
            """).RootElement;
        Assert.False(schema.Evaluate(doc).IsValid);
    }

    [Fact]
    public void AiResponseSchema_SuggestionPasses()
    {
        var schema = LoadSchema("schemas/ai/ai-response.schema.json");
        var doc = System.Text.Json.JsonDocument.Parse("""
            { "collection": "school", "subject": "IT421", "confidenceHint": 80,
              "explanation": "networks content" }
            """).RootElement;
        Assert.True(schema.Evaluate(doc).IsValid);
    }

    [Fact]
    public void AiResponseSchema_ConfidenceOutOfRangeFails()
    {
        var schema = LoadSchema("schemas/ai/ai-response.schema.json");
        var doc = System.Text.Json.JsonDocument.Parse("""{ "confidenceHint": 140 }""").RootElement;
        Assert.False(schema.Evaluate(doc).IsValid);
    }
}
