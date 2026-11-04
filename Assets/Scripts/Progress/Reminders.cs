using System;
using System.Collections.Generic;
#if UNITY_ANDROID || UNITY_IOS
using Unity.Notifications;
#endif

namespace Aib.Progress
{
    /// <summary>Daily local reminders (no server). Rescheduled on every app start and after each lesson.</summary>
    public static class Reminders
    {
        const int DaysAhead = 7;
        public static readonly int[] Hours = { 9, 13, 19, 21 };

        /// <summary>Local notifications exist only on phones; PC and web builds hide the setting.</summary>
#if UNITY_ANDROID || UNITY_IOS
        public const bool Supported = true;
#else
        public const bool Supported = false;
#endif

        static readonly string[] Texts =
        {
            "Բարև! Пора заниматься: 10 минут армянского.",
            "Повторение и новые буквы ждут вас.",
            "Чтобы не забыть буквы, повторяйте их каждый день.",
        };

        /// <summary>Next setting when the user taps the reminder button: 9 → 13 → 19 → 21 → off → 9.</summary>
        public static int NextHour(int hour)
        {
            int i = Array.IndexOf(Hours, hour);
            return i < 0 ? Hours[0] : i == Hours.Length - 1 ? -1 : Hours[i + 1];
        }

        public static string Label(int hour) => hour < 0 ? "Напоминание: выкл." : $"Напоминание: {hour}:00";

        /// <summary>Reminder fire times and texts for the coming days. Skips today if today's work is done.</summary>
        public static List<(DateTime time, string text)> Plan(ProgressData p, DateTime now, bool doneToday)
        {
            var plan = new List<(DateTime, string)>();
            if (p.reminderHour < 0) return plan;
            for (int d = 0; d < DaysAhead; d++)
            {
                var time = now.Date.AddDays(d).AddHours(p.reminderHour);
                if (time <= now || (d == 0 && doneToday)) continue;
                plan.Add((time, Texts[d % Texts.Length]));
            }
            return plan;
        }

        public static void Schedule(ProgressData p, bool doneToday)
        {
#if UNITY_ANDROID || UNITY_IOS
            var args = NotificationCenterArgs.Default;
            args.AndroidChannelId = "daily";
            args.AndroidChannelName = "Ежедневные напоминания";
            args.AndroidChannelDescription = "Напоминание об уроке армянского";
            NotificationCenter.Initialize(args);
            NotificationCenter.CancelAllScheduledNotifications();
            if (p.reminderHour < 0) return;
            NotificationCenter.RequestPermission();
            foreach (var (time, text) in Plan(p, DateTime.Now, doneToday))
            {
                NotificationCenter.ScheduleNotification(
                    new Notification { Title = "Այբ — армянский", Text = text },
                    new NotificationDateTimeSchedule(time));
            }
#endif
        }
    }
}
