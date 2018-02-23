using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MySharpServer.Common;

namespace MySharpServerExample.ServerService
{
    [Access(Name = "example")]
    public class ExampleService
    {
        [Access(Name = "hello")]
        public void Hello(RequestContext ctx)
        {
            string userName = ctx.Data.ToString();
            if (userName.Trim().Length <= 0)
            {
                ctx.Session.Send("Invalid user name");
                return;
            }

            string lastAccessTime = "";
            string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            try
            {
                var cache = ctx.DataHelper.OpenCache("cache");
                var value = cache.Get(userName);
                if (value != null) lastAccessTime = value.ToString();
                cache.Put(userName, currentTime);
            }
            catch (Exception ex)
            {
                var errMsg = "Failed to access cache";
                ctx.LocalLogger.Error(errMsg + " - " + ex.Message);
                ctx.LocalLogger.Error(ex.StackTrace);
                ctx.Session.Send(errMsg);
                return;
            }

            if (lastAccessTime == null || lastAccessTime.Length <= 0) lastAccessTime = "This is your fist time to say hello";
            else lastAccessTime = "Your last access time is " + lastAccessTime;

            ctx.Session.Send("[" + currentTime + "] Hello, " + userName + ". " + lastAccessTime);
        }
    }
}
