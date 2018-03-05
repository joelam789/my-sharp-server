using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;

using CacheManager.Core;
using MySharpServer.Common;

//using System.Data.SqlClient;
//using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;

namespace MySharpServer.Framework
{
    public class DataAccessHelper: IDataAccessHelper
    {
        public static readonly string CNN_STRING_SECTION = "connectionStrings";

        public string DefaultDatabaseName { get; set; }

        public string DefaultCacheName { get; set; }

        private Dictionary<string, DbConnectionProvider> m_DbCnnProviders = new Dictionary<string, DbConnectionProvider>();

        private CacheProvider m_CacheProvider = null;

        public DataAccessHelper()
        {
            DefaultDatabaseName = "";
            DefaultCacheName = "";

            // Get the application configuration file.
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            // Get the conectionStrings section.
            ConnectionStringsSection csSection = config.ConnectionStrings;

            for (int i = 0; i < ConfigurationManager.ConnectionStrings.Count; i++)
            {
                ConnectionStringSettings cnnstr = csSection.ConnectionStrings[i];
                if (!m_DbCnnProviders.ContainsKey(cnnstr.Name)) m_DbCnnProviders.Add(cnnstr.Name, new DbConnectionProvider(cnnstr.Name));
            }

            m_CacheProvider = new CacheProvider();
        }

        public void RefreshDatabaseSettings(string dbConfigSection = "")
        {
            try
            {
                ConfigurationManager.RefreshSection(DbConnectionProvider.DB_PROVIDER_SECTION);
                ConfigurationManager.RefreshSection(CNN_STRING_SECTION);
                if (dbConfigSection != null && dbConfigSection.Length > 0)
                    ConfigurationManager.RefreshSection(dbConfigSection);
            }
            catch { }

            foreach (var item in m_DbCnnProviders) item.Value.RefreshSetting();
        }

        public IDbConnection OpenDatabase(string cnnStrName = "")
        {
            var targetName = cnnStrName;
            if (targetName == null || targetName.Length <= 0) targetName = DefaultDatabaseName;

            IDbConnection cnn = null;
            DbConnectionProvider provider = null;
            if (m_DbCnnProviders.TryGetValue(targetName, out provider))
            {
                if (provider != null) cnn = provider.OpenDbConnection();
            }

            if (cnn == null) throw new Exception("Failed to open database: " + targetName);
            return cnn;
        }

        public IDataParameter AddParam(IDbCommand cmd, string paramName, object paramValue)
        {
            IDataParameter prm = cmd.CreateParameter();
            prm.ParameterName = paramName;
            prm.Value = paramValue;
            cmd.Parameters.Add(prm);
            return prm;
        }

        public ICacheManager<object> OpenCache(string cacheName = "")
        {
            var targetName = cacheName;
            if (targetName == null || targetName.Length <= 0) targetName = DefaultCacheName;

            var cache = m_CacheProvider.OpenCache(targetName);
            if (cache == null) throw new Exception("Failed to open cache storage: " + targetName);

            return cache;
        }

        public ISimpleLocker Lock(ICacheManager<object> cache, string key, int lifetimeSeconds = 10)
        {
            return new SimpleLocker(cache, key, null, lifetimeSeconds);
        }

        public ISimpleLocker Lock(ICacheManager<object> cache, string key, string region, int lifetimeSeconds = 10)
        {
            return new SimpleLocker(cache, key, region, lifetimeSeconds);
        }

        public void RefreshCacheSettings(string cacheConfigSection = "")
        {
            try
            {
                ConfigurationManager.RefreshSection(CacheProvider.CACHE_SECTION_NAME);
                if (cacheConfigSection != null && cacheConfigSection.Length > 0)
                    ConfigurationManager.RefreshSection(cacheConfigSection);
            }
            catch { }

            m_CacheProvider.Refresh();
        }

        //public static RetryPolicy GetRetryPolicy(int retryTimes = 5, int retryIntervalMS = 1500)
        //{
        //    return new RetryPolicy(new SQLTransientErrorDetectionStrategy(), retryTimes, TimeSpan.Zero, TimeSpan.FromMilliseconds(retryIntervalMS));
        //}
    }

    /*
    public class SQLTransientErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        // Simulate Entityframework's SqlAzureExecutionStrategy.
        public bool IsTransient(Exception ex)
        {
            if (ex is TimeoutException)
            {
                return true;
            }
            if (ex is SqlException)
            {
                Logger.Error("SQL DB Error From SQLTransientErrorDetectionStrategy: " + ((SqlException)ex).Number);
                switch (((SqlException)ex).Number)
                {
                    case 40613:
                    //case 40515: 
                    case 40501:
                    case 40197:
                    case 10929:
                    case 10928:
                    case 10060:
                    case 10054:
                    case 10053:
                    case 1231:
                    case 233:
                    case 64:
                    case 20:
                    //case 5:  // For testing only, connection creation blocked by firewall.
                    //case 0:  // For testing only, query's connection killed.
                        return true;
                }
            }
            return false;
        }
    }
    */

}

