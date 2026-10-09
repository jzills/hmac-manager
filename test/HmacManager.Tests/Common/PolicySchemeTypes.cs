namespace Unit.Tests;

public sealed class PolicySchemeType
{
    public readonly string Policy;
    public readonly string Scheme;

    public static readonly PolicySchemeType Policy_Memory               = new PolicySchemeType(nameof(Policy_Memory));
    public static readonly PolicySchemeType Policy_Memory_Scheme_1      = new PolicySchemeType(nameof(Policy_Memory), nameof(Policy_Memory_Scheme_1));
    public static readonly PolicySchemeType Policy_Memory_Scheme_2      = new PolicySchemeType(nameof(Policy_Memory), nameof(Policy_Memory_Scheme_2));
    public static readonly PolicySchemeType Policy_Memory_Scheme_3      = new PolicySchemeType(nameof(Policy_Memory), nameof(Policy_Memory_Scheme_3));
    public static readonly PolicySchemeType Policy_Distributed          = new PolicySchemeType(nameof(Policy_Distributed));
    public static readonly PolicySchemeType Policy_Distributed_Scheme_1 = new PolicySchemeType(nameof(Policy_Distributed), nameof(Policy_Distributed_Scheme_1));
    public static readonly PolicySchemeType Policy_Distributed_Scheme_2 = new PolicySchemeType(nameof(Policy_Distributed), nameof(Policy_Distributed_Scheme_2));
    public static readonly PolicySchemeType Policy_Distributed_Scheme_3 = new PolicySchemeType(nameof(Policy_Distributed), nameof(Policy_Distributed_Scheme_3));

    private PolicySchemeType(string policy, string scheme = null!) => 
        (Policy, Scheme) = (policy, scheme);
}