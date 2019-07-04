using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class DbConnectionProvider
    {
        public static readonly string DB_PROVIDER_SECTION = "system.data";
        public static readonly string DB_PROVIDER_FACTORY_TAG = "DbProviderFactories";

        private string m_ConnectionStringName = "";
        private DbConnectionConfig m_Config = null;

        public DbConnectionProvider(string cnnStrName)
        {
            m_ConnectionStringName = cnnStrName;
            RefreshSetting();
        }

        public void RefreshSetting()
        {
            DbProviderFactory factory = null;
            ConnectionStringSettings cnnstr = ConfigurationManager.ConnectionStrings[m_ConnectionStringName];

            try
            {
                factory = DbProviderFactories.GetFactory(cnnstr.ProviderName);
            }
            catch { }

            if (factory == null)
            {
                DataSet section = ConfigurationManager.GetSection(DB_PROVIDER_SECTION) as DataSet;
                if (section != null)
                {
                    DataTable tableFactory = section.Tables[DB_PROVIDER_FACTORY_TAG];
                    if (tableFactory != null)
                    {
                        foreach (DataRow row in tableFactory.Rows)
                        {
                            if (cnnstr.ProviderName.Equals(row["Name"]))
                            {
                                factory = DbProviderFactories.GetFactory(row);
                            }
                            if (factory != null) break;
                        }
                    }
                }
            }

            if (factory != null && cnnstr != null)
            {
                // thread-safe (reads and writes of reference types are atomic)
                if (DataConfigHelper.DbConnectionStringLoader != null)
                {
                    string newCnnStr = DataConfigHelper.DbConnectionStringLoader.GetConnectionString(cnnstr.Name);
                    if (String.IsNullOrEmpty(newCnnStr)) newCnnStr = cnnstr.ConnectionString;
                    m_Config = new DbConnectionConfig(factory, new ConnectionStringSettings(cnnstr.Name, newCnnStr, cnnstr.ProviderName));
                }
                else m_Config = new DbConnectionConfig(factory, cnnstr);
            }
        }

        public IDbConnection OpenDbConnection()
        {
            // thread-safe (reads and writes of reference types are atomic)
            DbConnectionConfig config = m_Config;

            DbProviderFactory factory = config.DbFactory;
            ConnectionStringSettings cnnstr = config.CnnString;

            IDbConnection conn = null;
            if (factory != null && cnnstr != null)
            {
                conn = factory.CreateConnection();
                if (conn != null)
                {
                    conn.ConnectionString = cnnstr.ConnectionString;
                    conn.Open();
                }
            }

            return conn;
        }

    }

    public class DbConnectionConfig
    {
        public DbProviderFactory DbFactory { get; private set; }
        public ConnectionStringSettings CnnString { get; private set; }

        public DbConnectionConfig(DbProviderFactory factory, ConnectionStringSettings cnnstr)
        {
            DbFactory = factory;
            CnnString = cnnstr;
        }
    }

    public class DbConnectionStringLoader : IDbConnectionStringLoader
    {
        string m_CnnStrSectionName = "connectionStrings";

        public List<string> Reload()
        {
            List<string> names = new List<string>();
            try
            {
                lock (m_CnnStrSectionName)
                {
                    ConfigurationManager.RefreshSection(m_CnnStrSectionName);

                    foreach (var item in ConfigurationManager.ConnectionStrings.Cast<ConnectionStringSettings>())
                    {
                        if (names.Contains(item.Name)) names.Remove(item.Name);
                        names.Add(item.Name);
                    }
                }
            }
            catch { }

            return names;
        }
        public string GetConnectionString(string cnnStrName = "")
        {
            ConnectionStringSettings cnnstr = null;
            try
            {
                lock (m_CnnStrSectionName)
                {
                    cnnstr = ConfigurationManager.ConnectionStrings[cnnStrName];
                }
            }
            catch { }

            return cnnstr == null ? "" : cnnstr.ConnectionString;
        }
    }
}
