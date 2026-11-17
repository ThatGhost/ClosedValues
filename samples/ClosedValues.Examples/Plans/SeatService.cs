namespace ClosedValues.Examples.Plans
{
    /// <summary>
    /// A service that receives the active plan through dependency injection, as a typed value instead of a string.
    /// </summary>
    public sealed class SeatService(SubscriptionPlan plan)
    {
        public string Invite(int currentUsers) =>
            plan.Allows(currentUsers + 1)
                ? $"Invited user {currentUsers + 1} on the {plan} plan."
                : $"The {plan} plan allows {plan.MaxUsers} users. Upgrade to {SubscriptionPlan.CheapestFor(currentUsers + 1)}.";
    }
}
