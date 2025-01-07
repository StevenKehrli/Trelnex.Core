namespace Trelnex.Core.Data.Tests.CommandProviders;

public class InMemoryCommandProviderTests : CommandProviderTests
{
    [SetUp]
    public void TestFixtureSetup()
    {
        // create our command provider
        _commandProvider =
            InMemoryCommandProvider.Create<ITestItem, TestItem>(
                typeName: "test-item",
                TestItem.Validator,
                CommandOperations.All);
    }
}
