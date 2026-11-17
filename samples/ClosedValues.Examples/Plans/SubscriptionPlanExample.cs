using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace ClosedValues.Examples.Plans
{
    /// <summary>
    /// Shows a closed value with several members: dependency injection, dictionary keys, queries over <c>All</c>,
    /// and an exhaustive <c>Match</c>.
    /// </summary>
    public sealed class SubscriptionPlanExample
    {
        public void Run()
        {
            Console.WriteLine("3. Subscription plan: several members, DI and queries");

            using ServiceProvider services = new ServiceCollection()
                .AddSingleton(SubscriptionPlan.Starter)
                .AddSingleton<SeatService>()
                .BuildServiceProvider();

            SeatService seats = services.GetRequiredService<SeatService>();
            Console.WriteLine($"   {seats.Invite(currentUsers: 3)}");
            Console.WriteLine($"   {seats.Invite(currentUsers: 5)}");

            Console.WriteLine($"   Upgrades from Starter: {string.Join(", ", SubscriptionPlan.Starter.Upgrades())}.");
            Console.WriteLine($"   Cheapest plan for 12 users: {SubscriptionPlan.CheapestFor(12)}.");

            Dictionary<SubscriptionPlan, int> customersPerPlan = new()
            {
                [SubscriptionPlan.Free] = 1200,
                [SubscriptionPlan.Starter] = 340,
                [SubscriptionPlan.Team] = 85,
                [SubscriptionPlan.Enterprise] = 6,
            };

            decimal monthlyRevenue = customersPerPlan.Sum(entry => entry.Key.MonthlyPrice * entry.Value);
            Console.WriteLine($"   Monthly revenue: {monthlyRevenue.ToString("N0", CultureInfo.InvariantCulture)}.");

            foreach (SubscriptionPlan plan in SubscriptionPlan.All.Where(plan => plan.IsPaid))
            {
                int trialDays = plan.Match(
                    free: () => 0,
                    starter: () => 14,
                    team: () => 30,
                    enterprise: () => 60);

                string support = plan.IncludesSupport ? "with support" : "without support";
                Console.WriteLine($"   {plan,-10} {plan.MonthlyPrice,4}/month, {plan.YearlyPrice,4}/year, {trialDays,2}-day trial, {support}");
            }

            Console.WriteLine();
        }
    }
}
