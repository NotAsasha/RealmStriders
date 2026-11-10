using System;
using System.Collections.Generic;

namespace Enemy
{
    public static class EnemyEncounterBudget
    {
        public static List<int> Generate(int budget, int maxDanger, int capacity, Random random)
        {
            if (budget < 1 || maxDanger < 1 || capacity < 1 || random == null)
                throw new ArgumentException("An encounter needs a positive budget, danger ceiling, capacity and random source.");
            budget = Math.Min(budget, checked(capacity * maxDanger));
            int minimumCount = (budget + maxDanger - 1) / maxDanger;
            int count = random.Next(minimumCount, Math.Min(capacity, budget) + 1);
            var result = new List<int>(count);
            int remaining = budget;
            for (int i = 0; i < count; i++)
            {
                int slotsLeft = count - i - 1;
                // Reserve enough budget to fill the remaining slots within the danger ceiling.
                int minimum = Math.Max(1, remaining - slotsLeft * maxDanger);
                int maximum = Math.Min(maxDanger, remaining - slotsLeft);
                int danger = random.Next(minimum, maximum + 1);
                result.Add(danger);
                remaining -= danger;
            }
            for (int i = result.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                int value = result[i];
                result[i] = result[other];
                result[other] = value;
            }
            return result;
        }
    }
}
