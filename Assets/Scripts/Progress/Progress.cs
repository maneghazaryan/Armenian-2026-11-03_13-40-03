using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Aib.Data;
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
        /// <summary>Ids of reading texts read to the end.</summary>
        public List<string> read = new List<string>();
        /// <summary>Topics (days after the alphabet) the learner chose to skip.</summary>
        public List<int> skipped = new List<int>();
        /// <summary>Words the learner doesn't want to learn: not taught, not reviewed.</summary>
        public List<string> removed = new List<string>();
        /// <summary>When true, the next lesson opens only on the next calendar day; the learner can switch it off.</summary>
        public bool dayLock = true;
        /// <summary>Day (yyyy-MM-dd) the short recall was last offered at app start, taken or skipped.</summary>
        public string lastRecall;
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

        /// <summary>
        /// For testers: marks every day as done (3 stars, as of yesterday, so nothing waits for tomorrow)
        /// and starts tracking all its letters and words. Days already done keep their stars.
        /// </summary>
        public void UnlockAll(IEnumerable<(int day, IEnumerable<string> keys)> days, DateTime today)
        {
            foreach (var (day, keys) in days)
            {
                if (Result(day) == null)
                    results.Add(new DayResult { day = day, stars = 3, date = DayUnlock.Format(today.AddDays(-1)) });
                partial.RemoveAll(x => x.day == day);
                Learn(keys, today);
            }
        }

        /// <summary>Starts over: forgets days, reviews, mistakes and read texts. Settings stay.</summary>
        public void Reset()
        {
            results.Clear();
            items.Clear();
            partial.Clear();
            misses.Clear();
            read.Clear();
            skipped.Clear();
            removed.Clear();
        }

        public bool IsSkipped(int day) => skipped.Contains(day);

        /// <summary>Skip a topic: it no longer counts among the open ones; what was done of it is forgotten.</summary>
        public void Skip(int day)
        {
            if (Result(day) != null || skipped.Contains(day)) return;
            skipped.Add(day);
            partial.RemoveAll(p => p.day == day);
        }

        public void Unskip(int day) => skipped.Remove(day);

        public bool IsRemoved(string key) => removed.Contains(key);

        /// <summary>Stop learning a word: it leaves the review and won't be taught in lessons.</summary>
        public void RemoveWord(string key)
        {
            if (!removed.Contains(key)) removed.Add(key);
            items.RemoveAll(i => i.key == key);
            misses.RemoveAll(m => m.key == key);
        }

        public void RestoreWord(string key) => removed.Remove(key);

        /// <summary>True when the letter or word is in training (learned on a day or added from a text).</summary>
        public bool IsLearning(string key) => items.Any(i => i.key == key);

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
            removed.RemoveAll(k => !exists(k));
        }

        /// <summary>
        /// Items for practice when nothing is due: hard ones first, then the least remembered (lowest box),
        /// in random order within each group.
        /// </summary>
        public List<string> PracticeKeys(int count, System.Random rng) => items
            .OrderByDescending(i => IsHard(i.key))
            .ThenBy(i => i.box)
            .ThenBy(_ => rng.Next())
            .Take(count)
            .Select(i => i.key)
            .ToList();

        /// <summary>
        /// A few items for the recall at app start: those due today first, then a random draw where
        /// hard and little-known items are much likelier than well-known ones.
        /// </summary>
        public List<string> RecallKeys(int count, System.Random rng, DateTime today)
        {
            var result = Due(today).Take(count).Select(i => i.key).ToList();
            var pool = items.Where(i => !result.Contains(i.key)).ToList();
            while (result.Count < count && pool.Count > 0)
            {
                double Weight(ItemState i) => IsHard(i.key) ? 2 * (Leitner.MaxBox + 1) : Leitner.MaxBox + 1 - i.box;
                double roll = rng.NextDouble() * pool.Sum(Weight);
                var pick = pool[pool.Count - 1];
                foreach (var i in pool)
                {
                    roll -= Weight(i);
                    if (roll < 0) { pick = i; break; }
                }
                result.Add(pick.key);
                pool.Remove(pick);
            }
            return result;
        }

        /// <summary>The recall is offered once a day, when something is learned already.</summary>
        public bool ShouldOfferRecall(DateTime today) =>
            items.Count >= RecallMin && lastRecall != DayUnlock.Format(today);

        public const int RecallMin = 3;

        public bool IsRead(string id) => read.Contains(id);

        public void MarkRead(string id)
        {
            if (!read.Contains(id)) read.Add(id);
        }

        /// <summary>Items whose review date has come, most overdue first.</summary>
        public List<ItemState> Due(DateTime today) => items
            .Where(i => DayUnlock.Parse(i.due) <= today.Date)
            .OrderBy(i => i.due).ThenBy(i => i.box)
            .ToList();

        /// <summary>Start tracking newly learned items; already known and removed ones are left alone.</summary>
        public void Learn(IEnumerable<string> keys, DateTime today)
        {
            foreach (var key in keys)
                if (!removed.Contains(key) && items.All(i => i.key != key))
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
        /// <summary>Days until the next review in each box: often at first, since new words are forgotten fast.</summary>
        static readonly int[] Days = { 1, 1, 2, 4, 7, 14, 30 };
        public const int MaxBox = 6;
        /// <summary>Most items in one daily review, so it stays short.</summary>
        public const int MaxPerSession = 12;

        public static int Interval(int box) => Days[Math.Clamp(box, 1, MaxBox)];
    }

    public enum DayState { Locked, LockedUntilTomorrow, ReviewFirst, Available, Done, Skipped }

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

        /// <summary>After the alphabet, this many topics are open at once; the learner picks one.</summary>
        public const int OpenTopics = 2;

        /// <summary>
        /// Letter days open one after another (see <see cref="State(int, ProgressData, DateTime)"/>).
        /// Topics after the alphabet open <see cref="OpenTopics"/> at a time: started ones first, then
        /// the next ones in course order that are neither done nor skipped. With «one lesson a day»,
        /// none opens on the day the last letter day or topic was finished.
        /// </summary>
        public static DayState State(Course course, int day, ProgressData p, DateTime today)
        {
            int alphabet = course.AlphabetDays;
            if (day <= alphabet) return State(day, p, today);
            if (p.Result(day) != null) return DayState.Done;
            if (IsSkipped(course, day, p)) return DayState.Skipped;
            if (p.Result(alphabet) == null || !OpenTopicDays(course, p).Contains(day)) return DayState.Locked;
            var last = p.results.Where(r => r.day >= alphabet).Select(r => Parse(r.date)).DefaultIfEmpty(DateTime.MinValue).Max();
            if (p.dayLock && last >= today.Date) return DayState.LockedUntilTomorrow;
            if (p.Due(today).Count > 0) return DayState.ReviewFirst;
            return DayState.Available;
        }

        /// <summary>Only optional topics can be skipped; obligatory ones always stay in the course.</summary>
        public static bool IsSkipped(Course course, int day, ProgressData p) =>
            p.IsSkipped(day) && course.Lesson(day)?.optional == true;

        /// <summary>Topics to choose from now: started ones first, then the next in order.</summary>
        public static List<int> OpenTopicDays(Course course, ProgressData p) => course.Topics
            .Where(l => p.Result(l.day) == null && !IsSkipped(course, l.day, p))
            .OrderByDescending(l => p.partial.Any(x => x.day == l.day && x.parts > 0))
            .ThenBy(l => l.day)
            .Take(OpenTopics)
            .Select(l => l.day)
            .ToList();

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
