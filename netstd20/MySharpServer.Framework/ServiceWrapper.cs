using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MySharpServer.Framework
{
    public class ServiceWrapper: IActionCaller
    {
        public bool IsPublic { get; private set; }
        public string ServiceName { get; private set; }
        public object ServiceObject { get; private set; }

        private IServerLogger m_Logger = null;

        private Dictionary<string, MethodInfo> m_LocalActions = new Dictionary<string, MethodInfo>();
        private Dictionary<string, MethodInfo> m_PublicActions = new Dictionary<string, MethodInfo>();
        private Dictionary<string, MethodInfo> m_InternalActions = new Dictionary<string, MethodInfo>();

        public static AccessAttribute GetAnnotation(Type objectType)
        {
            AccessAttribute attr = null;
            if (objectType != null) attr = objectType.GetCustomAttributes(typeof(AccessAttribute), false).FirstOrDefault() as AccessAttribute;
            return attr == null ? null : (attr.Name.Length > 0 ? attr : null);
        }

        public static AccessAttribute GetAnnotation(MethodInfo method)
        {
            AccessAttribute attr = null;
            if (method != null) attr = method.GetCustomAttributes(typeof(AccessAttribute), false).FirstOrDefault() as AccessAttribute;
            return attr == null ? null : (attr.Name.Length > 0 ? attr : null);
        }

        public ServiceWrapper(Type objectType, String serviceName, Boolean isPublic = true, IServerLogger logger = null)
        {
            IsPublic = isPublic;
            ServiceName = serviceName;

            m_Logger = logger;
            if (m_Logger == null) m_Logger = new ConsoleLogger();

            ServiceObject = Activator.CreateInstance(objectType);
            var methods = ServiceObject.GetType().GetMethods();
            foreach (var method in methods)
            {
                var attr = ServiceWrapper.GetAnnotation(method);
                if (attr != null)
                {
                    string actionName = attr.Name;
                    var actions = attr.IsLocal ? m_LocalActions : (attr.IsPublic ? m_PublicActions : m_InternalActions);
                    if (!actions.ContainsKey(actionName)) actions.Add(actionName, method);
                }
            }
        }

        public async Task<string> Load(object param)
        {
            var result = await Call("on-load", param, false, true, "");
            if (result != null) return result.ToString();
            return "";
        }

        public async Task<string> Unload(object param)
        {
            var result = await Call("on-unload", param, false, true, "");
            if (result != null) return result.ToString();
            return "";
        }

        public async Task<string> ValidateRequest(object param)
        {
            var result = await Call("validate-request", param, false, true, "");
            if (result != null) return result.ToString();
            return "";
        }

        public async Task<TaskFactory> GetTaskFactory(object param)
        {
            var result = await Call("get-task-factory", param, false, true, Task.Factory);
            if (result != null) return result as TaskFactory;
            return null;
        }

        public async Task<object> Call(string actionName, object param, bool publicOnly, bool includingLocal, object defaultResult)
        {
            object result = null;
            try
            {
                MethodInfo method = null;
                if (!m_PublicActions.TryGetValue(actionName, out method))
                {
                    method = null;
                    if (!publicOnly)
                    {
                        if (!m_InternalActions.TryGetValue(actionName, out method))
                        {
                            method = null;
                            if (includingLocal)
                            {
                                if (!m_LocalActions.TryGetValue(actionName, out method)) method = null;
                            }
                        }
                    }
                }
                if (method != null) result = method.Invoke(ServiceObject, new object[] { param });
                else if (defaultResult != null) result = defaultResult;

                if (result != null)
                {
                    Task<string> str = result as Task<string>;
                    if (str != null) return await str;
                    else
                    {
                        Task<object> ret = result as Task<object>;
                        if (ret != null) return await ret;
                        else
                        {
                            Task task = result as Task;
                            if (task != null) await task;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                m_Logger.Error("Failed to call service action - " + ServiceName + "." + actionName + " , error: " + ex.Message);
                m_Logger.Error(ex.StackTrace);
            }
            return result;
        }

        public async Task<object> LocalCall(string actionName, object param)
        {
            return await Call(actionName, param, false, true, null);
        }

        public async Task<object> Call(string actionName, object param, bool publicOnly = true)
        {
            return await Call(actionName, param, publicOnly, false, null);
        }

    }
}
