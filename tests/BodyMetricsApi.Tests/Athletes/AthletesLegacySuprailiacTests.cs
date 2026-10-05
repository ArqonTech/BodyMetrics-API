using System.Net;
using System.Net.Http.Json;
using BodyMetricsApi.Features.Athletes.Create;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.Commands;
using BodyMetricsApi.Features.Athletes.Shared.Enums;
using BodyMetricsApi.Features.Athletes.Shared.ViewModels;
using BodyMetricsApi.Features.Sports.Create;
using BodyMetricsApi.Features.Sports.Shared.ViewModels;
using BodyMetricsApi.Infrastructure.Persistence;
using BodyMetricsApi.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace BodyMetricsApi.Tests.Athletes;

[Collection(MongoCollectionDefinition.Name)]
public sealed class AthletesLegacySuprailiacTests(MongoContainerFixture mongoFixture, AzuriteContainerFixture azuriteFixture) : IClassFixture<AzuriteContainerFixture>
{
    [Fact]
    public async Task GetAthlete_ShouldExposeLegacySuprailiacValueAsIliacCrest()
    {
        await using var factory = new TestApplicationFactory(mongoFixture, azuriteFixture);
        using var client = factory.CreateAuthenticatedClient();
        var sportResponse = await client.PostAsJsonAsync(
            "/api/sports", new CreateSportCommand("Legacy Sport", ["Adult"], ["A"]), factory.JsonSerializerOptions);
        var sport = await sportResponse.Content.ReadFromJsonAsync<SportResponse>(factory.JsonSerializerOptions);
        Assert.NotNull(sport);

        var createResponse = await client.PostAsJsonAsync(
            "/api/athletes",
            new CreateAthleteCommand(
                "Legacy Athlete", sport.Id, "Adult", Phase.Competitive, "A", Sex.Female, Ethnicity.Asian,
                new DateOnly(1999, 04, 08),
                [
                    new PhysicalAssessmentCommand(
                        new DateOnly(2026, 01, 01),
                        new GeneralMeasurementsCommand(70.4m, 177.2m, 92.5m),
                        new SkinfoldsCommand(10.0m, null, null, null, null, 12.1m, null, null, null, null),
                        new CircumferencesCommand(null, null, null, null, null, null, null, null, null, null, null, null, null, null))
                ],
                null),
            factory.JsonSerializerOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);

        // Simulate a document written before "Supra-ilíaca" was renamed to "Crista ilíaca".
        var athletes = factory.Services.GetRequiredService<MongoDbContext>().Database.GetCollection<BsonDocument>("Athletes");
        var update = Builders<BsonDocument>.Update.Set("PhysicalAssessments.0.Skinfolds.SuprailiacMm", BsonValue.Create(11.5m));
        var updateResult = await athletes.UpdateOneAsync(Builders<BsonDocument>.Filter.Empty, update);
        Assert.Equal(1, updateResult.ModifiedCount);

        var response = await client.GetAsync($"/api/athletes/{created.Id}");
        var athlete = await response.Content.ReadFromJsonAsync<AthleteViewModel>(factory.JsonSerializerOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(athlete);
        Assert.Equal(11.5m, Assert.Single(athlete.PhysicalAssessments).Skinfolds.IliacCrestMm);
    }
}
