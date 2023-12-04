﻿using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Query;
using WindowsGSM.GameServer.Engine;
using System.IO;
using System.Linq;
using System.Net;



namespace WindowsGSM.Plugins
{
    public class ArkSurvivalAscended : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM.ArkSurvivalAscended", // WindowsGSM.XXXX
            author = "sh1ny",
            description = "WindowsGSM plugin for supporting ArkSurvivalAscended Dedicated Server with api",
            version = "1.21",
            url = "https://github.com/seyshell/WindowsGSM.ArkSurvivalAscended with api/", // Github repository link (Best practice)
            color = "#34c9eb" // Color Hex
        };

        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true; // As of 25.18, login is no longer needed. Source: https://discord.com/channels/729837326120910915/735188487615283232/1167603768091754537
        public override string AppId => "2430930"; // Game server appId

        // - Standard Constructor and properties
        public ArkSurvivalAscended(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public string Error, Notice;


        // - Game server Fixed variables
        public string StartPath = @"ShooterGame\Binaries\Win64\AsaApiLoader.exe"; // Game server start path
        public string FullName = "ArkSurvivalAscended Dedicated Server with ark api"; // Game server FullName
        public bool AllowsEmbedConsole = true;  // Does this server support output redirect?
        public int PortIncrements = 2; // This tells WindowsGSM how many ports should skip after installation
        public object QueryMethod = new A2S(); // Query method should be use on current server type. Accepted value: null or new A2S() or new FIVEM() or new UT3()


        // - Game server default values
        public string Port = "7777"; // Default port
        public string QueryPort = "27015"; // Default query port
        public string Defaultmap = "TheIsland_WP"; // Default map name
        public string Maxplayers = "40"; // Default maxplayers
        public string Additional = ""; // Additional server start parameter


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            //No config file seems
        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            var param = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(_serverData.ServerMap))
                param.Append($" {_serverData.ServerMap}");

            param.Append("?listen");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerName))
                param.Append($"?SessionName=\"\"\"{_serverData.ServerName}\"\"\"");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerIP))
                param.Append($"?MultiHome={_serverData.ServerIP}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerPort))
                param.Append($"?Port={_serverData.ServerPort}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerQueryPort))
                param.Append($"?QueryPort={_serverData.ServerQueryPort}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer))
                param.Append($"?MaxPlayers={_serverData.ServerMaxPlayer}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerParam))
                if(_serverData.ServerParam.StartsWith("?"))
                    param.Append($"{_serverData.ServerParam}");
                else if (_serverData.ServerParam.StartsWith("-"))
                    param.Append($" {_serverData.ServerParam}");

            if (!string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer))
                param.Append($" -WinLiveMaxPlayers={_serverData.ServerMaxPlayer}");

            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        FileName = shipExePath,
                        Arguments = param.ToString(),
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        FileName = shipExePath,
                        Arguments = param.ToString(),
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = false,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }

            return p;
        }


        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(async () =>
            {
                if (p.StartInfo.CreateNoWindow)
                {
                    p.CloseMainWindow();
                }
                else
                {
                    Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                    Functions.ServerConsole.SendWaitToMainWindow("quit");
                    Functions.ServerConsole.SendWaitToMainWindow("{ENTER}");
                    await Task.Delay(6000);
                }
            });
        }

        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId, true, loginAnonymous);
            Error = steamCMD.Error;

            return p;
        }

        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom, loginAnonymous: loginAnonymous);
            Error = error;
            return p;
        }

        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath));
        }

        public bool IsImportValid(string path)
        {
            string importPath = Path.Combine(path, StartPath);
            Error = $"Invalid Path! Fail to find {Path.GetFileName(StartPath)}";
            return File.Exists(importPath);
        }

        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }

    }
}
