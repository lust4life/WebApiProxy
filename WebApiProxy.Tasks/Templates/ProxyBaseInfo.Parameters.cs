using WebApiProxy.Tasks.Models;


namespace WebApiProxy.Tasks.Templates
{
    public partial class ProxyBaseInfoTemplate
    {
        public GenerateConfig ConfigInfo { get; set; }

        public ProxyBaseInfoTemplate(GenerateConfig config)
        {
            this.ConfigInfo = config;
        }
	}
}
