// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Globalization;
using FatSecretMcp.Auth;
using ModelContextProtocol.Server;

namespace FatSecretMcp.Tools;

[McpServerToolType]
public class FoodLookupTools(FatSecretOAuth2Client client)
{
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
