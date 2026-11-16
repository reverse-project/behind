using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations;

namespace Behind.Common;

public abstract record ContentLocaleDraft
{
    private string? contentLocale;
    [MaxLength(100)]
    public string? ContentLocale
    {
        get => contentLocale;
        init
        {
            contentLocale = value;
            ContentLocaleSpecified = true;
        }
    }

    [JsonIgnore]
    public bool ContentLocaleSpecified { get; private init; }
}
