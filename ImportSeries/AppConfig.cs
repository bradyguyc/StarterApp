using Microsoft.Extensions.Configuration;

namespace ImportSeries
{
    public static class AppConfig
    {
        public static IConfiguration Configuration { get; private set; }

        public static void Initialize(IConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}