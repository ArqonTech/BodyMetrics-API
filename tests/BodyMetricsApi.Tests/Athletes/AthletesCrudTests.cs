using System.Net;
using System.Net.Http.Json;
using BodyMetricsApi.Features.Athletes.Create;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.Commands;
using BodyMetricsApi.Features.Athletes.Shared.Commands;
using BodyMetricsApi.Features.Athletes.Shared.Enums;
using BodyMetricsApi.Features.Athletes.Shared.ViewModels;
using BodyMetricsApi.Features.Athletes.Update;
using BodyMetricsApi.Features.Sports.Create;
using BodyMetricsApi.Features.Sports.Shared.ViewModels;
using BodyMetricsApi.Shared.ViewModels;
using BodyMetricsApi.Tests.TestInfrastructure;

namespace BodyMetricsApi.Tests.Athletes;

[Collection(MongoCollectionDefinition.Name)]
public sealed class AthletesCrudTests(MongoContainerFixture mongoFixture, AzuriteContainerFixture azuriteFixture) : IClassFixture<AzuriteContainerFixture>
{
    private static readonly HttpClient BlobHttpClient = new();

    [Fact]
    public async Task AthletesEndpoints_ShouldRequireAuthentication()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/athletes?page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAthlete_ShouldReturnCreatedAndGenerateBlobUrl()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Volleyball");

        var request = BuildCreateAthleteCommand(sport.Id, "Adult", "A", includePhoto: true);
        var response = await client.PostAsJsonAsync("/api/athletes", request, factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions);
        Assert.NotNull(body);
        Assert.Equal("Jane Doe", body.FullName);
        Assert.NotNull(body.ProfilePhoto);
        Assert.NotNull(body.ProfilePhoto!.AccessUrl);
        Assert.StartsWith("http://127.0.0.1:", body.ProfilePhoto.AccessUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sig=", body.ProfilePhoto.AccessUrl, StringComparison.OrdinalIgnoreCase);

        var blobResponse = await BlobHttpClient.GetAsync(body.ProfilePhoto.AccessUrl);
        Assert.Equal(HttpStatusCode.OK, blobResponse.StatusCode);
    }

    [Theory]
    [InlineData("Invalid Sector", "A")]
    [InlineData("Adult", "Invalid Category")]
    public async Task CreateAthlete_ShouldRejectInvalidSportOptions(string sector, string category)
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Handball");

        var request = BuildCreateAthleteCommand(sport.Id, sector, category);
        var response = await client.PostAsJsonAsync("/api/athletes", request, factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task CreateAthlete_ShouldRejectFutureBirthDate(int daysInFuture)
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Gymnastics");

        var request = BuildCreateAthleteCommand(sport.Id, "Adult", "A") with
        {
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysInFuture))
        };

        var response = await client.PostAsJsonAsync("/api/athletes", request, factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAthleteById_ShouldReturnOwnedAssessments()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-a");
        var sport = await CreateSportAsync(client, factory, "Swimming");
        var created = await CreateAthleteAsync(client, factory, sport.Id);

        var response = await client.GetAsync($"/api/athletes/{created.Id}");
        var body = await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.PhysicalAssessments);
        Assert.Equal(70.4m, body.PhysicalAssessments[0].GeneralMeasurements.WeightKg);
    }

    [Fact]
    public async Task GetAthleteById_ShouldReturnNotFoundForAnotherUser()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var ownerClient = factory.CreateAuthenticatedClient("owner-user");
        using var strangerClient = factory.CreateAuthenticatedClient("stranger-user");
        var sport = await CreateSportAsync(ownerClient, factory, "Cycling");
        var created = await CreateAthleteAsync(ownerClient, factory, sport.Id);

        var response = await strangerClient.GetAsync($"/api/athletes/{created.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateAthlete_ShouldReplaceAssessmentsAndAllowPhotoReplacement()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Cycling");
        var created = await CreateAthleteAsync(client, factory, sport.Id);

        var updateRequest = CreateUpdateAthleteCommand(created.Id, sport.Id, "Youth", "B", includePhoto: true) with
        {
            FullName = "Updated Athlete",
            PhysicalAssessments =
            [
                new PhysicalAssessmentCommand(
                    new DateOnly(2026, 01, 15),
                    new GeneralMeasurementsCommand(68.1m, 180.0m, 91.0m),
                    null,
                    null),
                new PhysicalAssessmentCommand(
                    new DateOnly(2026, 02, 15),
                    new GeneralMeasurementsCommand(67.5m, 180.0m, 91.0m),
                    null,
                    null)
            ]
        };

        var response = await client.PutAsJsonAsync($"/api/athletes/{created.Id}", updateRequest, factory.JsonSerializerOptions);
        var body = await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Updated Athlete", body.FullName);
        Assert.Equal("Youth", body.Sector);
        Assert.Equal("B", body.Category);
        Assert.Equal(2, body.PhysicalAssessments.Count);
        Assert.NotNull(body.ProfilePhoto);
    }

    [Fact]
    public async Task DeleteAthlete_ShouldRemoveOwnedEntity()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Boxing");
        var created = await CreateAthleteAsync(client, factory, sport.Id);

        var deleteResponse = await client.DeleteAsync($"/api/athletes/{created.Id}");
        var getResponse = await client.GetAsync($"/api/athletes/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreateAthlete_ShouldAcceptNullPhysicalAssessmentMeasurements()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Rowing");

        var request = BuildCreateAthleteCommand(sport.Id, "Adult", "A") with
        {
            PhysicalAssessments =
            [
                new PhysicalAssessmentCommand(
                    new DateOnly(2026, 01, 01),
                    new GeneralMeasurementsCommand(null, null, null),
                    new SkinfoldsCommand(null, null, null, null, null, null, null, null, null, null),
                    new CircumferencesCommand(null, null, null, null, null, null, null, null, null, null, null, null, null, null))
            ]
        };

        var response = await client.PostAsJsonAsync("/api/athletes", request, factory.JsonSerializerOptions);
        var body = await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.PhysicalAssessments);
        Assert.Null(body.PhysicalAssessments[0].GeneralMeasurements.WeightKg);
        Assert.Null(body.PhysicalAssessments[0].GeneralMeasurements.HeightCm);
        Assert.Null(body.PhysicalAssessments[0].GeneralMeasurements.SittingHeightCm);
        Assert.Null(body.PhysicalAssessments[0].Skinfolds.ThoraxMm);
        Assert.Null(body.PhysicalAssessments[0].Circumferences.HipCm);
    }

    [Fact]
    public async Task CreateAthlete_ShouldTreatZeroPhysicalAssessmentMeasurementsAsNull()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Canoeing");

        var request = BuildCreateAthleteCommand(sport.Id, "Adult", "A") with
        {
            PhysicalAssessments =
            [
                new PhysicalAssessmentCommand(
                    new DateOnly(2026, 01, 01),
                    new GeneralMeasurementsCommand(0m, 0m, 0m),
                    new SkinfoldsCommand(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
                    new CircumferencesCommand(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m))
            ]
        };

        var response = await client.PostAsJsonAsync("/api/athletes", request, factory.JsonSerializerOptions);
        var body = await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.PhysicalAssessments);
        Assert.Null(body.PhysicalAssessments[0].GeneralMeasurements.WeightKg);
        Assert.Null(body.PhysicalAssessments[0].GeneralMeasurements.HeightCm);
        Assert.Null(body.PhysicalAssessments[0].GeneralMeasurements.SittingHeightCm);
        Assert.Null(body.PhysicalAssessments[0].Skinfolds.ThoraxMm);
        Assert.Null(body.PhysicalAssessments[0].Circumferences.HipCm);
    }

    [Fact]
    public async Task GetAllAthletes_ShouldReturnOnlyOwnerItemsWithPaginationAndFilters()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var ownerClient = factory.CreateAuthenticatedClient("owner-user");
        using var otherClient = factory.CreateAuthenticatedClient("other-user");
        var sport = await CreateSportAsync(ownerClient, factory, "Track and Field");

        await CreateAthleteAsync(ownerClient, factory, sport.Id, "Zeta Runner");
        await CreateAthleteAsync(ownerClient, factory, sport.Id, "Alpha Runner");
        await CreateAthleteAsync(otherClient, factory, sport.Id, "Other Runner");

        var response = await ownerClient.GetAsync($"/api/athletes?page=1&pageSize=1&fullName=Runner&sportId={sport.Id}&sector=Adult&category=A&phase=Competitive");
        var body = await response.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.TotalCount);
        Assert.Equal(2, body.TotalPages);
        Assert.Single(body.Items);
        Assert.Equal("Alpha Runner", body.Items[0].FullName);
    }

    [Theory]
    [InlineData("An")]
    [InlineData(" an ")]
    public async Task GetAllAthletes_ShouldFilterByPartialFullNameForAutocomplete(string fullName)
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Volleyball");

        await CreateAthleteAsync(client, factory, sport.Id, "Andre Lima");
        await CreateAthleteAsync(client, factory, sport.Id, "Andress Souza");
        await CreateAthleteAsync(client, factory, sport.Id, "Antonio Silva");
        await CreateAthleteAsync(client, factory, sport.Id, "Bruno Costa");

        var response = await client.GetAsync($"/api/athletes?page=1&pageSize=10&fullName={Uri.EscapeDataString(fullName)}");
        var body = await response.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(3, body.TotalCount);
        Assert.Equal(["Andre Lima", "Andress Souza", "Antonio Silva"], body.Items.Select(item => item.FullName).ToArray());
    }

    [Fact]
    public async Task GetAllAthletes_ShouldMatchPartialFullNameAgainstAnyNameToken()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sport = await CreateSportAsync(client, factory, "Basketball");

        await CreateAthleteAsync(client, factory, sport.Id, "Maria Andrade");
        await CreateAthleteAsync(client, factory, sport.Id, "Carlos Pereira");

        var response = await client.GetAsync("/api/athletes?page=1&pageSize=10&fullName=Andr");
        var body = await response.Content.ReadFromJsonAsync<PagedResponseViewModel<AthleteViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var athlete = Assert.Single(body.Items);
        Assert.Equal("Maria Andrade", athlete.FullName);
    }

    private static async Task<SportResponse> CreateSportAsync(HttpClient client, TestApplicationFactory factory, string name)
    {
        var request = new CreateSportCommand(name, ["Adult", "Youth"], ["A", "B"]);
        var response = await client.PostAsJsonAsync("/api/sports", request, factory.JsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SportResponse>(factory.JsonSerializerOptions))!;
    }

    private static async Task<AthleteViewModel> CreateAthleteAsync(HttpClient client, TestApplicationFactory factory, string sportId, string fullName = "Jane Doe")
    {
        var request = BuildCreateAthleteCommand(sportId, "Adult", "A") with { FullName = fullName };
        var response = await client.PostAsJsonAsync("/api/athletes", request, factory.JsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions))!;
    }

    private static CreateAthleteCommand BuildCreateAthleteCommand(string sportId, string sector, string category, bool includePhoto = false)
    {
        return new CreateAthleteCommand(
            "Jane Doe",
            sportId,
            sector,
            Phase.Competitive,
            category,
            Sex.Female,
            Ethnicity.Asian,
            new DateOnly(1999, 04, 08),
            [
                new PhysicalAssessmentCommand(
                    new DateOnly(2026, 01, 01),
                    new GeneralMeasurementsCommand(70.4m, 177.2m, 92.5m),
                    new SkinfoldsCommand(10.0m, 10.5m, null, null, null, 12.1m, null, null, null, null),
                    new CircumferencesCommand(108.0m, 94.0m, null, null, 75.0m, null, 96.0m, null, null, 37.0m, 36.5m, null, null, null))
            ],
            includePhoto
                ? new ProfilePhotoUploadCommand("avatar.png", "image/png", Convert.ToBase64String([1, 2, 3, 4]))
                : null);
    }

    private static UpdateAthleteCommand CreateUpdateAthleteCommand(string id, string sportId, string sector, string category, bool includePhoto = false)
    {
        return new UpdateAthleteCommand(
            id,
            "Jane Doe",
            sportId,
            sector,
            Phase.Competitive,
            category,
            Sex.Female,
            Ethnicity.Asian,
            new DateOnly(1999, 04, 08),
            [
                new PhysicalAssessmentCommand(
                    new DateOnly(2026, 01, 01),
                    new GeneralMeasurementsCommand(70.4m, 177.2m, 92.5m),
                    new SkinfoldsCommand(10.0m, 10.5m, null, null, null, 12.1m, null, null, null, null),
                    new CircumferencesCommand(108.0m, 94.0m, null, null, 75.0m, null, 96.0m, null, null, 37.0m, 36.5m, null, null, null))
            ],
            includePhoto
                ? new ProfilePhotoUploadCommand("avatar.png", "image/png", Convert.ToBase64String([1, 2, 3, 4]))
                : null);
    }
}


