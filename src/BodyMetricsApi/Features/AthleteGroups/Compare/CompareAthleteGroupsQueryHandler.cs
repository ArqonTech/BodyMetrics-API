using BodyMetricsApi.Features.Athletes;
using BodyMetricsApi.Features.Athletes.PhysicalAssessments;
using BodyMetricsApi.Features.AthleteGroups.Shared.Interfaces;
using BodyMetricsApi.Features.AthleteGroups.Shared.ViewModels;
using BodyMetricsApi.Shared.Authentication;
using BodyMetricsApi.Shared.Results;
using BodyMetricsApi.Shared.Validation;
using FluentValidation;

namespace BodyMetricsApi.Features.AthleteGroups.Compare;

public sealed class CompareAthleteGroupsQueryHandler(
    IAthleteGroupRepository groupRepository,
    ICurrentUserService currentUserService,
    IValidator<CompareAthleteGroupsQuery> validator)
{
    public async Task<OperationResult<List<AthleteGroupComparisonViewModel>>> HandleAsync(CompareAthleteGroupsQuery query, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            return OperationResult<List<AthleteGroupComparisonViewModel>>.Validation(validationResult.ToErrorDictionary());
        }

        var results = new List<AthleteGroupComparisonViewModel>();

        foreach (var groupId in query.GroupIds)
        {
            var group = await groupRepository.GetByIdAsync(groupId, currentUserService.UserId, cancellationToken);
            if (group is null)
            {
                return OperationResult<List<AthleteGroupComparisonViewModel>>.NotFound($"Athlete group '{groupId}' not found.");
            }

            var athletes = group.Members;

            var latestAssessments = athletes
                .Select(a => a.PhysicalAssessments.OrderByDescending(pa => pa.AssessmentDate).FirstOrDefault())
                .ToList();

            var withAssessments = latestAssessments.Count(pa => pa is not null);
            var withoutAssessments = athletes.Count - withAssessments;

            var nonNull = latestAssessments.Where(pa => pa is not null).Select(pa => pa!).ToList();

            results.Add(new AthleteGroupComparisonViewModel(
                GroupId: group.Id,
                GroupName: group.Name,
                AthleteCount: athletes.Count,
                AthletesWithAssessments: withAssessments,
                AthletesWithoutAssessments: withoutAssessments,
                GeneralMeasurements: AggregateGeneralMeasurements(nonNull),
                Skinfolds: AggregateSkinfolds(nonNull),
                Circumferences: AggregateCircumferences(nonNull)));
        }

        return OperationResult<List<AthleteGroupComparisonViewModel>>.Success(results);
    }

    private static GroupGeneralMeasurementsAggregateViewModel AggregateGeneralMeasurements(List<PhysicalAssessment> assessments)
    {
        return new GroupGeneralMeasurementsAggregateViewModel(
            WeightKg: Aggregate(assessments.Select(a => (decimal?)a.GeneralMeasurements.WeightKg).ToList()),
            HeightCm: Aggregate(assessments.Select(a => (decimal?)a.GeneralMeasurements.HeightCm).ToList()),
            SittingHeightCm: Aggregate(assessments.Select(a => (decimal?)a.GeneralMeasurements.SittingHeightCm).ToList()));
    }

    private static GroupSkinfoldsAggregateViewModel AggregateSkinfolds(List<PhysicalAssessment> assessments)
    {
        return new GroupSkinfoldsAggregateViewModel(
            RightTricepsMm: Aggregate(assessments.Select(a => a.Skinfolds.RightTricepsMm).ToList()),
            LeftTricepsMm: Aggregate(assessments.Select(a => a.Skinfolds.LeftTricepsMm).ToList()),
            SubscapularMm: Aggregate(assessments.Select(a => a.Skinfolds.SubscapularMm).ToList()),
            ThoraxMm: Aggregate(assessments.Select(a => a.Skinfolds.ThoraxMm).ToList()),
            SubaxillaryMm: Aggregate(assessments.Select(a => a.Skinfolds.SubaxillaryMm).ToList()),
            SuprailiacMm: Aggregate(assessments.Select(a => a.Skinfolds.SuprailiacMm).ToList()),
            AbdominalMm: Aggregate(assessments.Select(a => a.Skinfolds.AbdominalMm).ToList()),
            RightThighMm: Aggregate(assessments.Select(a => a.Skinfolds.RightThighMm).ToList()),
            LeftThighMm: Aggregate(assessments.Select(a => a.Skinfolds.LeftThighMm).ToList()),
            RightCalfMm: Aggregate(assessments.Select(a => a.Skinfolds.RightCalfMm).ToList()),
            LeftCalfMm: Aggregate(assessments.Select(a => a.Skinfolds.LeftCalfMm).ToList()),
            IliacCrestMm: Aggregate(assessments.Select(a => a.Skinfolds.IliacCrestMm).ToList()),
            SupraspinaleMm: Aggregate(assessments.Select(a => a.Skinfolds.SupraspinaleMm).ToList()));
    }

    private static GroupCircumferencesAggregateViewModel AggregateCircumferences(List<PhysicalAssessment> assessments)
    {
        return new GroupCircumferencesAggregateViewModel(
            ShoulderCm: Aggregate(assessments.Select(a => a.Circumferences.ShoulderCm).ToList()),
            ChestCm: Aggregate(assessments.Select(a => a.Circumferences.ChestCm).ToList()),
            RightArmCm: Aggregate(assessments.Select(a => a.Circumferences.RightArmCm).ToList()),
            LeftArmCm: Aggregate(assessments.Select(a => a.Circumferences.LeftArmCm).ToList()),
            WaistCm: Aggregate(assessments.Select(a => a.Circumferences.WaistCm).ToList()),
            AbdominalCm: Aggregate(assessments.Select(a => a.Circumferences.AbdominalCm).ToList()),
            HipCm: Aggregate(assessments.Select(a => a.Circumferences.HipCm).ToList()),
            RightMidThighCm: Aggregate(assessments.Select(a => a.Circumferences.RightMidThighCm).ToList()),
            LeftMidThighCm: Aggregate(assessments.Select(a => a.Circumferences.LeftMidThighCm).ToList()),
            RightCalfCm: Aggregate(assessments.Select(a => a.Circumferences.RightCalfCm).ToList()),
            LeftCalfCm: Aggregate(assessments.Select(a => a.Circumferences.LeftCalfCm).ToList()),
            RightWristCm: Aggregate(assessments.Select(a => a.Circumferences.RightWristCm).ToList()),
            RightKneeCm: Aggregate(assessments.Select(a => a.Circumferences.RightKneeCm).ToList()),
            RightAnkleCm: Aggregate(assessments.Select(a => a.Circumferences.RightAnkleCm).ToList()));
    }

    private static MetricAggregateViewModel Aggregate(List<decimal?> values)
    {
        var nonNull = values.Where(v => v.HasValue).Select(v => v!.Value).OrderBy(v => v).ToList();

        if (nonNull.Count == 0)
        {
            return new MetricAggregateViewModel(null, null, null, null);
        }

        var avg = nonNull.Average();
        var min = nonNull.Min();
        var max = nonNull.Max();
        var median = CalculateMedian(nonNull);

        return new MetricAggregateViewModel(
            Average: Math.Round(avg, 2),
            Min: min,
            Max: max,
            Median: median);
    }

    private static decimal CalculateMedian(List<decimal> sorted)
    {
        var count = sorted.Count;
        if (count % 2 == 1)
        {
            return sorted[count / 2];
        }

        return (sorted[count / 2 - 1] + sorted[count / 2]) / 2m;
    }
}
