using System.Globalization;
using HmacManager.Components;
using HmacManager.Exceptions;
using HmacManager.Mvc;

namespace Unit.Tests.Components;

public class Test_HmacHeaderParser_DateRange
{
    [TestCase(999999999999999999L)]
    [TestCase(long.MaxValue)]
    [TestCase(long.MinValue)]
    [TestCase(253402300800000L)]
    [TestCase(-62135596800001L)]
    public void Test_OutOfRangeDate_ThrowsBadHeaderFormat(long milliseconds)
    {
        var parser = CreateParser(milliseconds);
        Assert.Throws<BadHeaderFormatException>(() => parser.GetDateRequested());
    }

    [TestCase(-62135596800000L)]
    [TestCase(253402300799999L)]
    [TestCase(0L)]
    public void Test_InRangeDate_Parses(long milliseconds)
    {
        Assert.That(CreateParser(milliseconds).GetDateRequested().ToUnixTimeMilliseconds(),
            Is.EqualTo(milliseconds));
    }

    private static HmacHeaderParser CreateParser(long milliseconds) => new(new Dictionary<string, string>
    {
        [HmacAuthenticationDefaults.Headers.DateRequested] = milliseconds.ToString(CultureInfo.InvariantCulture)
    });
}
