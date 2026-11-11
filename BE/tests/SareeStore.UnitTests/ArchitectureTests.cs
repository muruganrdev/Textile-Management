using SareeStore.Application.Services;
using SareeStore.Domain.Common;

namespace SareeStore.UnitTests;

public class ApplicationArchitectureTests
{
    [Fact]
    public void Domain_Should_NotDependOn_OtherLayers()
    {
        var domainAssembly = typeof(BaseEntity).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, a => a.Name != null &&
            (a.Name.Contains("Application") || a.Name.Contains("Infrastructure") || a.Name.Contains("Api")));
    }

    [Fact]
    public void Application_Should_NotDependOn_InfrastructureOrApi()
    {
        var applicationAssembly = typeof(ProductService).Assembly;
        var referencedAssemblies = applicationAssembly.GetReferencedAssemblies();

        Assert.DoesNotContain(referencedAssemblies, a => a.Name != null &&
            (a.Name.Contains("Infrastructure") || a.Name.Contains("Api")));
    }
}
