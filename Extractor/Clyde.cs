﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using CommandLine;
using CommandLine.Text;
using Microsoft.Win32;
using NLog;

namespace PatchTool
{
    class Clyde
    {
        private sealed class Options
        {
            #region Standard Option Attribute
            [Option("r", "patchVersion",
                    Required = true,
                    HelpText = "The version number for this patch.")]
            public string patchVersion = String.Empty;

            [HelpOption(
                    HelpText = "Display this help screen.")]

            public string GetUsage()
            {
                var help = new HelpText("Envision Package Manager");
                help.AdditionalNewLineAfterOption = true;
                help.Copyright = new CopyrightInfo("Envision Telephony, Inc.", 2011);
                help.AddPreOptionsLine("Usage: Clyde -r<patchVersion>");
                help.AddPreOptionsLine("       Clyde -?");
                help.AddOptions(this);

                return help;
            }
            #endregion
        }

        private static Logger logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            // Get the intersection of those applications which are patched with those which are installed.  For
            // example, if Server, ChannelManager and Tools are patched, but only Server and ChannelManager are
            // installed, then we don't patch Tools.  But it may be staged if it's easier to do it than not.

            IEnumerable<string> patchableApps = new List<string> { "Server", "ChannelManager", "WMWrapperService", "Tools" };
            IDictionary<string, string> installedApps = getInstalledApps(patchableApps);

            Extractor e = new Extractor();

            Options options = new Options();
            ICommandLineParser parser = new CommandLineParser(new CommandLineParserSettings(Console.Error));
            if (!parser.ParseArguments(args, options))
                Environment.Exit(1);

            if (options.patchVersion == String.Empty)
            {
                // "pretty it up" and exit
                throw new ArgumentException("something's broken! (options.patchVersion)");
            }
            else
            {
                e.PatchVersion = options.patchVersion;
            }

            foreach (string iApp in installedApps.Keys)
            {
                try
                {
                    string appDir = installedApps[iApp];
                    // it's ugly but I don't care right now
                    string srcDirRoot = Path.Combine(e.ExtractDir, e.PatchVersion);
                    e.run(Path.Combine(srcDirRoot, iApp), appDir);
                }
                catch (System.UnauthorizedAccessException)
                {
                    MessageBox.Show("Clyde must be run as Administrator on this system", "sorry Charlie");
                    throw;
                }

                // TC: for testing
                Console.Write("Press any key to continue");
                Console.ReadLine();
            }
        }

        private static IDictionary<string, string> getInstalledApps(IEnumerable<string> patchableApps)
        {
            IDictionary<string, string> installedApps = new Dictionary<string, string>();
            IEnumerator<string> pApps = patchableApps.GetEnumerator();

            while (pApps.MoveNext())
            {
                try
                {
                    // Create a new RegistryKey instance every time, or the value detection fails (don't know why)
                    string subKey = @"SOFTWARE\Envision\Click2Coach\" + pApps.Current;
                    RegistryKey rk = Registry.LocalMachine.OpenSubKey(subKey);

                    // Registry.GetValue() throws an ArgumentException if the value is not found.  It's an error if the
                    // "null" is passed to installedApps, but the method requires a default value.
                    string installPath = Registry.GetValue(rk.ToString(), "InstallPath", "null").ToString();
                    installedApps.Add(pApps.Current, installPath);

                    logger.Info("InstallPath found for {0}", pApps.Current);
                    rk.Close();
                }
                catch (NullReferenceException)
                {
                    logger.Info("InstallPath not found for {0}", pApps.Current);
                }
                catch (ArgumentException)
                {
                    logger.Info("InstallPath not found for {0}", pApps.Current);
                }
            }

            if (installedApps.Count == 0)
            {
                logger.Warn("No Envision applications were found on this machine!");
            }
            else
            {
                logger.Info("Found {0} Envision applications:", installedApps.Count);
                foreach (KeyValuePair<string, string> item in installedApps)
                {
                    logger.Info("{0} installed at \"{1}\"", item.Key, item.Value);
                }
            }
            return installedApps;
        }
    }
}
