namespace BodyMetricsApi.Features.AthleteGroups.Shared.ViewModels;

public sealed record AthleteGroupComparisonViewModel(
    string GroupId,
    string GroupName,
    int AthleteCount,
    int AthletesWithAssessments,
    int AthletesWithoutAssessments,
    GroupGeneralMeasurementsAggregateViewModel GeneralMeasurements,
    GroupSkinfoldsAggregateViewModel Skinfolds,
    GroupCircumferencesAggregateViewModel Circumferences);

public sealed record MetricAggregateViewModel(
    decimal? Average,
    decimal? Min,
    decimal? Max,
    decimal? Median);

public sealed record GroupGeneralMeasurementsAggregateViewModel(
    MetricAggregateViewModel WeightKg,
    MetricAggregateViewModel HeightCm,
    MetricAggregateViewModel SittingHeightCm);

public sealed record GroupSkinfoldsAggregateViewModel(
    MetricAggregateViewModel RightTricepsMm,
    MetricAggregateViewModel LeftTricepsMm,
    MetricAggregateViewModel SubscapularMm,
    MetricAggregateViewModel ThoraxMm,
    MetricAggregateViewModel SubaxillaryMm,
    MetricAggregateViewModel AbdominalMm,
    MetricAggregateViewModel RightThighMm,
    MetricAggregateViewModel LeftThighMm,
    MetricAggregateViewModel RightCalfMm,
    MetricAggregateViewModel LeftCalfMm,
    MetricAggregateViewModel IliacCrestMm,
    MetricAggregateViewModel SupraspinaleMm);

public sealed record GroupCircumferencesAggregateViewModel(
    MetricAggregateViewModel ShoulderCm,
    MetricAggregateViewModel ChestCm,
    MetricAggregateViewModel RightArmCm,
    MetricAggregateViewModel LeftArmCm,
    MetricAggregateViewModel WaistCm,
    MetricAggregateViewModel AbdominalCm,
    MetricAggregateViewModel HipCm,
    MetricAggregateViewModel RightMidThighCm,
    MetricAggregateViewModel LeftMidThighCm,
    MetricAggregateViewModel RightCalfCm,
    MetricAggregateViewModel LeftCalfCm,
    MetricAggregateViewModel RightWristCm,
    MetricAggregateViewModel RightKneeCm,
    MetricAggregateViewModel RightAnkleCm);
