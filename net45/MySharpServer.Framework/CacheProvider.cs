using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

using CacheManager.Core;
using CacheManager.Core.Configuration;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class CacheProvider
    {
        public static readonly string CACHE_SECTION_NAME = "cacheManager";

        private Dictionary<string, ICacheManager<object>> m_Mgrs = null;

        public CacheProvider()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (DataConfigHelper.CacheConfigLoader != null)
                DataConfigHelper.CacheConfigLoader.Reload();

            Dictionary<string, ICacheManager<object>> mgrs = new Dictionary<string,ICacheManager<object>>();

            CacheManagerSection section = ConfigurationManager.GetSection(CACHE_SECTION_NAME) as CacheManagerSection;

            if (section != null && section.CacheManagers != null)
            {
                foreach (var item in section.CacheManagers)
                {
                    ICacheManagerConfiguration cfg = null;
                    if (DataConfigHelper.CacheConfigLoader != null)
                        cfg = DataConfigHelper.CacheConfigLoader.GetCacheConfig(item.Name);

                    if (mgrs.ContainsKey(item.Name)) mgrs.Remove(item.Name);

                    var cache = cfg == null ? CacheFactory.FromConfiguration<object>(item.Name) 
                                            : CacheFactory.FromConfiguration<object>(item.Name, cfg);

                    if (cache != null) mgrs.Add(item.Name, cache);
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

    
}
