using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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

            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = string.Format("\"{0}\" {1}", scriptPath, args),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };


            this.process = Process.Start(start);
            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        private void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            output.AppendLine(e.Data);
        }

        public void Dispose()
        {
            try
            {
                this.process?.Kill();
            }
            catch (Exception)
            { }
        }

        private readonly StringBuilder output = new();
    }
}