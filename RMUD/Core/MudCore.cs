﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Reflection;

namespace RMUD
{
    public static partial class Mud
    {
        internal static List<Client> ConnectedClients = new List<Client>();
        internal static Mutex DatabaseLock = new Mutex();
        private static bool ShuttingDown = false;

        internal static Settings SettingsObject;
        internal static ProscriptionList ProscriptionList;

        public static void ClientDisconnected(Client client)
        {
            DatabaseLock.WaitOne();
            RemoveClientFromAllChannels(client);
            ConnectedClients.Remove(client);
            if (client.Player != null)
            {
                client.Player.ConnectedClient = null;
                MudObject.Move(client.Player, null);
            }
            DatabaseLock.ReleaseMutex();
        }

        public enum ClientAcceptanceStatus
        {
            Accepted,
            Rejected,
        }

        public static ClientAcceptanceStatus ClientConnected(Client Client)
        {
            var ban = ProscriptionList.IsBanned(Client.IPString);
            if (ban.Banned)
            {
                LogError("Rejected connection from " + Client.IPString + ". Matched ban " + ban.SourceBan.Glob + " Reason: " + ban.SourceBan.Reason);
                return ClientAcceptanceStatus.Rejected;
            }

            DatabaseLock.WaitOne();

            Client.CommandHandler = LoginCommandHandler;

            var settings = GetObject("settings", s => Mud.SendMessage(Client, s + "\r\n")) as Settings;
            Mud.SendMessage(Client, settings.Banner);
            Mud.SendMessage(Client, settings.MessageOfTheDay);

            ConnectedClients.Add(Client);

            SendPendingMessages();

            DatabaseLock.ReleaseMutex();

            return ClientAcceptanceStatus.Accepted;
        }

        public static bool Start(String basePath)
        {
            try
            {
                InitializeDatabase(basePath);
                var settings = GetObject("settings") as Settings;
                if (settings == null) throw new InvalidProgramException("No settings object is defined in the database!");
                SettingsObject = settings;

                ProscriptionList = new ProscriptionList(settings.ProscriptionList);

                InitializeCommandProcessor();

                if (settings.UpfrontCompilation)
                {
                    var databaseObjectFiles = new List<String>();
                    EnumerateDatabase("", true, s => databaseObjectFiles.Add(s));

                    Console.WriteLine("Found {0} database objects to load.", databaseObjectFiles.Count);
                    var start = DateTime.Now;
                    foreach (var objectFile in databaseObjectFiles)
                    {
                        GetObject(objectFile);
                    }

                    UpdateMarkedObjects();

                    Console.WriteLine("Total compilation in {0}.", DateTime.Now - start);
                }

                Console.WriteLine("Engine ready with path " + basePath + ".");
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to start mud engine.");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                throw e;
            }
            return true;
        }

        public static void Shutdown()
        {
            ShuttingDown = true;
        }
    }
}
