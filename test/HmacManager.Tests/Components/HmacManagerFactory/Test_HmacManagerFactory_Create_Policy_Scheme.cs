using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using HmacManager.Caching;
using HmacManager.Caching.Memory;
using HmacManager.Components;
using HmacManager.Policies;

namespace Unit.Tests.Components;

[TestFixture]
public class Test_HmacManagerFactory_Create_Policy_Scheme
{
    IHmacManagerFactory HmacManagerFactory = default!;

    public void Init(string policy)
    {
        var policies = new HmacPolicyCollection();
        policies.Add(new HmacPolicy(policy)
        {
            Algorithms = new(),
            Keys = new KeyCredentials { PublicKey = Guid.NewGuid(), PrivateKey = "xCy0Ucg3YEKlmiK23Zph+g==" },
            Nonce = new Nonce { CacheType = NonceCacheType.Memory }
        });

        var caches = new NonceCacheCollection();
        caches.Add(NonceCacheType.Memory, 
            new NonceMemoryCache(
                new MemoryCache(Options.Create(new MemoryCacheOptions())), 
                new NonceCacheOptions())
        );

        HmacManagerFactory = new HmacManagerFactory(policies, caches, new HmacHeaderParserFactory(false), new HmacHeaderBuilderFactory(false));
    }

    [TestCaseSource(nameof(GetTestCaseData))]
    public void Test_Create_Returns_NotNull(string policy)
    {
        Init(policy);

        var hmacManager = HmacManagerFactory.Create(policy);
        Assert.That(hmacManager, Is.Not.Null);
    }

    [TestCaseSource(nameof(GetTestCaseDataThatThrows))]
    public void Test_Create_Throws_ArgumentException(string policy)
    {
        // Do not call Init here - we are
        // only testing HmacManagerFactory.Create against
        // a bad policy name.

        // Init(policy);

        // Further, a policy is validating when added to the HmacPolicyCollection
        // so the code will never pass that point.

        Assert.That(() => 
            HmacManagerFactory.Create(policy), 
            Throws.Exception
        );
    }

    public static IEnumerable<TestCaseData> GetTestCaseData() =>
    [
        new TestCaseData("SomePolicy"),
        new TestCaseData("_Some_Policy_"),
        new TestCaseData("!@#$%^&*()_+-={}[];',.<>/?"),
        new TestCaseData("1234567890-0987654321-1-2-3-4-5-6-7-8-9-0"),
        new TestCaseData("___ ____ ___ __ _ ___ _______")
    ];

    public static IEnumerable<TestCaseData> GetTestCaseDataThatThrows() =>
    [
        new TestCaseData(" "),
        new TestCaseData("           "),
        new TestCaseData(string.Empty),
        new TestCaseData(null)
    ];
}