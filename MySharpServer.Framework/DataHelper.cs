using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;

using CacheManager.Core;
using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class DataHelper: IDataHelper
    {
        public static readonly string CNN_STRING_SECTION = "connectionStrings";

        private Dictionary<string, DbConnectionProvider> m_DbCnnProviders = new Dictionary<string, DbConnectionProvider>();

        private CacheProvider m_CacheProvider = null;

        public DataHelper()
        {
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

        public IDbConnection OpenDatabase(string cnnStrName)
        {
            DbConnectionProvider provider = null;
            if (m_DbCnnProviders.TryGetValue(cnnStrName, out provider))
            {
                return provider.OpenDbConnection();
            }
            return null;
        }

        public IDataParameter AddParam(IDbCommand cmd, string paramName, object paramValue)
        {
            IDataParameter prm = cmd.CreateParameter();
            prm.ParameterName = paramName;
            prm.Value = paramValue;
            cmd.Parameters.Add(prm);
            return prm;
        }

        public ICacheManager<object> OpenCache(string cacheName)
        {
            return m_CacheProvider.OpenCache(cacheName);
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
    }
}
