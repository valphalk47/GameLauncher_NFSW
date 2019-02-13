﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using GameLauncher.App;
using GameLauncher.App.Classes;
using GameLauncher.App.Classes.Logger;
using GameLauncher.HashPassword;
using GameLauncherReborn;
using SharpRaven;
using SharpRaven.Data;
//using Memes;

namespace GameLauncher {
    internal static class Program {
        [STAThread]
        internal static void Main() {

            File.Delete("log.txt");

            Log.StartLogging();

            Log.Debug("Setting up current directory: " + Path.GetDirectoryName(Application.ExecutablePath));
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Application.ExecutablePath));

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);

            Form SplashScreen2 = null;

            Log.Debug("Checking current directory");

            if (Self.isTempFolder(Directory.GetCurrentDirectory())) {
                MessageBox.Show(null, "Please, extract me and my DLL files before executing...", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                Environment.Exit(0);
            }

			if(!File.Exists("GameLauncherUpdater.exe")) {
				try {
                    //Better update async
                    WebClientWithTimeout UpdaterExecutable = new WebClientWithTimeout();
                    UpdaterExecutable.DownloadDataAsync(new Uri(Self.mainserver + "/files/GameLauncherUpdater.exe"));
                    UpdaterExecutable.DownloadDataCompleted += (sender, e) => {
                        try {
                            if (!e.Cancelled && e.Error == null) {
                                File.WriteAllBytes("GameLauncherUpdater.exe", e.Result);
                            }
                        } catch { /* ignored */ }
                    };
                } catch { /* ignored */ }
            }

            if (!File.Exists("servers.json")) {
                try {
                    File.WriteAllText("servers.json", "[]");
                } catch { /* ignored */ }
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            if (Debugger.IsAttached) {
                Log.Debug("Checking Proxy");
                ServerProxy.Instance.Start();
                Log.Debug("Starting MainScreen");
                Application.Run(new MainScreen(SplashScreen2));
            } else {
                if (NFSW.isNFSWRunning()) {
                    MessageBox.Show(null, "An instance of Need for Speed: World is already running", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Process.GetProcessById(Process.GetCurrentProcess().Id).Kill();
                }

                var mutex = new Mutex(false, "GameLauncherNFSW-MeTonaTOR");
                try {
                    if (mutex.WaitOne(0, false)) {
                        string[] files = {
                            "SharpRaven.dll - 9D96F7DB1F3A986A15FA6B9DA3731FECE72914A0",
                            "Flurl.dll - 6B580E81409669BEA7FCD36627572396546D1BFD",
                            "Flurl.Http.dll - 8C4B785A6526846D79A7C3F183E1B3E16706DE7C",
                            "INIFileParser.dll - 18125861F0519CDF643560C0A988BF70C87D47B3",
                            "Microsoft.WindowsAPICodePack.dll - 085DACFCD1FFA398B795D096833D16367B0D2886",
                            "Microsoft.WindowsAPICodePack.Shell.dll - B45482A37B381DE2A0293B6BE48C4CDEF04AEBFF",
                            "Nancy.dll - 2BF2AE9E529F6689E3D65502114C01B62E4D5568",
                            "Nancy.Hosting.Self.dll - 5F448F8CBF12A9BE55C046351AABC146E2388BF0",
                            "Newtonsoft.Json.dll - 26C78DAD612AFF904F216F19F49089F84CC77EB8"

                        };

                        var missingfiles = new List<string>();

                        foreach (var file in files) {
                            var splitFileVersion = file.Split(new string[] { " - " }, StringSplitOptions.None);

                            if (!File.Exists(Directory.GetCurrentDirectory() + "\\" + splitFileVersion[0])) {
                                missingfiles.Add(splitFileVersion[0] + " - Not Found");
                            } else {
                                try { 
                                    var HashFile = SHA.HashFile(splitFileVersion[0]);

                                    if(HashFile == "") {
                                        missingfiles.Add(splitFileVersion[0] + " - Invalid File");
                                    } else { 
                                        if(Self.CheckArchitectureFile(splitFileVersion[0]) == false) {
                                            missingfiles.Add(splitFileVersion[0] + " - Wrong Architecture");
                                        } else { 
                                            if(HashFile != splitFileVersion[1]) {
                                                missingfiles.Add(splitFileVersion[0] + " - Invalid Hash");
                                            }
                                        }
                                    }
                                } catch {
                                    missingfiles.Add(splitFileVersion[0] + " - Invalid File");
                                }
                            }
                        }

                        if (missingfiles.Count != 0) {
                            var message = "Cannot launch GameLauncher. The following files are invalid:\n\n";

                            foreach (var file in missingfiles) {
                                message += "• " + file + "\n";
                            }

                            MessageBox.Show(null, message, "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        } else {
                            Log.Debug("Checking Proxy");
                            ServerProxy.Instance.Start();

                            Application.ThreadException += new ThreadExceptionEventHandler(ThreadExceptionEventHandler);
                            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionEventHandler);
                            
                            Log.Debug("Starting MainScreen");
                            Application.Run(new MainScreen(SplashScreen2));
                        }
                    } else {
                        MessageBox.Show(null, "An instance of the application is already running.", "GameLauncher", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                } finally {
                    mutex.Close();
                    mutex = null;
                }
            }
        }

        static void UnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs e) {
            Exception exception = (Exception)e.ExceptionObject;
            exception.Data.Add("BuildHash", SHA.HashFile(AppDomain.CurrentDomain.FriendlyName));

            var ravenClient = new RavenClient("https://12973f6fa1054f51a8e3a840e7dc021c@sentry.io/1325472");
            ravenClient.Capture(new SentryEvent(exception));

            using (ThreadExceptionDialog dialog = new ThreadExceptionDialog(exception)) {
                dialog.ShowDialog();
            }

            Application.Exit();
            Environment.Exit(0);
        }

        static void ThreadExceptionEventHandler(object sender, ThreadExceptionEventArgs e) {
            Exception exception = (Exception)e.Exception;
            exception.Data.Add("BuildHash", SHA.HashFile(AppDomain.CurrentDomain.FriendlyName));

            var ravenClient = new RavenClient("https://12973f6fa1054f51a8e3a840e7dc021c@sentry.io/1325472");
            ravenClient.Capture(new SentryEvent(exception));

            using (ThreadExceptionDialog dialog = new ThreadExceptionDialog(exception)) {
                dialog.ShowDialog();
            }

            Application.Exit();
            Environment.Exit(0);
        }
    }
}
