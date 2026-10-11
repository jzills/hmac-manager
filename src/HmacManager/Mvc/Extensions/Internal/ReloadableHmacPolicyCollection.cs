using HmacManager.Common;
using HmacManager.Policies;

namespace HmacManager.Mvc.Extensions.Internal;

/// <summary>
/// An <see cref="IHmacPolicyCollection"/> that supports being replaced wholesale by a
/// configuration reload without disturbing concurrent readers. Reads delegate to an inner
/// <see cref="HmacPolicyCollection"/> held behind a single <c>volatile</c> reference;
/// a reload builds a fresh collection and swaps that reference in one atomic assignment.
/// A reader therefore always observes a complete, immutable snapshot — either the old set
/// or the new one — never a half-mutated dictionary.
/// </summary>
internal sealed class ReloadableHmacPolicyCollection : IHmacPolicyCollection, IComponentCollection<HmacPolicy>
{
    private volatile HmacPolicyCollection _current;

    /// <summary>
    /// Creates a <see cref="ReloadableHmacPolicyCollection"/> around an initial policy set.
    /// </summary>
    /// <param name="initial">The initial <see cref="HmacPolicyCollection"/> to serve reads from.</param>
    public ReloadableHmacPolicyCollection(HmacPolicyCollection initial) => _current = initial;

    /// <summary>
    /// Atomically replaces the served policy set. Readers in flight complete against the
    /// previous set; subsequent reads observe <paramref name="next"/>.
    /// </summary>
    /// <param name="next">The freshly built <see cref="HmacPolicyCollection"/> to publish.</param>
    internal void Replace(HmacPolicyCollection next) => _current = next;

    /// <inheritdoc/>
    public IReadOnlyCollection<HmacPolicy> Values => _current.Values;

    /// <inheritdoc/>
    public void Add(HmacPolicy policy) => _current.Add(policy);

    /// <inheritdoc/>
    public void Remove(string policy) => _current.Remove(policy);

    /// <inheritdoc/>
    public HmacPolicy? Get(string name) => _current.Get(name);

    /// <inheritdoc/>
    public IReadOnlyCollection<HmacPolicy> GetAll() => _current.GetAll();
}
