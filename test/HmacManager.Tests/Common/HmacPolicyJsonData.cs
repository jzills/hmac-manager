using System.Text.Json;
using HmacManager.Policies;

namespace Unit.Tests.Common;

public static class HmacPolicyJsonData
{
    private const string PathToJson = "../../../../Data/hmac_policy_collection.json";

    public static IList<HmacPolicy> ReadFromJsonFile()
    {
        using var stream = new StreamReader(PathToJson);
        var policies = JsonSerializer.Deserialize<List<HmacPolicy>>(stream.ReadToEnd());
        return policies ?? throw new FormatException("The data at \"hmac_policy_collection.json\" could not be read in for testing.");
    }
}