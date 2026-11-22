using System.ComponentModel.DataAnnotations;

namespace Planning.Client.DayPlanning;

internal sealed class ActivityFormModel
{
    [VisibleText(ErrorMessage = "Vul een naam in.")] // C# staat toe dat je de naam zonder Attribute schrijft: [VisibleText] verwijst dus naar VisibleTextAttribute.
    [StringLength(250, ErrorMessage = "Gebruik maximaal 250 tekens.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Gebruik maximaal 500 tekens.")]
    public string? Description { get; set; }

    public DateOnly Date { get; set; }

    [Required(ErrorMessage = "Kies een tijdsblok.")] // bij Name zorgt visible text al voor het required stuk
    public int? TimeBlockId { get; set; }

    public bool IsImportant { get; set; }
}

internal sealed class VisibleTextAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string name && !string.IsNullOrWhiteSpace(name);
}
