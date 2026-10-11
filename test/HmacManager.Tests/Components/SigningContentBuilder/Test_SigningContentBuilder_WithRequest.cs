namespace Unit.Tests.Components.SigningContentBuilder;

[TestFixture]
public class Test_ServiceCollection_AddHmacManager_SigningContentBuilder_CreateBuilder
{
    public void Test()
    {
        var builder = new HmacManager.Components.SigningContentBuilder();
        Assert.That(ReferenceEquals(builder, builder.CreateBuilder()), Is.False);
    }
}