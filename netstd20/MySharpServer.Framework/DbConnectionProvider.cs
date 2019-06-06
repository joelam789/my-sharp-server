using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

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
                //factory = DbProviderFactories.GetFactory(cnnstr.ProviderName);
                factory = DbHelperFunc.GetDbProviderFactory(cnnstr.ProviderName);
            }
            catch { }

            if (factory == null)
            {
                /*
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
                                //factory = DbProviderFactories.GetFactory(row);
                                factory = DbHelperFunc.GetDbProviderFactory(row["Name"].ToString());
                            }
                            if (factory != null) break;
                        }
                    }
                }
                */

                try
                {
                    string folder = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
                    if (folder == null || folder.Trim().Length <= 0)
                    {
                        var entry = Assembly.GetEntryAssembly();
                        var location = "";
                        try
                        {
                            if (entry != null) location = entry.Location;
                        }
                        catch { }
                        if (location != null && location.Length > 0)
                        {
                            folder = Path.GetDirectoryName(location);
                        }
                    }
                    if (folder == null || folder.Trim().Length <= 0) folder = "";

                    XmlDocument xmlDoc = new XmlDocument();

                    var configFilepath = folder + "/" + AppDomain.CurrentDomain.FriendlyName;

                    if (File.Exists(configFilepath + ".exe.config")) configFilepath += ".exe.config";
                    else if (File.Exists(configFilepath + ".dll.config")) configFilepath += ".dll.config";
                    else configFilepath += ".config";

                    xmlDoc.Load(configFilepath);

                    foreach (var ele in xmlDoc.DocumentElement)
                    {
                        XmlElement element = ele as XmlElement;
                        if (element != null && element.Name.Equals("system.data"))
                        {
                            foreach (XmlNode node in element.ChildNodes)
                            {
                                if (node.Name.Equals("DbProviderFactories"))
                                {
                                    foreach (XmlNode subnode in node.ChildNodes)
                                    {
                                        if (subnode.Name.Equals("add"))
                                        {
                                            var attrItem = subnode.Attributes.GetNamedItem("name") as XmlAttribute;
                                            var providerName = attrItem == null ? "" : attrItem.Value;
                                            if (cnnstr.ProviderName.Equals(providerName))
                                            {
                                                //factory = DbProviderFactories.GetFactory(row);

                                                attrItem = subnode.Attributes.GetNamedItem("invariant") as XmlAttribute;
                                                var providerClass = attrItem == null ? providerName : attrItem.Value;

                                                factory = DbHelperFunc.GetDbProviderFactory(providerClass);
                                            }
                                            if (factory != null) break;
                                        }
                                    }
                                }
                                if (factory != null) break;
                            }
                        }
                        if (factory != null) break;
                    }
                }
                catch { }
            }

            if (factory != null && cnnstr != null)
            {
                // thread-safe (reads and writes of reference types are atomic)
                m_Config = new DbConnectionConfig(factory, cnnstr);
            }
        }

        public IDbConnection OpenDbConnection(string specifiedCnnStr = "")
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
                    conn.ConnectionString = String.IsNullOrEmpty(specifiedCnnStr)
                                                ? cnnstr.ConnectionString : specifiedCnnStr;
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


    // refer to 
    // https://weblog.west-wind.com/posts/2017/nov/27/working-around-the-lack-of-dynamic-dbproviderfactory-loading-in-net-core
    public static class DbHelperFunc
    {
        public enum DataAccessProviderTypes
        {
            SqlServer,
            SqLite,
            MySql,
            MySqlConnector, // https://mysql-net.github.io/MySqlConnector
            PostgreSql,
        }

        public static object GetStaticProperty(string typeName, string property)
        {
            Type type = GetTypeFromName(typeName);
            if (type == null)
                return null;

            return GetStaticProperty(type, property);
        }

        public static object GetStaticProperty(Type type, string property)
        {
            object result = null;
            try
            {
                result = type.InvokeMember(property, BindingFlags.Static | BindingFlags.Public | BindingFlags.GetField | BindingFlags.GetProperty, null, type, null);
            }
            catch
            {
                return null;
            }

            return result;
        }

        public static Type GetTypeFromName(string typeName, string assemblyName)
        {
            var type = Type.GetType(typeName, false);
            if (type != null)
                return type;

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            // try to find manually
            foreach (Assembly asm in assemblies)
            {
                type = asm.GetType(typeName, false);

                if (type != null)
                    break;
            }
            if (type != null)
                return type;

            // see if we can load the assembly
            if (!string.IsNullOrEmpty(assemblyName))
            {
                var a = LoadAssembly(assemblyName);
                if (a != null)
                {
                    type = Type.GetType(typeName, false);
                    if (type != null)
                        return type;
                }
            }

            return null;
        }

        public static Type GetTypeFromName(string typeName)
        {
            return GetTypeFromName(typeName, null);
        }

        public static Assembly LoadAssembly(string assemblyName)
        {
            Assembly assembly = null;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch { }

            if (assembly != null)
                return assembly;

            if (File.Exists(assemblyName))
            {
                assembly = Assembly.LoadFrom(assemblyName);
                if (assembly != null)
                    return assembly;
            }
            return null;
        }

        public static DbProviderFactory GetDbProviderFactory(string dbProviderFactoryTypename, string assemblyName)
        {
            var instance = GetStaticProperty(dbProviderFactoryTypename, "Instance");
            if (instance == null)
            {
                var a = LoadAssembly(assemblyName);
                if (a != null)
                    instance = GetStaticProperty(dbProviderFactoryTypename, "Instance");
            }

            if (instance == null)
                throw new InvalidOperationException("Unable to retrieve DbProviderFactory: " + dbProviderFactoryTypename);

            return instance as DbProviderFactory;
        }

        public static DbProviderFactory GetDbProviderFactory(DataAccessProviderTypes type)
        {
            //if (type == DataAccessProviderTypes.SqlServer)
            //    return SqlClientFactory.Instance; // this library has a ref to SqlClient so this works

            if (type == DataAccessProviderTypes.SqlServer)
                return GetDbProviderFactory("System.Data.SqlClient.SqlClientFactory", "System.Data");
            if (type == DataAccessProviderTypes.SqLite)
                return GetDbProviderFactory("Microsoft.Data.Sqlite.SqliteFactory", "Microsoft.Data.Sqlite");
            if (type == DataAccessProviderTypes.MySql)
                return GetDbProviderFactory("MySql.Data.MySqlClient.MySqlClientFactory", "MySql.Data");
            if (type == DataAccessProviderTypes.MySqlConnector)
                return GetDbProviderFactory("MySql.Data.MySqlClient.MySqlClientFactory", "MySqlConnector");
            if (type == DataAccessProviderTypes.PostgreSql)
                return GetDbProviderFactory("Npgsql.NpgsqlFactory", "Npgsql");

            //#if NETFULL
            //    if (type == DataAccessProviderTypes.OleDb)
            //        return System.Data.OleDb.OleDbFactory.Instance;
            //    if (type == DataAccessProviderTypes.SqlServerCompact)
            //        return DbProviderFactories.GetFactory("System.Data.SqlServerCe.4.0");                
            //#endif

            throw new NotSupportedException("Unsupported Provider Factory: " + type.ToString());
        }

        public static DbProviderFactory GetDbProviderFactory(string providerName)
        {
            var dbProviderName = providerName.ToLower();

            if (dbProviderName == "system.data.sqlclient")
                return GetDbProviderFactory(DataAccessProviderTypes.SqlServer);
            if (dbProviderName == "system.data.sqlite" || dbProviderName == "microsoft.data.sqlite")
                return GetDbProviderFactory(DataAccessProviderTypes.SqLite);
            if (dbProviderName == "mysql.data.mysqlclient" || dbProviderName == "mysql.data")
                return GetDbProviderFactory(DataAccessProviderTypes.MySql);
            if (dbProviderName == "mysqlconnector")
                return GetDbProviderFactory(DataAccessProviderTypes.MySqlConnector);
            if (dbProviderName == "npgsql")
                return GetDbProviderFactory(DataAccessProviderTypes.PostgreSql);

            throw new NotSupportedException("Unsupported Provider Factory: " + providerName);
        }
    }
}
