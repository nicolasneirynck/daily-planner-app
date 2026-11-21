namespace Planning.Shared.DayPlanning;

public class CreatePlannedActivityRequest
{
    public required DateOnly Date { get; set; }
    public required int TimeBlockId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }

    public class Validator : AbstractValidator<CreatePlannedActivityRequest>
    {
        public Validator()
        {
            RuleFor(request => request.Name)
                .Must(name => !string.IsNullOrWhiteSpace(name))
                .WithMessage("Activity name is required.")
                .MaximumLength(250);
            RuleFor(request => request.Description)
                .MaximumLength(500)
                .WithMessage("Description must be 500 characters or fewer.");
        }
    }
}
