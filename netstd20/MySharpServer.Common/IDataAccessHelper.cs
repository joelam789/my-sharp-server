using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using CacheManager.Core;

namespace MySharpServer.Common
{
    public interface IDataAccessHelper
    {
        IDbConnection OpenDatabase(string dbName = "");
        IDataParameter AddParam(IDbCommand cmd, string paramName, object paramValue);

        void RefreshDatabaseSettings(string configLocation = "");

        ICacheManager<object> OpenCache(string cacheName = "");

        ISimpleLocker Lock(ICacheManager<object> cache, string key, int lifetimeSeconds = 10);
        ISimpleLocker Lock(ICacheManager<object> cache, string key, string region, int lifetimeSeconds = 10);

        void RefreshCacheSettings(string configLocation = "");
    }

    public interface IDbConnectionStringLoader
    {
        List<string> Reload();
        string GetConnectionString(string cnnStrName = "");
    }

    public interface ICacheConfigLoader
    {
        List<string> Reload();
        ICacheManagerConfiguration GetCacheConfig(string cacheName = "");
    }
}
