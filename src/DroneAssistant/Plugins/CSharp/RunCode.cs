using System.ComponentModel;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.CSharp.Scripting;


public sealed class RunCode
{
    [SKFunction, Description("CodeRun")]
    public async Task<string> CodeRun(
        [Description("The base C# code to run"), SKName("code")] string codeToRun)
    {
        var result = "OK";
        var options = ScriptOptions.Default
            .AddReferences(typeof(object).Assembly)
            .AddReferences(typeof(TelloSharp.Tello).Assembly)
            .AddImports("System", "System.IO", "System.Text", "System.Text.RegularExpressions");

        // convert the code to run to a valid string in unicode
        codeToRun = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(codeToRun));

        // validate that the code to run is not empty
        if (string.IsNullOrEmpty(codeToRun))
        {
            result = "Error: Code to run is empty";
            return result;
        }

        try
        {
            Console.WriteLine("===============================");
            Console.WriteLine(">> Code to run");
            Console.WriteLine("  ");
            Console.WriteLine(codeToRun);
            Console.WriteLine("  ");
            Console.WriteLine("===============================");
            Console.WriteLine("  ");
            Console.WriteLine("===============================");
            Console.WriteLine(">> Start running code ...");
            Console.WriteLine("");
            var t = await CSharpScript.RunAsync(codeToRun, options);
            Console.WriteLine("");
            Console.WriteLine("Running code done");
            Console.WriteLine("===============================");
        }
        catch (CompilationErrorException ex)
        {
            Console.WriteLine("===============================");
            Console.WriteLine(">> Compilation Error");
            Console.WriteLine("===============================");

            var sb = new StringBuilder();
            foreach (var err in ex.Diagnostics)
                sb.AppendLine(err.ToString());
            result = sb.ToString();
            Console.WriteLine(result);
            Console.WriteLine("===============================");
        }
        catch (Exception ex)
        {
            // Runtime Errors
            result = ex.ToString();
            Console.WriteLine("===============================");
            Console.WriteLine(">> Run time exception");
            Console.WriteLine("===============================");
            Console.WriteLine(result);
            Console.WriteLine("===============================");
        }

        return result;
    }
}