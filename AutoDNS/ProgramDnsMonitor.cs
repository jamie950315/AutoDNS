using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AutoDNS
{
    public class ProgramDnsRule
    {
        public string ProgramPath { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
    }

    public sealed class ProgramDnsMonitor : IDisposable
    {
        private readonly MainForm form;
        private readonly List<ProgramDnsRule> rules;
        private readonly Timer timer;
        private DnsProfile? currentProfile;

        public ProgramDnsMonitor(MainForm form)
        {
            this.form = form;
            rules = LoadRules();
            timer = new Timer(CheckRules, null, 0, 5000);
        }

        private static List<ProgramDnsRule> LoadRules()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "programs.json");
                if (!File.Exists(path)) return new List<ProgramDnsRule>();
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ProgramDnsRule>>(json) ?? new List<ProgramDnsRule>();
            }
            catch
            {
                return new List<ProgramDnsRule>();
            }
        }

        private void CheckRules(object? state)
        {
            foreach (var rule in rules)
            {
                if (IsRunning(rule.ProgramPath))
                {
                    var profile = form.ProfileByName(rule.Profile);
                    if (profile != null && currentProfile != profile)
                    {
                        form.BeginInvoke(new Func<Task>(async () => await form.ApplyProfileAsync(profile, true)));
                        currentProfile = profile;
                    }
                    return;
                }
            }

            if (currentProfile != form.AdGuard)
            {
                form.BeginInvoke(new Func<Task>(async () => await form.ApplyProfileAsync(form.AdGuard, true)));
                currentProfile = form.AdGuard;
            }
        }

        private static bool IsRunning(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return false;
            string normalized = Path.GetFullPath(fullPath);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var path = p.MainModule?.FileName;
                    if (path != null && string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                catch
                {
                    // ignore processes we cannot access
                }
            }
            return false;
        }

        public void Dispose()
        {
            timer.Dispose();
        }
    }
}

