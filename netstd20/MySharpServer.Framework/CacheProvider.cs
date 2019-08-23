using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;

using CacheManager.Core;
//using CacheManager.Core.Configuration;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class CacheProvider
    {
        public static readonly string CACHE_SECTION_NAME = "cacheManagers";
        public static readonly string CACHE_CONFIG_FILE = "cachesetting.json";

        private Dictionary<string, ICacheManager<object>> m_Mgrs = null;

        public CacheProvider()
        {
            Refresh();
        }

        public void Refresh()
        {
            Dictionary<string, ICacheManager<object>> mgrs = new Dictionary<string,ICacheManager<object>>();

            /*
            CacheManagerSection section = ConfigurationManager.GetSection(CACHE_SECTION_NAME) as CacheManagerSection;

            if (section != null && section.CacheManagers != null)
            {
                foreach (var item in section.CacheManagers)
                {
                    if (mgrs.ContainsKey(item.Name)) mgrs.Remove(item.Name);
                    var cache = CacheFactory.FromConfiguration<object>(item.Name);
                    if (cache != null) mgrs.Add(item.Name, cache);
                }
            }
            */

            if (DataConfigHelper.CacheConfigLoader != null)
            {
                var cacheNames = DataConfigHelper.CacheConfigLoader.Reload();
                foreach (var cacheName in cacheNames)
                {
                    string itemName = cacheName;
                    var cacheConfiguration = DataConfigHelper.CacheConfigLoader.GetCacheConfig(itemName);
                    if (mgrs.ContainsKey(itemName)) mgrs.Remove(itemName);
                    var cache = CacheFactory.FromConfiguration<object>(cacheConfiguration);
                    if (cache != null) mgrs.Add(itemName, cache);
                }
            }
            else
            {
                var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                //.SetBasePath(Directory.GetCurrentDirectory()) // Directory where the json files are located
                .AddJsonFile(CACHE_CONFIG_FILE, optional: false, reloadOnChange: true)
                .Build();

                var managerSections = config.GetSection(CACHE_SECTION_NAME).GetChildren();
                foreach (var managerSection in managerSections)
                {
                    string itemName = managerSection["name"];

                    var cacheConfiguration = config.GetCacheConfiguration(itemName);

                    if (mgrs.ContainsKey(itemName)) mgrs.Remove(itemName);
                    var cache = CacheFactory.FromConfiguration<object>(cacheConfiguration);
                    if (cache != null) mgrs.Add(itemName, cache);
                }
            }

            m_Mgrs = mgrs; // thread-safe (reads and writes of reference types are atomic)
        }

        public ICacheManager<object> OpenCache(string cacheName)
        {
            var mgrs = m_Mgrs; // thread-safe (reads and writes of reference types are atomic)
            ICacheManager<object> cache = null;
            if (mgrs != null && mgrs.Count > 0)
            {
                if (!mgrs.TryGetValue(cacheName, out cache)) cache = null;
            }
            return cache;
        }

    }

    public class InMemoryFileProvider : IFileProvider
    {
        private class InMemoryFile : IFileInfo
        {
            private readonly byte[] _data;
            public InMemoryFile(string json) => _data = Encoding.UTF8.GetBytes(json);
            public Stream CreateReadStream() => new MemoryStream(_data);
            public bool Exists { get; } = true;
            public long Length => _data.Length;
            public string PhysicalPath { get; } = string.Empty;
            public string Name { get; } = string.Empty;
            public DateTimeOffset LastModified { get; } = DateTimeOffset.UtcNow;
            public bool IsDirectory { get; } = false;
        }

        private readonly IFileInfo _fileInfo;
        public InMemoryFileProvider(string json) => _fileInfo = new InMemoryFile(json);
        public IFileInfo GetFileInfo(string _) => _fileInfo;
        public IDirectoryContents GetDirectoryContents(string _) => null;
        public IChangeToken Watch(string _) => NullChangeToken.Singleton;
    }

    public class CacheConfigLoader : ICacheConfigLoader
    {
        Dictionary<string, ICacheManagerConfiguration> m_configs = new Dictionary<string, ICacheManagerConfiguration>();

        public virtual List<string> Reload()
        {
            List<string> names = new List<string>();
            Dictionary<string, ICacheManagerConfiguration> configs = new Dictionary<string, ICacheManagerConfiguration>();

            string jsonFilePath = CacheProvider.CACHE_CONFIG_FILE;
            if (File.Exists(jsonFilePath))
            {
                string jsonText = File.ReadAllText(jsonFilePath);
                var memoryFileProvider = new InMemoryFileProvider(jsonText);
                var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddJsonFile(memoryFileProvider, "fake.json", false, false)
                    .Build();

                var managerSections = config.GetSection(CacheProvider.CACHE_SECTION_NAME).GetChildren();
                foreach (var managerSection in managerSections)
                {
                    string itemName = managerSection["name"];

                    var cacheConfiguration = config.GetCacheConfiguration(itemName);
                    if (configs.ContainsKey(itemName)) configs.Remove(itemName);
                    configs.Add(itemName, cacheConfiguration);

                    if (names.Contains(itemName)) names.Remove(itemName);
                    names.Add(itemName);
                }

                m_configs = configs;
            }

            return names;
        }

        public virtual ICacheManagerConfiguration GetCacheConfig(string cacheName = "")
        {
            var configs = m_configs;
            if (configs.ContainsKey(cacheName)) return configs[cacheName];
            else return null;
        }

    }
}
