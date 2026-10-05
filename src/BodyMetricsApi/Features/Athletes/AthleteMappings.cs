using BodyMetricsApi.Features.Athletes.PhysicalAssessments;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.Commands;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.ValueObjects;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.ViewModels;
using BodyMetricsApi.Features.Athletes.Shared.ViewModels;
using BodyMetricsApi.Infrastructure.Storage;

namespace BodyMetricsApi.Features.Athletes;

public static class AthleteMappings
{
    public static List<PhysicalAssessment> ToDomain(this IReadOnlyList<PhysicalAssessmentCommand> requests)
    {
        return requests.Select(request => new PhysicalAssessment(
                request.AssessmentDate,
                request.GeneralMeasurements is null
                    ? new GeneralMeasurementsValueObject()
                    : new GeneralMeasurementsValueObject(
                        request.GeneralMeasurements.WeightKg,
                        request.GeneralMeasurements.HeightCm,
                        request.GeneralMeasurements.SittingHeightCm),
                request.Skinfolds is null
                    ? new SkinfoldsValueObject()
                    : new SkinfoldsValueObject(
                        request.Skinfolds.RightTricepsMm,
                        request.Skinfolds.LeftTricepsMm,
                        request.Skinfolds.SubscapularMm,
                        request.Skinfolds.ThoraxMm,
                        request.Skinfolds.SubaxillaryMm,
                        request.Skinfolds.AbdominalMm,
                        request.Skinfolds.RightThighMm,
                        request.Skinfolds.LeftThighMm,
                        request.Skinfolds.RightCalfMm,
                        request.Skinfolds.LeftCalfMm,
                        request.Skinfolds.IliacCrestMm,
                        request.Skinfolds.SupraspinaleMm),
                request.Circumferences is null
                    ? new CircumferencesValueObject()
                    : new CircumferencesValueObject(
                        request.Circumferences.ShoulderCm,
                        request.Circumferences.ChestCm,
                        request.Circumferences.RightArmCm,
                        request.Circumferences.LeftArmCm,
                        request.Circumferences.WaistCm,
                        request.Circumferences.AbdominalCm,
                        request.Circumferences.HipCm,
                        request.Circumferences.RightMidThighCm,
                        request.Circumferences.LeftMidThighCm,
                        request.Circumferences.RightCalfCm,
                        request.Circumferences.LeftCalfCm,
                        request.Circumferences.RightWristCm,
                        request.Circumferences.RightKneeCm,
                        request.Circumferences.RightAnkleCm,
                        request.Circumferences.EnvergaduraCm)))
            .ToList();
    }

    public static async Task<AthleteViewModel> ToViewModelAsync(this Athlete athlete, IAthletePhotoStorage photoStorage, CancellationToken cancellationToken)
    {
        var photoViewModel = athlete.ProfilePhoto is null
            ? null
            : new ProfilePhotoViewModel(
                athlete.ProfilePhoto.BlobPath,
                athlete.ProfilePhoto.FileName,
                athlete.ProfilePhoto.ContentType,
                athlete.ProfilePhoto.UploadedAtUtc,
                (await photoStorage.GetReadUrlAsync(athlete.ProfilePhoto.BlobPath, cancellationToken))?.ToString());

        return new AthleteViewModel(
            athlete.Id,
            athlete.FullName,
            athlete.SportId,
            athlete.SportName,
            athlete.Sector,
            athlete.Phase,
            athlete.Category,
            athlete.Sex,
            athlete.Ethnicity,
            athlete.BirthDate,
            photoViewModel,
            athlete.PhysicalAssessments.Select(assessment => new PhysicalAssessmentViewModel(
                    assessment.AssessmentDate,
                    new GeneralMeasurementsViewModel(
                        assessment.GeneralMeasurements.WeightKg,
                        assessment.GeneralMeasurements.HeightCm,
                        assessment.GeneralMeasurements.SittingHeightCm),
                    new SkinfoldsViewModel(
                        assessment.Skinfolds.RightTricepsMm,
                        assessment.Skinfolds.LeftTricepsMm,
                        assessment.Skinfolds.SubscapularMm,
                        assessment.Skinfolds.ThoraxMm,
                        assessment.Skinfolds.SubaxillaryMm,
                        assessment.Skinfolds.AbdominalMm,
                        assessment.Skinfolds.RightThighMm,
                        assessment.Skinfolds.LeftThighMm,
                        assessment.Skinfolds.RightCalfMm,
                        assessment.Skinfolds.LeftCalfMm,
                        assessment.Skinfolds.EffectiveIliacCrestMm,
                        assessment.Skinfolds.SupraspinaleMm),
                    new CircumferencesViewModel(
                        assessment.Circumferences.ShoulderCm,
                        assessment.Circumferences.ChestCm,
                        assessment.Circumferences.RightArmCm,
                        assessment.Circumferences.LeftArmCm,
                        assessment.Circumferences.WaistCm,
                        assessment.Circumferences.AbdominalCm,
                        assessment.Circumferences.HipCm,
                        assessment.Circumferences.RightMidThighCm,
                        assessment.Circumferences.LeftMidThighCm,
                        assessment.Circumferences.RightCalfCm,
                        assessment.Circumferences.LeftCalfCm,
                        assessment.Circumferences.RightWristCm,
                        assessment.Circumferences.RightKneeCm,
                        assessment.Circumferences.RightAnkleCm,
                        assessment.Circumferences.EnvergaduraCm)))
                .ToList());
    }
}
