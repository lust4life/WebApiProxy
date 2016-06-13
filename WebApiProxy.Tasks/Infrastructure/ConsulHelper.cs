using System;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace WebApiProxy.Tasks.Infrastructure
{
    public static class ConsulHelper
    {
        public const string TraefikServiceName = "traefik";

        public class ServiceInfo
        {
            public string ServiceName { get; set; } 
            public string Address { get; set; }
            public string Port { get; set; }
        }


        public static ServiceInfo DiscoveryService(string consulEndpoint, string serviceName,string tag=null)
        {
            using (var client = new HttpClient())
            {
                var traefikUrl = string.Format("{0}/v1/health/service/{1}", consulEndpoint, serviceName);

                if (!string.IsNullOrEmpty(tag))
                {
                    traefikUrl = traefikUrl + "?tag=" + tag;
                }

                var traefikInfo = client.GetAsync(traefikUrl).Result.Content.ReadAsAsync<JArray>().Result;
                foreach (var info in traefikInfo)
                {
                    var serviceIsOk = info["Checks"].Any(item => item.Value<string>("CheckID") == "service:"+serviceName
                                                         && item.Value<string>("Status") == "passing");
                    if (!serviceIsOk)
                    {
                        continue;
                    }

                    var port = info.SelectToken("Service.Port").Value<string>();
                    var address = info.SelectToken("Service.Address").Value<string>();
                    if (string.IsNullOrEmpty(address))
                    {
                        address = info.SelectToken("Node.Address").Value<string>();
                    }

                    var serviceInfo = new ServiceInfo
                                      {
                                          Address = address,
                                          Port = port,
                                          ServiceName = serviceName
                                      };
                    return serviceInfo;
                }

                return null;
            }
        }

    }
}
