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
        IDbConnection OpenDatabase(string cnnStrName = "", string specifiedCnnStr = "");
        IDataParameter AddParam(IDbCommand cmd, string paramName, object paramValue);

        void RefreshDatabaseSettings(string dbConfigSection = "");

        ICacheManager<object> OpenCache(string cacheName);

        ISimpleLocker Lock(ICacheManager<object> cache, string key, int lifetimeSeconds = 10);
        ISimpleLocker Lock(ICacheManager<object> cache, string key, string region, int lifetimeSeconds = 10);

        void RefreshCacheSettings(string cacheConfigSection = "");
    }
}
