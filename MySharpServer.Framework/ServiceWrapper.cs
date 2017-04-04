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
                    var actions = attr.IsPublic ? m_PublicActions : m_InternalActions;
                    if (!actions.ContainsKey(actionName)) actions.Add(actionName, method);
                }
            }
        }

        public string ValidateRequest(object param)
        {
            var result = Call("validate-request", param, false, "");
            if (result != null) return result.ToString();
            return "";
        }

        public TaskFactory GetTaskFactory(object param)
        {
            var result = Call("get-task-factory", param, false, Task.Factory);
            if (result != null && result is TaskFactory) return result as TaskFactory;
            return null;
        }

        public object Call(string actionName, object param, bool publicOnly, object defaultResult)
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
                        if (!m_InternalActions.TryGetValue(actionName, out method)) method = null;
                    }
                }
                if (method != null) result = method.Invoke(ServiceObject, new object[] { param });
                else if (defaultResult != null) result = defaultResult;
            }
            catch (Exception ex)
            {
                m_Logger.Error("Failed to call service action - " + ServiceName + "." +  actionName + " , error: " + ex.Message);
                m_Logger.Error(ex.StackTrace);
            }
            return result;
        }

        public object Call(string actionName, object param, bool publicOnly = true)
        {
            return Call(actionName, param, publicOnly, null);
        }

    }
}
