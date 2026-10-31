namespace Services;

public static class ServiceErrors
{
    public static bool IsUnavailable(Exception error) => Repositories.Data.PersistenceErrors.IsUnavailable(error);
}
