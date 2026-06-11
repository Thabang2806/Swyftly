using Mabuntle.Domain;

namespace Mabuntle.UnitTests;

public class FoundationTests
{
    [Fact]
    public void DomainAssemblyReference_ExposesDomainAssembly()
    {
        Assert.Equal("Mabuntle.Domain", DomainAssemblyReference.Assembly.GetName().Name);
    }
}
