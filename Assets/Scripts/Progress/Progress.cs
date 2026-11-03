using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Aib.Progress
{
    [Serializable]
    public class DayResult
    {
        public int day;
        public int stars;
        public string date; // yyyy-MM-dd, local date of first completion
    }

    /// <summary>A letter ("l:a") or word ("w:sar") in a Leitner box, due for review on a date.</summary>
    [Serializable]
    public class ItemState
    {
        public string key;
        public int box;
        public string due; // yyyy-MM-dd
    }

    [Serializable]
    public class ProgressData
    {
        public List<DayResult> results = new List<DayResult>();
        public List<ItemState> items = new List<ItemState>();
        /// <summary>When true, the next lesson opens only on the next calendar day. Off only in debug builds.</summary>
        public bool dayLock = true;
        /// <summary>Hour of the daily reminder, or -1 when reminders are off.</summary>
        public int reminderHour = 19;

        public DayResult Result(int day) => results.FirstOrDefault(r => r.day == day);

        public int LastCompletedDay => results.Count == 0 ? 0 : results.Max(r => r.day);

        public void Complete(int day, int stars, DateTime today)
        {
            var r = Result(day);
            if (r == null)
                results.Add(new DayResult { day = day, stars = stars, date = DayUnlock.Format(today) });
            else
                r.stars = Math.Max(r.stars, stars);
        }

        /// <summary>Items whose review date has come, most overdue first.</summary>
        public List<ItemState> Due(DateTime today) => items
            .Where(i => DayUnlock.Parse(i.due) <= today.Date)
            .OrderBy(i => i.due).ThenBy(i => i.box)
            .ToList();

        /// <summary>Start tracking newly learned items; already known ones are left alone.</summary>
        public void Learn(IEnumerable<string> keys, DateTime today)
        {
            foreach (var key in keys)
                if (items.All(i => i.key != key))
                    items.Add(new ItemState { key = key, box = 1, due = DayUnlock.Format(today.AddDays(Leitner.Interval(1))) });
        }

        /// <summary>Move a reviewed item up a box when remembered, back to box 1 when not.</summary>
        public void Reviewed(string key, bool remembered, DateTime today)
        {
            var item = items.FirstOrDefault(i => i.key == key);
            if (item == null) return;
            item.box = remembered ? Math.Min(item.box + 1, Leitner.MaxBox) : 1;
            item.due = DayUnlock.Format(today.AddDays(Leitner.Interval(item.box)));
        }
    }

    public static class Leitner
    {
        static readonly int[] Days = { 1, 1, 3, 7, 14, 30 };
        public const int MaxBox = 5;
        /// <summary>Most items in one daily review, so it stays short.</summary>
        public const int MaxPerSession = 12;

        public static int Interval(int box) => Days[Math.Clamp(box, 1, MaxBox)];
    }

    public enum DayState { Locked, LockedUntilTomorrow, ReviewFirst, Available, Done }

    public static class DayUnlock
    {
        public static string Format(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static DateTime Parse(string s) =>
            DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static DayState State(int day, ProgressData p, DateTime today)
        {
            if (p.Result(day) != null) return DayState.Done;
            if (day > 1)
            {
                var prev = p.Result(day - 1);
                if (prev == null) return DayState.Locked;
                if (p.dayLock && Parse(prev.date) >= today.Date) return DayState.LockedUntilTomorrow;
            }
            if (p.Due(today).Count > 0) return DayState.ReviewFirst;
            return DayState.Available;
        }

        public static int Stars(int mistakes) => mistakes == 0 ? 3 : mistakes <= 3 ? 2 : 1;
    }

    /// <summary>Saves progress as JSON in Application.persistentDataPath.</summary>
    public static class ProgressStore
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "progress.json");

        public static ProgressData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<ProgressData>(File.ReadAllText(FilePath)) ?? new ProgressData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not read progress, starting fresh: {e.Message}");
            }
            return new ProgressData();
        }

        public static void Save(ProgressData data) =>
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
    }
}
