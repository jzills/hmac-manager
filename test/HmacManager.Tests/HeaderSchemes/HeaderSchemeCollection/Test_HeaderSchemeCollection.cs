using HmacManager.Headers;
using HmacManager.Schemes;

namespace Unit.Tests.Schemes;

[TestFixture]
public class Test_SchemeCollection
{
    [Test]
    public void Test()
    {
        var schemes = new SchemeCollection();
        var builder = new SchemeBuilder("Scheme_1");
        builder.AddHeader("Header_1", string.Empty);
        builder.AddHeader("Header_2", "          ");
        builder.AddHeader("Header_3", default!);
        builder.AddHeader("Header_4", null!);
        var scheme = builder.Build();

        var hasEmptyClaimTypes = scheme.Headers.GetAll().Any(header => string.IsNullOrWhiteSpace(header.ClaimType));
        Assert.IsFalse(hasEmptyClaimTypes);
    }
}