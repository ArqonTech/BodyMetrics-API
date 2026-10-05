using MongoDB.Bson.Serialization.Attributes;

namespace BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.ValueObjects;

// Ignores stored elements that no longer exist (e.g. the removed legacy suprailiac skinfold),
// so documents written before the removal still deserialize through the raw MongoDB driver.
[BsonIgnoreExtraElements]
public sealed class SkinfoldsValueObject
{
    public decimal? RightTricepsMm { get; private set; }
    public decimal? LeftTricepsMm { get; private set; }
    public decimal? SubscapularMm { get; private set; }
    public decimal? ThoraxMm { get; private set; }
    public decimal? SubaxillaryMm { get; private set; }
    public decimal? AbdominalMm { get; private set; }
    public decimal? RightThighMm { get; private set; }
    public decimal? LeftThighMm { get; private set; }
    public decimal? RightCalfMm { get; private set; }
    public decimal? LeftCalfMm { get; private set; }
    public decimal? IliacCrestMm { get; private set; }
    public decimal? SupraspinaleMm { get; private set; }

    public SkinfoldsValueObject()
    {
    }

    public SkinfoldsValueObject(
        decimal? rightTricepsMm,
        decimal? leftTricepsMm,
        decimal? subscapularMm,
        decimal? thoraxMm,
        decimal? subaxillaryMm,
        decimal? abdominalMm,
        decimal? rightThighMm,
        decimal? leftThighMm,
        decimal? rightCalfMm,
        decimal? leftCalfMm,
        decimal? iliacCrestMm = null,
        decimal? supraspinaleMm = null)
    {
        RightTricepsMm = EnsurePositiveIfPresent(rightTricepsMm, nameof(RightTricepsMm));
        LeftTricepsMm = EnsurePositiveIfPresent(leftTricepsMm, nameof(LeftTricepsMm));
        SubscapularMm = EnsurePositiveIfPresent(subscapularMm, nameof(SubscapularMm));
        ThoraxMm = EnsurePositiveIfPresent(thoraxMm, nameof(ThoraxMm));
        SubaxillaryMm = EnsurePositiveIfPresent(subaxillaryMm, nameof(SubaxillaryMm));
        AbdominalMm = EnsurePositiveIfPresent(abdominalMm, nameof(AbdominalMm));
        RightThighMm = EnsurePositiveIfPresent(rightThighMm, nameof(RightThighMm));
        LeftThighMm = EnsurePositiveIfPresent(leftThighMm, nameof(LeftThighMm));
        RightCalfMm = EnsurePositiveIfPresent(rightCalfMm, nameof(RightCalfMm));
        LeftCalfMm = EnsurePositiveIfPresent(leftCalfMm, nameof(LeftCalfMm));
        IliacCrestMm = EnsurePositiveIfPresent(iliacCrestMm, nameof(IliacCrestMm));
        SupraspinaleMm = EnsurePositiveIfPresent(supraspinaleMm, nameof(SupraspinaleMm));
    }

    private static decimal? EnsurePositiveIfPresent(decimal? value, string propertyName)
    {
        if (value is null || value == 0)
        {
            return null;
        }

        if (value < 0)
        {
            throw new ArgumentException($"{propertyName} must be greater than or equal to zero when provided.", propertyName);
        }

        return value;
    }
}

