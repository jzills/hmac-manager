using HmacManager.Operator.Controllers;
using HmacManager.Operator.Entities;
using NUnit.Framework;

namespace HmacManager.Operator.Tests;

/// <summary>
/// Guards the status-transition rule the controller uses to decide whether to write a policy's
/// <c>.status</c>. The subtle case is a valid policy edited in a still-valid way: phase and message
/// are unchanged, but <c>observedGeneration</c> must still advance so that clients gating on
/// <c>observedGeneration == generation</c> can tell the operator has processed the latest spec.
/// </summary>
[TestFixture]
public class StatusReconciliationTests
{
    [Test]
    public void TryApplyStatus_reports_change_and_records_generation_on_first_transition()
    {
        var status = new V1HmacPolicy.PolicyStatus(); // Phase "Pending", ObservedGeneration 0.

        var changed = HmacPolicyController.TryApplyStatus(status, "Ready", null, generation: 1);

        Assert.IsTrue(changed);
        Assert.AreEqual("Ready", status.Phase);
        Assert.IsNull(status.Message);
        Assert.AreEqual(1, status.ObservedGeneration);
    }

    [Test]
    public void TryApplyStatus_advances_observedGeneration_even_when_phase_and_message_are_unchanged()
    {
        // Regression: a still-valid spec edit bumps generation while leaving phase/message identical.
        var status = new V1HmacPolicy.PolicyStatus { Phase = "Ready", Message = null, ObservedGeneration = 1 };

        var changed = HmacPolicyController.TryApplyStatus(status, "Ready", null, generation: 2);

        Assert.IsTrue(changed, "a bumped generation must be recorded even when phase/message are unchanged");
        Assert.AreEqual(2, status.ObservedGeneration);
    }

    [Test]
    public void TryApplyStatus_reports_no_change_at_steady_state()
    {
        // Same phase, message and generation as already recorded — no write, so no needless requeue.
        var status = new V1HmacPolicy.PolicyStatus { Phase = "Ready", Message = null, ObservedGeneration = 2 };

        var changed = HmacPolicyController.TryApplyStatus(status, "Ready", null, generation: 2);

        Assert.IsFalse(changed);
    }

    [Test]
    public void TryApplyStatus_reports_change_when_the_message_changes()
    {
        var status = new V1HmacPolicy.PolicyStatus { Phase = "Invalid", Message = "old", ObservedGeneration = 3 };

        var changed = HmacPolicyController.TryApplyStatus(status, "Invalid", "new", generation: 3);

        Assert.IsTrue(changed);
        Assert.AreEqual("new", status.Message);
    }
}
