using System.Text;

namespace Unit.Tests;

public abstract class TestBase
{
    protected static readonly Guid PublicKey = Guid.NewGuid();
    protected static readonly string PrivateKey = CreatePrivateKey("thisIsMySuperCoolPrivateKey"); 

    protected static readonly Guid PublicKey2 = Guid.NewGuid();
    protected static readonly string PrivateKey2 = CreatePrivateKey("thisIsMyOtherSuperCoolPrivateKey"); 

    private static string CreatePrivateKey(string text) => 
        Convert.ToBase64String(
            Encoding.UTF8.GetBytes(text));
}