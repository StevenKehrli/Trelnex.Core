using System.Text.Json.Serialization;
using FluentValidation;

namespace Trelnex.Core.Data.Tests.CommandProviders;

internal interface ITestItem : IBaseItem
{
    string Message { get; set; }
}

internal class TestItem : BaseItem, ITestItem
{
    [TrackChange]
    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;

    public static AbstractValidator<TestItem> Validator { get; } = new TestItemValidator();

    private class TestItemValidator : AbstractValidator<TestItem>
    {
        public TestItemValidator()
        {
        }
    }
}
