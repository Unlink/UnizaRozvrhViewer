using System;
using System.Collections.Generic;
using System.Linq;
using RozvrhUniza.Models;
using KST.UnizaSchedule.Api;
using KST.UnizaSchedule.Table;

namespace RozvrhUniza.Services
{
    public static class SchedulePreviewService
    {
        public static (Dictionary<DayOfWeek, List<PreviewSpan>> previewMap, Dictionary<DayOfWeek, bool[]> scheduleOcc) BuildPreview(
        List<ScheduleContent> currentSchedule,
        List<CustomBlock> customBlocks,
        List<ScheduleVertical> verticals,
        bool includeBlocks)
        {
            var previewMap = new Dictionary<DayOfWeek, List<PreviewSpan>>();
            var scheduleOcc = new Dictionary<DayOfWeek, bool[]>();

            var filtered = includeBlocks
            ? currentSchedule
            : currentSchedule.Where(s => s.LessonType != KST.UnizaSchedule.Api.Enums.LessonType.Blocked);

            var totalHalf = (ScheduleTable.LastBlockHour - ScheduleTable.FirstBlockHour + 1) * 2;
            var occupancy = new Dictionary<DayOfWeek, bool[]>();
            foreach (var d in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
            {
                occupancy[d] = new bool[totalHalf];
                scheduleOcc[d] = new bool[totalHalf];
            }
            foreach (var cb in customBlocks)
            {
                if (!occupancy.TryGetValue(cb.Day, out var arr)) continue;
                var s = Math.Clamp(cb.StartHalfIndex, 0, totalHalf);
                var e = Math.Clamp(cb.StartHalfIndex + cb.DurationHalfSlots, 0, totalHalf);
                for (int i = s; i < e; i++) arr[i] = true;
            }

            var ordered = filtered
            .OrderBy(s => s.Day)
            .ThenBy(s => s.BlockNumber)
            .ThenBy(s => s.CourseName)
            .ToList();

            int idx = 0;
            while (idx < ordered.Count)
            {
                var cur = ordered[idx];
                int count = 1;
                int j = idx + 1;
                while (j < ordered.Count)
                {
                    var nx = ordered[j];
                    if (nx.Day == cur.Day &&
                    nx.CourseName == cur.CourseName &&
                    nx.RoomName == cur.RoomName &&
                    nx.LessonType == cur.LessonType &&
                    nx.SubjectShortcut == cur.SubjectShortcut &&
                    nx.BlockNumber == cur.BlockNumber + (j - idx))
                    {
                        count++;
                        j++;
                    }
                    else break;
                }

                var startHour = ScheduleTable.FirstBlockHour + cur.BlockNumber - 1;
                var startHalf = (startHour - ScheduleTable.FirstBlockHour) * 2;
                var durHalf = count * 2;

                var occSch = scheduleOcc[cur.Day];
                for (int h = startHalf; h < startHalf + durHalf; h++) occSch[h] = true;

                var occ = occupancy[cur.Day];
                int k = startHalf;
                int end = startHalf + durHalf;
                while (k < end)
                {
                    while (k < end && occ[k]) k++;
                    if (k >= end) break;
                    int segStart = k;
                    while (k < end && !occ[k]) k++;
                    int segDur = k - segStart;
                    if (segDur > 0)
                    {
                        AddSpan(previewMap, new PreviewSpan
                        {
                            Day = cur.Day,
                            StartHalfIndex = segStart,
                            DurationHalfSlots = segDur,
                            LessonType = cur.LessonType,
                            CourseName = cur.CourseName,
                            SubjectShortcut = cur.SubjectShortcut,
                            RoomName = cur.RoomName,
                            IsCustom = false,
                            Vertical = verticals.Any(v => v.Day == cur.Day && v.StartHalf == segStart && v.DurationHalf == segDur)
                        });
                    }
                }

                idx = j;
            }

            foreach (var cb in customBlocks)
            {
                AddSpan(previewMap, new PreviewSpan
                {
                    Day = cb.Day,
                    StartHalfIndex = cb.StartHalfIndex,
                    DurationHalfSlots = cb.DurationHalfSlots,
                    LessonType = KST.UnizaSchedule.Api.Enums.LessonType.Blocked,
                    CustomText = cb.Text,
                    IsCustom = true,
                    Vertical = cb.Vertical,
                    CustomKind = cb.Kind
                });
            }

            return (previewMap, scheduleOcc);
        }

        private static void AddSpan(Dictionary<DayOfWeek, List<PreviewSpan>> map, PreviewSpan span)
        {
            if (!map.TryGetValue(span.Day, out var list))
            {
                list = new List<PreviewSpan>();
                map[span.Day] = list;
            }
            list.Add(span);
        }
    }
}
