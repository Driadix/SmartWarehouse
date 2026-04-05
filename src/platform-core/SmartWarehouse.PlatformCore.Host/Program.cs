namespace SmartWarehouse.PlatformCore.Host;

public class Program
{
  public static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddControllers();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/healthz");

    app.Run();
  }
}
