namespace LibraSys.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "LibraSys.Api";
    public string Audience { get; set; } = "LibraSys.Client";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}
