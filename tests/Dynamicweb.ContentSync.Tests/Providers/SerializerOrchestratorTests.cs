using Dynamicweb.ContentSync.Models;
using Dynamicweb.ContentSync.Providers;
using Moq;
using Xunit;

namespace Dynamicweb.ContentSync.Tests.Providers;

[Trait("Category", "Phase14")]
public class SerializerOrchestratorTests
{
    private readonly Mock<ISerializationProvider> _contentProvider;
    private readonly Mock<ISerializationProvider> _sqlTableProvider;
    private readonly ProviderRegistry _registry;
    private readonly SerializerOrchestrator _orchestrator;

    private static readonly ProviderPredicateDefinition ContentPred1 = new()
    {
        Name = "Pages",
        ProviderType = "Content",
        Path = "/",
        AreaId = 1
    };

    private static readonly ProviderPredicateDefinition ContentPred2 = new()
    {
        Name = "Blog",
        ProviderType = "Content",
        Path = "/blog",
        AreaId = 1
    };

    private static readonly ProviderPredicateDefinition SqlTablePred = new()
    {
        Name = "Order Flows",
        ProviderType = "SqlTable",
        Table = "EcomOrderFlow",
        NameColumn = "OrderFlowName"
    };

    public SerializerOrchestratorTests()
    {
        _contentProvider = new Mock<ISerializationProvider>();
        _contentProvider.Setup(p => p.ProviderType).Returns("Content");
        _contentProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        _contentProvider.Setup(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()))
            .Returns(new SerializeResult { RowsSerialized = 5, TableName = "Content" });
        _contentProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>()))
            .Returns(new ProviderDeserializeResult { Created = 2, Updated = 1, TableName = "Content" });

        _sqlTableProvider = new Mock<ISerializationProvider>();
        _sqlTableProvider.Setup(p => p.ProviderType).Returns("SqlTable");
        _sqlTableProvider.Setup(p => p.ValidatePredicate(It.IsAny<ProviderPredicateDefinition>()))
            .Returns(ValidationResult.Success());
        _sqlTableProvider.Setup(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()))
            .Returns(new SerializeResult { RowsSerialized = 10, TableName = "EcomOrderFlow" });
        _sqlTableProvider.Setup(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>()))
            .Returns(new ProviderDeserializeResult { Created = 3, Updated = 2, Skipped = 1, TableName = "EcomOrderFlow" });

        _registry = new ProviderRegistry();
        _registry.Register(_contentProvider.Object);
        _registry.Register(_sqlTableProvider.Object);

        _orchestrator = new SerializerOrchestrator(_registry);
    }

    // --- SerializeAll tests ---

    [Fact]
    public void SerializeAll_TwoContentPredicates_DispatchesBothToContentProvider()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, ContentPred2 };

        var result = _orchestrator.SerializeAll(predicates, "/output");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>()), Times.Once);
        _contentProvider.Verify(p => p.Serialize(ContentPred2, "/output", It.IsAny<Action<string>?>()), Times.Once);
        Assert.Equal(2, result.SerializeResults.Count);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void SerializeAll_MixedPredicates_DispatchesToCorrectProviders()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>()), Times.Once);
        Assert.Equal(2, result.SerializeResults.Count);
    }

    [Fact]
    public void SerializeAll_FilterContent_SkipsSqlTablePredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: "Content");

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()), Times.Never);
        Assert.Single(result.SerializeResults);
    }

    [Fact]
    public void SerializeAll_FilterSqlTable_SkipsContentPredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: "SqlTable");

        _contentProvider.Verify(p => p.Serialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>()), Times.Never);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>()), Times.Once);
        Assert.Single(result.SerializeResults);
    }

    [Fact]
    public void SerializeAll_NullFilter_DispatchesAllPredicates()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.SerializeAll(predicates, "/output", providerFilter: null);

        _contentProvider.Verify(p => p.Serialize(ContentPred1, "/output", It.IsAny<Action<string>?>()), Times.Once);
        _sqlTableProvider.Verify(p => p.Serialize(SqlTablePred, "/output", It.IsAny<Action<string>?>()), Times.Once);
        Assert.Equal(2, result.SerializeResults.Count);
    }

    [Fact]
    public void SerializeAll_UnknownProviderType_LogsErrorAndContinues()
    {
        var unknownPred = new ProviderPredicateDefinition
        {
            Name = "Unknown",
            ProviderType = "Nonexistent"
        };
        var predicates = new List<ProviderPredicateDefinition> { unknownPred, ContentPred1 };
        var logs = new List<string>();

        var result = _orchestrator.SerializeAll(predicates, "/output", log: msg => logs.Add(msg));

        // Unknown predicate should be skipped with error, Content should still be processed
        Assert.Single(result.SerializeResults);
        Assert.Single(result.Errors);
        Assert.Contains("Nonexistent", result.Errors[0]);
        Assert.Contains("WARNING", logs.First(l => l.Contains("Nonexistent")));
    }

    [Fact]
    public void SerializeAll_FailedValidation_SkipsWithErrorLogged()
    {
        var invalidPred = new ProviderPredicateDefinition
        {
            Name = "BadPred",
            ProviderType = "Content",
            Path = "",
            AreaId = 0
        };
        _contentProvider.Setup(p => p.ValidatePredicate(invalidPred))
            .Returns(ValidationResult.Failure("Path is required"));

        var predicates = new List<ProviderPredicateDefinition> { invalidPred, SqlTablePred };
        var logs = new List<string>();

        var result = _orchestrator.SerializeAll(predicates, "/output", log: msg => logs.Add(msg));

        // Invalid predicate should be skipped, SqlTable should proceed
        Assert.Single(result.SerializeResults);
        Assert.Single(result.Errors);
        Assert.Contains("Path is required", result.Errors[0]);
    }

    // --- DeserializeAll tests ---

    [Fact]
    public void DeserializeAll_MixedPredicates_DispatchesToCorrectProviders()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.DeserializeAll(predicates, "/input");

        _contentProvider.Verify(p => p.Deserialize(ContentPred1, "/input", It.IsAny<Action<string>?>(), false), Times.Once);
        _sqlTableProvider.Verify(p => p.Deserialize(SqlTablePred, "/input", It.IsAny<Action<string>?>(), false), Times.Once);
        Assert.Equal(2, result.DeserializeResults.Count);
    }

    [Fact]
    public void DeserializeAll_FilterAndDryRun_PassesThroughCorrectly()
    {
        var predicates = new List<ProviderPredicateDefinition> { ContentPred1, SqlTablePred };

        var result = _orchestrator.DeserializeAll(predicates, "/input", isDryRun: true, providerFilter: "SqlTable");

        _contentProvider.Verify(p => p.Deserialize(It.IsAny<ProviderPredicateDefinition>(), It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<bool>()), Times.Never);
        _sqlTableProvider.Verify(p => p.Deserialize(SqlTablePred, "/input", It.IsAny<Action<string>?>(), true), Times.Once);
        Assert.Single(result.DeserializeResults);
    }

    [Fact]
    public void DeserializeAll_UnknownProviderType_LogsErrorAndContinues()
    {
        var unknownPred = new ProviderPredicateDefinition
        {
            Name = "Unknown",
            ProviderType = "Nonexistent"
        };
        var predicates = new List<ProviderPredicateDefinition> { unknownPred, SqlTablePred };

        var result = _orchestrator.DeserializeAll(predicates, "/input");

        Assert.Single(result.DeserializeResults);
        Assert.Single(result.Errors);
        Assert.Contains("Nonexistent", result.Errors[0]);
    }

    [Fact]
    public void DeserializeAll_FailedValidation_SkipsWithErrorLogged()
    {
        var invalidPred = new ProviderPredicateDefinition
        {
            Name = "BadPred",
            ProviderType = "SqlTable",
            Table = ""
        };
        _sqlTableProvider.Setup(p => p.ValidatePredicate(invalidPred))
            .Returns(ValidationResult.Failure("Table is required"));

        var predicates = new List<ProviderPredicateDefinition> { invalidPred, ContentPred1 };

        var result = _orchestrator.DeserializeAll(predicates, "/input");

        Assert.Single(result.DeserializeResults);
        Assert.Single(result.Errors);
        Assert.Contains("Table is required", result.Errors[0]);
    }

    // --- OrchestratorResult tests ---

    [Fact]
    public void OrchestratorResult_HasErrors_TrueWhenErrorsExist()
    {
        var result = new OrchestratorResult { Errors = new List<string> { "fail" } };
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void OrchestratorResult_HasErrors_TrueWhenSerializeResultHasErrors()
    {
        var result = new OrchestratorResult
        {
            SerializeResults = new List<SerializeResult>
            {
                new() { Errors = new[] { "serialize error" } }
            }
        };
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void OrchestratorResult_HasErrors_FalseWhenNoErrors()
    {
        var result = new OrchestratorResult
        {
            SerializeResults = new List<SerializeResult> { new() { RowsSerialized = 5 } }
        };
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void OrchestratorResult_Summary_AggregatesCounts()
    {
        var result = new OrchestratorResult
        {
            SerializeResults = new List<SerializeResult>
            {
                new() { RowsSerialized = 5, TableName = "Content" },
                new() { RowsSerialized = 10, TableName = "EcomOrderFlow" }
            }
        };

        Assert.Contains("15", result.Summary);
    }
}
