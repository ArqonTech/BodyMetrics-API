using System.Net.Http.Headers;
using System.Net.Http.Json;
using BodyMetricsApi.Features.AthleteGroups.Create;
using BodyMetricsApi.Features.AthleteGroups.Shared.ViewModels;
using BodyMetricsApi.Features.Athletes.Import.ViewModels;
using BodyMetricsApi.Features.Athletes.Shared.ViewModels;
using BodyMetricsApi.Shared.ViewModels;
using BodyMetricsApi.Tests.TestInfrastructure;
using ClosedXML.Excel;

namespace BodyMetricsApi.Tests.Athletes;

[Collection(MongoCollectionDefinition.Name)]
public sealed class AthletesImportTeamColumnTests(MongoContainerFixture mongoFixture, AzuriteContainerFixture azuriteFixture) : IClassFixture<AzuriteContainerFixture>
{
    private static readonly IReadOnlyList<string> Headers =
    [
        "Setor", "Posição", "Fase", "Nome", "Sexo", "Raça", "Categoria", "Nascimento",
        "Data avaliação", "Peso", "Altura", "Altura sentado", "Tricep D.", "Tricep E.",
        "Sub esc", "Torax", "Sub. Axi", "Crist. ilíaca", "Sup. Espin", "abd", "Coxa D", "Coxa E",
        "Pantu D", "Pantu E", "C. ombro", "C.Peitoral", "C.Braço D.", "C.Braço E.",
        "C.Cintura", "C.Quadril", "C. Medial D", "C.Medial E", "Pantu. D.", "Pantu. E.",
        "D.Punho", "D.Joelho", "D.Tornozelo", "Time"
    ];

    [Fact]
    public async Task ImportSpreadsheet_WithTeamColumn_ShouldCreateGroupAndAssignAthlete()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-import-team-new");
        using var content = CreateImportContent("Soccer-import-team", BuildWorkbookBytes([CreateRow("Team Athlete", "Alpha")]));

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(1, summary!.CreatedAthletes);
        Assert.Equal(1, summary.CreatedGroups);

        var group = await FindGroupByNameAsync(client, factory, "Alpha");
        Assert.NotNull(group);
        var members = await GetMembersAsync(client, factory, group!.Id);
        Assert.Single(members, m => m.FullName == "Team Athlete");

        var athletesResponse = await client.GetAsync("/api/athletes?page=1&pageSize=10");
        var athletes = await athletesResponse.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);
        Assert.Equal(1, athletes!.TotalCount);
        Assert.Single(athletes.Items, athlete => athlete.FullName == "Team Athlete");
    }

    [Fact]
    public async Task ImportSpreadsheet_WithExistingGroupName_ShouldReuseGroupInsteadOfDuplicating()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-import-team-reuse");

        var existingGroup = await CreateGroupAsync(client, factory, "Bravo");
        using var content = CreateImportContent("Soccer-import-reuse", BuildWorkbookBytes([CreateRow("Reuse Athlete", "Bravo")]));

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(0, summary!.CreatedGroups);

        var allGroups = await GetAllGroupsAsync(client, factory);
        Assert.Single(allGroups, g => g.Name == "Bravo");

        var members = await GetMembersAsync(client, factory, existingGroup.Id);
        Assert.Single(members, m => m.FullName == "Reuse Athlete");
    }

    [Fact]
    public async Task ImportSpreadsheet_ReimportingGroupedAthlete_ShouldUpdateInPlaceNotDuplicate()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-import-team-reimport");

        using var firstContent = CreateImportContent("Soccer-import-reimport", BuildWorkbookBytes([CreateRow("Reimport Athlete", "Charlie", weightKg: 70m)]));
        await client.PostAsync("/api/athletes/import", firstContent);

        using var secondContent = CreateImportContent("Soccer-import-reimport", BuildWorkbookBytes([CreateRow("Reimport Athlete", "Charlie", weightKg: 80m)]));
        var response = await client.PostAsync("/api/athletes/import", secondContent);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(0, summary!.CreatedAthletes);
        Assert.Equal(1, summary.UpdatedAthletes);
        Assert.Equal(0, summary.CreatedGroups);

        var group = await FindGroupByNameAsync(client, factory, "Charlie");
        var members = await GetMembersAsync(client, factory, group!.Id);
        var member = Assert.Single(members, m => m.FullName == "Reimport Athlete");
        Assert.Equal(80m, member.PhysicalAssessments[0].GeneralMeasurements.WeightKg);
    }

    [Fact]
    public async Task ImportSpreadsheet_ChangingTeamValue_ShouldMoveAthleteBetweenGroups()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-import-team-move");

        using var firstContent = CreateImportContent("Soccer-import-move", BuildWorkbookBytes([CreateRow("Move Athlete", "Delta")]));
        await client.PostAsync("/api/athletes/import", firstContent);

        using var secondContent = CreateImportContent("Soccer-import-move", BuildWorkbookBytes([CreateRow("Move Athlete", "Echo")]));
        await client.PostAsync("/api/athletes/import", secondContent);

        var deltaGroup = await FindGroupByNameAsync(client, factory, "Delta");
        var echoGroup = await FindGroupByNameAsync(client, factory, "Echo");

        var deltaMembers = await GetMembersAsync(client, factory, deltaGroup!.Id);
        var echoMembers = await GetMembersAsync(client, factory, echoGroup!.Id);

        Assert.Empty(deltaMembers);
        Assert.Single(echoMembers, m => m.FullName == "Move Athlete");
    }

    [Fact]
    public async Task ImportSpreadsheet_BlankTeamOnReimport_ShouldKeepAthleteInExistingGroup()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-import-team-blank");

        using var firstContent = CreateImportContent("Soccer-import-blank", BuildWorkbookBytes([CreateRow("Blank Athlete", "Foxtrot")]));
        await client.PostAsync("/api/athletes/import", firstContent);

        using var secondContent = CreateImportContent("Soccer-import-blank", BuildWorkbookBytes([CreateRow("Blank Athlete", team: null, weightKg: 81m)]));
        await client.PostAsync("/api/athletes/import", secondContent);

        var group = await FindGroupByNameAsync(client, factory, "Foxtrot");
        var members = await GetMembersAsync(client, factory, group!.Id);
        var member = Assert.Single(members, m => m.FullName == "Blank Athlete");
        Assert.Equal(81m, member.PhysicalAssessments[0].GeneralMeasurements.WeightKg);
    }

    private static async Task<AthleteGroupViewModel> CreateGroupAsync(HttpClient client, TestApplicationFactory factory, string name)
    {
        var response = await client.PostAsJsonAsync("/api/athlete-groups", new CreateAthleteGroupCommand(name), factory.JsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AthleteGroupViewModel>(factory.JsonSerializerOptions))!;
    }

    private static async Task<List<AthleteGroupViewModel>> GetAllGroupsAsync(HttpClient client, TestApplicationFactory factory)
    {
        var response = await client.GetAsync("/api/athlete-groups");
        return (await response.Content.ReadFromJsonAsync<List<AthleteGroupViewModel>>(factory.JsonSerializerOptions))!;
    }

    private static async Task<AthleteGroupViewModel?> FindGroupByNameAsync(HttpClient client, TestApplicationFactory factory, string name)
    {
        var groups = await GetAllGroupsAsync(client, factory);
        return groups.SingleOrDefault(g => g.Name == name);
    }

    private static async Task<List<AthleteViewModel>> GetMembersAsync(HttpClient client, TestApplicationFactory factory, string groupId)
    {
        var response = await client.GetAsync($"/api/athlete-groups/{groupId}/members");
        return (await response.Content.ReadFromJsonAsync<List<AthleteViewModel>>(factory.JsonSerializerOptions))!;
    }

    private static MultipartFormDataContent CreateImportContent(string sportName, byte[] workbookBytes)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(sportName), "SportName");

        var fileContent = new ByteArrayContent(workbookBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "File", "athletes.xlsx");

        return content;
    }

    private static byte[] BuildWorkbookBytes(IReadOnlyList<object?[]> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Athletes");

        for (var columnIndex = 0; columnIndex < Headers.Count; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = Headers[columnIndex];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < Headers.Count; columnIndex++)
            {
                var value = rows[rowIndex][columnIndex];
                if (value is DateOnly dateOnly)
                {
                    worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = dateOnly.ToDateTime(TimeOnly.MinValue);
                    continue;
                }

                if (value is null)
                {
                    continue;
                }

                switch (value)
                {
                    case string stringValue:
                        worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = stringValue;
                        break;
                    case decimal decimalValue:
                        worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = decimalValue;
                        break;
                    default:
                        worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = value.ToString();
                        break;
                }
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static object?[] CreateRow(string fullName, string? team, decimal weightKg = 75m)
    {
        return
        [
            "Adult", "Outside Hitter", "Competitivo", fullName, "Feminino", "Asiática", "A",
            new DateOnly(1999, 04, 08),
            new DateOnly(2026, 01, 01),
            weightKg, 177.2m, 92.5m,
            10.0m, 10.5m, 11.1m, 9.8m, 10.2m, 12.4m, 6.1m, 13.0m, 14.0m, 13.7m, 9.6m, 9.5m,
            108.0m, 94.0m, 32.0m, 31.5m, 75.0m, 96.0m, 55.0m, 54.5m, 37.0m, 36.5m,
            16.0m, 34.0m, 22.0m,
            team
        ];
    }
}
