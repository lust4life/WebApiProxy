using DocsByReflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Description;
using WebApiProxy.Core.Models;

namespace WebApiProxy.Server
{
    public class MetadataProvider
    {
        private readonly List<ModelDefinition> models;
        private readonly List<string> typesToIgnore = new List<string>();
        private readonly HttpConfiguration config;

        public MetadataProvider(HttpConfiguration config)
        {
            
            this.models = new List<ModelDefinition>();
            this.typesToIgnore = new List<string>();
            this.config = config;
        }

        public Metadata GetMetadata(HttpRequestMessage request)
        {
            var host = request.RequestUri.Scheme + "://" + request.RequestUri.Authority;
            var descriptions = config.Services.GetApiExplorer().ApiDescriptions;
            var documentationProvider = config.Services.GetDocumentationProvider();
           
            ILookup<HttpControllerDescriptor, ApiDescription> apiGroups = descriptions
                .Where(a => !a.ActionDescriptor.ControllerDescriptor.ControllerType.IsAbstract
                    && !a.RelativePath.Contains("Swagger")
                    && !a.RelativePath.Contains("docs"))
                .ToLookup(a => a.ActionDescriptor.ControllerDescriptor);

            var metadata = new Metadata
            {
                Definitions = from d in apiGroups
                              where !d.Key.ControllerType.IsExcluded()
                              select new ControllerDefinition
                              {
                                  Name = d.Key.ControllerName,
                                  Description = documentationProvider == null ? "" : documentationProvider.GetDocumentation(d.Key) ?? "",
                                  ActionMethods = from a in descriptions
                                                  where !a.ActionDescriptor.ControllerDescriptor.ControllerType.IsExcluded()
                                                  && !a.ActionDescriptor.IsExcluded()
                                                  && !a.RelativePath.Contains("Swagger")
                                                  && !a.RelativePath.Contains("docs")
                                                  && a.ActionDescriptor.ControllerDescriptor.ControllerName == d.Key.ControllerName
                                                  select new ActionMethodDefinition
                                                  {
                                                      Name = a.ActionDescriptor.ActionName,
                                                      BodyParameter = (from b in a.ParameterDescriptions
                                                                       where b.Source == ApiParameterSource.FromBody
                                                                       select new ParameterDefinition
                                                                       {
                                                                           Name = b.ParameterDescriptor.ParameterName,
                                                                           Type = ParseType(b.ParameterDescriptor.ParameterType),
                                                                           Description = b.Documentation ?? ""
                                                                       }).FirstOrDefault(),
                                                      UrlParameters = from b in a.ParameterDescriptions.Where(p => p.ParameterDescriptor != null)
                                                                      where b.Source == ApiParameterSource.FromUri
                                                                      select new ParameterDefinition
                                                                      {
                                                                          Name = b.ParameterDescriptor.ParameterName,
                                                                          Type = ParseType(b.ParameterDescriptor.ParameterType),
                                                                          Description = b.Documentation ?? "",
                                                                          IsOptional = b.ParameterDescriptor.IsOptional,
                                                                          DefaultValue = b.ParameterDescriptor.DefaultValue
                                                                      },
                                                      Url = a.RelativePath,

                                                      Description = a.Documentation ?? "",
                                                      ReturnType = ParseType(a.ResponseDescription.ResponseType ?? a.ResponseDescription.DeclaredType),
                                                      Type = a.HttpMethod.Method
                                                  }
                              },
                Models = models,
                Host = host
            };

            metadata.Definitions = metadata.Definitions.Distinct().OrderBy(d => d.Name);
            metadata.Models = metadata.Models.Distinct(new ModelDefinitionEqualityComparer()).OrderBy(d => d.Name);
            return metadata;

        }

        private string ParseType(Type type, ModelDefinition model = null)
        {
            string res;

            if (type == null)
                return "";

            // If the type is a generic type format to correct class name.
            if (type.IsGenericType)
            {
                res = GetGenericRepresentation(type, (t) => ParseType(t, model), model);

                AddModelDefinition(type);
            }
            else
            {
                if (type.ToString().StartsWith("System."))
                {
                    if (type.ToString().Equals("System.Void"))
                        res = "void";
                    else
                        res = type.Name;
                }
                else
                {
                    res = type.Name;

                    if (!type.IsGenericParameter)
                    {
                        AddModelDefinition(type);
                    }
                }
            }

            return res;
        }

        private string GetGenericRepresentation(Type type, Func<Type, string> getTypedParameterRepresentation, ModelDefinition model = null)
        {
            string res = type.Name;
            int index = res.IndexOf('`');
            if (index > -1)
                res = res.Substring(0, index);

            Type[] args = type.GetGenericArguments();

            res += "<";

            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0)
                    res += ", ";
                //Recursivly find nested arguments

                var arg = args[i];
                if (model != null && model.IsGenericArgument(arg.Name))
                {
                    res += model.GetGenericArgument(arg.Name);
                }
                else
                {
                    res += getTypedParameterRepresentation(arg);
                }
            }
            res += ">";
            return res;
        }

        private string GetGenericTypeDefineRepresentation(Type genericTypeDefClass)
        {

            string res = genericTypeDefClass.Name;
            int index = res.IndexOf('`');
            if (index > -1)
                res = res.Substring(0, index);

            Type[] args = genericTypeDefClass.GetGenericArguments();

            res += "<";

            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0)
                    res += ", ";

                var arg = args[i];
                res += arg.Name;
            }

            res += ">";
            return res;
        }



        private void AddModelDefinition(Type classToDef)
        {
            var documentationProvider = config.Services.GetDocumentationProvider();
            //When the class is an array redefine the classToDef as the array type
            if (classToDef.IsArray)
            {
                classToDef = classToDef.GetElementType();
            }
            // Is is not a .NET Framework generic, then add to the models collection.
            if (classToDef.Namespace.StartsWith("System", StringComparison.OrdinalIgnoreCase))
            {
                AddTypeToIgnore(classToDef.Name);
                return;
            }
            //If the class has not been mapped then map into metadata
            if (!typesToIgnore.Contains(classToDef.Name))
            {
                ModelDefinition model = new ModelDefinition();
                model.Name = classToDef.Name;
                model.Description = GetDescription(classToDef);
                if (classToDef.IsGenericType)
                {
                    model.Name = GetGenericTypeDefineRepresentation(classToDef.GetGenericTypeDefinition());
                }
                model.Type = classToDef.IsEnum ? "enum" : "class";
                model.CustomAttributes = GetCustomAttributes(classToDef);
                var constants = classToDef
                    .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                    .Where(f => f.IsLiteral && !f.IsInitOnly)
                    .ToList();
                model.Constants = from constant in constants
                                  select new ConstantDefinition
                                  {
                                      Name = constant.Name,
                                      Type = ParseType(constant.FieldType),
                                      Value = GetConstantValue(constant),
                                      Description = GetDescription(constant),
                                      CustomAttributes = GetCustomAttributes(constant)
                                  };
                var properties = classToDef.IsGenericType
                                     ? classToDef.GetGenericTypeDefinition().GetProperties()
                                     : classToDef.GetProperties();
            
                model.Properties = from property in properties
                                   select new ModelProperty
                                          {
                                              Name = property.Name,
                                              Type = ParseType(property.PropertyType, model),
                                              Description = GetDescription(property),
                                              CustomAttributes = GetCustomAttributes(property)
                                   };
                AddTypeToIgnore(model.Name);
                foreach (var p in properties)
                {
                    var type = p.PropertyType;

                    if (!models.Any(c => c.Name.Equals(type.Name)))// && !type.IsInterface)
                    {
                        ParseType(type);
                    }
                }
                models.Add(model);
            }
        }

        private void AddTypeToIgnore(string name)
        {
            if (!typesToIgnore.Contains(name))
            {
                typesToIgnore.Add(name);
            }
        }

        private static IEnumerable<string> GetCustomAttributes(Type type)
        {

            var result = new List<string>();
            if (type == (Type)null)
                throw new ArgumentNullException("type");
            var metadatas = type.GetCustomAttributesData();
            foreach (var attr in metadatas)
            {
                var sb = new StringBuilder();
                sb.Append("[");
                sb.Append(attr.AttributeType.FullName);
                sb.Append("(");
                HandleConstructorArguments(attr.ConstructorArguments, sb);
                if (attr.ConstructorArguments.Count > 0 && attr.NamedArguments.Count > 0)
                {
                    sb.Append(",");
                }
                HandleNamedArguments(attr.NamedArguments, sb);
                sb.Append(")]");

                result.Add(sb.ToString());
            }

            return result;
        }
        private static IEnumerable<string> GetCustomAttributes(MemberInfo memberInfo)
        {

            var result = new List<string>();
            if (memberInfo == (MemberInfo)null)
                throw new ArgumentNullException("memberInfo");
            var metadatas = memberInfo.GetCustomAttributesData();
            foreach (var attr in metadatas)
            {
                var sb = new StringBuilder();
                sb.Append("[");
                sb.Append(attr.AttributeType.FullName);
                sb.Append("(");
                HandleConstructorArguments(attr.ConstructorArguments, sb);
                if (attr.ConstructorArguments.Count > 0 && attr.NamedArguments.Count > 0)
                {
                    sb.Append(",");
                }
                HandleNamedArguments(attr.NamedArguments, sb);
                sb.Append(")]");

                result.Add(sb.ToString());
            }

            return result;
        }
        private static void HandleConstructorArguments(IList<CustomAttributeTypedArgument> arguments, StringBuilder sb)
        {
            if (arguments.Count > 0)
            {
                var constructorValue = new List<string>();
                foreach (var argument in arguments)
                {
                    if (argument.ArgumentType.Name.Equals("Boolean", StringComparison.CurrentCultureIgnoreCase))
                    {
                        constructorValue.Add(string.Format("{0}", argument.Value.ToString().ToLower()));
                    }
                    else if (argument.ArgumentType.Name.Equals("String", StringComparison.CurrentCultureIgnoreCase))
                    {
                        constructorValue.Add(string.Format("@\"{0}\"", argument.Value));
                    }
                    else if (argument.ArgumentType.BaseType != null && argument.ArgumentType.BaseType.Name.Equals("Enum", StringComparison.CurrentCultureIgnoreCase))
                    {
                        constructorValue.Add(string.Format("({0}){1}", argument.ArgumentType.FullName, argument.Value));
                    }
                    else if (argument.ArgumentType.Name.Equals("Type", StringComparison.CurrentCultureIgnoreCase))
                    {
                        constructorValue.Add(string.Format("typeof({0})", argument.Value));
                    }
                    else
                    {
                        if (argument.ArgumentType.BaseType != null && !argument.ArgumentType.BaseType.Name.Equals("Class", StringComparison.CurrentCultureIgnoreCase))
                        {
                            //不支持Class 复杂类型
                            constructorValue.Add(string.Format("{0}", argument.Value));
                        }

                    }

                }
                sb.Append(string.Join(",", constructorValue));
            }
        }
        private static void HandleNamedArguments(IList<CustomAttributeNamedArgument> arguments, StringBuilder sb)
        {
            if (arguments.Count > 0)
            {
                var nameValue = new List<string>();
                foreach (var argument in arguments)
                {
                    if (argument.TypedValue.ArgumentType.Name.Equals("Boolean", StringComparison.CurrentCultureIgnoreCase))
                    {
                        nameValue.Add(string.Format("{0}={1}", argument.MemberInfo.Name, argument.TypedValue.Value.ToString().ToLower()));
                    }
                    else if (argument.TypedValue.ArgumentType.Name.Equals("String", StringComparison.CurrentCultureIgnoreCase))
                    {
                        nameValue.Add(string.Format("{0}=@\"{1}\"", argument.MemberInfo.Name, argument.TypedValue.Value));
                    }
                    else if (argument.TypedValue.ArgumentType.BaseType != null && argument.TypedValue.ArgumentType.BaseType.Name.Equals("Enum", StringComparison.CurrentCultureIgnoreCase))
                    {
                        nameValue.Add(string.Format("{0}=({1}){2}", argument.MemberInfo.Name, argument.TypedValue.ArgumentType.FullName, argument.TypedValue.Value));
                    }
                    else if (argument.TypedValue.ArgumentType.Name.Equals("Type", StringComparison.CurrentCultureIgnoreCase))
                    {
                        nameValue.Add(string.Format("{0}=typeof({1})", argument.MemberInfo.Name, argument.TypedValue.Value));
                    }
                    else
                    {
                        if (argument.TypedValue.ArgumentType.BaseType != null && !argument.TypedValue.ArgumentType.BaseType.Name.Equals("Class", StringComparison.CurrentCultureIgnoreCase))
                        {
                            //不支持Class 复杂类型
                            nameValue.Add(string.Format("{0}={1}", argument.MemberInfo.Name, argument.TypedValue.Value));
                        }

                    }

                }
                sb.Append(string.Join(",", nameValue));
            }
        }

        private string GetConstantValue(FieldInfo constant)
        {
            var value = constant.GetRawConstantValue().ToString();
            return value;
        }

        private static string GetDescription(MemberInfo member)
        {
            try
            {
                var xml = DocsService.GetXmlFromMember(member, false);
                if (xml != null)
                {
                    return xml.InnerText.Trim();
                }
            }
            catch (Exception)
            {
            }
            return String.Empty;
        }

        private static string GetDescription(Type type)
        {
            try
            {
                var xml = DocsService.GetXmlFromType(type, false);

                if (xml != null)
                {
                    return xml.InnerText.Trim();
                }
            }
            catch (Exception)
            {
            }
            return String.Empty;
        }
    }
}
