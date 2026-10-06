using System.Net;
using System.Net.Http.Json;
using BodyMetricsApi.Features.Athletes.Create;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.Commands;
using BodyMetricsApi.Features.Athletes.Shared.Commands;
using BodyMetricsApi.Features.Athletes.Shared.Enums;
using BodyMetricsApi.Features.Athletes.Shared.ViewModels;
using BodyMetricsApi.Features.AthleteGroups.Create;
using BodyMetricsApi.Features.AthleteGroups.Shared.ViewModels;
using BodyMetricsApi.Features.Sports.Create;
using BodyMetricsApi.Features.Sports.Shared.ViewModels;
using BodyMetricsApi.Tests.TestInfrastructure;

namespace BodyMetricsApi.Tests.AthleteGroups;

[Collection(MongoCollectionDefinition.Name)]
public sealed class CompareGroupsTests(MongoContainerFixture mongoFixture, AzuriteContainerFixture azuriteFixture)
    : IClassFixture<AzuriteContainerFixture>
{
    [Fact]
    public async Task Compare_TwoGroups_ShouldReturnAggregatesForEach()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-cmp-two");

        var sport = await CreateSportAsync(client, factory, "Soccer-CmpTwo");
        var a1 = await CreateAthleteAsync(client, factory, sport.Id, "Cmp A1", weightKg: 70m);
        var a2 = await CreateAthleteAsync(client, factory, sport.Id, "Cmp A2", weightKg: 80m);
        var a3 = await CreateAthleteAsync(client, factory, sport.Id, "Cmp A3", weightKg: 90m);

        var g1 = await CreateGroupAsync(client, factory, "Group CmpOne");
        var g2 = await CreateGroupAsync(client, factory, "Group CmpTwo");

        await client.PostAsync($"/api/athlete-groups/{g1.Id}/members/{a1.Id}", null);
        await client.PostAsync($"/api/athlete-groups/{g1.Id}/members/{a2.Id}", null);
        await client.PostAsync($"/api/athlete-groups/{g2.Id}/members/{a3.Id}", null);

        var response = await client.GetAsync($"/api/athlete-groups/comparison?groupIds={g1.Id}&groupIds={g2.Id}");
        var body = await response.Content.ReadFromJsonAsync<List<AthleteGroupComparisonViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);

        var result1 = body.First(r => r.GroupId == g1.Id);
        var result2 = body.First(r => r.GroupId == g2.Id);

        Assert.Equal(2, result1.AthleteCount);
        Assert.Equal(2, result1.AthletesWithAssessments);
        Assert.Equal(75m, result1.GeneralMeasurements.WeightKg.Average);

        Assert.Equal(1, result2.AthleteCount);
        Assert.Equal(90m, result2.GeneralMeasurements.WeightKg.Average);
    }

    [Fact]
    public async Task Compare_ThreeGroups_ShouldReturnThreeEntries()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-cmp-three");

        var sport = await CreateSportAsync(client, factory, "Soccer-CmpThree");
        var a1 = await CreateAthleteAsync(client, factory, sport.Id, "CmpThree A1");
        var a2 = await CreateAthleteAsync(client, factory, sport.Id, "CmpThree A2");
        var a3 = await CreateAthleteAsync(client, factory, sport.Id, "CmpThree A3");

        var g1 = await CreateGroupAsync(client, factory, "G1 Three");
        var g2 = await CreateGroupAsync(client, factory, "G2 Three");
        var g3 = await CreateGroupAsync(client, factory, "G3 Three");

        await client.PostAsync($"/api/athlete-groups/{g1.Id}/members/{a1.Id}", null);
        await client.PostAsync($"/api/athlete-groups/{g2.Id}/members/{a2.Id}", null);
        await client.PostAsync($"/api/athlete-groups/{g3.Id}/members/{a3.Id}", null);

        var response = await client.GetAsync($"/api/athlete-groups/comparison?groupIds={g1.Id}&groupIds={g2.Id}&groupIds={g3.Id}");
        var body = await response.Content.ReadFromJsonAsync<List<AthleteGroupComparisonViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(3, body.Count);
    }

    [Fact]
    public async Task Compare_FewerThanTwoGroups_ShouldReturnBadRequest()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-cmp-onego");

        var group = await CreateGroupAsync(client, factory, "Group OneOnly");

        var response = await client.GetAsync($"/api/athlete-groups/comparison?groupIds={group.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Compare_NoGroupIds_ShouldReturnBadRequest()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-cmp-empty");

        var response = await client.GetAsync("/api/athlete-groups/comparison");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Compare_EmptyGroup_ShouldReturnZeroAthletesAndNullMetrics()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-cmp-empty-grp");

        var sport = await CreateSportAsync(client, factory, "Soccer-CmpEmptyGrp");
        var athlete = await CreateAthleteAsync(client, factory, sport.Id, "CmpEmptyGrp Athlete");

        var g1 = await CreateGroupAsync(client, factory, "Empty G1 Cmp");
        var g2 = await CreateGroupAsync(client, factory, "With Members G2 Cmp");
        await client.PostAsync($"/api/athlete-groups/{g2.Id}/members/{athlete.Id}", null);

        var response = await client.GetAsync($"/api/athlete-groups/comparison?groupIds={g1.Id}&groupIds={g2.Id}");
        var body = await response.Content.ReadFromJsonAsync<List<AthleteGroupComparisonViewModel>>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);

        var emptyResult = body.First(r => r.GroupId == g1.Id);
        Assert.Equal(0, emptyResult.AthleteCount);
        Assert.Equal(0, emptyResult.AthletesWithAssessments);
        Assert.Null(emptyResult.GeneralMeasurements.WeightKg.Average);
    }

    [Fact]
    public async Task Compare_AthletesWithNoAssessments_ShouldReturnNullMetrics()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-cmp-no-assess");

        var sport = await CreateSportAsync(client, factory, "Soccer-CmpNoAssess");

        var athleteWithAssessment = await CreateAthleteAsync(client, factory, sport.Id, "With Assessment", weightKg: 70m);

        var g1 = await CreateGroupAsync(client, factory, "HasAssessments Grp");
        var g2 = await CreateGroupAsync(client, factory, "NoAssessments Grp");
        await client.PostAsync($"/api/athlete-groups/{g1.Id}/members/{athleteWithAssessment.Id}", null);

        var response = await client.GetAsync($"/api/athlete-groups/comparison?groupIds={g1.Id}&groupIds={g2.Id}");
        var body = await response.Content.ReadFromJsonAsync<List<AthleteGroupComparisonViewModel>>(factory.JsonSerializerOptions);

        Assert.NotNull(body);
        var g2Result = body.First(r => r.GroupId == g2.Id);
        Assert.Equal(0, g2Result.AthletesWithAssessments);
        Assert.Null(g2Result.GeneralMeasurements.WeightKg.Average);
    }

    [Fact]
    public async Task Compare_NullableSkinfolds_ShouldAggregateOnlyNonNullValues()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient("user-cmp-nullable");

        var sport = await CreateSportAsync(client, factory, "Soccer-CmpNullable");
        var a1 = await CreateAthleteAsync(client, factory, sport.Id, "Nullable A1", abdominalMm: 15m);
        var a2 = await CreateAthleteAsync(client, factory, sport.Id, "Nullable A2", abdominalMm: null);

        var g1 = await CreateGroupAsync(client, factory, "Nullable Group Cmp");
        var g2 = await CreateGroupAsync(client, factory, "Nullable Group Cmp2");
        await client.PostAsync($"/api/athlete-groups/{g1.Id}/members/{a1.Id}", null);
        await client.PostAsync($"/api/athlete-groups/{g1.Id}/members/{a2.Id}", null);
        await client.PostAsync($"/api/athlete-groups/{g2.Id}/members/{a2.Id}", null);

        var response = await client.GetAsync($"/api/athlete-groups/comparison?groupIds={g1.Id}&groupIds={g2.Id}");
        var body = await response.Content.ReadFromJsonAsync<List<AthleteGroupComparisonViewModel>>(factory.JsonSerializerOptions);

        Assert.NotNull(body);
        var r1 = body.First(r => r.GroupId == g1.Id);
        Assert.Equal(15m, r1.Skinfolds.AbdominalMm.Average);

        var r2 = body.First(r => r.GroupId == g2.Id);
        Assert.Null(r2.Skinfolds.AbdominalMm.Average);
    }

    [Fact]
    public async Task Compare_CrossOwnerGroup_ShouldReturnNotFound()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var ownerClient = factory.CreateAuthenticatedClient("owner-cmp-cross");
        using var strangerClient = factory.CreateAuthenticatedClient("stranger-cmp-cross");

        var myGroup = await CreateGroupAsync(ownerClient, factory, "My Cmp Group");
        var theirGroup = await CreateGroupAsync(strangerClient, factory, "Their Cmp Group");

        var response = await ownerClient.GetAsync($"/api/athlete-groups/comparison?groupIds={myGroup.Id}&groupIds={theirGroup.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<AthleteGroupViewModel> CreateGroupAsync(HttpClient client, TestApplicationFactory factory, string name)
    {
        var response = await client.PostAsJsonAsync("/api/athlete-groups",
            new CreateAthleteGroupCommand(name), factory.JsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AthleteGroupViewModel>(factory.JsonSerializerOptions))!;
    }

    private static async Task<SportResponse> CreateSportAsync(HttpClient client, TestApplicationFactory factory, string name)
    {
        var response = await client.PostAsJsonAsync("/api/sports",
            new CreateSportCommand(name, ["Adult"], ["A"]), factory.JsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SportResponse>(factory.JsonSerializerOptions))!;
    }

    private static async Task<AthleteViewModel> CreateAthleteAsync(
        HttpClient client, TestApplicationFactory factory, string sportId, string fullName,
        decimal weightKg = 75m, decimal? abdominalMm = null)
    {
        var skinfolds = abdominalMm.HasValue
            ? new SkinfoldsCommand(null, null, null, null, null, abdominalMm, null, null, null, null)
            : null;

        var cmd = new CreateAthleteCommand(
            fullName, sportId, "Adult", Phase.Competitive, "A", Sex.Male, Ethnicity.White,
            new DateOnly(1995, 1, 1),
            [new PhysicalAssessmentCommand(new DateOnly(2026, 1, 1),
                new GeneralMeasurementsCommand(weightKg, 180m, 92m), skinfolds, null)], null);
        var response = await client.PostAsJsonAsync("/api/athletes", cmd, factory.JsonSerializerOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions))!;
    }
}
