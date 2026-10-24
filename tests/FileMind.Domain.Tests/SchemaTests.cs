using System.Collections.Concurrent;
using Json.Schema;
using Xunit;

namespace FileMind.Domain.Tests;

public class SchemaTests
{
    private static readonly ConcurrentDictionary<string, JsonSchema> Cache = new();
    private static readonly object LoadLock = new();

    private static string RepoPath(string relative) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", relative));

    private static JsonSchema LoadSchema(string relative)
    {
        lock (LoadLock)
        {
            return Cache.GetOrAdd(relative, r => JsonSchema.FromText(File.ReadAllText(RepoPath(r))));
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
}
