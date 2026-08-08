namespace Scorecard.Api.Domain;

/// <summary>
/// Editorial reference text, stored as a single jsonb column via an EF owned type.
///
/// Deliberately NOT used for runtime explanations: those live in flat
/// ExplanationMn / ExplanationEn columns on <see cref="AssetFactorScore"/>.
/// Reference text is small, hand-authored and may gain locales; explanations are
/// engine-generated, high-volume and never expand beyond the supported set.
/// </summary>
public sealed class LocalizedText
{
    public string Mn { get; set; } = string.Empty;
    public string En { get; set; } = string.Empty;

    public LocalizedText() { }

    public LocalizedText(string mn, string en)
    {
        Mn = mn;
        En = en;
    }

    public string For(string locale) =>
        locale.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? En : Mn;

    public static LocalizedText Of(string mn, string en) => new(mn, en);
}
