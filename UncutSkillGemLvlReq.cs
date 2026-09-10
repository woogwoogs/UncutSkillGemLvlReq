using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;
using ExileCore2;
using ExileCore2.PoEMemory;
using ExileCore2.PoEMemory.Elements;
using ImGuiNET;

namespace UncutSkillGemLvlReq;

public sealed class UncutSkillGemLvlReq : BaseSettingsPlugin<UncutSkillGemLvlReqSettings>
{
    // PoE2DB Uncut Skill Gem / Tier Level, checked 2026-09-10.
    // Index is gem level, NOT item level. The blank level-1 requirement
    // is displayed as 1 (the lowest possible character level).
    private static readonly int[] Requirements =
        { 0, 1, 3, 6, 10, 14, 18, 22, 26, 31, 36, 41, 46, 52, 58, 64, 66, 72, 78, 84, 90 };

    private static readonly Regex LevelLine = new(
        @"^\s*Level\s*:?\s*(\d{1,2})\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
    private static readonly Regex GemTitle = new(
        @"^\s*Uncut\s+(?:Skill|Spirit)\s+Gem\s*\(\s*Level\s*:?\s*(\d{1,2})\s*\)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
    private static readonly Regex Tags = new("<[^>]*>", RegexOptions.Compiled);
    private static readonly Regex SupportedUncutGemPath = new(
        @"^Metadata/Items/Gems/(?:SkillGemUncut(?:Quest)?|ReservationGemUncut)\d*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Dictionary<Type, PropertyInfo?> TextProperties = new();
    public override bool Initialise() => true;

    public override void Render()
    {
        if (!Settings.Enable.Value) return;
        try
        {
            var hover = GameController.Game.IngameState.UIHover?.AsObject<HoverItemIcon>();
            if (hover == null || !hover.IsValid) return;
            var item = hover.Item;
            var tooltip = hover.Tooltip;
            if (item == null || !item.IsValid || tooltip == null || !tooltip.IsVisible) return;

            // Live paths carry the tier, e.g. SkillGemUncut14 and ReservationGemUncut6.
            if (!SupportedUncutGemPath.IsMatch(item.Path ?? string.Empty)) return;

            var rect = tooltip.GetClientRect();
            if (rect.Width <= 0 || rect.Height <= 0) return;
            var level = ReadGemLevel(tooltip);
            if (level < 1 || level >= Requirements.Length) return;

            var text = $"lvl {Requirements[level]}";
            var font = ImGui.GetFont();
            var size = (float)Settings.TextSize.Value;
            var textSize = font.CalcTextSizeA(size, float.MaxValue, 0, text);
            var viewport = ImGui.GetIO().DisplaySize;
            var position = new Vector2(
                rect.Right - textSize.X - 18 + Settings.OffsetX.Value,
                rect.Top + 12 + Settings.OffsetY.Value);
            position.X = Math.Clamp(position.X, 0, Math.Max(0, viewport.X - textSize.X));
            position.Y = Math.Clamp(position.Y, 0, Math.Max(0, viewport.Y - textSize.Y));

            var draw = ImGui.GetForegroundDrawList();
            // Outline keeps the small label legible without adding a window or badge.
            var outline = ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 1));
            draw.AddText(font, size, position + new Vector2(-1, 0), outline, text);
            draw.AddText(font, size, position + new Vector2(1, 0), outline, text);
            draw.AddText(font, size, position + new Vector2(0, -1), outline, text);
            draw.AddText(font, size, position + new Vector2(0, 1), outline, text);
            draw.AddText(font, size, position,
                ImGui.ColorConvertFloat4ToU32(new Vector4(.85f, .85f, .85f, 1)), text);
        }
        catch
        {
            // Hover memory may disappear during panel/area transitions. No stale label.
        }
    }

    private static int ReadGemLevel(Element tooltip)
    {
        var pending = new Stack<(Element Element, int Depth)>();
        var seen = new HashSet<long>();
        var lines = new List<string>();
        pending.Push((tooltip, 0));
        var visited = 0;
        while (pending.Count > 0 && visited++ < 256)
        {
            var (element, depth) = pending.Pop();
            if (!element.IsValid || !seen.Add(element.Address)) continue;
            var type = element.GetType();
            if (!TextProperties.TryGetValue(type, out var property))
            {
                property = type.GetProperty("TextNoTags") ?? type.GetProperty("Text");
                TextProperties[type] = property;
            }
            var raw = property?.GetValue(element) as string;
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var text = Tags.Replace(raw, "").Replace('\u00a0', ' ');
                // Current PoE2 puts the level in the title, e.g.
                // "Uncut Skill Gem (Level 14)", not a separate Level: line.
                var match = GemTitle.Match(text);
                if (!match.Success) match = LevelLine.Match(text);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var level)) return level;
                if (element.Children.Count == 0) lines.Add(text.Trim());
            }
            if (depth >= 12) continue;
            var children = element.Children;
            // Preserve UI order for separate "Level:" and "11" text elements.
            for (var i = Math.Min(children.Count, 256) - 1; i >= 0; i--)
                pending.Push((children[i], depth + 1));
        }
        var joined = string.Join("\n", lines);
        var combined = GemTitle.Match(joined);
        if (!combined.Success) combined = LevelLine.Match(joined);
        return combined.Success && int.TryParse(combined.Groups[1].Value, out var result) ? result : 0;
    }
}
