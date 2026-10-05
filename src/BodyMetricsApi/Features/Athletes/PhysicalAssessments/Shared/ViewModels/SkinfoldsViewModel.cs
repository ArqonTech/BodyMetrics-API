namespace BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.ViewModels;

public sealed record SkinfoldsViewModel(
    decimal? RightTricepsMm,
    decimal? LeftTricepsMm,
    decimal? SubscapularMm,
    decimal? ThoraxMm,
    decimal? SubaxillaryMm,
    decimal? AbdominalMm,
    decimal? RightThighMm,
    decimal? LeftThighMm,
    decimal? RightCalfMm,
    decimal? LeftCalfMm,
    decimal? IliacCrestMm = null,
    decimal? SupraspinaleMm = null);

