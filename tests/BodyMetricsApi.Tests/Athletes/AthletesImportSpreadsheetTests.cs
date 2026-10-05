using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BodyMetricsApi.Features.Athletes.Create;
using BodyMetricsApi.Features.Athletes.Import.ViewModels;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.Commands;
using BodyMetricsApi.Features.Athletes.Shared.Enums;
using BodyMetricsApi.Features.Athletes.Shared.ViewModels;
using BodyMetricsApi.Features.Sports.Create;
using BodyMetricsApi.Features.Sports.Shared.ViewModels;
using BodyMetricsApi.Shared.ViewModels;
using BodyMetricsApi.Tests.TestInfrastructure;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;

namespace BodyMetricsApi.Tests.Athletes;

[Collection(MongoCollectionDefinition.Name)]
public sealed class AthletesImportSpreadsheetTests(MongoContainerFixture mongoFixture, AzuriteContainerFixture azuriteFixture) : IClassFixture<AzuriteContainerFixture>
{
    private static readonly IReadOnlyList<string> DefaultHeaders =
    [
        "Setor",
        "Posição",
        "Fase",
        "Nome",
        "Sexo",
        "Raça",
        "Categoria",
        "Nascimento",
        "Data avaliação",
        "Peso",
        "Altura",
        "Altura sentado",
        "Tricep D.",
        "Tricep E.",
        "Sub esc",
        "Torax",
        "Sub. Axi",
        "Crist. ilíaca",
        "Sup. Espin",
        "abd",
        "Coxa D",
        "Coxa E",
        "Pantu D",
        "Pantu E",
        "C. ombro",
        "C.Peitoral",
        "C.Braço D.",
        "C.Braço E.",
        "C.Cintura",
        "C.Quadril",
        "C. Medial D",
        "C.Medial E",
        "Pantu. D.",
        "Pantu. E.",
        "D.Punho",
        "D.Joelho",
        "D.Tornozelo"
    ];

    [Fact]
    public async Task ImportSpreadsheet_ShouldRequireAuthentication()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateClient();
        using var content = CreateImportContent("Volleyball", BuildWorkbookBytes(DefaultHeaders, [CreateSpreadsheetRow("Jane Doe")]));

        var response = await client.PostAsync("/api/athletes/import", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldRejectMissingRequiredHeaders()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();

        var missingHeaderIndex = Array.IndexOf(DefaultHeaders.ToArray(), "Data avaliação");
        var headers = DefaultHeaders.Where((_, index) => index != missingHeaderIndex).ToArray();
        var rows = new[] { CreateSpreadsheetRow("Jane Doe").Where((_, index) => index != missingHeaderIndex).ToArray() };
        using var content = CreateImportContent("Volleyball", BuildWorkbookBytes(headers, rows));

        var response = await client.PostAsync("/api/athletes/import", content);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("Data avaliação", string.Join(' ', body.Errors[nameof(Features.Athletes.Import.ImportAthletesSpreadsheetCommand.File)]));
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldCreateSportAndAthletesFromWorkbook()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var workbookBytes = BuildWorkbookBytes(
            DefaultHeaders,
            [
                CreateSpreadsheetRow("Jane Doe", sector: "Adult", category: "A", assessmentDate: new DateOnly(2026, 01, 01), weightKg: 70.4m),
                CreateSpreadsheetRow("Mary Roe", sector: "Youth", category: "B", assessmentDate: new DateOnly(2026, 02, 01), weightKg: 62.3m)
            ]);
        using var content = CreateImportContent("Volleyball", workbookBytes);

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);
        var sportsResponse = await client.GetAsync("/api/sports?page=1&pageSize=10&name=Volleyball");
        var sports = await sportsResponse.Content.ReadFromJsonAsync<PagedResponseViewModel<SportResponse>>(factory.JsonSerializerOptions);
        var athletesResponse = await client.GetAsync("/api/athletes?page=1&pageSize=10");
        var athletes = await athletesResponse.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.True(summary.SportCreated);
        Assert.Equal(2, summary.CreatedAthletes);
        Assert.Equal(0, summary.UpdatedAthletes);
        Assert.Equal(2, summary.ImportedAssessments);
        Assert.NotNull(sports);
        Assert.Equal(1, sports.TotalCount);
        Assert.NotNull(athletes);
        Assert.Equal(2, athletes.TotalCount);
        Assert.Equal(["Jane Doe", "Mary Roe"], athletes.Items.Select(item => item.FullName).OrderBy(name => name).ToArray());
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldMatchHeadersIgnoringCaseSpacesAndPunctuation()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var alternativeHeaders = new[]
        {
            " setor ",
            "posicao",
            "FASE",
            " nome ",
            "sexo",
            "raca",
            "categoria",
            "nascimento",
            "data-avaliacao",
            "peso",
            "altura",
            "altura sentado",
            "triceps d",
            "triceps e",
            "sub. esc",
            "torax",
            "sub axi",
            "supra ili",
            "ABD",
            "coxa d.",
            "coxa e.",
            "pantu d.",
            "pantu e.",
            "c ombro",
            "c peitoral",
            "c braco d",
            "c braco e",
            "c cintura",
            "c quadril",
            "c medial d",
            "c medial e",
            "pantu d",
            "pantu e",
            "d punho",
            "d joelho",
            "d tornozelo"
        };
        using var content = CreateImportContent("Handball", BuildWorkbookBytes(alternativeHeaders, [CreateSpreadsheetRow("Header Match")]));

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.CreatedAthletes);
        Assert.Equal("Handball", summary.SportName);
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldUpsertExistingAthleteAndReplaceAssessmentWithSameDate()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Swimming", ["Adult"], ["A"]);
        var createdAthlete = await CreateAthleteAsync(client, factory, sport.Id, "Jane Doe");
        using var content = CreateImportContent(
            "Swimming",
            BuildWorkbookBytes(DefaultHeaders, [CreateSpreadsheetRow("Jane Doe", sector: "Adult", category: "A", assessmentDate: new DateOnly(2026, 01, 01), weightKg: 75.5m)]));

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);
        var athleteResponse = await client.GetAsync($"/api/athletes/{createdAthlete.Id}");
        var athlete = await athleteResponse.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(0, summary.CreatedAthletes);
        Assert.Equal(1, summary.UpdatedAthletes);
        Assert.Equal(1, summary.ReplacedAssessments);
        Assert.NotNull(athlete);
        Assert.Single(athlete.PhysicalAssessments);
        Assert.Equal(75.5m, athlete.PhysicalAssessments[0].GeneralMeasurements.WeightKg);
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldAddMissingSportSectorAndCategory()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Basketball", ["Adult"], ["A"]);
        using var content = CreateImportContent(
            "Basketball",
            BuildWorkbookBytes(DefaultHeaders, [CreateSpreadsheetRow("New Prospect", sector: "Youth", category: "B")]));

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);
        var sportResponse = await client.GetAsync($"/api/sports/{sport.Id}");
        var updatedSport = await sportResponse.Content.ReadFromJsonAsync<SportResponse>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.AddedSportSectors);
        Assert.Equal(1, summary.AddedSportCategories);
        Assert.NotNull(updatedSport);
        Assert.Contains("Youth", updatedSport.Sectors);
        Assert.Contains("B", updatedSport.Categories);
    }

    [Theory]
    [InlineData("Male", Sex.Male)]
    [InlineData("Female", Sex.Female)]
    [InlineData("H", Sex.Male)]
    [InlineData("F", Sex.Female)]
    [InlineData(" h ", Sex.Male)]
    [InlineData(" f ", Sex.Female)]
    public async Task ImportSpreadsheet_ShouldAcceptEnumValuesAndAliasesForSex(string sexCellValue, Sex expectedSex)
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var row = CreateSpreadsheetRow("Sex Alias Check");
        row[4] = sexCellValue;
        using var content = CreateImportContent(
            "Volleyball",
            BuildWorkbookBytes(DefaultHeaders, [row]));

        var importResponse = await client.PostAsync("/api/athletes/import", content);
        var athletesResponse = await client.GetAsync("/api/athletes?page=1&pageSize=10&fullName=Sex%20Alias%20Check");
        var athletes = await athletesResponse.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.NotNull(athletes);
        var athlete = Assert.Single(athletes.Items);
        Assert.Equal(expectedSex, athlete.Sex);
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldKeepSkinfoldAndCircumferenceCalfColumnsSeparated()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        using var content = CreateImportContent(
            "Volleyball",
            BuildWorkbookBytes(
                DefaultHeaders,
                [CreateSpreadsheetRow("Calf Check", rightCalfSkinfoldMm: 9.6m, leftCalfSkinfoldMm: 9.5m, rightCalfCircumferenceCm: 37.0m, leftCalfCircumferenceCm: 36.5m)]));

        var importResponse = await client.PostAsync("/api/athletes/import", content);
        var athletesResponse = await client.GetAsync("/api/athletes?page=1&pageSize=10&fullName=Calf%20Check");
        var athletes = await athletesResponse.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.NotNull(athletes);
        var athlete = Assert.Single(athletes.Items);
        var assessment = Assert.Single(athlete.PhysicalAssessments);
        Assert.Equal(9.6m, assessment.Skinfolds.RightCalfMm);
        Assert.Equal(9.5m, assessment.Skinfolds.LeftCalfMm);
        Assert.Equal(37.0m, assessment.Circumferences.RightCalfCm);
        Assert.Equal(36.5m, assessment.Circumferences.LeftCalfCm);
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldImportIliacCrestAndSupraspinale()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        using var content = CreateImportContent("Volleyball", BuildWorkbookBytes(DefaultHeaders, [CreateSpreadsheetRow("Iliac Check")]));

        var importResponse = await client.PostAsync("/api/athletes/import", content);
        var athletesResponse = await client.GetAsync("/api/athletes?page=1&pageSize=10&fullName=Iliac%20Check");
        var athletes = await athletesResponse.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, importResponse.StatusCode);
        Assert.NotNull(athletes);
        var athlete = Assert.Single(athletes.Items);
        var assessment = Assert.Single(athlete.PhysicalAssessments);
        Assert.Equal(12.4m, assessment.Skinfolds.IliacCrestMm);
        Assert.Equal(6.1m, assessment.Skinfolds.SupraspinaleMm);
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldCreateAthletesAcrossMultipleWriteBatches()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        const int athleteCount = 205; // exceeds the importer's internal write-batch size (200)
        var rows = Enumerable.Range(1, athleteCount)
            .Select(index => CreateSpreadsheetRow($"Batch Athlete {index:D4}"))
            .ToArray();
        using var content = CreateImportContent("Rugby", BuildWorkbookBytes(DefaultHeaders, rows));

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);
        var athletesResponse = await client.GetAsync("/api/athletes?page=1&pageSize=1&fullName=Batch%20Athlete");
        var athletes = await athletesResponse.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(athleteCount, summary.CreatedAthletes);
        Assert.NotNull(athletes);
        Assert.Equal(athleteCount, athletes.TotalCount);
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldTreatBlankCellAsEmptyWhenRowIsAlsoAStrayTableHeaderRow()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        using var content = CreateImportContent("Volleyball", BuildWorkbookWithStrayTableHeaderOnDataRow());

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);
        var athletesResponse = await client.GetAsync("/api/athletes?page=1&pageSize=10&fullName=Stray%20Table%20Row");
        var athletes = await athletesResponse.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.CreatedAthletes);
        Assert.NotNull(athletes);
        var athlete = Assert.Single(athletes.Items);
        var assessment = Assert.Single(athlete.PhysicalAssessments);
        Assert.Null(assessment.Circumferences.ShoulderCm);
    }

    [Fact]
    public async Task ImportSpreadsheet_ShouldSkipRowWithStrayContentOutsideMappedColumns()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        using var content = CreateImportContent("Volleyball", BuildWorkbookWithStrayContentOutsideMappedColumns());

        var response = await client.PostAsync("/api/athletes/import", content);
        var summary = await response.Content.ReadFromJsonAsync<AthleteSpreadsheetImportViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(summary);
        Assert.Equal(1, summary.CreatedAthletes);
    }

    // Reproduces a real-world spreadsheet defect: a genuinely blank row (no data in any column
    // this importer maps) can still carry leftover content in unrelated columns beyond the ones
    // we read (e.g. derived/computed columns from a wider export). That row must still be
    // skipped instead of being treated as an athlete record with missing required fields.
    private static byte[] BuildWorkbookWithStrayContentOutsideMappedColumns()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Athletes");

        for (var columnIndex = 0; columnIndex < DefaultHeaders.Count; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = DefaultHeaders[columnIndex];
        }

        var row = CreateSpreadsheetRow("Mapped Row");
        for (var columnIndex = 0; columnIndex < DefaultHeaders.Count; columnIndex++)
        {
            var value = row[columnIndex];
            worksheet.Cell(2, columnIndex + 1).Value = value is DateOnly dateOnly
                ? dateOnly.ToDateTime(TimeOnly.MinValue)
                : value switch
                {
                    decimal decimalValue => decimalValue,
                    _ => value!.ToString()
                };
        }

        // Row 3 is blank in every column this importer maps, but has leftover content in a
        // column beyond the last mapped header - it must still be treated as empty and skipped.
        worksheet.Cell(3, DefaultHeaders.Count + 5).Value = "leftover computed value";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Reproduces a real-world spreadsheet defect: a stray Excel Table object (e.g. from
    // copy-pasting a table-formatted range) can turn an ordinary data row into that table's
    // header row. ClosedXML then reports a blank cell on that row as "Column<N>" instead of
    // empty, because Excel Tables require non-blank header cell text.
    private static byte[] BuildWorkbookWithStrayTableHeaderOnDataRow()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Athletes");

        for (var columnIndex = 0; columnIndex < DefaultHeaders.Count; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = DefaultHeaders[columnIndex];
        }

        var row = CreateSpreadsheetRow("Stray Table Row");
        var shoulderColumnIndex = Array.IndexOf(DefaultHeaders.ToArray(), "C. ombro");
        row[shoulderColumnIndex] = null;

        for (var columnIndex = 0; columnIndex < DefaultHeaders.Count; columnIndex++)
        {
            var value = row[columnIndex];
            if (value is DateOnly dateOnly)
            {
                worksheet.Cell(2, columnIndex + 1).Value = dateOnly.ToDateTime(TimeOnly.MinValue);
                continue;
            }

            if (value is null)
            {
                continue;
            }

            worksheet.Cell(2, columnIndex + 1).Value = value switch
            {
                decimal decimalValue => decimalValue,
                _ => value.ToString()
            };
        }

        // Wraps the single data row (row 2) as its own mini Excel Table, mimicking the stray
        // tables found in real exported spreadsheets. This makes row 2 act as that table's
        // header row from ClosedXML's perspective, even though it also holds athlete data.
        var strayRange = worksheet.Range(2, 1, 2, DefaultHeaders.Count);
        strayRange.CreateTable("StrayTable");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<SportResponse> CreateSportAsync(HttpClient client, TestApplicationFactory factory, string name, string[] sectors, string[] categories)
    {
        var response = await client.PostAsJsonAsync("/api/sports", new CreateSportCommand(name, sectors, categories), factory.JsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SportResponse>(factory.JsonSerializerOptions))!;
    }

    private static async Task<AthleteViewModel> CreateAthleteAsync(HttpClient client, TestApplicationFactory factory, string sportId, string fullName)
    {
        var command = new CreateAthleteCommand(
            fullName,
            sportId,
            "Adult",
            Phase.Competitive,
            "A",
            Sex.Female,
            Ethnicity.Asian,
            new DateOnly(1999, 04, 08),
            [
                new PhysicalAssessmentCommand(
                    new DateOnly(2026, 01, 01),
                    new GeneralMeasurementsCommand(70.4m, 177.2m, 92.5m),
                    null,
                    null)
            ],
            null);

        var response = await client.PostAsJsonAsync("/api/athletes", command, factory.JsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions))!;
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

    private static byte[] BuildWorkbookBytes(IReadOnlyList<string> headers, IReadOnlyList<object?[]> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Athletes");

        for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
        {
            worksheet.Cell(1, columnIndex + 1).Value = headers[columnIndex];
        }

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
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
                    case double doubleValue:
                        worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = doubleValue;
                        break;
                    case int intValue:
                        worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = intValue;
                        break;
                    case DateTime dateTimeValue:
                        worksheet.Cell(rowIndex + 2, columnIndex + 1).Value = dateTimeValue;
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

    private static object?[] CreateSpreadsheetRow(
        string fullName,
        string sector = "Adult",
        string category = "A",
        DateOnly? birthDate = null,
        DateOnly? assessmentDate = null,
        decimal weightKg = 70.4m,
        decimal heightCm = 177.2m,
        decimal sittingHeightCm = 92.5m,
        decimal rightCalfSkinfoldMm = 9.6m,
        decimal leftCalfSkinfoldMm = 9.5m,
        decimal rightCalfCircumferenceCm = 37.0m,
        decimal leftCalfCircumferenceCm = 36.5m)
    {
        return
        [
            sector,
            "Outside Hitter",
            "Competitivo",
            fullName,
            "Feminino",
            "Asiática",
            category,
            birthDate ?? new DateOnly(1999, 04, 08),
            assessmentDate ?? new DateOnly(2026, 01, 01),
            weightKg,
            heightCm,
            sittingHeightCm,
            10.0m,
            10.5m,
            11.1m,
            9.8m,
            10.2m,
            12.4m,
            6.1m,
            13.0m,
            14.0m,
            13.7m,
            rightCalfSkinfoldMm,
            leftCalfSkinfoldMm,
            108.0m,
            94.0m,
            32.0m,
            31.5m,
            75.0m,
            96.0m,
            55.0m,
            54.5m,
            rightCalfCircumferenceCm,
            leftCalfCircumferenceCm,
            16.0m,
            34.0m,
            22.0m
        ];
    }
}



