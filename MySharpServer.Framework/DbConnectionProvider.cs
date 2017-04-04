using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MySharpServer.Framework
{
    public class DbConnectionProvider
    {
        public static readonly string DB_PROVIDER_SECTION = "system.data";

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
                DataSet section = ConfigurationManager.GetSection("system.data") as DataSet;
                if (section != null)
                {
                    DataTable table = section.Tables["DbProviderFactories"];
                    if (table != null)
                    {
                        foreach (DataRow row in table.Rows)
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
                m_Config = new DbConnectionConfig(factory, cnnstr);
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
                conn.ConnectionString = cnnstr.ConnectionString;
                if (conn != null) conn.Open();
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
}
