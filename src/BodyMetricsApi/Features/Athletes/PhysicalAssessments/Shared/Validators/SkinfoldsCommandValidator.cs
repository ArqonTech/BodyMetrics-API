using BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.Commands;
using FluentValidation;

namespace BodyMetricsApi.Features.Athletes.PhysicalAssessments.Shared.Validators;

public sealed class SkinfoldsCommandValidator : AbstractValidator<SkinfoldsCommand>
{
    public SkinfoldsCommandValidator()
    {
        RuleFor(skinfolds => skinfolds.RightTricepsMm).GreaterThan(0).When(value => value.RightTricepsMm.HasValue && value.RightTricepsMm.Value != 0);
        RuleFor(skinfolds => skinfolds.LeftTricepsMm).GreaterThan(0).When(value => value.LeftTricepsMm.HasValue && value.LeftTricepsMm.Value != 0);
        RuleFor(skinfolds => skinfolds.SubscapularMm).GreaterThan(0).When(value => value.SubscapularMm.HasValue && value.SubscapularMm.Value != 0);
        RuleFor(skinfolds => skinfolds.ThoraxMm).GreaterThan(0).When(value => value.ThoraxMm.HasValue && value.ThoraxMm.Value != 0);
        RuleFor(skinfolds => skinfolds.SubaxillaryMm).GreaterThan(0).When(value => value.SubaxillaryMm.HasValue && value.SubaxillaryMm.Value != 0);
        RuleFor(skinfolds => skinfolds.SuprailiacMm).GreaterThan(0).When(value => value.SuprailiacMm.HasValue && value.SuprailiacMm.Value != 0);
        RuleFor(skinfolds => skinfolds.AbdominalMm).GreaterThan(0).When(value => value.AbdominalMm.HasValue && value.AbdominalMm.Value != 0);
        RuleFor(skinfolds => skinfolds.RightThighMm).GreaterThan(0).When(value => value.RightThighMm.HasValue && value.RightThighMm.Value != 0);
        RuleFor(skinfolds => skinfolds.LeftThighMm).GreaterThan(0).When(value => value.LeftThighMm.HasValue && value.LeftThighMm.Value != 0);
        RuleFor(skinfolds => skinfolds.RightCalfMm).GreaterThan(0).When(value => value.RightCalfMm.HasValue && value.RightCalfMm.Value != 0);
        RuleFor(skinfolds => skinfolds.LeftCalfMm).GreaterThan(0).When(value => value.LeftCalfMm.HasValue && value.LeftCalfMm.Value != 0);
        RuleFor(skinfolds => skinfolds.IliacCrestMm).GreaterThan(0).When(value => value.IliacCrestMm.HasValue && value.IliacCrestMm.Value != 0);
        RuleFor(skinfolds => skinfolds.SupraspinaleMm).GreaterThan(0).When(value => value.SupraspinaleMm.HasValue && value.SupraspinaleMm.Value != 0);
    }
}

