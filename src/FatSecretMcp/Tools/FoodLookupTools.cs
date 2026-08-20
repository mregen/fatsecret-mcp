// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Globalization;
using FatSecretMcp.Auth;
using ModelContextProtocol.Server;

namespace FatSecretMcp.Tools;

[McpServerToolType]
public class FoodLookupTools(FatSecretOAuth2Client client)
{
    [McpServerTool(Name = "search_foods")]
    [Description("Search FatSecret's food database by name and return matching foods with a brief nutrition summary.")]
    public Task<string> SearchFoods(
        [Description("Food name or search term")] string expression,
        [Description("Zero-based results page number, default 0")] int? pageNumber = null,
        [Description("Results per page, 1-50, default 20")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string> { ["search_expression"] = expression };
        if (pageNumber is not null) parameters["page_number"] = pageNumber.Value.ToString(CultureInfo.InvariantCulture);
        if (maxResults is not null) parameters["max_results"] = maxResults.Value.ToString(CultureInfo.InvariantCulture);

        return client.CallApiAsync("foods.search", parameters, "basic", cancellationToken);
    }

    [McpServerTool(Name = "find_food_by_barcode")]
    [Description("Look up a food by its UPC/EAN barcode. Accepts UPC-A, EAN-13, or EAN-8, padded with " +
        "leading zeros to 13 digits (GTIN-13); UPC-E barcodes should be converted to UPC-A first.")]
    public Task<string> FindFoodByBarcode(
        [Description("13-digit GTIN-13 barcode")] string barcode,
        CancellationToken cancellationToken = default) =>
        client.CallApiAsync(
            "food.find_id_for_barcode.v2",
            new Dictionary<string, string> { ["barcode"] = barcode },
            "barcode",
            cancellationToken);

    [McpServerTool(Name = "autocomplete_food")]
    [Description("Get food-name autocomplete suggestions for a partial search term.")]
    public Task<string> AutocompleteFood(
        [Description("Partial food name to get suggestions for")] string expression,
        [Description("Maximum suggestions to return (1-10, default 4)")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string> { ["expression"] = expression };
        if (maxResults is not null) parameters["max_results"] = maxResults.Value.ToString(CultureInfo.InvariantCulture);

        // FatSecret's docs list this as requiring the "premier" scope, despite being a basic-sounding feature.
        return client.CallApiAsync("foods.autocomplete.v2", parameters, "premier", cancellationToken);
    }
}
