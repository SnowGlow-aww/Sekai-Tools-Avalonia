using System.Text;

namespace SekaiToolsPlatform.ViewModels;

public enum DiffPartKind
{
    Same,
    Add,
    Remove,
}

public sealed record DiffPartModel(string Text, DiffPartKind Kind)
{
    public bool IsSame => Kind == DiffPartKind.Same;
    public bool IsAdd => Kind == DiffPartKind.Add;
    public bool IsRemove => Kind == DiffPartKind.Remove;
}

public static class TextDiffBuilder
{
    public static (DiffPartModel[] LeftParts, DiffPartModel[] RightParts) Build(string? left, string? right)
    {
        var leftRunes = (left ?? string.Empty).EnumerateRunes().ToArray();
        var rightRunes = (right ?? string.Empty).EnumerateRunes().ToArray();

        if (leftRunes.Length == 0 && rightRunes.Length == 0)
        {
            return (Array.Empty<DiffPartModel>(), Array.Empty<DiffPartModel>());
        }

        var lcs = BuildLcsTable(leftRunes, rightRunes);
        var operations = BacktrackOperations(leftRunes, rightRunes, lcs);

        var leftParts = new List<DiffPartModel>();
        var rightParts = new List<DiffPartModel>();
        foreach (var (leftKind, leftText, rightKind, rightText) in operations)
        {
            Append(leftParts, leftKind, leftText);
            Append(rightParts, rightKind, rightText);
        }

        return (leftParts.ToArray(), rightParts.ToArray());
    }

    private static int[,] BuildLcsTable(Rune[] left, Rune[] right)
    {
        var table = new int[left.Length + 1, right.Length + 1];
        for (var i = left.Length - 1; i >= 0; i--)
        {
            for (var j = right.Length - 1; j >= 0; j--)
            {
                table[i, j] = left[i] == right[j]
                    ? table[i + 1, j + 1] + 1
                    : Math.Max(table[i + 1, j], table[i, j + 1]);
            }
        }

        return table;
    }

    private static List<(DiffPartKind LeftKind, string LeftText, DiffPartKind RightKind, string RightText)> BacktrackOperations(
        Rune[] left,
        Rune[] right,
        int[,] table)
    {
        var operations = new List<(DiffPartKind, string, DiffPartKind, string)>();
        var i = 0;
        var j = 0;
        while (i < left.Length && j < right.Length)
        {
            if (left[i] == right[j])
            {
                var text = left[i].ToString();
                operations.Add((DiffPartKind.Same, text, DiffPartKind.Same, text));
                i++;
                j++;
                continue;
            }

            if (table[i + 1, j] >= table[i, j + 1])
            {
                operations.Add((DiffPartKind.Remove, left[i].ToString(), DiffPartKind.Same, string.Empty));
                i++;
            }
            else
            {
                operations.Add((DiffPartKind.Same, string.Empty, DiffPartKind.Add, right[j].ToString()));
                j++;
            }
        }

        while (i < left.Length)
        {
            operations.Add((DiffPartKind.Remove, left[i].ToString(), DiffPartKind.Same, string.Empty));
            i++;
        }

        while (j < right.Length)
        {
            operations.Add((DiffPartKind.Same, string.Empty, DiffPartKind.Add, right[j].ToString()));
            j++;
        }

        return operations;
    }

    private static void Append(List<DiffPartModel> parts, DiffPartKind kind, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (parts.Count > 0 && parts[^1].Kind == kind)
        {
            var last = parts[^1];
            parts[^1] = last with { Text = last.Text + text };
            return;
        }

        parts.Add(new DiffPartModel(text, kind));
    }
}
