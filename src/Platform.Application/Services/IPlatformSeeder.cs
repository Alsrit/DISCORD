namespace Platform.Application.Services;

public interface IPlatformSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
