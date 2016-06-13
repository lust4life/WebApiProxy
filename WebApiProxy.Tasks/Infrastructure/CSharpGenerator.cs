using System;
using System.Net.Http;
using WebApiProxy.Core.Models;
using WebApiProxy.Tasks.Models;
using WebApiProxy.Tasks.Templates;

namespace WebApiProxy.Tasks.Infrastructure
{
    public class CSharpGenerator
    {
        private readonly Configuration _config;

        public CSharpGenerator(Configuration config)
        {
            this._config = config;
        }

        public string Generate()
        {
            var metaData = GetProxy();
            var template = new CSharpProxyTemplate(_config, metaData);
            var source = template.TransformText();
            return source;
        }

        private Metadata GetProxy()
        {
            string metaDataUrl = _config.Endpoint;

            if (string.IsNullOrEmpty(metaDataUrl))
            {
                if (string.IsNullOrEmpty(_config.ConsulEndpointForGenerateFile))
                {
                    throw new Exception("请配置 consul url");
                }

                try
                {
                    if (_config.NotUseTraefik)
                    {
                        var serviceInfo = ConsulHelper.DiscoveryService(_config.ConsulEndpointForGenerateFile,
                                                                        _config.Name);
                        if (serviceInfo == null)
                        {
                            throw new Exception($"{_config.Name} 服务不正常");
                        }

                        metaDataUrl = $"http://{serviceInfo.Address}:{serviceInfo.Port}/";
                    }
                    else
                    {
                        var traefikInfo = ConsulHelper.DiscoveryService(_config.ConsulEndpointForGenerateFile,
                                                                        ConsulHelper.TraefikServiceName);

                        if (traefikInfo == null)
                        {
                            throw new Exception("traefik 服务不正常");
                        }

                        metaDataUrl = $"http://{traefikInfo.Address}:{traefikInfo.Port}/go-{_config.Name}/";
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("通过 consul 发现服务失败", ex);
                }
            }

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-Proxy-Type", "metadata");

                var response = client.GetAsync(metaDataUrl+"/api/proxies").Result;
                response.EnsureSuccessStatusCode();
                var metadata = response.Content.ReadAsAsync<Metadata>().Result;
                return metadata;
            }
        }

    }
}
