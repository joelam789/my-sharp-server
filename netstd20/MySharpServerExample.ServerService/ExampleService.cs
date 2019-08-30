using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySharpServer.Common;

namespace MySharpServerExample.ServerService
{
    [Access(Name = "example")]
    public class ExampleService
    {
        [Access(Name = "on-load", IsLocal = true)]
        public async Task<string> Load(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            node.GetLogger().Info(this.GetType().Name + " is loading...");
            await Task.Delay(100);
            node.GetLogger().Info(this.GetType().Name + " is loaded");

            return "";
        }

        [Access(Name = "on-unload", IsLocal = true)]
        public async Task<string> Unload(IServerNode node)
        {
            //System.Diagnostics.Debugger.Break();

            //await Task.Delay(100);
            node.GetLogger().Info(this.GetType().Name + " is unloading...");
            await Task.Delay(100);
            node.GetLogger().Info(this.GetType().Name + " is unloaded");
            //await Task.Delay(100);

            return "";
        }

        [Access(Name = "hello")]
        public async Task Hello(RequestContext ctx)
        {
            //System.Diagnostics.Debugger.Break();

            string userName = ctx.Data.ToString();
            if (userName.Trim().Length <= 0)
            {
                await ctx.Session.Send("Invalid user name");
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
                ctx.Logger.Error(errMsg + " - " + ex.Message);
                ctx.Logger.Error(ex.StackTrace);
                await ctx.Session.Send(errMsg);
                return;
            }

            if (lastAccessTime == null || lastAccessTime.Length <= 0) lastAccessTime = "This is your fist time to say hello";
            else lastAccessTime = "Your last access time is " + lastAccessTime;

            await ctx.Session.Send("[" + currentTime + "] Hello, " + userName + ". " + lastAccessTime);
        }
    }
}
