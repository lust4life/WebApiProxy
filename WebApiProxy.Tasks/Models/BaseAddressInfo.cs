using System.Collections.Concurrent;

namespace WebApiProxy.Tasks.Models
{
    public abstract class BaseAddressInfo
    {
        public static ConcurrentDictionary<string,string> Infos = new ConcurrentDictionary<string, string>();

        public abstract void LoadAddressInfo();
    }
}