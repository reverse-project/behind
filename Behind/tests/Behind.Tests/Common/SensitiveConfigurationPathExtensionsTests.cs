using Behind.Common.Configuration;

namespace Behind.Tests.Common;

public class SensitiveConfigurationPathExtensionsTests
{
    [Theory]
    [InlineData("Security:SecretKey")]
    [InlineData("Security:Jwt:SigningKey")]
    [InlineData("Security:Elevation:DeveloperSecretKey")]
    [InlineData("Security:MagicLink:ActualSecret")]
    [InlineData("Security:MagicLink:OldSecret")]
    [InlineData("Security:DataProtection:CertificatePassword")]
    [InlineData("Email:Authentication:Password")]
    [InlineData("Email:ApiToken")]
    [InlineData("ConnectionStrings:DefaultConnection")]
    [InlineData("ConnectionStrings:Anything:Else")]
    public void IsSensitiveConfigurationPath_MasksSecrets(string path)
    {
        Assert.True(path.IsSensitiveConfigurationPath);
    }

    [Theory]
    [InlineData("Security:Jwt:Issuer")]
    [InlineData("Security:Elevation:Enabled")]
    [InlineData("Security:DataProtection:KeysPath")]
    [InlineData("Database:Key")]
    [InlineData("Backend:DataDirectory")]
    [InlineData("Frontend:Site:Title")]
    public void IsSensitiveConfigurationPath_ShowsPlainSettings(string path)
    {
        Assert.False(path.IsSensitiveConfigurationPath);
    }

    [Fact]
    public void IsSensitiveConfigurationPath_WorksForARootLevelValue()
    {
        Assert.True("Password".IsSensitiveConfigurationPath);
        Assert.False("AllowedHosts".IsSensitiveConfigurationPath);
    }
}
