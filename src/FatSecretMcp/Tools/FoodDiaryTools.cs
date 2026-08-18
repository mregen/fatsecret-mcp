// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Globalization;
using FatSecretMcp.Auth;
using ModelContextProtocol.Server;

namespace FatSecretMcp.Tools;

[McpServerToolType]
public class FoodDiaryTools(FatSecretPremierApi api)
{
    [McpServerTool(Name = "get_food_entries")]
    [Description("Get the user's food diary entries for a given date.")]
    public Task<string> GetFoodEntries(
        [Description("Date in yyyy-MM-dd format")] string date,
        CancellationToken cancellationToken = default) =>
        api.CallAsync(
            "food_entries.get.v2",
            new Dictionary<string, string> { ["date"] = FatSecretDate.ToDateInt(date).ToString(CultureInfo.InvariantCulture) },
            cancellationToken);

    [McpServerTool(Name = "add_food_entry")]
    [Description("Log a food into the user's diary. Look up food_id and serving_id first via search_foods/get_food.")]
    public Task<string> AddFoodEntry(
        [Description("The FatSecret food_id from search_foods/get_food")] long foodId,
        [Description("Description shown in the diary, e.g. '2 slices Toast'")] string foodEntryName,
        [Description("The FatSecret serving_id from get_food")] long servingId,
        [Description("Number of servings consumed")] decimal numberOfUnits,
        [Description("One of: breakfast, lunch, dinner, other")] string meal,
        [Description("Date in yyyy-MM-dd format; defaults to today if omitted")] string? date = null,
        CancellationToken cancellationToken = default) =>
        api.CallAsync(
            "food_entry.create",
            new Dictionary<string, string> {
                ["food_id"] = foodId.ToString(CultureInfo.InvariantCulture),
                ["food_entry_name"] = foodEntryName,
                ["serving_id"] = servingId.ToString(CultureInfo.InvariantCulture),
                ["number_of_units"] = numberOfUnits.ToString(CultureInfo.InvariantCulture),
                ["meal"] = meal,
                ["date"] = FatSecretDate.ToDateInt(date ?? FatSecretDate.Today()).ToString(CultureInfo.InvariantCulture),
            },
            cancellationToken);

    [McpServerTool(Name = "edit_food_entry")]
    [Description("Edit an existing food diary entry. Only supplied fields are changed. The entry's date " +
        "cannot be changed this way - delete and recreate the entry instead.")]
    public Task<string> EditFoodEntry(
        [Description("The food_entry_id to edit, from get_food_entries")] long foodEntryId,
        [Description("New description, or omit to leave unchanged")] string? foodEntryName = null,
        [Description("New serving_id, or omit to leave unchanged")] long? servingId = null,
        [Description("New number of servings, or omit to leave unchanged")] decimal? numberOfUnits = null,
        [Description("New meal (breakfast/lunch/dinner/other), or omit to leave unchanged")] string? meal = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string> { ["food_entry_id"] = foodEntryId.ToString(CultureInfo.InvariantCulture) };
        if (foodEntryName is not null) parameters["food_entry_name"] = foodEntryName;
        if (servingId is not null) parameters["serving_id"] = servingId.Value.ToString(CultureInfo.InvariantCulture);
        if (numberOfUnits is not null) parameters["number_of_units"] = numberOfUnits.Value.ToString(CultureInfo.InvariantCulture);
        if (meal is not null) parameters["meal"] = meal;

        return api.CallAsync("food_entry.edit", parameters, cancellationToken);
    }

    [McpServerTool(Name = "delete_food_entry")]
    [Description("Delete a food diary entry.")]
    public Task<string> DeleteFoodEntry(
        [Description("The food_entry_id to delete, from get_food_entries")] long foodEntryId,
        CancellationToken cancellationToken = default) =>
        api.CallAsync(
            "food_entry.delete",
            new Dictionary<string, string> { ["food_entry_id"] = foodEntryId.ToString(CultureInfo.InvariantCulture) },
            cancellationToken);
}
