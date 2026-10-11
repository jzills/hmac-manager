using HmacManager.Components;
using HmacManager.Extensions;
using HmacManager.Mvc;

namespace Unit.Tests;

public class Test_DateTimeOffsetExtensions_HasValidDateRequested : TestBase
{
    [Test]
    public void Test_HasValidDateRequested_IsTrue()
    {
        Assert.That(DateTimeOffset.UtcNow.HasValidDateRequested(10), Is.True);
    }

    [Test]
    public void Test_HasValidDateRequested_IsFalse()
    {
        Assert.That(DateTimeOffset.UtcNow.AddSeconds(-10).HasValidDateRequested(10), Is.False);
    }

    // Security: future-dated timestamps must be rejected.
    // The current implementation only checks UtcNow - dateRequested < maxAge,
    // which produces a negative TimeSpan for future dates — always less than
    // the positive bound — so all three tests below fail until the fix is applied.

    [Test]
    public void Test_HasValidDateRequested_FutureByOneSecond_IsFalse()
    {
        Assert.That(DateTimeOffset.UtcNow.AddSeconds(1).HasValidDateRequested(30), Is.False);
    }

    [Test]
    public void Test_HasValidDateRequested_FutureByOneHour_IsFalse()
    {
        Assert.That(DateTimeOffset.UtcNow.AddHours(1).HasValidDateRequested(30), Is.False);
    }

    [Test]
    public void Test_HasValidDateRequested_FutureByOneYear_IsFalse()
    {
        Assert.That(DateTimeOffset.UtcNow.AddYears(1).HasValidDateRequested(30), Is.False);
    }
}