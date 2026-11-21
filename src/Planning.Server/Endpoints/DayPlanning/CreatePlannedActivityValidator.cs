using FluentValidation;
using Planning.Domain.PlannedActivities;
using Planning.Shared.DayPlanning;

namespace Planning.Server.Endpoints.DayPlanning;

public sealed class CreatePlannedActivityValidator
    : AbstractValidator<CreatePlannedActivityRequest>
{
    public CreatePlannedActivityValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage(PlannedActivityRules.NameRequiredMessage)
            .MaximumLength(PlannedActivityRules.MaxNameLength)
            .WithMessage(PlannedActivityRules.NameTooLongMessage);

        RuleFor(request => request.Description)
            .MaximumLength(PlannedActivityRules.MaxDescriptionLength)
            .WithMessage(PlannedActivityRules.DescriptionTooLongMessage);
    }
}
