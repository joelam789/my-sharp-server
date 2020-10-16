using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CacheManager.Core;
using MySharpServer.Common;

namespace MySharpServer.Framework
{
    // it could be used to process concurrent requests.
    // but there is no queueing inside, it uses just "fast failure", very simple
    public class SimpleLocker : ISimpleLocker
    {
        private ICacheManager<object> m_Cache = null;
        private string m_Region = null;
        private string m_Key = null;
        private string m_LockKey = null;
        private bool m_IsLocked = false;
        private string m_ErrorMessage = "";

        public bool IsLocked { get { return m_IsLocked; } }
        public string ErrorMessage { get { return m_ErrorMessage; } }

        public SimpleLocker(ICacheManager<object> cache, string key, string region = null, int lifetimeSeconds = 0)
        {
            m_Cache = cache;
            m_Region = region;
            m_Key = key;
            m_IsLocked = false;

            try
            {
                if (m_Cache != null && key != null && key.Length > 0)
                {
                    m_LockKey = GetLockKey();
                    if (region != null && region.Length > 0) m_IsLocked = m_Cache.Add(m_LockKey, key, region);
                    else m_IsLocked = m_Cache.Add(m_LockKey, key);

                    if (m_IsLocked && lifetimeSeconds > 0)
                    {
                        m_Cache.Expire(m_LockKey, TimeSpan.FromSeconds(lifetimeSeconds));
                    }
                }
            }
            catch (Exception ex)
            {
                m_ErrorMessage = ex.Message;
            }
        }

        public void Dispose()
        {
            try
            {
                if (m_IsLocked && m_Cache != null && m_LockKey != null)
                {
                    if (m_Region != null && m_Region.Length > 0) m_Cache.Remove(m_LockKey, m_Region);
                    else m_Cache.Remove(m_LockKey);
                    m_IsLocked = false;
                }
            }
            catch { }
        }

        public string GetLockKey()
        {
            if (m_Key != null && m_Key.Length > 0)
            {
                return m_Key + "|[L.O.C.K]";
            }
            return "";
        }
    }
}
