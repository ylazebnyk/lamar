using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LamarCodeGeneration.Model;
using LamarCodeGeneration.Util;

namespace LamarCodeGeneration
{
    public class DynamicCodeBuilder
    {
        private readonly GenerationRules _rules;
        private readonly IServiceProvider _services;
        private readonly IGeneratesCode[] _generators;

        public DynamicCodeBuilder(GenerationRules rules, IServiceProvider services, IGeneratesCode[] generators)
        {
            _rules = rules;
            _services = services;
            _generators = generators;
        }

        public string GenerateAllCode()
        {
            var writer = new StringWriter();

            foreach (var generator in _generators)
            {
                var code = generateCode(generator);
                writer.WriteLine(code);
                writer.WriteLine();
                writer.WriteLine();
            }

            return writer.ToString();
        }

        public string GenerateCodeFor(string codeType)
        {
            var generator = _generators.FirstOrDefault(x => StringExtensions.EqualsIgnoreCase(x.CodeType, codeType));
            if (generator == null)
            {
                throw new ArgumentOutOfRangeException($"Unknown {nameof(codeType)} '{codeType}'. Known code types are {CodeTypes.Join(", ")}");
            }

            return generateCode(generator);
        }

        public void WriteGeneratedCode(Action<string> onFileWritten, string directory = null)
        {
            directory = directory ?? _rules.GeneratedCodeOutputPath.ToFullPath();


            foreach (var generator in _generators)
            {
                var code = generateCode(generator);
                var fileName = Path.Combine(directory, generator.CodeType.Replace(" ", "_") + ".cs");

                File.WriteAllText(fileName, code);
                onFileWritten(fileName);
            }
        }

        private string generateCode(IGeneratesCode generator)
        {
            var generatedAssembly = new GeneratedAssembly(_rules);
            var serviceVariables = generator.AssemblyTypes(_rules, generatedAssembly);

            var code = generatedAssembly.GenerateCode(serviceVariables);
            return code;
        }

        public void TryBuildAndCompileAll(Action<GeneratedAssembly, IServiceVariableSource> withAssembly)
        {
            foreach (var generator in _generators)
            {
                var generatedAssembly = new GeneratedAssembly(_rules);
                var serviceVariables = generator.AssemblyTypes(_rules, generatedAssembly);

                try
                {
                    withAssembly(generatedAssembly, serviceVariables);
                }
                catch (Exception e)
                {
                    throw new GeneratorCompilationFailureException(generator, e);
                }
            }
        }

        public void LoadPrebuiltTypes(Assembly assembly)
        {
            foreach (var generator in _generators)
            {
                generator.AttachPreBuiltTypes(assembly, _services);
            }
        }

        public string[] CodeTypes => _generators.Select(x => x.CodeType).ToArray();


        public void AttachAllCompiledTypes(IServiceProvider services)
        {
            foreach (var generatesCode in _generators)
            {
                generatesCode.AttachGeneratedTypes(services);
            }
        }
    }
}