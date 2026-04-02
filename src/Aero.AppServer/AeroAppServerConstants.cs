using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aero.AppServer;

public static class AeroAppServerConstants
{
    public const string CacheName = "aero-cache";
    public const string CacheUrl = "localhost:33333";
    public const string CacheHost = "localhost";
    public const int CachePort = 33333;
    public const string PgVersion = "18.3.0";
    public const int PgPort = 5433;
    public const string EmbedConnString = "Host=localhost;Port=5433;Username=aero;Password=*aeroLocal1;Database=aero;";
}
