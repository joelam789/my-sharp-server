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

        private Dictionary<string, DbConnectionProvider> m_DbCnnProviders = null;

        private CacheProvider m_CacheProvider = null;

        public DataAccessHelper()
        {
            DefaultDatabaseName = "";
            DefaultCacheName = "";

            m_DbCnnProviders = new Dictionary<string, DbConnectionProvider>();

            m_CacheProvider = new CacheProvider();

            RefreshDatabaseSettings();
        }

        public void RefreshDatabaseSettings(string configLocation = "")
        {
            /*
            // try to reload configuration file.
            bool reloadedConfig = false;
            Configuration config = null;
            try
            {
                config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            }
            catch { }

            try
            {
                if (config == null) config = System.Web.Configuration.WebConfigurationManager.OpenWebConfiguration("~");
            }
            catch { }

            try
            {
                if (config != null)
                {
                    var providers = new Dictionary<string, DbConnectionProvider>();

                    // Get the conectionStrings section.
                    ConnectionStringsSection csSection = config.ConnectionStrings;
                    for (int i = 0; i < ConfigurationManager.ConnectionStrings.Count; i++)
                    {
                        ConnectionStringSettings cnnstr = csSection.ConnectionStrings[i];
                        if (!providers.ContainsKey(cnnstr.Name)) providers.Add(cnnstr.Name, new DbConnectionProvider(cnnstr.Name));
                    }

                    m_DbCnnProviders = providers; // thread-safe (reads and writes of reference types are atomic)
                    reloadedConfig = true;
                }
            }
            catch { }
            */

            lock (m_DbCnnProviders)
            {
                if (DataConfigHelper.DbConnectionStringLoader != null)
                    DataConfigHelper.DbConnectionStringLoader.Reload();

                bool reloadedConfig = false;
                try
                {
                    ConfigurationManager.RefreshSection(DbConnectionProvider.DB_PROVIDER_SECTION);
                    ConfigurationManager.RefreshSection(CNN_STRING_SECTION);

                    if (configLocation != null && configLocation.Length > 0)
                        ConfigurationManager.RefreshSection(configLocation);

                    var providers = new Dictionary<string, DbConnectionProvider>();
                    var cnnStringSection = ConfigurationManager.ConnectionStrings;
                    foreach (var item in cnnStringSection)
                    {
                        ConnectionStringSettings cnnstr = item as ConnectionStringSettings;
                        if (cnnstr != null && !providers.ContainsKey(cnnstr.Name))
                            providers.Add(cnnstr.Name, new DbConnectionProvider(cnnstr.Name));
                    }
                    m_DbCnnProviders = providers; // thread-safe (reads and writes of reference types are atomic)
                    reloadedConfig = true;

                }
                catch { }

                if (!reloadedConfig) foreach (var item in m_DbCnnProviders) item.Value.RefreshSetting(); // refresh existing providers
            }
        }

        public IDbConnection OpenDatabase(string dbName = "")
        {
            var targetName = dbName;
            if (targetName == null || targetName.Length <= 0) targetName = DefaultDatabaseName;

            IDbConnection cnn = null;
            DbConnectionProvider provider = null;
            var providers = m_DbCnnProviders; // thread-safe (reads and writes of reference types are atomic)
            if (providers.TryGetValue(targetName, out provider))
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

        public void RefreshCacheSettings(string configLocation = "")
        {
            /*
            try
            {
                ConfigurationManager.RefreshSection(CacheProvider.CACHE_SECTION_NAME);
                if (cacheConfigSection != null && cacheConfigSection.Length > 0)
                    ConfigurationManager.RefreshSection(cacheConfigSection);
            }
            catch { }
            */

            lock (m_CacheProvider)
            {
                m_CacheProvider.Refresh();
            }
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

    public static class DataConfigHelper
    {
        public static IDbConnectionStringLoader DbConnectionStringLoader { get; set; }
        public static ICacheConfigLoader CacheConfigLoader { get; set; }

        static DataConfigHelper()
        {
            DbConnectionStringLoader = new DbConnectionStringLoader();
            CacheConfigLoader = new CacheConfigLoader();
        }
    }
}

