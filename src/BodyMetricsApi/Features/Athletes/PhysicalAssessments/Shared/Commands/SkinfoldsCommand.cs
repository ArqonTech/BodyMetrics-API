namespace BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.Commands;

public sealed record SkinfoldsCommand(
    decimal? RightTricepsMm,
    decimal? LeftTricepsMm,
    decimal? SubscapularMm,
    decimal? ThoraxMm,
    decimal? SubaxillaryMm,
    decimal? SuprailiacMm,
    decimal? AbdominalMm,
    decimal? RightThighMm,
    decimal? LeftThighMm,
    decimal? RightCalfMm,
    decimal? LeftCalfMm,
    decimal? IliacCrestMm = null,
    decimal? SupraspinaleMm = null);

