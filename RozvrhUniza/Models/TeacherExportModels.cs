using System;
using System.Collections.Generic;
using KST.UnizaSchedule.Api;

namespace RozvrhUniza.Models
{
    public enum UserBlockKind
    {
        Lecture,
        Excercise,
        Laboratory,
        Blocked,
        Meeting,
        Consultation,
        Lunch
    }

    public class PreviewSpan
    {
        public DayOfWeek Day { get; set; }
        public int StartHalfIndex { get; set; }
        public int DurationHalfSlots { get; set; }
        public KST.UnizaSchedule.Api.Enums.LessonType LessonType { get; set; }
        public string? CourseName { get; set; }
        public string? SubjectShortcut { get; set; }
        public string? RoomName { get; set; }
        public bool IsCustom { get; set; }
        public string? CustomText { get; set; }
        public bool Vertical { get; set; }
        public UserBlockKind? CustomKind { get; set; }
    }

    public class CustomBlock
    {
        public DayOfWeek Day { get; set; }
        public int StartHalfIndex { get; set; }
        public int DurationHalfSlots { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool Vertical { get; set; } = false;
        public UserBlockKind Kind { get; set; } = UserBlockKind.Blocked;
        // Legacy compatibility field (ignored for rendering)
        public KST.UnizaSchedule.Api.Enums.LessonType Type { get; set; } = KST.UnizaSchedule.Api.Enums.LessonType.Blocked;
    }

    public class ScheduleVertical
    {
        public DayOfWeek Day { get; set; }
        public int StartHalf { get; set; }
        public int DurationHalf { get; set; }
    }
}
