namespace SQLScripter.Security;

public enum AuthenticationType
{
    Sql = 0,
    Windows = 1
}

public class Credential
{
    public string Server { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public AuthenticationType AuthType { get; set; } = AuthenticationType.Sql;
}
