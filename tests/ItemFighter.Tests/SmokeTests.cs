namespace ItemFighter.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreHasNoMonoGameReference()
    {
        var refs = typeof(ItemFighter.Core.Determinism.Rng).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(refs, r => r.Name!.StartsWith("MonoGame"));
    }
}
