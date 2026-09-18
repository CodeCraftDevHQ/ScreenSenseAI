using System.Windows;
using ScreenTranslator.Models;

namespace ScreenTranslator.Services.Text;

public static class BlockGrouper
{
    public static IReadOnlyList<OcrBlock> GroupIntoParagraphs(IReadOnlyList<OcrBlock> lines, double verticalToleranceRatio = 0.85, double horizontalToleranceRatio = 2.5)
    {
        if (lines.Count == 0)
            return Array.Empty<OcrBlock>();

        var ordered = lines
            .OrderBy(x => x.Bounds.Top)
            .ThenBy(x => x.Bounds.Left)
            .ToList();

        var used = new bool[ordered.Count];
        var result = new List<OcrBlock>();

        for (var i = 0; i < ordered.Count; i++)
        {
            if (used[i])
                continue;

            var group = new List<OcrBlock> { ordered[i] };
            used[i] = true;

            var current = ordered[i];

            for (var j = i + 1; j < ordered.Count; j++)
            {
                if (used[j])
                    continue;

                var candidate = ordered[j];

                if (!BelongsTogether(current, candidate, verticalToleranceRatio, horizontalToleranceRatio))
                    continue;

                group.Add(candidate);
                used[j] = true;
                current = candidate;
            }

            result.Add(Merge(group));
        }

        return result;
    }

    private static bool BelongsTogether(OcrBlock a, OcrBlock b, double verticalRatio, double horizontalRatio)
    {
        var averageHeight = (a.Bounds.Height + b.Bounds.Height) / 2.0;

        if (averageHeight <= 0)
            return false;

        var heightRatio = Math.Abs(a.Bounds.Height - b.Bounds.Height) / averageHeight;

        if (heightRatio > 0.45)
            return false;

        var verticalGap = b.Bounds.Top - a.Bounds.Bottom;

        if (verticalGap < -averageHeight * 0.5 || verticalGap > averageHeight * verticalRatio)
            return false;

        var overlapLeft = Math.Max(a.Bounds.Left, b.Bounds.Left);
        var overlapRight = Math.Min(a.Bounds.Right, b.Bounds.Right);
        var overlap = overlapRight - overlapLeft;
        var minWidth = Math.Min(a.Bounds.Width, b.Bounds.Width);

        if (minWidth <= 0)
            return false;

        return overlap / minWidth > 0.35 || Math.Abs(a.Bounds.Left - b.Bounds.Left) < averageHeight * horizontalRatio;
    }

    private static OcrBlock Merge(List<OcrBlock> group)
    {
        var left = group.Min(x => x.Bounds.Left);
        var top = group.Min(x => x.Bounds.Top);
        var right = group.Max(x => x.Bounds.Right);
        var bottom = group.Max(x => x.Bounds.Bottom);

        return new OcrBlock
        {
            Text = string.Join(" ", group.Select(x => x.Text.Trim())).Trim(),
            Bounds = new Rect(left, top, right - left, bottom - top),
            Confidence = group.Average(x => x.Confidence)
        };
    }

    public static IReadOnlyList<OcrBlock> FilterNoise(IReadOnlyList<OcrBlock> blocks, double minConfidence)
    {
        return blocks
            .Where(x => x.Confidence >= minConfidence)
            .Where(x => x.Text.Trim().Length > 1)
            .Where(x => x.Bounds.Width > 8 && x.Bounds.Height > 6)
            .Where(x => x.Text.Any(char.IsLetter))
            .ToList();
    }
}
