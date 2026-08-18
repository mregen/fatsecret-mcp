// SPDX-License-Identifier: MIT

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using FatSecretMcp.Auth;
using ModelContextProtocol.Server;

namespace FatSecretMcp.Tools;

[McpServerToolType]
public class ExerciseTools(FatSecretPremierApi api)
{
    [McpServerTool(Name = "get_exercise_entries")]
    [Description("Get how the user's full 24-hour day is allocated across activities for a given date. " +
        "FatSecret tracks the whole day, not just workouts - expect a baseline activity (varies per user, " +
        "e.g. a connected fitness tracker, or a default like Sleeping/Resting) holding most of the 1440 minutes.")]
    public Task<string> GetExerciseEntries(
        [Description("Date in yyyy-MM-dd format; defaults to today if omitted")] string? date = null,
        CancellationToken cancellationToken = default) =>
        api.CallAsync(
            "exercise_entries.get.v2",
            new Dictionary<string, string> { ["date"] = FatSecretDate.ToDateInt(date ?? FatSecretDate.Today()).ToString(CultureInfo.InvariantCulture) },
            cancellationToken);

    [McpServerTool(Name = "search_exercises")]
    [Description("Search FatSecret's exercise catalog by name to find an exercise_id for use with shift_exercise_time.")]
    public async Task<string> SearchExercises(
        [Description("Case-insensitive substring to match against exercise names, e.g. 'running'")] string query,
        CancellationToken cancellationToken = default)
    {
        var body = await api.CallAsync("exercises.get.v2", cancellationToken: cancellationToken);
        using var document = JsonDocument.Parse(body);
        var matches = document.RootElement
            .GetProperty("exercises")
            .GetProperty("exercise")
            .EnumerateArray()
            .Where(exercise => exercise.GetProperty("exercise_name").GetString()?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
            .Select(exercise => new {
                exercise_id = exercise.GetProperty("exercise_id").GetString(),
                exercise_name = exercise.GetProperty("exercise_name").GetString(),
            });

        return JsonSerializer.Serialize(matches);
    }

    [McpServerTool(Name = "shift_exercise_time")]
    [Description(
        "FatSecret models a day as a full 24-hour (1440 minute) allocation across activities, not independent " +
        "log entries - logging or removing exercise means 'shifting' minutes between two activities for that date. " +
        "Call get_exercise_entries first to see which activity currently holds the time you want to reassign " +
        "(often a baseline activity that varies per user - check before assuming which one). To log a new workout, " +
        "shift minutes FROM that baseline activity TO the exercise being logged. To remove or shorten a logged " +
        "exercise, shift minutes FROM that exercise back TO the baseline activity.")]
    public Task<string> ShiftExerciseTime(
        [Description("exercise_id to take minutes from (see get_exercise_entries)")] long fromExerciseId,
        [Description("exercise_id to give minutes to (see search_exercises)")] long toExerciseId,
        [Description("Minutes to shift")] int minutes,
        [Description("Date in yyyy-MM-dd format; defaults to today if omitted")] string? date = null,
        [Description("Custom name if fromExerciseId is 0 (\"Other\")")] string? fromExerciseName = null,
        [Description("Custom name if toExerciseId is 0 (\"Other\")")] string? toExerciseName = null,
        [Description("Calories burned, only used when toExerciseId is a custom (id 0) exercise")] int? kcal = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string> {
            ["shift_from_id"] = fromExerciseId.ToString(CultureInfo.InvariantCulture),
            ["shift_to_id"] = toExerciseId.ToString(CultureInfo.InvariantCulture),
            ["minutes"] = minutes.ToString(CultureInfo.InvariantCulture),
            ["date"] = FatSecretDate.ToDateInt(date ?? FatSecretDate.Today()).ToString(CultureInfo.InvariantCulture),
        };
        if (fromExerciseName is not null) parameters["shift_from_name"] = fromExerciseName;
        if (toExerciseName is not null) parameters["shift_to_name"] = toExerciseName;
        if (kcal is not null) parameters["kcal"] = kcal.Value.ToString(CultureInfo.InvariantCulture);

        return api.CallAsync("exercise_entry.edit", parameters, cancellationToken);
    }
}
