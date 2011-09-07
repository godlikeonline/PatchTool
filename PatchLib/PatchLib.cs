﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Ionic.Zip;
using Nini.Config;
using NLog;

// using DotNetZip library
// http://dotnetzip.codeplex.com/
// http://cheeso.members.winisp.net/DotNetZipHelp/html/d4648875-d41a-783b-d5f4-638df39ee413.htm
//
// TODO - maybe
// 1: look at ExtractExistingFileAction OverwriteSilently
//  http://cheeso.members.winisp.net/DotNetZipHelp/html/5443c4c0-6f74-9ae1-37fd-9a4ae936832d.htm
// 2: add rollback
// 3: add undo (like rollback only after the patch completes)
// 4: add continue / cancel "breakpoints"
// 5: add "list file contents" to the archives (e.g. APP-VER.exe)
// 6: add logging when creating archives

// NOTES
// 1: Extractor will have to maintain the list of registry keys for each application that is
//    patched.  But Archiver will need to pass a hint to let Extractor know which registry key to
//    query.
//
//    Late note: No - have the user run the patch from APPDIR for the target application instead.
//    One less dependency, a lot less hassle with 32- and 64-bit OS's.

namespace PatchTool
{
    public class Archiver
    {
        private string _sourceDir;
        public string SourceDir
        {
            get { return _sourceDir; }
            set { _sourceDir = value; }
        }

        // This is no longer set per-application, so now we need a better name.  EnvisionWidget was originally a throwaway.
        private string _appName = "PacMan";
        public string AppName
        {
            get { return _appName; }
            set { _appName = value; }
        }

        private string _patchVersion = "0.0.0.0";
        public string PatchVersion
        {
            get { return _patchVersion; }
            set { _patchVersion = value; }
        }

        private string _extractDir = ".";
        public string ExtractDir
        {
            get { return _extractDir; }
            set { _extractDir = value; }
        }

        // Source config is a list of where to find files on the Aristotle working copy.  It is independent of the
        // apps to patch.  Every file in makeTargetConfig (identified by key) must be here.
        public void makeSourceConfig()
        {
            IniConfigSource source = new IniConfigSource();
            IConfig config = source.AddConfig("PatchSources");
            config.Set("srcRoot", @"C:\Source\builds\Aristotle");

            // These files are not specific to any application.  Each one should have a unique key.  For future work,
            // it might make sense to bundle them together per application.  It might look like this:
            //
            // [ServerSources]
            // Envision.jar = ${srcRoot}\Release\Envision.jar
            // ...
            // [ServerTargets]
            // Envision.jar = ${serverRoot}\Envision.jar|${serverRoot}\WebServer\webapps\ET\WEB-INF\lib\Envision.jar|${serverRoot}\wwwroot\EnvisionComponents\Envision.jar
            // ...
            //
            // This duplicates some sources, which may lead to maintenance headaches.  If the source configs are kept
            // separate, there needs to be some way to associate the source with the target.

            // Could I say the file name keys must be the same on the source and targets?  What would this break?  If
            // it works, then gathering the files for a patch looks like this:
            //   getPatchFile("server", "Envision.jar");
            //
            // Something like this would be better:
            //   getPatchFile(server, Envision_jar);
            //
            // This would be better still, but it's not to be in C#:
            //   getPatchFile(:server, :Envision_jar)

            // Q: Similar to makeTargetConfig(), which of these is the right way to go?
            // A: It'll have to be (2).  If we have a second file with the same name and different path, we'll need to
            //    append something (maybe "_1") to the end of the second file of the same name.  But we can't append
            //    a mangled file name to the path to identify the file.
            //
            // (1) config.Set("Envision.jar", @"${srcRoot}\Release");
            // (2) config.Set("Envision.jar", @"${srcRoot}\Release\Envision.jar");

            config.Set("Envision.jar", @"${srcRoot}\Release\Envision.jar");
            config.Set("envision_schema.xml", @"${srcRoot}\config\server\envision_schema.xml");
            config.Set("envision_schema_central.xml", @"${srcRoot}\config\server\envision_schema_central.xml");
            config.Set("ETScheduleService.xml", @"${srcRoot}\config\server\C2CServiceDescriptions\ETScheduleService.xml");
            config.Set("ChannelBrokerService.xml", @"${srcRoot}\config\server\C2CServiceDescriptions\ChannelBrokerService.xml");
            config.Set("CiscoICM.dll", @"${srcRoot}\workdir\ContactSourceRunner\CiscoICM.dll");
            config.Set("cstaLoader.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader.dll");
            config.Set("cstaLoader_1_2.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_1_2.dll");
            config.Set("cstaLoader_1_3_3.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_1_3_3.dll");
            config.Set("cstaLoader_3_33.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_3_33.dll");
            config.Set("cstaLoader_9_1.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_9_1.dll");
            config.Set("cstaLoader_9_5.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_9_5.dll");
            config.Set("ctcapi32.dll", @"${srcRoot}\workdir\ContactSourceRunner\ctcapi32.dll");
            config.Set("ctcLoader_6.0.dll", @"${srcRoot}\workdir\ContactSourceRunner\ctcLoader_6.0.dll");
            config.Set("ctcLoader_7.0.dll", @"${srcRoot}\workdir\ContactSourceRunner\ctcLoader_7.0.dll");
            config.Set("NetMerge.dll", @"${srcRoot}\workdir\ContactSourceRunner\NetMerge.dll");
            config.Set("SourceRunnerService.exe", @"${srcRoot}\workdir\ContactSourceRunner\SourceRunnerService.exe");
            config.Set("TeliaCallGuide.dll", @"${srcRoot}\workdir\ContactSourceRunner\TeliaCallGuide.dll");
            config.Set("Tsapi.dll", @"${srcRoot}\workdir\ContactSourceRunner\Tsapi.dll");
            config.Set("CommonUpdates.xml", @"${srcRoot}\config\server\DatabaseUpdates\CommonUpdates.xml");
            config.Set("MSSQLUpdate_build_10.0.0303.1.xml", @"${srcRoot}\config\server\DatabaseUpdates\Common\10.0\MSSQLUpdate_build_10.0.0303.1.xml");
            config.Set("audiocodesChannel.dll", @"${srcRoot}\workdir\ChannelManager\audiocodesChannel.dll");
            config.Set("audiocodesChannel.pdb", @"${srcRoot}\workdir\ChannelManager\audiocodesChannel.pdb");
            config.Set("AvayaVoipChannel.dll", @"${srcRoot}\workdir\ChannelManager\AvayaVoipChannel.dll");
            config.Set("AvayaVoipChannel.pdb", @"${srcRoot}\workdir\ChannelManager\AvayaVoipChannel.pdb");
            config.Set("ChanMgrSvc.exe", @"${srcRoot}\workdir\ChannelManager\ChanMgrSvc.exe");
            config.Set("ChanMgrSvc.pdb", @"${srcRoot}\workdir\ChannelManager\ChanMgrSvc.pdb");
            config.Set("DemoModeChannel.dll", @"${srcRoot}\workdir\ChannelManager\DemoModeChannel.dll");
            config.Set("DemoModeChannel.pdb", @"${srcRoot}\workdir\ChannelManager\DemoModeChannel.pdb");
            config.Set("DialogicChannel.dll", @"${srcRoot}\workdir\ChannelManager\DialogicChannel.dll");
            config.Set("DialogicChannel.pdb", @"${srcRoot}\workdir\ChannelManager\DialogicChannel.pdb");
            config.Set("DialogicChannel60.dll", @"${srcRoot}\workdir\ChannelManager\DialogicChannel60.dll");
            config.Set("DialogicChannel60.pdb", @"${srcRoot}\workdir\ChannelManager\DialogicChannel60.pdb");
            config.Set("DMCCConfigLib.dll", @"${srcRoot}\workdir\ChannelManager\DMCCConfigLib.dll");
            config.Set("DMCCConfigLib.pdb", @"${srcRoot}\workdir\ChannelManager\DMCCConfigLib.pdb");
            config.Set("DMCCWrapperLib.dll", @"${srcRoot}\workdir\ChannelManager\DMCCWrapperLib.dll");
            config.Set("DMCCWrapperLib.pdb", @"${srcRoot}\workdir\ChannelManager\DMCCWrapperLib.pdb");
            config.Set("DMCCWrapperLib.tlb", @"${srcRoot}\workdir\ChannelManager\DMCCWrapperLib.tlb");
            config.Set("IPXChannel.dll", @"${srcRoot}\workdir\ChannelManager\IPXChannel.dll");
            config.Set("IPXChannel.pdb", @"${srcRoot}\workdir\ChannelManager\IPXChannel.pdb");
            config.Set("RtpTransmitter.dll", @"${srcRoot}\workdir\ChannelManager\RtpTransmitter.dll");
            config.Set("RtpTransmitter.pdb", @"${srcRoot}\workdir\ChannelManager\RtpTransmitter.pdb");
            config.Set("EnvisionSR.bat", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\EnvisionSR.bat");
            config.Set("EnvisionSR.reg", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\EnvisionSR.reg");
            config.Set("instsrv.exe", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\instsrv.exe");
            config.Set("sleep.exe", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\sleep.exe");
            config.Set("srvany.exe", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\srvany.exe");
            config.Set("svcmgr.exe", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\svcmgr.exe");
            config.Set("DefaultEnvisionProfile.prx", @"${srcRoot}\src\winservices\WMWrapperService\DefaultEnvisionProfile.prx");

            source.ExpandKeyValues();
            source.Save("Aristotle_source.config");
        }

        // These are the on-disk targets for each application.  At the moment we patch four applications: Server,
        // ChannelManager, WMWrapperService and Tools.  All four should be more-or-less represented in the targets
        // listed here.  The list will grow as files are added, but I won't try to include them all up front.
        public void makeTargetConfig()
        {
            IniConfigSource source = new IniConfigSource();

            // Each patchableApp (see Clyde.cs) needs its own config section and appRoot.
            IConfig server = source.AddConfig("Server");
            server.Set("serverRoot", @".");

            // Note how we configure multiple copies of the same file on the same app
            server.Set("Envision.jar", @"${serverRoot}\Envision.jar|${serverRoot}\WebServer\webapps\ET\WEB-INF\lib\Envision.jar|${serverRoot}\wwwroot\EnvisionComponents\Envision.jar");

            server.Set("envision_schema.xml", @"${serverRoot}\envision_schema.xml");
            server.Set("envision_schema_central.xml", @"${serverRoot}\envision_schema_central.xml");
            server.Set("ETScheduleService.xml", @"${serverRoot}\C2CServiceDescriptions\ETScheduleService.xml");
            server.Set("ChannelBrokerService.xml", @"${serverRoot}\C2CServiceDescriptions\ChannelBrokerService.xml");
            server.Set("CiscoICM.dll", @"${serverRoot}\ContactSourceRunner\CiscoICM.dll");
            server.Set("cstaLoader.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader.dll");
            server.Set("cstaLoader_1_2.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_1_2.dll");
            server.Set("cstaLoader_1_3_3.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_1_3_3.dll");
            server.Set("cstaLoader_3_33.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_3_33.dll");
            server.Set("cstaLoader_9_1.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_3_33.dll");
            server.Set("cstaLoader_9_5.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_9_5.dll");
            server.Set("ctcapi32.dll", @"${serverRoot}\ContactSourceRunner\ctcapi32.dll");
            server.Set("ctcLoader_6.0.dll", @"${serverRoot}\ContactSourceRunner\ctcLoader_6.0.dll");
            server.Set("ctcLoader_7.0.dll", @"${serverRoot}\ContactSourceRunner\ctcLoader_7.0.dll");
            server.Set("NetMerge.dll", @"${serverRoot}\ContactSourceRunner\NetMerge.dll");
            server.Set("SourceRunnerService.exe", @"${serverRoot}\ContactSourceRunner\SourceRunnerService.exe");
            server.Set("TeliaCallGuide.dll", @"${serverRoot}\ContactSourceRunner\TeliaCallGuide.dll");
            server.Set("Tsapi.dll", @"${serverRoot}\ContactSourceRunner\Tsapi.dll");
            server.Set("CommonUpdates.xml", @"${serverRoot}\DatabaseUpdates\CommonUpdates.xml");
            server.Set("MSSQLUpdate_build_10.0.0303.1.xml", @"${serverRoot}\DatabaseUpdates\Common\10.0\MSSQLUpdate_build_10.0.0303.1.xml");
            

            IConfig cm = source.AddConfig("ChannelManager");
            cm.Set("cmRoot", @".");

            cm.Set("audiocodesChannel.dll", @"${cmRoot}\ChannelManager\audiocodesChannel.dll");
            cm.Set("audiocodesChannel.pdb", @"${cmRoot}\ChannelManager\audiocodesChannel.pdb");
            cm.Set("AvayaVoipChannel.dll", @"${cmRoot}\ChannelManager\AvayaVoipChannel.dll");
            cm.Set("AvayaVoipChannel.pdb", @"${cmRoot}\ChannelManager\AvayaVoipChannel.pdb");
            cm.Set("ChanMgrSvc.exe", @"${cmRoot}\ChannelManager\ChanMgrSvc.exe");
            cm.Set("ChanMgrSvc.pdb", @"${cmRoot}\ChannelManager\ChanMgrSvc.pdb");
            cm.Set("DemoModeChannel.dll", @"${cmRoot}\ChannelManager\DemoModeChannel.dll");
            cm.Set("DemoModeChannel.pdb", @"${cmRoot}\ChannelManager\DemoModeChannel.pdb");
            cm.Set("DialogicChannel.dll", @"${cmRoot}\ChannelManager\DialogicChannel.dll");
            cm.Set("DialogicChannel.pdb", @"${cmRoot}\ChannelManager\DialogicChannel.pdb");
            cm.Set("DialogicChannel60.dll", @"${cmRoot}\ChannelManager\DialogicChannel60.dll");
            cm.Set("DialogicChannel60.pdb", @"${cmRoot}\ChannelManager\DialogicChannel60.pdb");
            cm.Set("DMCCConfigLib.dll", @"${cmRoot}\ChannelManager\DMCCConfigLib.dll");
            cm.Set("DMCCConfigLib.pdb", @"${cmRoot}\ChannelManager\DMCCConfigLib.pdb");
            cm.Set("DMCCWrapperLib.dll", @"${cmRoot}\ChannelManager\DMCCWrapperLib.dll");
            cm.Set("DMCCWrapperLib.pdb", @"${cmRoot}\ChannelManager\DMCCWrapperLib.pdb");
            cm.Set("DMCCWrapperLib.tlb", @"${cmRoot}\ChannelManager\DMCCWrapperLib.tlb");
            cm.Set("IPXChannel.dll", @"${cmRoot}\ChannelManager\IPXChannel.dll");
            cm.Set("IPXChannel.pdb", @"${cmRoot}\ChannelManager\IPXChannel.pdb");
            cm.Set("RtpTransmitter.dll", @"${cmRoot}\ChannelManager\RtpTransmitter.dll");
            cm.Set("RtpTransmitter.pdb", @"${cmRoot}\ChannelManager\RtpTransmitter.pdb");

            cm.Set("EnvisionSR.bat", @"${cmRoot}\ChannelManager\EnvisionSR\EnvisionSR.bat");
            cm.Set("EnvisionSR.reg", @"${cmRoot}\ChannelManager\EnvisionSR\EnvisionSR.reg");
            cm.Set("instsrv.exe", @"${cmRoot}\ChannelManager\EnvisionSR\instsrv.exe");
            cm.Set("sleep.exe", @"${cmRoot}\ChannelManager\EnvisionSR\sleep.exe");
            cm.Set("srvany.exe", @"${cmRoot}\ChannelManager\EnvisionSR\srvany.exe");
            cm.Set("svcmgr.exe", @"${cmRoot}\ChannelManager\EnvisionSR\svcmgr.exe");


            IConfig wmws = source.AddConfig("WMWrapperService");
            wmws.Set("wmwsRoot", @".");
            wmws.Set("DefaultEnvisionProfile.prx", @"${wmwsRoot}\DefaultEnvisionProfile.prx");
            

            IConfig tools = source.AddConfig("Tools");
            tools.Set("toolsRoot", @".");

            source.ExpandKeyValues();
            source.Save("Aristotle_targets.config");
        }

        // Get the list of files for a specific application and put them in a portable filesystem structure.
        // NB: patchFileKeys binds the source and target tables.  That may turn out to be a bad choice.
        //
        // This method will read the target config and the source config for appToPatch.
        public void makePortablePatch(string appToPatch, IEnumerable<string> patchFileKeys)
        {
            foreach (string pFile in patchFileKeys)
            {
                // var source, destination
                // source, destination = getPatchFile(
                Console.WriteLine(pFile);
            }
            //throw new NotImplementedException();
        }

        public bool getPatchFile(string source, string target)
        {
            // returns true if the file is found and copied to it's target location
            throw new NotImplementedException();
        }

        public void run()
        {
            using (ZipFile zip = new ZipFile())
            {
                // TC: watch out for unwanted appending to an existing archive (TEST)
                if (Directory.Exists(SourceDir))
                {
                    // How to identify multiple applications under one directory?  By "friendly name" (e.g. Envision Server Suite)?
                    // Use the names we search the registry with: Server, ChannelManager, WMWrapperService, Tools.
                    zip.AddDirectory(SourceDir, Path.GetFileName(SourceDir));
                }
                else
                {
                    Console.WriteLine("{0} is not a valid directory", SourceDir);
                    // NOT A BIG FAN of these types of side-effects
                    System.Environment.Exit(1);
                }

                // these files install and log the patch
                zip.AddFile("Clyde.exe");
                zip.AddFile("PatchLib.dll");
                zip.AddFile("CommandLine.dll");
                zip.AddFile("NLog.dll");
                zip.AddFile("NLog.config");

                SelfExtractorSaveOptions options = new SelfExtractorSaveOptions();
                options.Flavor = SelfExtractorFlavor.ConsoleApplication;
                options.ProductVersion = PatchVersion;
                options.DefaultExtractDirectory = ExtractDir;
                options.Copyright = "Copyright 2011 Envision Telephony";
                string commandLine = @"Clyde.exe --patchVersion=" + PatchVersion;
                options.PostExtractCommandLine = commandLine;
                // false for dev, (maybe) true for production
                options.RemoveUnpackedFilesAfterExecute = false;

                // TC: delete other patches before reusing file name!
                string patchName = AppName + @"-" + PatchVersion + @".exe";
                // debug
                Console.WriteLine("patchName: {0}", patchName);

                zip.SaveSelfExtractor(patchName, options);
            }
        }

        // TC: given name of ExistingZipFile (param), list the file's contents.  This
        //  method will be mostly useful, if a little self-referential (it could be called
        //  on itself! :)), in Extractor.
        //public void List()
        //{
        //    using (ZipFile zip = ZipFile.Read(ExistingZipFile))
        //    {
        //        foreach (ZipEntry e in zip)
        //        {
        //            if (header)
        //            {
        //                System.Console.WriteLine("Zipfile: {0}", zip.Name);
        //                if ((zip.Comment != null) && (zip.Comment != ""))
        //                    System.Console.WriteLine("Comment: {0}", zip.Comment);
        //                System.Console.WriteLine("\n{1,-22} {2,8}  {3,5}   {4,8}  {5,3} {0}",
        //                                         "Filename", "Modified", "Size", "Ratio", "Packed", "pw?");
        //                System.Console.WriteLine(new System.String('-', 72));
        //                header = false;
        //            }
        //            System.Console.WriteLine("{1,-22} {2,8} {3,5:F0}%   {4,8}  {5,3} {0}",
        //                                     e.FileName,
        //                                     e.LastModified.ToString("yyyy-MM-dd HH:mm:ss"),
        //                                     e.UncompressedSize,
        //                                     e.CompressionRatio,
        //                                     e.CompressedSize,
        //                                     (e.UsesEncryption) ? "Y" : "N");
        //        }
        //    }
        //}
    }

    public class Extractor
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private void init()
        {
            Console.SetWindowSize(100, 50);
            //Console.SetWindowSize(140, 50);
        }

        public Extractor()
        {
            init();
        }

        public Extractor(string _patchVersion)
        {
            init();
            PatchVersion = _patchVersion;
        }

        private string _appDir;
        public string AppDir
        {
            get { return _appDir; }
            set { _appDir = value; }
        }

        private string _patchVersion;
        public string PatchVersion
        {
            get { return _patchVersion; }
            set { _patchVersion = value; }
        }

        // This should be equivalent to ExtractDir in Archiver.  I should probably find a better
        // solution.
        //
        // TC: constructor param?
        private string _extractDir = Directory.GetCurrentDirectory();
        public string ExtractDir
        {
            get { return _extractDir; }
        }

        // Are all files in srcDir also present in dstDir (extractDir and appDir, respectively)?
        // Should it return void or bool?
        //
        // NB: may need "C:\patches\d7699dbd-8214-458e-adb0-8317dfbfaab1>runas /env /user:administrator Clyde.exe"
        public void run(string _srcDir, string _dstDir)
        {
            // patch directory and local target
            DirectoryInfo srcDir = new DirectoryInfo(_srcDir);
            DirectoryInfo dstDir = new DirectoryInfo(_dstDir);

            // create backup folders
            string newPathStr = CombinePaths("patches", PatchVersion, "new");
            DirectoryInfo backupDirNew = new DirectoryInfo(Path.Combine(dstDir.ToString(), newPathStr));
            if (!Directory.Exists(backupDirNew.ToString()))
            {
                try
                {
                    Directory.CreateDirectory(backupDirNew.ToString());
                }
                catch (System.UnauthorizedAccessException)
                {
                    MessageBox.Show("PatchTool must be run as Administrator on this system", "sorry Charlie");
                    throw;
                }
            }
            //
            string oldPathStr = CombinePaths("patches", PatchVersion, "old");
            DirectoryInfo backupDirOld = new DirectoryInfo(Path.Combine(dstDir.ToString(), oldPathStr));
            if (!Directory.Exists(backupDirOld.ToString()))
            {
                try
                {
                    Directory.CreateDirectory(backupDirOld.ToString());
                }
                catch (System.UnauthorizedAccessException)
                {
                    MessageBox.Show("PatchTool must be run as Administrator on this system", "sorry Charlie");
                    throw;
                }
            }

            FileInfo[] srcFiles = srcDir.GetFiles("*", SearchOption.AllDirectories);

            // each file in the patch, with relative directories; base paths are the heads
            string tail;
            // each file to patch, full path
            string fileToPatch;
            // each file bound for the old/ directory
            string bakFileOld;

            // TC: three steps
            // 1: copy srcDir to backupDirNew
            //    (e.g. C:/patches/APPNAME/PATCHVER -> APPDIR/patches/10.1.0001.0/new/)
            // 2: copy the same files from dstDir to backupDirOld;
            //    (e.g., APPDIR/ -> APPDIR/patches/10.1.0001.0/old/)
            // 3: apply the patch.

            // 1: copy srcDir to backupDirNew
            CopyFolder(srcDir.ToString(), backupDirNew.ToString());
            Console.WriteLine("INFO: Did everything unzip okay?  The files in the new backup location [1]");
            Console.WriteLine("      should match the files in the extract dir [2]:");
            Console.WriteLine("\t[1] {0}", backupDirNew);
            Console.WriteLine("\t[2] {0}", ExtractDir);
            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                string origTmp = Path.Combine(srcDir.ToString(), Path.GetDirectoryName(tail));
                string orig = Path.Combine(origTmp, f.ToString());
                string copiedTmp = Path.Combine(backupDirNew.ToString(), Path.GetDirectoryName(tail));
                string copied = Path.Combine(copiedTmp, f.ToString());
                FileCompare(orig, copied, tail);
            }
            Console.WriteLine();

            //
            // 2: copy the same files from dstDir to backupDirOld
            //
            // TC: want an INFO message here, describing what's going on
            // (verifying that all files to be replaced are found on the system).
            //Console.WriteLine("INFO: Are all files to be replaced present on the system?  The files in APPDIR");
            //Console.WriteLine("      should match the files in the patch");

            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                bakFileOld = Path.GetFullPath(Path.Combine(backupDirOld.ToString(), tail));

                // Get and check original location; eventually this will be a milestone: if the
                // file is missing, user may want to cancel
                fileToPatch = Path.GetFullPath(Path.Combine(dstDir.ToString(), tail));
                // TC: commented out for now -- too noisy
                //FileStat(fileToPatch);

                // Create any nested subdirectories included in the patch.  Note, this will loop
                // over the same location multiple times; it's a little big ugly
                DirectoryInfo backupSubdirOld = new DirectoryInfo(Path.GetDirectoryName(bakFileOld.ToString()));
                if (!Directory.Exists(backupSubdirOld.ToString()))
                {
                    Directory.CreateDirectory(backupSubdirOld.ToString());
                }

                try
                {
                    File.Copy(fileToPatch, bakFileOld, true);
                }
                catch (System.IO.FileNotFoundException)
                {
                    Console.WriteLine("WARN: a file to backup was not found: {0}", bakFileOld);
                }
                catch (System.IO.DirectoryNotFoundException)
                {
                    // This exception occurs when the patch includes a new directory that is not
                    // on the machine being patched.  As a result, the directory is also not in
                    // patches/VERSION/old, which causes this exception.  Ignore it.
                }
                // TC: commented out for now -- too noisy
                //FileStat(bakFileOld);
                //Console.WriteLine();
            }

            Console.WriteLine("INFO: Did the backup succeed?  The files to replace in APPDIR [1]");
            Console.WriteLine("      should match the files in the old backup location [2]:");
            Console.WriteLine("\t[1] {0}", dstDir);
            Console.WriteLine("\t[2] {0}", backupDirOld);
            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                bakFileOld = Path.GetFullPath(Path.Combine(backupDirOld.ToString(), tail));
                fileToPatch = Path.GetFullPath(Path.Combine(dstDir.ToString(), tail));

                // Compare each file in old/ with the original in APPDIR.
                string orig = fileToPatch;
                string copied = bakFileOld;
                // TC: explain this
                FileCompare(orig, copied, tail);
            }
            Console.WriteLine();

            //
            // 3: apply the patch.
            Console.WriteLine("INFO: patching {0}", dstDir.ToString());
            Console.WriteLine();

            CopyFolder(srcDir.ToString(), dstDir.ToString());

            Console.WriteLine("INFO: Did the patch succeed?  The files in APPDIR [1] should match");
            Console.WriteLine("      the files in the new backup location [2]:");
            Console.WriteLine("\t[1] {0}", dstDir);
            Console.WriteLine("\t[2] {0}", backupDirNew);
            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                string origTmp = Path.Combine(srcDir.ToString(), Path.GetDirectoryName(tail));
                string orig = Path.Combine(origTmp, f.ToString());
                string copiedTmp = Path.Combine(dstDir.ToString(), Path.GetDirectoryName(tail));
                string copied = Path.Combine(copiedTmp, f.ToString());
                // TC: explain this
                FileCompare(orig, copied, tail);
            }
            Console.WriteLine();
        }

        // TC: probably want to return bool and not write to STDOUT
        private void FileCompare(string fileName1, string fileName2, string fileName3)
        {
            try
            {
                FileEquals(fileName1, fileName2);
            }
            catch (System.IO.FileNotFoundException)
            {
                Console.WriteLine("WARN: a file to compare was not found: {0}", fileName2);
                return;
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                Console.WriteLine("WARN: a file to compare was not found: {0}", fileName2);
                return;
            }

            if (FileEquals(fileName1, fileName2))
            {
                Console.Write("{0, -90}", "* " + fileName3, Console.WindowWidth, Console.WindowHeight);
                //Console.Write("{0, -130}", fileName3, Console.WindowWidth, Console.WindowHeight);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(String.Format("{0, 9}", "[matches]"), Console.WindowWidth, Console.WindowHeight);
                Console.ResetColor();
            }
            else
            {
                Console.Write("{0, -90}", "* " + fileName3, Console.WindowWidth, Console.WindowHeight);
                //Console.Write("{0, -130}", fileName3, Console.WindowWidth, Console.WindowHeight);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(String.Format("{0, 9}", "[nomatch]"), Console.WindowWidth, Console.WindowHeight);
                Console.ResetColor();
            }
        }

        private void FileStat(string fileName)
        {
            FileInfo fileInfo = new FileInfo(fileName);
            if (fileInfo.Exists)
            {
                Console.Write("{0, -130}", fileInfo, Console.WindowWidth, Console.WindowHeight);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(String.Format("{0, 9}", "[present]"), Console.WindowWidth, Console.WindowHeight);
                Console.ResetColor();
            }
            else
            {
                Console.Write("{0, -130}", fileInfo, Console.WindowWidth, Console.WindowHeight);
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(String.Format("{0, 9}", "[missing]"), Console.WindowWidth, Console.WindowHeight);
                Console.ResetColor();
            }
        }

        // http://stackoverflow.com/questions/968935/c-binary-file-compare
        static bool FileEquals(string fileName1, string fileName2)
        {
            // Check the file size and CRC equality here.. if they are equal...
            using (var file1 = new FileStream(fileName1, FileMode.Open))
            using (var file2 = new FileStream(fileName2, FileMode.Open))
                return StreamsContentsAreEqual(file1, file2);
        }

        private static bool StreamsContentsAreEqual(Stream stream1, Stream stream2)
        {
            const int bufferSize = 2048 * 2;
            var buffer1 = new byte[bufferSize];
            var buffer2 = new byte[bufferSize];

            while (true)
            {
                int count1 = stream1.Read(buffer1, 0, bufferSize);
                int count2 = stream2.Read(buffer2, 0, bufferSize);

                if (count1 != count2)
                {
                    return false;
                }

                if (count1 == 0)
                {
                    return true;
                }

                int iterations = (int)Math.Ceiling((double)count1 / sizeof(Int64));
                for (int i = 0; i < iterations; i++)
                {
                    if (BitConverter.ToInt64(buffer1, i * sizeof(Int64)) != BitConverter.ToInt64(buffer2, i * sizeof(Int64)))
                    {
                        return false;
                    }
                }
            }
        }

        // http://www.csharp411.com/c-copy-folder-recursively/
        public static void CopyFolder(string sourceFolder, string destFolder)
        {
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            string[] files = Directory.GetFiles(sourceFolder);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                string dest = Path.Combine(destFolder, name);
                try
                {
                    File.Copy(file, dest, true);
                }
                catch (System.IO.FileNotFoundException)
                {
                    Console.WriteLine("WARN: a file to replace was not found: {0}", file);
                }
            }
            string[] folders = Directory.GetDirectories(sourceFolder);
            foreach (string folder in folders)
            {
                string name = Path.GetFileName(folder);
                string dest = Path.Combine(destFolder, name);
                CopyFolder(folder, dest);
            }
        }

        // http://mrpmorris.blogspot.com/2007/05/convert-absolute-path-to-relative-path.html
        private string RelativePath(string absolutePath, string relativeTo)
        {
            string[] absoluteDirectories = absolutePath.Split('\\');
            string[] relativeDirectories = relativeTo.Split('\\');

            //Get the shortest of the two paths
            int length = absoluteDirectories.Length < relativeDirectories.Length ? absoluteDirectories.Length : relativeDirectories.Length;

            //Use to determine where in the loop we exited
            int lastCommonRoot = -1;
            int index;

            //Find common root
            for (index = 0; index < length; index++)
                if (absoluteDirectories[index] == relativeDirectories[index])
                    lastCommonRoot = index;
                else
                    break;

            //If we didn't find a common prefix then throw
            if (lastCommonRoot == -1)
                throw new ArgumentException("Paths do not have a common base");

            //Build up the relative path
            StringBuilder relativePath = new StringBuilder();

            //Add on the ..
            for (index = lastCommonRoot + 1; index < absoluteDirectories.Length; index++)
                if (absoluteDirectories[index].Length > 0)
                    relativePath.Append("..\\");

            //Add on the folders
            for (index = lastCommonRoot + 1; index < relativeDirectories.Length - 1; index++)
                relativePath.Append(relativeDirectories[index] + "\\");
            relativePath.Append(relativeDirectories[relativeDirectories.Length - 1]);

            return relativePath.ToString();
        }

        // http://stackoverflow.com/questions/144439/building-a-directory-string-from-component-parts-in-c
        string CombinePaths(params string[] parts)
        {
            string result = String.Empty;
            foreach (string s in parts)
            {
                result = Path.Combine(result, s);
            }
            return result;
        }
    }
}
