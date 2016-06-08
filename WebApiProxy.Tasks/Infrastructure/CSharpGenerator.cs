using System;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using WebApiProxy.Core.Models;
using WebApiProxy.Tasks.Models;
using WebApiProxy.Tasks.Templates;

namespace WebApiProxy.Tasks.Infrastructure
{
    public class CSharpGenerator
    {
        private readonly Configuration config;

        public CSharpGenerator(Configuration config)
        {
            this.config = config;
        }

        public string Generate()
        {
            var metaData = GetProxy();
            var template = new CSharpProxyTemplate(config, metaData);
            var source = template.TransformText();
            return source;
        }


        private Metadata GetProxy()
        {
            string metaDataUrl = config.Endpoint;

            if (string.IsNullOrEmpty(metaDataUrl))
            {
                if (string.IsNullOrEmpty(config.ConsulEndpoint))
                {
                    throw new Exception("请配置 consul url");
                }

                try
                {
                    metaDataUrl = DiscoveryServiceUrl(config.ConsulEndpoint, config.Name);
                }
                catch (Exception ex)
                {
                    throw new Exception("通过 consul 发现服务失败", ex);
                }
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Proxy-Type", "metadata");
                var response = client.GetAsync(metaDataUrl).Result;
                response.EnsureSuccessStatusCode();
                var metadata = response.Content.ReadAsAsync<Metadata>().Result;
                return metadata;
            }
        }

        private string DiscoveryServiceUrl(string consulEndpoint, string serviceName)
        {
            using (var client = new HttpClient())
            {
                var traefikUrl = string.Format("{0}/v1/health/service/traefik", consulEndpoint);

                var traefikInfo = client.GetAsync(traefikUrl).Result.Content.ReadAsAsync<JArray>().Result;
                foreach (var info in traefikInfo)
                {
                    var serviceIsOk = info["Checks"].Any(item => item.Value<string>("Name") == "traefik"
                                                         && item.Value<string>("Status") == "passing");
                    if (!serviceIsOk)
                    {
                        continue;
                    }

                    var port = info.Value<string>("Service.Port");
                    var address = info.Value<string>("Service.Address");
                    if (string.IsNullOrEmpty(address))
                    {
                        address = info.Value<string>("Node.Address");
                    }

                    return string.Format("http://{0}:{1}/go-{2}/", address, port, serviceName);
                }

                throw new Exception("traefik 服务不正常");
            }
        }

    }
}
