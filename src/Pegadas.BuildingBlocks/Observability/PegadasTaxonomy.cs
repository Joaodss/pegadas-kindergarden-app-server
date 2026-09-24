using Microsoft.Extensions.Compliance.Classification;

namespace Pegadas.BuildingBlocks.Observability;

/// <summary>
/// Data classifications for log redaction. Annotate <c>[LoggerMessage]</c> parameters (or
/// properties of logged objects) that could hold personal data; the redactor erases them.
/// </summary>
public static class PegadasTaxonomy
{
    public const string TaxonomyName = "Pegadas";

    /// <summary>Names, emails, free text written about a child or staff member.</summary>
    public static DataClassification PersonalData => new(TaxonomyName, nameof(PersonalData));

    /// <summary>GDPR Art. 9 special categories: allergies, dietary restrictions, health notes.</summary>
    public static DataClassification SensitiveData => new(TaxonomyName, nameof(SensitiveData));
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false)]
public sealed class PersonalDataAttribute : DataClassificationAttribute
{
    public PersonalDataAttribute()
        : base(PegadasTaxonomy.PersonalData)
    {
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false)]
public sealed class SensitiveDataAttribute : DataClassificationAttribute
{
    public SensitiveDataAttribute()
        : base(PegadasTaxonomy.SensitiveData)
    {
    }
}
