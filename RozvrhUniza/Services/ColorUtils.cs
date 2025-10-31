using System;
using System.Collections.Generic;
using System.Linq;
using KST.UnizaSchedule.Api;
using RozvrhUniza.Models;

namespace RozvrhUniza.Services
{
    public static class ColorUtils
    {
        public static string NormalizeHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return "#000000";
            var h = hex.Trim();
            if (!h.StartsWith('#')) h = "#" + h;
            if (h.Length == 4) // #RGB -> #RRGGBB
            {
                h = "#" + new string(new[] { h[1], h[1], h[2], h[2], h[3], h[3] });
            }
            return h.Length >= 7 ? h.Substring(0, 7) : h;
        }

        public static string NormalizeStaticHex(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return "#000000";
            var h = hex.Trim();
            if (!h.StartsWith('#')) h = "#" + h;
            if (h.Length == 4)
            {
                h = "#" + new string(new[] { h[1], h[1], h[2], h[2], h[3], h[3] });
            }
            if (h.Length < 7) h = h.PadRight(7, '0');
            return h.Substring(0, 7);
        }

        public static (int r, int g, int b) ParseHex(string hex)
        {
            var h = NormalizeStaticHex(hex);
            int r = Convert.ToInt32(h.Substring(1, 2), 16);
            int g = Convert.ToInt32(h.Substring(3, 2), 16);
            int b = Convert.ToInt32(h.Substring(5, 2), 16);
            return (r, g, b);
        }

        public static string DarkenHex(string hex, double amount)
        {
            (int r, int g, int b) = ParseHex(hex);
            r = (int)Math.Clamp(r * (1 - amount), 0, 255);
            g = (int)Math.Clamp(g * (1 - amount), 0, 255);
            b = (int)Math.Clamp(b * (1 - amount), 0, 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        public static string GetIdealTextColor(string bgHex)
        {
            var (r, g, b) = ParseHex(bgHex);
            var brightness = (r * 299 + g * 587 + b * 114) / 1000;
            return brightness < 128 ? "#FFFFFF" : "#000000";
        }

        public static string GetFillHex(KST.UnizaSchedule.Api.Enums.LessonType type)
        => type switch
        {
            KST.UnizaSchedule.Api.Enums.LessonType.Lecture => "#F9CB9C",
            KST.UnizaSchedule.Api.Enums.LessonType.Laboratory or KST.UnizaSchedule.Api.Enums.LessonType.Excercise => "#FFF2CC",
            KST.UnizaSchedule.Api.Enums.LessonType.Blocked => "#D5D5D5",
            _ => "#EA9999"
        };

        public static string GetFillHexForKind(UserBlockKind kind)
        => kind switch
        {
            UserBlockKind.Lecture => "#F9CB9C",
            UserBlockKind.Laboratory or UserBlockKind.Excercise => "#FFF2CC",
            UserBlockKind.Blocked => "#D5D5D5",
            UserBlockKind.Meeting => "#6C757D",
            UserBlockKind.Consultation => "#0D6EFD",
            UserBlockKind.Lunch => "#3b3b3b",
            _ => "#EA9999"
        };

        public static string GetSubjectColorHex(Dictionary<string, string> subjectColors, Dictionary<string, string> aliases, string? courseOrShortcut)
        {
            if (string.IsNullOrWhiteSpace(courseOrShortcut)) return "";
            if (subjectColors.TryGetValue(courseOrShortcut!, out var c) && !string.IsNullOrWhiteSpace(c)) return NormalizeHex(c);
            // try find by alias reverse mapping
            var kv = aliases.FirstOrDefault(kv => string.Equals(kv.Value, courseOrShortcut, StringComparison.Ordinal));
            if (!string.IsNullOrEmpty(kv.Key) && subjectColors.TryGetValue(kv.Key, out var c2)) return NormalizeHex(c2);
            return "";
        }

        public static string GetSubjectBasedFill(
        Dictionary<string, string> subjectColors,
        Dictionary<string, string> aliases,
        string? courseName,
        string? subjShortcut,
        KST.UnizaSchedule.Api.Enums.LessonType type)
        {
            var key = string.IsNullOrWhiteSpace(courseName) ? subjShortcut : courseName;
            var baseHex = GetSubjectColorHex(subjectColors, aliases, key);
            if (string.IsNullOrWhiteSpace(baseHex))
            {
                return GetFillHex(type);
            }
            if (type == KST.UnizaSchedule.Api.Enums.LessonType.Lecture)
                return DarkenHex(baseHex, 0.25);
            return baseHex;
        }
    }
}
