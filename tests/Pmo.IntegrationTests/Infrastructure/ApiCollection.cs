namespace Pmo.IntegrationTests.Infrastructure;

/// <summary>All tests in this collection share one API + one database container.</summary>
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<PmoApiFactory>
{
    public const string Name = "api";
}
