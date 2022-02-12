using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class HapAccessory : IDisposable
    {
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

        public void Dispose()
        {
            try
            {
                if (this.process != null)
                {
                    if (!this.process.HasExited) { this.process.Kill(); }
                    this.process.WaitForExit();
                    this.process.Dispose();
                }
            }
            catch (Exception)
            {
                //ignore all exceptions here
            }
        }

        public async Task WaitForSuccessStart(CancellationToken token)
        {
            CancellationTokenTaskSource<bool> cancellationTokenTaskSource = new(token);
            await Task.WhenAny(startedSuccessFully.Task, cancellationTokenTaskSource.Task).ConfigureAwait(false);
        }

        private void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            string data = e.Data;
            if (data != null)
            {
                output.AppendLine(data);
                Console.WriteLine(data);

                if (!startedSuccessFully.Task.IsCompleted)
                {
                    var match = startedRegEx.Match(data);
                    if (match.Success)
                    {
                        startedSuccessFully.SetResult(true);
                    }
                }
            }
        }

        private readonly StringBuilder output = new();

        private readonly Process process;

        private readonly Regex startedRegEx = new(@"^\s*\[accessory_driver\]\s*AccessoryDriver\s*for\s*\w+\s*started\ssuccessfully\s*$",
                                                                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly TaskCompletionSource<bool> startedSuccessFully = new();
    }
}