namespace WebApiProxy.Tasks.Models
{
    public class Configuration
    {
        public const string JsonConfigFileName = "WebApiProxy.json";

        private string _clientSuffix = "Client";

        public string ConsulEndpointForGenerateFile { get; set; }

        public bool NotUseTraefik { get; set; }

        public string ClientSuffix
        {
            get
            {
                return _clientSuffix.DefaultIfEmpty("Client");
            }
            set
            {
                _clientSuffix = value;
            }
        }

        public string Namespace { get; set; } = "WebApi.Proxies";

        public string Name { get; set; } = "MyWebApiProxy";

        public string Endpoint { get; set; }

        public bool GenerateAsyncReturnTypes { get; set; }

    }

}