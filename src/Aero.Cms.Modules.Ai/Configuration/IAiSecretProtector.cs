namespace Aero.Cms.Modules.Ai.Configuration;

public interface IAiSecretProtector
{
    string Protect(string secret);

    string Unprotect(string protectedSecret);
}
