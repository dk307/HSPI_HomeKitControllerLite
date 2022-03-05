using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HSPI_HomeKitControllerTest
{
    internal sealed class PythonScriptWrapper : IDisposable
    {
        public PythonScriptWrapper(string scriptPath, string args)
        {
            Assert.IsTrue(File.Exists(scriptPath));

            ProcessStartInfo start = new()
            {
                FileName = "python",
                Arguments = string.Format("-u \"{0}\" {1}", scriptPath, args),
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
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

        ~PythonScriptWrapper() => Dispose();

        public void Dispose()
        {
            try
            {
                if (this.process != null)
                {
                    if (!this.process.HasExited)
                    {
                        Console.WriteLine("Killing Accessory process");
                        this.process.Kill();
                    }
                    this.process.WaitForExit(10000);
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
            await Task.WhenAny(startedSuccessFully.WaitAsync(token)).ConfigureAwait(false);
            if (!startSuccess)
            {
                throw new Exception("Failed to start accessory process");
            }
        }

        private void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            string data = e.Data;
            if (data != null)
            {
                Console.WriteLine(data);

                if (!startedSuccessFully.IsSet)
                {
                    foreach (var error in errorsInStart)
                    {
                        if (data.Contains(error))
                        {
                            startedSuccessFully.Set();
                            return;
                        }
                    }

                    var match = startedRegEx.Match(data);
                    if (match.Success)
                    {
                        startSuccess = true;
                        startedSuccessFully.Set();
                    }
                }
            }
        }

        private readonly string[] errorsInStart = new string[] { "error while attempting to bind on address" };
        private readonly Process process;

        private readonly Regex startedRegEx = new(@"^\s*\[accessory_driver\]\s*AccessoryDriver\s*for\s*\w+\s*started\ssuccessfully\s*$",
                                                                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly AsyncManualResetEvent startedSuccessFully = new();
        private bool startSuccess = false;
    }
}