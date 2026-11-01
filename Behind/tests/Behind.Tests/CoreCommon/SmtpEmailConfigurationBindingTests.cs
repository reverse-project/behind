using Behind.Common.Configuration;
using Behind.Core.Common.Configuration;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.CoreCommon;

public class SmtpEmailConfigurationBindingTests
{
    private static SmtpEmailConfiguration Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build()
            .GetSection(EmailConfiguration.SectionName)
            .GetOptionAware<SmtpEmailConfiguration>()!;

    private static Dictionary<string, string?> Smtp() =>
        new()
        {
            ["Email:Provider"] = "Smtp",
            ["Email:SmtpServer"] = "smtp.example.com",
            ["Email:Port"] = "587"
        };

    [Fact]
    public void Bind_WithFrom_ExposesTheSenderAddress()
    {
        var values = Smtp();
        values["Email:From"] = "noreply@example.com";

        Assert.Equal(Some("noreply@example.com"), Bind(values).From);
    }

    [Fact]
    public void Bind_WithoutFrom_LeavesTheSenderAddressNone()
    {
        Assert.Equal(None, Bind(Smtp()).From);
    }
}
