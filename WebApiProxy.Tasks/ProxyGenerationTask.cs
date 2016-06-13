using System.IO;
using Newtonsoft.Json;
using WebApiProxy.Tasks.Infrastructure;
using WebApiProxy.Tasks.Models;
using WebApiProxy.Tasks.Templates;

namespace WebApiProxy.Tasks
{
    public class ProxyGenerationTask 
    {
        public ProxyGenerationTask(string rootPath)
        {
            Root = rootPath;
        }

        public string Root { get; set; }

        public void Generate()
        {
            var jsonConfigFile = Path.Combine(Root, Configuration.JsonConfigFileName);
            GenerateConfig jsonConfig;

            using (var sr = new StreamReader(jsonConfigFile))
            {
                jsonConfig = JsonConvert.DeserializeObject<GenerateConfig>(sr.ReadToEnd());
            }

            var oldConfigs = jsonConfig.TransformToOldConfig();
            foreach (var config in oldConfigs)
            {
                GenerateProxyFile(config);
            }

            var template = new ProxyBaseInfoTemplate(jsonConfig);
            var source = template.TransformText();
            File.WriteAllText(Path.Combine(Root, "ProxyBaseInfo") + ".cs", source);

        }

        private void GenerateProxyFile(Configuration config)
        {
            var csFilePath = Path.Combine(Root, config.Name) + ".cs";
            var generator = new CSharpGenerator(config);
            var source = generator.Generate();
            File.WriteAllText(csFilePath, source);
        }

    }
}