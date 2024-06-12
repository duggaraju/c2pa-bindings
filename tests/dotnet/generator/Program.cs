﻿// See https://aka.ms/new-console-template for more information
using System.Diagnostics;
using CppSharp;
using CppSharp.AST;
using CppSharp.Generators;
using CppSharp.Passes;

var library = new Library();
ConsoleDriver.Run(library);
Console.WriteLine("Generated C# output!");

class Library : ILibrary
{
    public static string GetRootDirectory()
    {
        // use git rev-parse --show-toplevel to get the root directory
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --show-toplevel",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output.Trim();
    }

    public void Postprocess(Driver driver, ASTContext ctx)
    {
    }

    public void Preprocess(Driver driver, ASTContext ctx)
    {
    }

    public void Setup(Driver driver)
    {
        var baseDir = GetRootDirectory();
        Console.WriteLine("Base Dir is {0}", baseDir);
        var options = driver.Options;
        options.GeneratorKind = GeneratorKind.CSharp;
        options.Encoding = System.Text.Encoding.UTF8;
        options.OutputDir = Path.Combine(baseDir, "tests/dotnet/test");
        options.CheckSymbols = false;
        var module = options.AddModule("C2paBindings");        
        module.IncludeDirs.Add(Path.Combine(baseDir, "tests/c"));
        module.Headers.Add("c2pa.h");
        module.LibraryDirs.Add(Path.Combine(baseDir, "target/release"));
        module.Libraries.Add("c2pa_bindings.dll");
        module.OutputNamespace = "C2pa.Bindings";
    }

    public void SetupPasses(Driver driver)
    {
	    driver.Context.TranslationUnitPasses.RenameDeclsUpperCase(RenameTargets.Any);
	    //driver.Context.TranslationUnitPasses.AddPass(new FunctionToInstanceMethodPass());
    }
}