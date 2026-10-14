using HmacManager.Headers;
using HmacManager.Schemes;

namespace HmacManager.Mvc;

internal class SchemeConfigurationSection : Scheme
{
    public SchemeConfigurationSection(string? name) : base(name)
    {
    }

    public new List<Header>? Headers { get; set; }
}