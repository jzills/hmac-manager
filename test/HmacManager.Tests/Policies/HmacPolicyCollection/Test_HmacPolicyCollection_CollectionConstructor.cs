using HmacManager.Components;
using HmacManager.Policies;

namespace Unit.Tests.Policies;

[TestFixture]
public class Test_HmacPolicyCollection_CollectionConstructor
{
    [Test]
    public void Test_Add_Throws()
    {
        Assert.Throws<FormatException>(() => new HmacPolicyCollection([ new HmacPolicy("Some Policy") ]));
    }

    [Test]
    public void Test_Add_DoesNotThrow()
    {
        var policy = new HmacPolicy("Some Policy") 
        {
            Keys = new KeyCredentials
            {
                PublicKey = Guid.NewGuid(),
                PrivateKey = "xCy0Ucg3YEKlmiK23Zph+g==",
            },
            Algorithms = new Algorithms
            {
                ContentHashAlgorithm = ContentHashAlgorithm.SHA1,
                SigningHashAlgorithm = SigningHashAlgorithm.HMACSHA1
            }
        };

        Assert.DoesNotThrow(() => new HmacPolicyCollection([policy]));
    }
}