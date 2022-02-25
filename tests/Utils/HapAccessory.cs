﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.AsyncEx;
using System;
using System.Diagnostics;
using System.IO;
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

            ProcessStartInfo start = new()
            {
                FileName = "python",
                Arguments = string.Format("-u \"{0}\" {1}", scriptPath, args),
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

        ~HapAccessory() => Dispose();

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
        }

        private void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            string data = e.Data;
            if (data != null)
            {
                Console.WriteLine(data);

                if (!startedSuccessFully.IsSet)
                {
                    var match = startedRegEx.Match(data);
                    if (match.Success)
                    {
                        startedSuccessFully.Set();
                    }
                }
            }
        }

        private readonly Process process;

        private readonly Regex startedRegEx = new(@"^\s*\[accessory_driver\]\s*AccessoryDriver\s*for\s*\w+\s*started\ssuccessfully\s*$",
                                                                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private readonly AsyncManualResetEvent startedSuccessFully = new();
    }
}