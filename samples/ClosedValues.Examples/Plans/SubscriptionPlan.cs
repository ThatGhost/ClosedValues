using System.Collections.Generic;
using System.Linq;

namespace ClosedValues.Examples.Plans
{
    /// <summary>
    /// A subscription plan of a software product: four members, computed properties, and behaviour that compares plans.
    /// </summary>
    [ClosedValue]
    public sealed partial class SubscriptionPlan
    {
        public string Name { get; }

        public decimal MonthlyPrice { get; }

        public int MaxUsers { get; }

        public bool IncludesSupport { get; }

        public bool IsPaid => MonthlyPrice > 0;

        public decimal YearlyPrice => MonthlyPrice * 10;

        public static readonly SubscriptionPlan Free = new("Free", 0m, 1, false);
        public static readonly SubscriptionPlan Starter = new("Starter", 9m, 5, false);
        public static readonly SubscriptionPlan Team = new("Team", 29m, 25, true);
        public static readonly SubscriptionPlan Enterprise = new("Enterprise", 99m, int.MaxValue, true);

        /// <summary>
        /// Returns whether this plan allows the given number of users.
        /// </summary>
        public bool Allows(int users) => users <= MaxUsers;

        /// <summary>
        /// Returns the plans that cost more than this plan, cheapest first.
        /// </summary>
        public IEnumerable<SubscriptionPlan> Upgrades() =>
            All.Where(plan => plan.MonthlyPrice > MonthlyPrice).OrderBy(plan => plan.MonthlyPrice);

        /// <summary>
        /// Returns the cheapest plan that allows the given number of users.
        /// </summary>
        public static SubscriptionPlan CheapestFor(int users) =>
            All.Where(plan => plan.Allows(users)).OrderBy(plan => plan.MonthlyPrice).First();
    }
}
