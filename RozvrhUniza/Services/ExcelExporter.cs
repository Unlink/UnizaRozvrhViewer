using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using RozvrhUniza.Models;
using KST.UnizaSchedule.Api;
using KST.UnizaSchedule.Table;

namespace RozvrhUniza.Services
{
    public static class ExcelExporter
    {
        public static byte[] ExportTeacherSchedule(
        List<ScheduleContent> schedules,
        string teacherName,
        bool includeBlocked,
        Dictionary<string, string> aliases,
        List<CustomBlock> customBlocks,
        List<ScheduleVertical> scheduleVerticals,
        Dictionary<string, string> subjectColors)
        {
            if (!includeBlocked)
            {
                schedules = schedules.Where(s => s.LessonType != KST.UnizaSchedule.Api.Enums.LessonType.Blocked).ToList();
            }

            using var templateStream = GetTemplateStream();
            using var wb = new XLWorkbook(templateStream);
            var ws = wb.Worksheets.First();

            var now = DateTime.Now;
            var sem = KST.UnizaSchedule.Api.Enums.Semester.Winter == ScheduleApiHelpers.GetSemester(now) ? "zimný" : "letný";
            var studyYear = ScheduleApiHelpers.GetStudyYear(now);
            var studyYearText = sem == "zimný" ? $"{studyYear}/{studyYear + 1}" : $"{studyYear - 1}/{studyYear}";
            var titleText = $"{teacherName} - {sem} semester {studyYearText}";
            var titleCell = ws.CellsUsed(c => c.GetString().Contains("semester", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            if (titleCell is not null)
            {
                titleCell.Value = titleText;
            }

            var hourCellsAll = ws.CellsUsed(c =>
            {
                var s2 = c.GetString();
                return s2.EndsWith(":00", StringComparison.OrdinalIgnoreCase) && int.TryParse(s2.AsSpan(0, s2.IndexOf(':')), out _);
            }).ToList();
            if (!hourCellsAll.Any())
                throw new InvalidOperationException("V šablóne neboli nájdené hlavièky hodín");

            var headerRow = hourCellsAll
            .GroupBy(c => c.Address.RowNumber)
            .OrderByDescending(g => g.Count())
            .First().Key;

            var headerRowCells = hourCellsAll
            .Where(c => c.Address.RowNumber == headerRow)
            .OrderBy(c => c.Address.ColumnNumber)
            .ToList();

            var hourToRange = new Dictionary<int, (int startCol, int endCol)>();
            foreach (var c in headerRowCells)
            {
                var text = c.GetString();
                var hour = int.Parse(text.Substring(0, text.IndexOf(':')));
                if (hourToRange.ContainsKey(hour)) continue;
                var range = c.IsMerged() ? c.MergedRange() : c.AsRange();
                var start = range.RangeAddress.FirstAddress.ColumnNumber;
                var end = range.RangeAddress.LastAddress.ColumnNumber;
                hourToRange.Add(hour, (start, end));
            }
            for (var h = ScheduleTable.FirstBlockHour; h <= ScheduleTable.LastBlockHour; h++)
            {
                if (!hourToRange.ContainsKey(h))
                    throw new InvalidOperationException($"V šablóne nebol nájdený ståpec pre èas {h}:00");
            }

            var dayMap = new Dictionary<DayOfWeek, (int top, int bottom)>();
            var skDays = new (DayOfWeek day, string text)[]
            {
                 (DayOfWeek.Monday, "Pondelok"),
                 (DayOfWeek.Tuesday, "Utorok"),
                 (DayOfWeek.Wednesday, "Streda"),
                 (DayOfWeek.Thursday, "Štvrtok"),
                 (DayOfWeek.Friday, "Piatok")
            };
            foreach (var (day, text) in skDays)
            {
                var cell = ws.CellsUsed(c => string.Equals(c.GetString(), text, StringComparison.Ordinal)).FirstOrDefault();
                if (cell is null)
                    throw new InvalidOperationException($"V šablóne nebol nájdený riadok pre deò '{text}'");
                dayMap[day] = (cell.Address.RowNumber, cell.Address.RowNumber + 1);
            }

            (int start, int end) HalfToCols(int halfIndex)
            {
                int hour = ScheduleTable.FirstBlockHour + (halfIndex / 2);
                bool second = (halfIndex % 2) == 1;
                var (hs, he) = hourToRange[hour];
                var w = he - hs + 1;
                if (w <= 1)
                {
                    return (hs, he);
                }
                int midEnd = hs + (w / 2) - 1;
                if (!second)
                    return (hs, midEnd);
                return (midEnd + 1, he);
            }

            var totalHalf = (ScheduleTable.LastBlockHour - ScheduleTable.FirstBlockHour + 1) * 2;
            var occupancy = new Dictionary<DayOfWeek, bool[]>();
            foreach (var d in skDays.Select(x => x.day)) occupancy[d] = new bool[totalHalf];
            foreach (var cb in customBlocks)
            {
                if (!occupancy.TryGetValue(cb.Day, out var arr)) continue;
                var s = Math.Clamp(cb.StartHalfIndex, 0, totalHalf);
                var e = Math.Clamp(cb.StartHalfIndex + cb.DurationHalfSlots, 0, totalHalf);
                for (int i = s; i < e; i++) arr[i] = true;
            }

            var ordered = schedules
            .OrderBy(s => s.Day)
            .ThenBy(s => s.BlockNumber)
            .ThenBy(s => s.CourseName)
            .ToList();

            var spans = new List<(DayOfWeek day, int startHalf, int durHalf, KST.UnizaSchedule.Api.Enums.LessonType type, string? course, string? subjShortcut, string? room, bool isCustom, string? customText, bool isVertical, UserBlockKind? customKind)>();
            int idx = 0;
            while (idx < ordered.Count)
            {
                var cur = ordered[idx];
                int count = 1;
                int j = idx + 1;
                while (j < ordered.Count)
                {
                    var nx = ordered[j];
                    if (nx.Day == cur.Day && nx.CourseName == cur.CourseName && nx.RoomName == cur.RoomName && nx.LessonType == cur.LessonType && nx.SubjectShortcut == cur.SubjectShortcut && nx.BlockNumber == cur.BlockNumber + (j - idx))
                    { count++; j++; }
                    else break;
                }
                var startHour = ScheduleTable.FirstBlockHour + cur.BlockNumber - 1;
                var startHalf = (startHour - ScheduleTable.FirstBlockHour) * 2;
                var durHalf = count * 2;
                var occ = occupancy[cur.Day];
                int k = startHalf; int end = startHalf + durHalf;
                while (k < end)
                {
                    while (k < end && occ[k]) k++;
                    if (k >= end) break;
                    int segStart = k;
                    while (k < end && !occ[k]) k++;
                    int segDur = k - segStart;
                    if (segDur > 0)
                        spans.Add((cur.Day, segStart, segDur, cur.LessonType, cur.CourseName, cur.SubjectShortcut, cur.RoomName, false, null, scheduleVerticals.Any(v => v.Day == cur.Day && v.StartHalf == segStart && v.DurationHalf == segDur), null));
                }
                idx = j;
            }
            foreach (var cb in customBlocks)
            {
                spans.Add((cb.Day, cb.StartHalfIndex, cb.DurationHalfSlots, KST.UnizaSchedule.Api.Enums.LessonType.Blocked, null, null, null, true, cb.Text, cb.Vertical, cb.Kind));
            }

            foreach (var s in spans.OrderBy(x => x.day).ThenBy(x => x.startHalf))
            {
                var (topRow, bottomRow) = dayMap[s.day];
                var (startCol, _) = HalfToCols(s.startHalf);
                var (_, endCol) = HalfToCols(s.startHalf + s.durHalf - 1);
                var rng = ws.Range(topRow, startCol, bottomRow, endCol);
                rng.Merge();

                var lines = new List<string>();
                if (s.isCustom)
                {
                    if (!string.IsNullOrWhiteSpace(s.customText)) lines.Add(s.customText!);
                }
                else
                {
                    lines.Add(LessonTypeSk(s.type));
                    var subj = s.course ?? s.subjShortcut;
                    if (!string.IsNullOrWhiteSpace(s.course) && aliases.TryGetValue(s.course!, out var alias) && !string.IsNullOrWhiteSpace(alias)) subj = alias;
                    if (!string.IsNullOrWhiteSpace(subj)) lines.Add(subj!);
                    if (!string.IsNullOrWhiteSpace(s.room)) lines.Add(s.room!);
                }
                rng.Value = string.Join("\n", lines);

                var style = rng.Style;
                style.Alignment.WrapText = true;
                style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                style.Alignment.TextRotation = s.isVertical ? 90 : 0;
                style.Border.OutsideBorder = XLBorderStyleValues.Medium;
                style.Border.OutsideBorderColor = XLColor.Black;
                style.Font.Bold = false;

                string fillHex;
                if (s.isCustom)
                {
                    fillHex = GetFillHexForKindExcel(s.customKind ?? UserBlockKind.Blocked);
                }
                else
                {
                    var baseHex = GetSubjectColorHexExcel(subjectColors, aliases, s.course, s.subjShortcut);
                    fillHex = string.IsNullOrWhiteSpace(baseHex) ? ColorUtils.GetFillHex(s.type) : (s.type == KST.UnizaSchedule.Api.Enums.LessonType.Lecture ? ColorUtils.DarkenHex(baseHex, 0.25) : baseHex);
                }
                style.Fill.BackgroundColor = XLColor.FromHtml(fillHex);
                var fontHex = ColorUtils.GetIdealTextColor(fillHex);
                style.Font.FontColor = XLColor.FromHtml(fontHex);
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static Stream GetTemplateStream()
        {
            var asm = typeof(ExcelExporter).Assembly;
            var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("RozvrhDverePriklad.xlsx", StringComparison.OrdinalIgnoreCase));
            if (name is null)
                throw new InvalidOperationException("Šablóna RozvrhDverePriklad.xlsx nebola nájdená ako EmbeddedResource.");
            var stream = asm.GetManifestResourceStream(name);
            if (stream is null)
                throw new InvalidOperationException("Nepodarilo sa otvori stream šablóny RozvrhDverePriklad.xlsx.");
            return stream;
        }

        private static string LessonTypeSk(KST.UnizaSchedule.Api.Enums.LessonType type)
        {
            return type switch
            {
                KST.UnizaSchedule.Api.Enums.LessonType.Lecture => "Prednáška",
                KST.UnizaSchedule.Api.Enums.LessonType.Laboratory or KST.UnizaSchedule.Api.Enums.LessonType.Excercise => "Cvièenie",
                KST.UnizaSchedule.Api.Enums.LessonType.Blocked => "Blok",
                _ => ""
            };
        }

        private static string GetFillHexForKindExcel(UserBlockKind kind)
        {
            return kind switch
            {
                UserBlockKind.Lecture => "#F9CB9C",
                UserBlockKind.Laboratory or UserBlockKind.Excercise => "#FFF2CC",
                UserBlockKind.Blocked => "#D5D5D5",
                UserBlockKind.Meeting => "#6C757D",
                UserBlockKind.Consultation => "#0D6EFD",
                UserBlockKind.Lunch => "#000000",
                _ => "#EA9999"
            };
        }

        private static string GetSubjectColorHexExcel(Dictionary<string, string> subjectColors, Dictionary<string, string> aliases, string? course, string? subjShortcut)
        {
            if (!string.IsNullOrWhiteSpace(course) && subjectColors.TryGetValue(course!, out var c) && !string.IsNullOrWhiteSpace(c)) return ColorUtils.NormalizeStaticHex(c);
            if (!string.IsNullOrWhiteSpace(course) && aliases.TryGetValue(course!, out var alias) && !string.IsNullOrWhiteSpace(alias) && subjectColors.TryGetValue(alias, out var c2)) return ColorUtils.NormalizeStaticHex(c2);
            if (!string.IsNullOrWhiteSpace(subjShortcut) && subjectColors.TryGetValue(subjShortcut!, out var c3)) return ColorUtils.NormalizeStaticHex(c3);
            return "";
        }
    }
}
