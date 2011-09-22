﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Ionic.Zip;
using Microsoft.Win32;
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
// 1: How to pause.
//      // TC: for testing
//      //Console.Write("Press any key to continue");
//      //Console.ReadLine();


namespace PatchTool
{
    public class Archiver
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private string _sourceDir;
        public string SourceDir
        {
            get { return _sourceDir; }
            set { _sourceDir = value; }
        }

        // This is no longer set per-application, so now we need a different name.  Envision maybe?  As in
        // Envision-10.1.10.0.exe
        private string _appName = "envision-installer";
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
            IConfig config = source.AddConfig("Sources");
            config.Set("srcRoot", @"C:\Source\builds\Aristotle");
            config.Set("webapps_version", @"10_1_10_82");

            // Q: Similar to makeTargetConfig(), which of these is the right way to go?
            // A: It'll have to be (2).  If we have a second file with the same name and different path, we'll need to
            //    append something (maybe "_1") to the end of the second file of the same name.  But we can't append
            //    a mangled file name to the path to identify the file.
            //
            // (1) config.Set("Envision.jar", @"${srcRoot}\Release");
            // (2) config.Set("Envision.jar", @"${srcRoot}\Release\Envision.jar");

            // from the working copy
            config.Set("AlvasAudio.dll", @"${srcRoot}\workdir\SharedResources\AlvasAudio.dll");
            config.Set("AlvasAudio.pdb", @"${srcRoot}\workdir\SharedResources\AlvasAudio.pdb");
            config.Set("AlvasAudio.tlb", @"${srcRoot}\workdir\SharedResources\AlvasAudio.tlb");
            config.Set("AlvasAudio.bat", @"${srcRoot}\config\chanmgr\AlvasAudio.bat");
            config.Set("audiocodesChannel.dll", @"${srcRoot}\workdir\ChannelManager\audiocodesChannel.dll");
            config.Set("audiocodesChannel.pdb", @"${srcRoot}\workdir\ChannelManager\audiocodesChannel.pdb");
            config.Set("AudioReader.dll", @"${srcRoot}\workdir\ChannelManager\AudioReader.dll");
            config.Set("AudioReader.pdb", @"${srcRoot}\workdir\ChannelManager\AudioReader.pdb");
            config.Set("AvayaVoipChannel.dll", @"${srcRoot}\workdir\ChannelManager\AvayaVoipChannel.dll");
            config.Set("AvayaVoipChannel.pdb", @"${srcRoot}\workdir\ChannelManager\AvayaVoipChannel.pdb");
            config.Set("centricity.dll", @"${srcRoot}\workdir\centricity\ET\bin\centricity.dll");
            config.Set("Centricity_BLL.dll", @"${srcRoot}\workdir\Centricity\ET\bin\Centricity_BLL.dll");
            config.Set("Centricity_DAL.dll", @"${srcRoot}\workdir\centricity\ET\bin\Centricity_DAL.dll");
            config.Set("ChanMgrSvc.exe", @"${srcRoot}\workdir\ChannelManager\ChanMgrSvc.exe");
            config.Set("ChanMgrSvc.pdb", @"${srcRoot}\workdir\ChannelManager\ChanMgrSvc.pdb");
            config.Set("ChannelBrokerService.xml", @"${srcRoot}\config\server\C2CServiceDescriptions\ChannelBrokerService.xml");
            config.Set("CiscoICM.dll", @"${srcRoot}\workdir\ContactSourceRunner\CiscoICM.dll");
            config.Set("CommonUpdates.xml", @"${srcRoot}\config\server\DatabaseUpdates\CommonUpdates.xml");
            config.Set("cstaLoader.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader.dll");
            config.Set("cstaLoader_1_2.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_1_2.dll");
            config.Set("cstaLoader_1_3_3.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_1_3_3.dll");
            config.Set("cstaLoader_3_33.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_3_33.dll");
            config.Set("cstaLoader_6_4_3.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_6_4_3.dll");
            config.Set("cstaLoader_9_1.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_9_1.dll");
            config.Set("cstaLoader_9_5.dll", @"${srcRoot}\workdir\ContactSourceRunner\cstaLoader_9_5.dll");
            config.Set("ctcLoader_6.0.dll", @"${srcRoot}\workdir\ContactSourceRunner\ctcLoader_6.0.dll");
            config.Set("ctcLoader_7.0.dll", @"${srcRoot}\workdir\ContactSourceRunner\ctcLoader_7.0.dll");
            config.Set("DBMigration_84SP9_To_10.sql", @"${srcRoot}\src\tools\DBMigration\v2\DBMigration_84SP9_To_10.sql");
            config.Set("DefaultEnvisionProfile.prx", @"${srcRoot}\src\winservices\WMWrapperService\DefaultEnvisionProfile.prx");
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
            config.Set("EditEvaluation.aspx", @"${srcRoot}\workdir\centricity\ET\PerformanceManagement\Evaluations\EditEvaluation.aspx");
            config.Set("EnvisionSR.bat", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\EnvisionSR.bat");
            config.Set("EnvisionSR.reg", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\EnvisionSR.reg");
            config.Set("Envision.jar", @"${srcRoot}\Release\Envision.jar");
            config.Set("envision_schema.xml", @"${srcRoot}\config\server\envision_schema.xml");
            config.Set("envision_schema_central.xml", @"${srcRoot}\config\server\envision_schema_central.xml");
            config.Set("EnvisionTheme.css", @"${srcRoot}\workdir\centricity\ET\App_Themes\EnvisionTheme\EnvisionTheme.css");
            config.Set("ETContactSource.exe", @"${srcRoot}\workdir\ContactSourceRunner\ETContactSource.exe");
            config.Set("ETContactSource.pdb", @"${srcRoot}\workdir\ContactSourceRunner\ETContactSource.pdb");
            config.Set("ETScheduleService.xml", @"${srcRoot}\config\server\C2CServiceDescriptions\ETScheduleService.xml");
            config.Set("GatewayLib.dll", @"${srcRoot}\workdir\SharedResources\GatewayLib.dll");
            config.Set("GatewayLib.pdb", @"${srcRoot}\workdir\SharedResources\GatewayLib.pdb");
            config.Set("GatewayLogging.xml", @"${srcRoot}\config\SIPGateway\GatewayLogging.xml");
            config.Set("instsrv.exe", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\instsrv.exe");
            config.Set("IPXChannel.dll", @"${srcRoot}\workdir\ChannelManager\IPXChannel.dll");
            config.Set("IPXChannel.pdb", @"${srcRoot}\workdir\ChannelManager\IPXChannel.pdb");
            config.Set("log4net.dll", @"${srcRoot}\workdir\SharedResources\log4net.dll");
            config.Set("LumiSoft.Net.dll", @"${srcRoot}\workdir\SharedResources\LumiSoft.Net.dll");
            config.Set("LumiSoft.Net.pdb", @"${srcRoot}\workdir\SharedResources\LumiSoft.Net.pdb");
            config.Set("LumiSoft.Net.xml", @"${srcRoot}\src\Components\LumiSoft_SIP_SDK\LumiSoft.Net.xml");
            config.Set("MSSQLUpdate_build_10.0.0303.1.xml", @"${srcRoot}\config\server\DatabaseUpdates\Common\10.0\MSSQLUpdate_build_10.0.0303.1.xml");
            config.Set("NetMerge.dll", @"${srcRoot}\workdir\ContactSourceRunner\NetMerge.dll");
            config.Set("NewEvaluation.aspx", @"${srcRoot}\workdir\centricity\ET\PerformanceManagement\Evaluations\NewEvaluation.aspx");
            config.Set("RadEditor.skin", @"${srcRoot}\workdir\centricity\ET\App_Themes\EnvisionTheme\RadEditor.skin");
            config.Set("RAL.dll", @"${srcRoot}\workdir\centricity\ET\bin\RAL.dll");
            config.Set("RtpTransmitter.dll", @"${srcRoot}\workdir\ChannelManager\RtpTransmitter.dll");
            config.Set("RtpTransmitter.pdb", @"${srcRoot}\workdir\ChannelManager\RtpTransmitter.pdb");
            config.Set("server.dll", @"${srcRoot}\workdir\SharedResources\server.dll");
            config.Set("server.pdb", @"${srcRoot}\workdir\SharedResources\server.pdb");
            config.Set("SIPChannel.dll", @"${srcRoot}\workdir\ChannelManager\SIPChannel.dll");
            config.Set("SIPChannel.pdb", @"${srcRoot}\workdir\ChannelManager\SIPChannel.pdb");
            config.Set("SIPChannelHpxMedia.dll", @"${srcRoot}\workdir\ChannelManager\SIPChannelHpxMedia.dll");
            config.Set("SIPChannelHpxMedia.pdb", @"${srcRoot}\workdir\ChannelManager\SIPChannelHpxMedia.pdb");
            config.Set("SIPConfigLib.dll", @"${srcRoot}\workdir\ChannelManager\SIPConfigLib.dll");
            config.Set("SIPConfigLib.pdb", @"${srcRoot}\workdir\ChannelManager\SIPConfigLib.pdb");
            config.Set("SIPGateway.exe", @"${srcRoot}\workdir\SIPGateway\SIPGateway.exe");
            config.Set("SIPGateway.exe.config", @"${srcRoot}\workdir\SIPGateway\SIPGateway.exe.config");
            config.Set("SIPGateway.pdb", @"${srcRoot}\workdir\SIPGateway\SIPGateway.pdb");
            config.Set("SIPPhone.dll", @"${srcRoot}\workdir\ChannelManager\SIPPhone.dll");
            config.Set("SIPPhone.pdb", @"${srcRoot}\workdir\ChannelManager\SIPPhone.pdb");
            config.Set("SIPWrapperLib.dll", @"${srcRoot}\workdir\ChannelManager\SIPWrapperLib.dll");
            config.Set("SIPWrapperLib.pdb", @"${srcRoot}\workdir\ChannelManager\SIPWrapperLib.pdb");
            config.Set("SIPWrapperLib.tlb", @"${srcRoot}\workdir\ChannelManager\SIPWrapperLib.tlb");
            config.Set("sleep.exe", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\sleep.exe");
            config.Set("SourceRunnerService.exe", @"${srcRoot}\workdir\ContactSourceRunner\SourceRunnerService.exe");
            config.Set("SourceRunnerService.pdb", @"${srcRoot}\workdir\ContactSourceRunner\SourceRunnerService.pdb");
            config.Set("srvany.exe", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\srvany.exe");
            config.Set("svcmgr.exe", @"${srcRoot}\src\tools\Scripts\ChannelManager\EnvisionSR\svcmgr.exe");
            config.Set("TeliaCallGuide.dll", @"${srcRoot}\workdir\ContactSourceRunner\TeliaCallGuide.dll");
            config.Set("Tsapi.dll", @"${srcRoot}\workdir\ContactSourceRunner\Tsapi.dll");

            // AVPlayer
            config.Set("AVPlayer.application", @"${srcRoot}\workdir\AVPlayer\AVPlayer.application");
            config.Set("AgentSupport.exe.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\AgentSupport.exe.deploy");
            config.Set("AVPlayer.exe.config.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\AVPlayer.exe.config.deploy");
            config.Set("AVPlayer.exe.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\AVPlayer.exe.deploy");
            config.Set("AVPlayer.exe.manifest", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\AVPlayer.exe.manifest");
            config.Set("CentricityApp.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\CentricityApp.dll.deploy");
            config.Set("hasp_windows.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\hasp_windows.dll.deploy");
            config.Set("Interop.WMPLib.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\Interop.WMPLib.dll.deploy");
            config.Set("log4net.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\log4net.dll.deploy");
            config.Set("nativeServiceWin32.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\nativeServiceWin32.dll.deploy");
            config.Set("server.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\server.dll.deploy");
            config.Set("SharedResources.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\SharedResources.dll.deploy");
            config.Set("ISource.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\_ISource.dll.deploy");
            config.Set("AVPlayer.resources.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\de\AVPlayer.resources.dll.deploy");
            config.Set("AVPlayer.resources.dll.deploy_1", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\es\AVPlayer.resources.dll.deploy");
            config.Set("CentricityApp.resources.dll.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\de\CentricityApp.resources.dll.deploy");
            config.Set("CentricityApp.resources.dll.deploy_1", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\es\CentricityApp.resources.dll.deploy");
            config.Set("AVPlayerIcon.ico.deploy", @"${srcRoot}\workdir\AVPlayer\Application Files\AVPlayer_${webapps_version}\Resources\AVPlayerIcon.ico.deploy");

            // RecordingDownloadTool
            config.Set("RecordingDownloadTool.application", @"${srcRoot}\workdir\RecordingDownloadTool\RecordingDownloadTool.application");
            config.Set("CentricityApp.dll.deploy_1", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\CentricityApp.dll.deploy");
            config.Set("log4net.dll.deploy_1", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\log4net.dll.deploy");
            config.Set("RecordingDownloadTool.exe.config.deploy", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\RecordingDownloadTool.exe.config.deploy");
            config.Set("RecordingDownloadTool.exe.deploy", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\RecordingDownloadTool.exe.deploy");
            config.Set("RecordingDownloadTool.exe.manifest", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\RecordingDownloadTool.exe.manifest");
            config.Set("server.dll.deploy_1", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\server.dll.deploy");
            config.Set("sox.exe.deploy", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\sox.exe.deploy");
            config.Set("CentricityApp.resources.dll.deploy_2", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\de\CentricityApp.resources.dll.deploy");
            config.Set("CentricityApp.resources.dll.deploy_3", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\es\CentricityApp.resources.dll.deploy");
            config.Set("RecordingDownloadTool.resources.dll.deploy", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\de\RecordingDownloadTool.resources.dll.deploy");
            config.Set("RecordingDownloadTool.resources.dll.deploy_1", @"${srcRoot}\workdir\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\es\RecordingDownloadTool.resources.dll.deploy");

            // from %ETSDK%
            try
            {
                string gacutil = Path.Combine(Environment.GetEnvironmentVariable("ETSDK"), @"Microsoft.NET\v3.5\gacutil.exe");
                config.Set("gacutil.exe", gacutil);
                string regasm = Path.Combine(Environment.GetEnvironmentVariable("ETSDK"), @"Microsoft.NET\v2.0\regasm.exe");
                config.Set("regasm.exe", regasm);
            }
            catch (ArgumentNullException)
            {
                logger.Fatal("Please set %ETSDK% and try again");
            }

            source.ExpandKeyValues();
            source.Save("Aristotle_sources.config");
        }

        // Target config is where the files are installed on each application.  At the moment we patch Server,
        // ChannelManager, Tools and WMWrapperService.  All four should be more-or-less represented in the targets
        // listed here.  The list will grow as files are added, but I won't try to include them all up front.
        public void makeTargetConfig()
        {
            IniConfigSource source = new IniConfigSource();

            // Each patchableApp (see Clyde.cs) needs its own config section and appRoot.
            IConfig server = source.AddConfig("Server");
            server.Set("serverRoot", @".");

            server.Set("centricity.dll", @"${serverRoot}\bin\centricity.dll");
            server.Set("Centricity_BLL.dll", @"${serverRoot}\bin\Centricity_BLL.dll");
            server.Set("Centricity_DAL.dll", @"${serverRoot}\bin\Centricity_DAL.dll");
            server.Set("ChannelBrokerService.xml", @"${serverRoot}\C2CServiceDescriptions\ChannelBrokerService.xml");
            server.Set("CiscoICM.dll", @"${serverRoot}\ContactSourceRunner\CiscoICM.dll");
            server.Set("CommonUpdates.xml", @"${serverRoot}\DatabaseUpdates\CommonUpdates.xml");
            server.Set("cstaLoader.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader.dll");
            server.Set("cstaLoader_1_2.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_1_2.dll");
            server.Set("cstaLoader_1_3_3.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_1_3_3.dll");
            server.Set("cstaLoader_3_33.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_3_33.dll");
            server.Set("cstaLoader_6_4_3.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_6_4_3.dll");
            server.Set("cstaLoader_9_1.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_3_33.dll");
            server.Set("cstaLoader_9_5.dll", @"${serverRoot}\ContactSourceRunner\cstaLoader_9_5.dll");
            server.Set("ctcLoader_6.0.dll", @"${serverRoot}\ContactSourceRunner\ctcLoader_6.0.dll");
            server.Set("ctcLoader_7.0.dll", @"${serverRoot}\ContactSourceRunner\ctcLoader_7.0.dll");
            server.Set("EditEvaluation.aspx", @"${serverRoot}\PerformanceManagement\Evaluations\EditEvaluation.aspx");
            // Note how we configure multiple copies of the same file on the same app
            server.Set("Envision.jar", @"${serverRoot}\Envision.jar|${serverRoot}\WebServer\webapps\ET\WEB-INF\lib\Envision.jar|${serverRoot}\wwwroot\EnvisionComponents\Envision.jar");
            server.Set("envision_schema.xml", @"${serverRoot}\envision_schema.xml");
            server.Set("envision_schema_central.xml", @"${serverRoot}\envision_schema_central.xml");
            server.Set("EnvisionTheme.css", @"${serverRoot}\App_Themes\EnvisionTheme\EnvisionTheme.css");
            server.Set("ETContactSource.exe", @"${serverRoot}\ContactSourceRunner\ETContactSource.exe");
            server.Set("ETContactSource.pdb", @"${serverRoot}\ContactSourceRunner\ETContactSource.pdb");
            server.Set("ETScheduleService.xml", @"${serverRoot}\C2CServiceDescriptions\ETScheduleService.xml");
            server.Set("MSSQLUpdate_build_10.0.0303.1.xml", @"${serverRoot}\DatabaseUpdates\Common\10.0\MSSQLUpdate_build_10.0.0303.1.xml");
            server.Set("NetMerge.dll", @"${serverRoot}\ContactSourceRunner\NetMerge.dll");
            server.Set("NewEvaluation.aspx", @"${serverRoot}\PerformanceManagement\Evaluations\NewEvaluation.aspx");
            server.Set("RadEditor.skin", @"${serverRoot}\App_Themes\EnvisionTheme\RadEditor.skin");
            server.Set("RAL.dll", @"${serverRoot}\bin\RAL.dll");
            server.Set("SourceRunnerService.exe", @"${serverRoot}\ContactSourceRunner\SourceRunnerService.exe");
            server.Set("SourceRunnerService.pdb", @"${serverRoot}\ContactSourceRunner\SourceRunnerService.pdb");
            server.Set("TeliaCallGuide.dll", @"${serverRoot}\ContactSourceRunner\TeliaCallGuide.dll");
            server.Set("Tsapi.dll", @"${serverRoot}\ContactSourceRunner\Tsapi.dll");


            IConfig cm = source.AddConfig("ChannelManager");
            cm.Set("cmRoot", @".");

            // Should probably stash the AlvasAudio.dll.  It needs to be registered in the GAC.
            cm.Set("AlvasAudio.dll", @"${cmRoot}\AlvasAudio.dll");
            cm.Set("AlvasAudio.bat", @"${cmRoot}\AlvasAudio.bat");
            cm.Set("gacutil.exe", @"${cmRoot}\gacutil.exe");
            cm.Set("regasm.exe", @"${cmRoot}\regasm.exe");

            cm.Set("AlvasAudio.pdb", @"${cmRoot}\AlvasAudio.pdb");
            cm.Set("AlvasAudio.tlb", @"${cmRoot}\AlvasAudio.tlb");
            cm.Set("audiocodesChannel.dll", @"${cmRoot}\audiocodesChannel.dll");
            cm.Set("audiocodesChannel.pdb", @"${cmRoot}\audiocodesChannel.pdb");
            cm.Set("AudioReader.dll", @"${cmRoot}\AudioReader.dll");
            cm.Set("AudioReader.pdb", @"${cmRoot}\AudioReader.pdb");
            cm.Set("AvayaVoipChannel.dll", @"${cmRoot}\AvayaVoipChannel.dll");
            cm.Set("AvayaVoipChannel.pdb", @"${cmRoot}\AvayaVoipChannel.pdb");
            cm.Set("ChanMgrSvc.exe", @"${cmRoot}\ChanMgrSvc.exe");
            cm.Set("ChanMgrSvc.pdb", @"${cmRoot}\ChanMgrSvc.pdb");
            cm.Set("DemoModeChannel.dll", @"${cmRoot}\DemoModeChannel.dll");
            cm.Set("DemoModeChannel.pdb", @"${cmRoot}\DemoModeChannel.pdb");
            cm.Set("DialogicChannel.dll", @"${cmRoot}\DialogicChannel.dll");
            cm.Set("DialogicChannel.pdb", @"${cmRoot}\DialogicChannel.pdb");
            cm.Set("DialogicChannel60.dll", @"${cmRoot}\DialogicChannel60.dll");
            cm.Set("DialogicChannel60.pdb", @"${cmRoot}\DialogicChannel60.pdb");
            cm.Set("DMCCConfigLib.dll", @"${cmRoot}\DMCCConfigLib.dll");
            cm.Set("DMCCConfigLib.pdb", @"${cmRoot}\DMCCConfigLib.pdb");
            cm.Set("DMCCWrapperLib.dll", @"${cmRoot}\DMCCWrapperLib.dll");
            cm.Set("DMCCWrapperLib.pdb", @"${cmRoot}\DMCCWrapperLib.pdb");
            cm.Set("DMCCWrapperLib.tlb", @"${cmRoot}\DMCCWrapperLib.tlb");
            cm.Set("IPXChannel.dll", @"${cmRoot}\IPXChannel.dll");
            cm.Set("IPXChannel.pdb", @"${cmRoot}\IPXChannel.pdb");
            cm.Set("LumiSoft.Net.dll", @"${cmRoot}\LumiSoft.Net.dll");
            cm.Set("LumiSoft.Net.pdb", @"${cmRoot}\LumiSoft.Net.pdb");
            cm.Set("RtpTransmitter.dll", @"${cmRoot}\RtpTransmitter.dll");
            cm.Set("RtpTransmitter.pdb", @"${cmRoot}\RtpTransmitter.pdb");
            cm.Set("server.dll", @"${cmRoot}\server.dll");
            cm.Set("server.pdb", @"${cmRoot}\server.pdb");
            cm.Set("SIPChannel.dll", @"${cmRoot}\SIPChannel.dll");
            cm.Set("SIPChannel.pdb", @"${cmRoot}\SIPChannel.pdb");
            cm.Set("SIPChannelHpxMedia.dll", @"${cmRoot}\SIPChannelHpxMedia.dll");
            cm.Set("SIPChannelHpxMedia.pdb", @"${cmRoot}\SIPChannelHpxMedia.pdb");
            cm.Set("SIPConfigLib.dll", @"${cmRoot}\SIPConfigLib.dll");
            cm.Set("SIPConfigLib.pdb", @"${cmRoot}\SIPConfigLib.pdb");
            cm.Set("SIPPhone.dll", @"${cmRoot}\SIPPhone.dll");
            cm.Set("SIPPhone.pdb", @"${cmRoot}\SIPPhone.pdb");
            cm.Set("SIPWrapperLib.dll", @"${cmRoot}\SIPWrapperLib.dll");
            cm.Set("SIPWrapperLib.pdb", @"${cmRoot}\SIPWrapperLib.pdb");
            cm.Set("SIPWrapperLib.tlb", @"${cmRoot}\SIPWrapperLib.tlb");

            // EnvisionSR
            cm.Set("EnvisionSR.bat", @"${cmRoot}\EnvisionSR\EnvisionSR.bat");
            cm.Set("EnvisionSR.reg", @"${cmRoot}\EnvisionSR\EnvisionSR.reg");
            cm.Set("instsrv.exe", @"${cmRoot}\EnvisionSR\instsrv.exe");
            cm.Set("sleep.exe", @"${cmRoot}\EnvisionSR\sleep.exe");
            cm.Set("srvany.exe", @"${cmRoot}\EnvisionSR\srvany.exe");
            cm.Set("svcmgr.exe", @"${cmRoot}\EnvisionSR\svcmgr.exe");

            // SIPGateway
            cm.Set("GatewayLib.dll", @"${cmRoot}\SIPGateway\GatewayLib.dll");
            cm.Set("GatewayLib.pdb", @"${cmRoot}\SIPGateway\GatewayLib.pdb");
            cm.Set("GatewayLogging.xml", @"${cmRoot}\SIPGateway\GatewayLogging.xml");
            cm.Set("LumiSoft.Net.dll", @"${cmRoot}\SIPGateway\LumiSoft.Net.dll");
            cm.Set("LumiSoft.Net.xml", @"${cmRoot}\SIPGateway\LumiSoft.Net.xml");
            cm.Set("LumiSoft.Net.pdb", @"${cmRoot}\SIPGateway\LumiSoft.Net.pdb");
            cm.Set("log4net.dll", @"${cmRoot}\SIPGateway\log4net.dll");
            cm.Set("server.dll", @"${cmRoot}\SIPGateway\server.dll");
            cm.Set("server.pdb", @"${cmRoot}\SIPGateway\server.pdb");
            cm.Set("SIPGateway.exe", @"${cmRoot}\SIPGateway\SIPGateway.exe");
            cm.Set("SIPGateway.exe.config", @"${cmRoot}\SIPGateway\SIPGateway.exe.config");
            cm.Set("SIPGateway.pdb", @"${cmRoot}\SIPGateway\SIPGateway.pdb");


            IConfig wmws = source.AddConfig("WMWrapperService");
            wmws.Set("wmwsRoot", @".");
            wmws.Set("DefaultEnvisionProfile.prx", @"${wmwsRoot}\DefaultEnvisionProfile.prx");


            IConfig tools = source.AddConfig("Tools");
            tools.Set("toolsRoot", @".");
            tools.Set("DBMigration_84SP9_To_10.sql", @"${toolsRoot}\DBMigration\DBMigration_84SP9_To_10.sql");


            IConfig webapps = source.AddConfig("WebApps");
            webapps.Set("webappsRoot", @".");
            webapps.Set("webapps_version", @"10_1_10_82");

            // AVPlayer
            //    "AVPlayer.application", "AgentSupport.exe.deploy",
            //    "AVPlayer.exe.config.deploy", "AVPlayer.exe.deploy", "AVPlayer.exe.manifest",
            //    "CentricityApp.dll.deploy", "hasp_windows.dll.deploy", "Interop.WMPLib.dll.deploy",
            //    "log4net.dll.deploy", "nativeServiceWin32.dll.deploy",
            //    "server.dll.deploy", "SharedResources.dll.deploy", "ISource.dll.deploy",
            //    "AVPlayer.resources.dll.deploy", "AVPlayer.resources.dll.deploy_1",
            //    "CentricityApp.resources.dll.deploy", "CentricityApp.resources.dll.deploy_1",
            //    "AVPlayerIcon.ico.deploy",
            //
            // RecordingDownloadTool
            //    "RecordingDownloadTool.application", "CentricityApp.dll.deploy_1", "log4net.dll.deploy_1",
            //    "RecordingDownloadTool.exe.config.deploy", "RecordingDownloadTool.exe.deploy",
            //    "RecordingDownloadTool.exe.manifest", "server.dll.deploy_1", "sox.exe.deploy",
            //    "CentricityApp.resources.dll.deploy_2", "CentricityApp.resources.dll.deploy_3",
            //    "RecordingDownloadTool.resources.dll.deploy", "RecordingDownloadTool.resources.dll.deploy_1",

            // AVPlayer
            webapps.Set("AVPlayer.application", @"${webappsRoot}\AVPlayer\AVPlayer.application");
            webapps.Set("AgentSupport.exe.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\AgentSupport.exe.deploy");
            webapps.Set("AVPlayer.exe.config.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\AVPlayer.exe.config.deploy");
            webapps.Set("AVPlayer.exe.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\AVPlayer.exe.deploy");
            webapps.Set("AVPlayer.exe.manifest", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\AVPlayer.exe.manifest");
            webapps.Set("CentricityApp.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\CentricityApp.dll.deploy");
            webapps.Set("hasp_windows.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\hasp_windows.dll.deploy");
            webapps.Set("Interop.WMPLib.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\Interop.WMPLib.dll.deploy");
            webapps.Set("log4net.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\log4net.dll.deploy");
            webapps.Set("nativeServiceWin32.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\nativeServiceWin32.dll.deploy");
            webapps.Set("server.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\server.dll.deploy");
            webapps.Set("SharedResources.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\SharedResources.dll.deploy");
            webapps.Set("ISource.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\_ISource.dll.deploy");
            webapps.Set("AVPlayer.resources.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\de\AVPlayer.resources.dll.deploy");
            webapps.Set("AVPlayer.resources.dll.deploy_1", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\es\AVPlayer.resources.dll.deploy");
            webapps.Set("CentricityApp.resources.dll.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\de\CentricityApp.resources.dll.deploy");
            webapps.Set("CentricityApp.resources.dll.deploy_1", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\es\CentricityApp.resources.dll.deploy");
            webapps.Set("AVPlayerIcon.ico.deploy", @"${webappsRoot}\AVPlayer\Application Files\AVPlayer_${webapps_version}\Resources\AVPlayerIcon.ico.deploy");

            // RecordingDownloadTool
            webapps.Set("RecordingDownloadTool.application", @"${webappsRoot}\RecordingDownloadTool\RecordingDownloadTool.application");
            webapps.Set("CentricityApp.dll.deploy_1", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\CentricityApp.dll.deploy");
            webapps.Set("log4net.dll.deploy_1", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\log4net.dll.deploy");
            webapps.Set("RecordingDownloadTool.exe.config.deploy", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\RecordingDownloadTool.exe.config.deploy");
            webapps.Set("RecordingDownloadTool.exe.deploy", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\RecordingDownloadTool.exe.deploy");
            webapps.Set("RecordingDownloadTool.exe.manifest", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\RecordingDownloadTool.exe.manifest");
            webapps.Set("server.dll.deploy_1", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\server.dll.deploy");
            webapps.Set("sox.exe.deploy", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\sox.exe.deploy");
            webapps.Set("CentricityApp.resources.dll.deploy_2", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\de\CentricityApp.resources.dll.deploy");
            webapps.Set("CentricityApp.resources.dll.deploy_3", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\es\CentricityApp.resources.dll.deploy");
            webapps.Set("RecordingDownloadTool.resources.dll.deploy", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\de\RecordingDownloadTool.resources.dll.deploy");
            webapps.Set("RecordingDownloadTool.resources.dll.deploy_1", @"${webappsRoot}\RecordingDownloadTool\Application Files\RecordingDownloadTool_${webapps_version}\es\RecordingDownloadTool.resources.dll.deploy");

            source.ExpandKeyValues();
            source.Save("Aristotle_targets.config");
        }

        // Each application passes in a list of keys that identifies files to patch.  Walk over the list and copy each
        // source file to it's destination.
        //
        // Depends: appKeys, sourceConfig and targetConfig all have to use the same key names.
        public void makePortablePatch(string appToPatch, IEnumerable<string> appKeys)
        {
            // Make the roots.  Sometimes they'll be empty--fix it later.
            DirectoryInfo di = new DirectoryInfo(Path.Combine(PatchVersion, appToPatch));
            if (di.Exists == false)
            {
                di.Create();
            }
            else
            {
                logger.Error("directory already exists: {0}", di.FullName);
                Environment.Exit(1);
            }

            IConfigSource sourceConfig = new IniConfigSource("Aristotle_sources.config");
            IConfigSource targetConfig = new IniConfigSource("Aristotle_targets.config");
            foreach (string key in appKeys)
            {
                // Try all patchableApps, but skip if their appKeys are empty.  This lets me update the patch lists
                // in PacMan.cs without messing around in here.  Alternate approach is try to fetch the key, log
                // warning when not found.
                if (key.Length == 0)
                {
                    break;
                }
                else
                {
                    // e.g., C:\Source\builds\Aristotle\src\tools\Scripts\ChannelManager\EnvisionSR\svcmgr.exe
                    string source = sourceConfig.Configs["Sources"].Get(key);

                    // May not look like it, but it's missing the app name in front
                    // e.g., .\ChannelManager\EnvisionSR\svcmgr.exe
                    string[] targets = targetConfig.Configs[appToPatch].Get(key).Split('|');

                    foreach (string t in targets)
                    {
                        // full paths to each file being added to the patch
                        string fqTargetPath = Path.GetFullPath(Path.Combine(di.ToString(), t));

                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(fqTargetPath));
                            // File.Copy throws many exceptions ...
                            File.Copy(source, fqTargetPath);
                        }
                        catch (Exception e)
                        {
                            if (e is FileNotFoundException || e is DirectoryNotFoundException)
                            {
                                logger.Error("not found: {0}", source);
                            }
                        }
                    }
                }
            }
        }

        public void run()
        {
            logger.Info("Making archive [what's it called?]");

            using (ZipFile zip = new ZipFile())
            {
                if (Directory.Exists(SourceDir))
                {
                    zip.AddDirectory(SourceDir, Path.GetFileName(SourceDir));
                }
                else
                {
                    // logger?
                    Console.WriteLine("{0} is not a valid directory", SourceDir);
                    Environment.Exit(1);
                }

                // these files install and log the patch
                zip.AddFile("Clyde.exe");
                zip.AddFile("PatchLib.dll");
                zip.AddFile("CommandLine.dll");
                zip.AddFile("Nini.dll");
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

        // This should be equivalent to ExtractDir in Archiver.  I should probably find a better solution.
        private string _extractDir = Directory.GetCurrentDirectory();
        public string ExtractDir
        {
            get { return _extractDir; }
        }

        // Should this return bool for success or failure?
        //
        // NB: may need "C:\patches\d7699dbd-8214-458e-adb0-8317dfbfaab1>runas /env /user:administrator Clyde.exe"
        public void run(string _srcDir, string _dstDir, bool replaceAll=false)
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
                catch (UnauthorizedAccessException)
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
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("PatchTool must be run as Administrator on this system", "sorry Charlie");
                    throw;
                }
            }

            FileInfo[] srcFiles = srcDir.GetFiles("*", SearchOption.AllDirectories);

            // Sometimes (like with Centricity Web Apps) we replace all files.  Keep it simple.
            if (replaceAll == true)
            {
                // 1: MOVE everything in dstDir to dstDir/patches/old
                logger.Info("copying {0} to {1}", dstDir.ToString(), oldPathStr);
                //MoveFolderContents(dstDir.ToString(), backupDirOld.ToString());
                Console.WriteLine(Path.Combine(dstDir.ToString(), "AVPlayer"), Path.Combine(backupDirOld.ToString(), "AVPlayer"));
                Console.WriteLine(Path.Combine(dstDir.ToString(), "RecordingDownloadTool"), Path.Combine(backupDirOld.ToString(), "RecordingDownloadTool"));
                Console.WriteLine(backupDirOld.ToString());
                Directory.Move(Path.Combine(dstDir.ToString(), "AVPlayer"), Path.Combine(backupDirOld.ToString(), "AVPlayer"));
                Directory.Move(Path.Combine(dstDir.ToString(), "RecordingDownloadTool"), Path.Combine(backupDirOld.ToString(), "RecordingDownloadTool"));

                // 2: copy everything in srcDir to srcDir/patches/new
                logger.Info("moving {0} to {1}", srcDir.ToString(), newPathStr);
                CopyFolder(srcDir.ToString(), backupDirNew.ToString());

                // 3: copy everything in srcDir to dstDir
                logger.Info("copying {0} to {1}", srcDir.ToString(), dstDir.ToString());
                CopyFolder(srcDir.ToString(), dstDir.ToString());

                return;
            }

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

            // TODO log this instead of, or in addition to the big ugly console

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

            // TODO log this instead of, or in addition to the big ugly console

            //
            // 2: copy the same files from dstDir to backupDirOld
            //
            // TC: want an INFO message here, describing what's going on (verifying that all files to be replaced are
            // found on the system).
            //Console.WriteLine("INFO: Are all files to be replaced present on the system?  The files in APPDIR");
            //Console.WriteLine("      should match the files in the patch");

            foreach (FileInfo f in srcFiles)
            {
                tail = RelativePath(srcDir.ToString(), f.FullName);
                bakFileOld = Path.GetFullPath(Path.Combine(backupDirOld.ToString(), tail));

                // Get and check original location; eventually this will be a milestone: if the file is missing, user
                // may want to cancel
                fileToPatch = Path.GetFullPath(Path.Combine(dstDir.ToString(), tail));

                // Create any nested subdirectories included in the patch.  Note, this will loop over the same
                // location multiple times; it's a little big ugly
                DirectoryInfo backupSubdirOld = new DirectoryInfo(Path.GetDirectoryName(bakFileOld.ToString()));
                if (!Directory.Exists(backupSubdirOld.ToString()))
                {
                    Directory.CreateDirectory(backupSubdirOld.ToString());
                }

                try
                {
                    File.Copy(fileToPatch, bakFileOld, true);
                }
                catch (Exception e)
                {
                    // DirectoryNotFoundException occurs when the patch includes a new directory that is not on the
                    // machine being patched.  As a result, the directory is also not in patches/VERSION/old, which
                    // causes this exception.  Log it but don't rethrow.
                    if (e is FileNotFoundException || e is DirectoryNotFoundException)
                    {
                        logger.Warn("a file to backup was not found: {0}", bakFileOld);
                    }
                }
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

            // TODO log this instead of, or in addition to the big ugly console

            //
            // 3: apply the patch.
            logger.Info("patching {0}", dstDir.ToString());
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

        // 
        public IDictionary<string, string> getInstalledApps(IEnumerable<string> patchableApps)
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
                catch (Exception e)
                {
                    if (e is NullReferenceException || e is ArgumentException)
                    {
                        logger.Info("InstallPath not found for {0}", pApps.Current);
                    }
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

        // dup dup
        public string GetInstallLocation(string appName)
        {
            string keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            RegistryKey key = Registry.CurrentUser.OpenSubKey(keyName);
            try
            {
                foreach (String a in key.GetSubKeyNames())
                {
                    RegistryKey subkey = key.OpenSubKey(a);
                    try
                    {
                        if (subkey.GetValue("DisplayName").ToString() == appName)
                        {
                            return subkey.GetValue("InstallLocation").ToString();
                        }
                    }
                    catch (NullReferenceException) { }
                }
            }
            catch (NullReferenceException) { }

            keyName = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            key = Registry.LocalMachine.OpenSubKey(keyName);
            try
            {
                foreach (String a in key.GetSubKeyNames())
                {
                    RegistryKey subkey = key.OpenSubKey(a);
                    try
                    {
                        if (subkey.GetValue("DisplayName").ToString() == appName)
                        {
                            return subkey.GetValue("InstallLocation").ToString();
                        }
                    }
                    catch (NullReferenceException) { }
                }
            }
            catch (NullReferenceException) { }

            keyName = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall";
            key = Registry.LocalMachine.OpenSubKey(keyName);
            try
            {
                foreach (String a in key.GetSubKeyNames())
                {
                    RegistryKey subkey = key.OpenSubKey(a);
                    try
                    {
                        if (subkey.GetValue("DisplayName").ToString() == appName)
                        {
                            return subkey.GetValue("InstallLocation").ToString();
                        }
                    }
                    catch (NullReferenceException) { }
                }
            }
            catch (NullReferenceException) { }

            return "NONE";
        }

        // TC: probably want to return bool and not write to STDOUT
        private void FileCompare(string fileName1, string fileName2, string fileName3)
        {
            try
            {
                FileEquals(fileName1, fileName2);
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException || e is DirectoryNotFoundException)
                {
                    logger.Warn("WARN: a file to compare was not found: {0}", fileName2);
                    return;
                }
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
                catch (FileNotFoundException)
                {
                    logger.Warn("a file to replace was not found: {0}", file);
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