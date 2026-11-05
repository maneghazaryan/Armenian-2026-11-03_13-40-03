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

    /// <summary>A day whose parts are partly done.</summary>
    [Serializable]
    public class PartProgress
    {
        public int day;
        public int parts;    // parts finished, in order
        public int mistakes; // over all finished parts
    }

    /// <summary>How often a letter or word was answered wrongly.</summary>
    [Serializable]
    public class MissCount
    {
        public string key;
        public int count;
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
        public List<PartProgress> partial = new List<PartProgress>();
        public List<MissCount> misses = new List<MissCount>();
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

        /// <summary>Parts of a day finished so far (all of them once the day is done).</summary>
        public int PartsDone(int day, int partCount) =>
            Result(day) != null ? partCount : partial.FirstOrDefault(p => p.day == day)?.parts ?? 0;

        /// <summary>
        /// Records a finished part of a day. Parts count only in order and only until the day is done
        /// (replays change nothing). Returns true when this completed the day.
        /// </summary>
        public bool CompletePart(int day, int part, int partCount, int mistakes, DateTime today)
        {
            if (Result(day) != null) return false;
            var p = partial.FirstOrDefault(x => x.day == day);
            if (p == null) partial.Add(p = new PartProgress { day = day });
            if (part != p.parts + 1) return false;
            p.parts = part;
            p.mistakes += mistakes;
            if (part < partCount) return false;
            partial.Remove(p);
            Complete(day, DayUnlock.Stars(p.mistakes), today);
            return true;
        }

        /// <summary>Count a wrong answer for each of these letters or words.</summary>
        public void Missed(IEnumerable<string> keys)
        {
            foreach (var key in keys.Where(k => k != null))
            {
                var m = misses.FirstOrDefault(x => x.key == key);
                if (m == null) misses.Add(m = new MissCount { key = key });
                m.count++;
            }
        }

        /// <summary>Hard item: answered wrongly and not yet remembered in two reviews in a row.</summary>
        public bool IsHard(string key) =>
            misses.Any(m => m.key == key) && (items.FirstOrDefault(i => i.key == key)?.box ?? 1) < 3;

        /// <summary>Forget items that are no longer in the course (renamed or removed words).</summary>
        public void Prune(Func<string, bool> exists)
        {
            items.RemoveAll(i => !exists(i.key));
            misses.RemoveAll(m => !exists(m.key));
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

        public static void Save(ProgressData data)
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
#if UNITY_WEBGL && !UNITY_EDITOR
            AibSyncFs();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void AibSyncFs();
#endif
    }
}
