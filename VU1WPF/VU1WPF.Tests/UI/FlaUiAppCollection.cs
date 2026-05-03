using Xunit;

namespace VU1WPF.Tests.UI;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FlaUiAppCollection : ICollectionFixture<FlaUiAppFixture>
{
    public const string Name = "FlaUI MainWindow";
}
