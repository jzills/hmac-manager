namespace Unit.Tests.Caching;

public class TestCaseData
{
    public static readonly int MaxNonces = 10;
    public static IEnumerable<Guid> GetNonces() => Enumerable
        .Range(0, MaxNonces)
        .Select(_ => Guid.NewGuid())
        .Append(Guid.Empty);
}