using System;
using System.IO;
using System.Text;
using Unity.Entities;
using UnityEngine;

namespace TollboothHighways.Utilities
{
    internal static class VehicleDebugLogger
    {
        private static readonly object _gate = new object();
        private static string _root;
        private static bool _initTried;

        public static void Init()
        {
            if (_initTried) return;
            _initTried = true;
            try
            {
                var modPath = Application.persistentDataPath + "/Logs";
                _root = Path.Combine(modPath ?? ".", "TollboothVehicleLogs");
                Directory.CreateDirectory(_root);
            }
            catch
            {
                _root = null;
            }
        }

        public static void Log(Entity vehicle, string message)
        {
            if (_root == null) return;
            try
            {
                var file = Path.Combine(_root, $"veh_{vehicle.Index}_{vehicle.Version}.log");
                var line = $"{DateTime.UtcNow:O} | {message}";
                lock (_gate)
                {
                    File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // swallow – debug only
            }
        }

        public static void LogOnce(string message)
        {
            if (_root == null) return;
            try
            {
                var file = Path.Combine(_root, $"_system.log");
                var line = $"{DateTime.UtcNow:O} | {message}";
                lock (_gate)
                {
                    File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }
}