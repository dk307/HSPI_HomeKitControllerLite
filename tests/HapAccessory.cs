using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class HapAccessory : IDisposable
    {
        private readonly Process process;

        public HapAccessory(string script, string args)
        {
            string codeBase = new Uri(typeof(HapAccessory).Assembly.CodeBase).LocalPath;
            string workingDirectory = Path.GetDirectoryName(codeBase);
            string scriptPath = Path.Combine(workingDirectory, "scripts", script);

            Assert.IsTrue(File.Exists(scriptPath));

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "python";
            start.Arguments = string.Format("\"{0}\" {1}", scriptPath, args);
            start.WorkingDirectory = workingDirectory;
            start.UseShellExecute = false;
            start.CreateNoWindow = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            this.process = Process.Start(start);
        }

        public void Dispose()
        {
            this.process?.Kill();
        }
    }
}