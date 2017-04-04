using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using CacheManager.Core;

namespace MySharpServer.Common
{
    public interface IDataHelper
    {
        IDbConnection OpenDatabase(string cnnStrName);
        IDataParameter AddParam(IDbCommand cmd, string paramName, object paramValue);

        void RefreshDatabaseSettings(string dbConfigSection = "");

        ICacheManager<object> OpenCache(string cacheName);

        void RefreshCacheSettings(string cacheConfigSection = "");
    }
}
