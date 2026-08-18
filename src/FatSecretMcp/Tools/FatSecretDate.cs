// SPDX-License-Identifier: MIT

namespace FatSecretMcp.Tools;

/// <summary>
/// Converts between ISO date strings (natural for an LLM to produce) and FatSecret's "date_int"
/// format (days since 1970-01-01), used across diary/weight/exercise entries.
/// </summary>
public static class FatSecretDate
{
    private static readonly DateOnly Epoch = new(1970, 1, 1);

    public static int ToDateInt(string isoDate) => ToDateInt(DateOnly.Parse(isoDate));

    public static int ToDateInt(DateOnly date) => date.DayNumber - Epoch.DayNumber;

    public static string Today() => DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
}
