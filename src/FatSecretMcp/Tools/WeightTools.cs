// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Globalization;
using FatSecretMcp.Auth;
using ModelContextProtocol.Server;

namespace FatSecretMcp.Tools;

[McpServerToolType]
public class WeightTools(FatSecretPremierApi api)
{
    [McpServerTool(Name = "get_weight_history")]
    [Description("Get the user's recorded weight entries for the month containing the given date.")]
    public Task<string> GetWeightHistory(
        [Description("Any date within the target month, yyyy-MM-dd format; defaults to the current month if omitted")] string? date = null,
        CancellationToken cancellationToken = default) =>
        api.CallAsync(
            "weights.get_month.v2",
            new Dictionary<string, string> { ["date"] = FatSecretDate.ToDateInt(date ?? FatSecretDate.Today()).ToString(CultureInfo.InvariantCulture) },
            cancellationToken);

    [McpServerTool(Name = "add_weight_entry")]
    [Description("Record the user's weight for a date. goalWeightKg and heightCm are only required for the " +
        "user's very first-ever weigh-in; omit them otherwise.")]
    public Task<string> AddWeightEntry(
        [Description("Weight in kilograms")] decimal weightKg,
        [Description("Date in yyyy-MM-dd format; defaults to today if omitted")] string? date = null,
        [Description("Optional note about this weigh-in")] string? comment = null,
        [Description("Goal weight in kilograms - required only for the very first weigh-in")] decimal? goalWeightKg = null,
        [Description("Height in centimeters - required only for the very first weigh-in")] decimal? heightCm = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string> {
            ["current_weight_kg"] = weightKg.ToString(CultureInfo.InvariantCulture),
            ["date"] = FatSecretDate.ToDateInt(date ?? FatSecretDate.Today()).ToString(CultureInfo.InvariantCulture),
        };
        if (comment is not null) parameters["comment"] = comment;
        if (goalWeightKg is not null) parameters["goal_weight_kg"] = goalWeightKg.Value.ToString(CultureInfo.InvariantCulture);
        if (heightCm is not null) parameters["current_height_cm"] = heightCm.Value.ToString(CultureInfo.InvariantCulture);

        return api.CallAsync("weight.update", parameters, cancellationToken);
    }
}
