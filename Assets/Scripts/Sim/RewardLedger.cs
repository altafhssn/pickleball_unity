using System;
using System.Collections.Generic;

namespace Pickleball.Sim
{
    /// <summary>
    /// Remembers which rewards have already been credited, by id -- a match id for a match reward,
    /// an ad-placement id for a rewarded ad -- so the same one can never pay twice: not if its
    /// completion fires again, not if the player retries, not after a restart (the ids persist with
    /// the profile). Oldest entries are forgotten first once <see cref="EconomyConfig.LedgerCapacity"/>
    /// is reached.
    /// </summary>
    public class RewardLedger
    {
        private readonly List<string> order = new List<string>();
        private readonly HashSet<string> ids = new HashSet<string>();
        private readonly int capacity;

        public RewardLedger(int capacity = EconomyConfig.LedgerCapacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        public IReadOnlyList<string> Entries => order;

        public bool HasCredited(string id) => !string.IsNullOrEmpty(id) && ids.Contains(id);

        /// <summary>Records <paramref name="id"/> as credited. False -- credit nothing -- if it is
        /// empty or was already credited.</summary>
        public bool TryRecord(string id)
        {
            if (string.IsNullOrEmpty(id) || !ids.Add(id)) return false;
            order.Add(id);
            while (order.Count > capacity)
            {
                ids.Remove(order[0]);
                order.RemoveAt(0);
            }
            return true;
        }

        public void Load(IEnumerable<string> entries)
        {
            order.Clear();
            ids.Clear();
            if (entries == null) return;
            foreach (string id in entries) TryRecord(id);
        }
    }

    /// <summary>The daily allowance of rewarded ads.</summary>
    public static class AdRewardRules
    {
        /// <summary>Ads still able to pay today, given the day the stored count belongs to.</summary>
        public static int RemainingToday(string countedDay, int countedToday, string today)
        {
            int used = countedDay == today ? Math.Max(0, countedToday) : 0;
            return Math.Max(0, EconomyConfig.AdRewardsPerDay - used);
        }
    }
}
