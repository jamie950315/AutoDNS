using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AutoDNS
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            if (!IsAdministrator())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                }
                catch
                {
                    MessageBox.Show("需要系統管理員權限來修改 DNS 設定。\n請以系統管理員身分重新執行。", "AutoDNS", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
    }

    public class DnsProfile
    {
        public string Name { get; }
        public string IPv4Primary { get; }
        public string IPv4Secondary { get; }
        public string IPv6Primary { get; }
        public string IPv6Secondary { get; }

        public DnsProfile(string name, string v4p, string v4s, string v6p, string v6s)
        { Name = name; IPv4Primary = v4p; IPv4Secondary = v4s; IPv6Primary = v6p; IPv6Secondary = v6s; }

        public override string ToString() => Name;
    }

    // 封裝要顯示在清單裡的介面資訊（避免名稱衝突，改用 ifIndex 套用設定）
    public class InterfaceItem
    {
        public string Name { get; set; } = "";       // 介面顯示名稱（Alias）
        public string Id { get; set; } = "";         // 介面 GUID
        public int IfIndex { get; set; }              // 介面索引（用於 PowerShell/Set-DnsClientServerAddress）
            = -1;
        public override string ToString() => IfIndex >= 0 ? $"{Name} (ifIndex={IfIndex})" : Name;
    }

    public class MainForm : Form
    {
        private CheckedListBox clbIfaces;
        private CheckBox chkSelectAll;
        private CheckBox chkIncludeAdvanced; // 新增：包含進階/虛擬介面
        private CheckBox chkAdGuard;
        private CheckBox chkDhcp;
        private GroupBox grpProvider;
        private RadioButton rbHiNet, rbCloudflare, rbGoogle;
        private Button btnApply, btnRefresh, btnShow, btnToggleLogs, btnFlush;
        private TextBox txtLog;

        // Logs toggle state (default OFF)
        private bool logsEnabled = false;

        // Profiles
        private readonly DnsProfile AdGuard = new("AdGuard", "94.140.14.14", "94.140.15.15", "2a10:50c0::ad1:ff", "2a10:50c0::ad2:ff");
        private readonly DnsProfile HiNet = new("HiNet", "168.95.1.1", "168.95.192.1", "2001:b000:168::1", "2001:b000:168::2");
        private readonly DnsProfile Cloudflare = new("Cloudflare", "1.1.1.1", "1.0.0.1", "2606:4700:4700::1111", "2606:4700:4700::1001");
        private readonly DnsProfile Google = new("Google", "8.8.8.8", "8.8.4.4", "2001:4860:4860::8888", "2001:4860:4860::8844");

        public MainForm()
        {
            Text = "AutoDNS";
            Width = 980; Height = 680; StartPosition = FormStartPosition.CenterScreen;

            var lblIf = new Label { Left = 15, Top = 15, Width = 540, Text = "選擇要套用的網路介面 (乙太網路 / Wi‑Fi / 進階可選)：" };
            clbIfaces = new CheckedListBox { Left = 15, Top = 40, Width = 500, Height = 260, CheckOnClick = true };
            chkSelectAll = new CheckBox { Left = 15, Top = 305, Width = 250, Text = "全選目前運作中的介面" };
            chkIncludeAdvanced = new CheckBox { Left = 270, Top = 305, Width = 250, Text = "包含進階/虛擬/撥接介面" };

            chkAdGuard = new CheckBox { Left = 540, Top = 40, Width = 400, Text = "使用 AdGuard DNS (94.140.14.14)" };
            chkAdGuard.Checked = true;
            chkAdGuard.CheckedChanged += (s, e) => { if (chkAdGuard.Checked) chkDhcp.Checked = false; UpdateProviderEnable(); };

            chkDhcp = new CheckBox { Left = 540, Top = 70, Width = 300, Text = "自動取得 DNS (DHCP)" };
            chkDhcp.CheckedChanged += (s, e) => { if (chkDhcp.Checked) chkAdGuard.Checked = false; UpdateProviderEnable(); };

            grpProvider = new GroupBox { Left = 535, Top = 105, Width = 415, Height = 150, Text = "未勾 AdGuard 與 DHCP 時，改用以下 DNS：" };
            rbHiNet = new RadioButton { Left = 20, Top = 25, Width = 200, Text = "HiNet(168.95.1.1)" };
            rbCloudflare = new RadioButton { Left = 20, Top = 55, Width = 200, Text = "Cloudflare (1.1.1.1)" };
            rbGoogle = new RadioButton { Left = 20, Top = 85, Width = 200, Text = "Google (8.8.8.8)" };
            rbCloudflare.Checked = true;
            grpProvider.Controls.AddRange(new Control[] { rbHiNet, rbCloudflare, rbGoogle });

            btnApply = new Button { Left = 540, Top = 265, Width = 97, Height = 30, Text = "套用設定" };
            btnRefresh = new Button { Left = 647, Top = 265, Width = 98, Height = 30, Text = "掃描介面卡" };
            btnShow = new Button { Left = 755, Top = 265, Width = 97, Height = 30, Text = "顯示目前 DNS" };
            btnToggleLogs = new Button { Left = 862, Top = 265, Width = 98, Height = 30, Text = "顯示紀錄：關" };
            btnFlush = new Button { Left = 540, Top = 305, Width = 420, Height = 30, Text = "清除 DNS 快取 (ipconfig /flushdns)" };

            btnApply.Click += async (s, e) => await ApplyAsync();
            btnRefresh.Click += (s, e) => InitInterfaces();
            btnShow.Click += async (s, e) => await ShowDnsAsync();
            btnToggleLogs.Click += (s, e) => ToggleLogs();
            btnFlush.Click += async (s, e) => await FlushDnsAsync();

            var lblInfo = new Label
            {
                Left = 15,
                Top = 340,
                Width = 940,
                Height = 40,
                Text = "提示：需要系統管理員權限。若設定未生效，可嘗試重新連線或清除 DNS 快取。"
            };
            txtLog = new TextBox { Left = 15, Top = 385, Width = 945, Height = 220, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
            txtLog.Visible = false; // 預設：關閉紀錄（不顯示）

            Controls.AddRange(new Control[] { lblIf, clbIfaces, chkSelectAll, chkIncludeAdvanced, chkAdGuard, chkDhcp, grpProvider, btnApply, btnRefresh, btnShow, btnToggleLogs, btnFlush, lblInfo, txtLog });

            Load += (_, __) => InitInterfaces();
            chkSelectAll.CheckedChanged += (s, e) => SelectAllUpInterfaces(chkSelectAll.Checked);
            chkIncludeAdvanced.CheckedChanged += (s, e) => InitInterfaces();
            UpdateProviderEnable();
        }

        private void ToggleLogs()
        {
            logsEnabled = !logsEnabled;
            txtLog.Visible = logsEnabled;
            btnToggleLogs.Text = logsEnabled ? "顯示紀錄：開" : "顯示紀錄：關";
        }

        private void UpdateProviderEnable()
        {
            grpProvider.Enabled = !chkAdGuard.Checked && !chkDhcp.Checked;
        }

        private bool IsSupportedType(NetworkInterfaceType t)
        {
            // 基本：乙太網路、Wi‑Fi
            if (t == NetworkInterfaceType.Ethernet || t == NetworkInterfaceType.Wireless80211)
                return true;
            // 進階：可選擇包含撥接/未知等（排除 Loopback/Tunnel 等無意義者）
            if (!chkIncludeAdvanced.Checked) return false;
            if (t == NetworkInterfaceType.Loopback || t == NetworkInterfaceType.Tunnel) return false;
            if (t == NetworkInterfaceType.Ppp || t == NetworkInterfaceType.Unknown) return true;
            // 其他型別一律不列
            return false;
        }

        private void InitInterfaces()
        {
            // 保留原先勾選的介面（以 GUID 標識）
            var previouslyChecked = new HashSet<string>(clbIfaces.CheckedItems.Cast<InterfaceItem>().Select(x => x.Id));

            clbIfaces.Items.Clear();
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                         .Where(n => n.OperationalStatus == OperationalStatus.Up && IsSupportedType(n.NetworkInterfaceType)))
            {
                // 嘗試從 IPv4Properties 取得 ifIndex；若取不到，設 -1，之後套用時仍可用 Alias 當後備
                int ifIndex = -1;
                try { ifIndex = nic.GetIPProperties()?.GetIPv4Properties()?.Index ?? -1; } catch { }
                var item = new InterfaceItem { Name = nic.Name, Id = nic.Id, IfIndex = ifIndex };
                bool shouldCheck = previouslyChecked.Contains(item.Id) || true; // 預設勾選
                clbIfaces.Items.Add(item, shouldCheck);
            }
        }

        private void SelectAllUpInterfaces(bool check)
        {
            for (int i = 0; i < clbIfaces.Items.Count; i++) clbIfaces.SetItemChecked(i, check);
        }

        private DnsProfile CurrentProfile()
        {
            if (chkAdGuard.Checked) return AdGuard;
            if (rbHiNet.Checked) return HiNet;
            if (rbGoogle.Checked) return Google;
            return Cloudflare;
        }

        private async Task ApplyAsync()
        {
            var selected = clbIfaces.CheckedItems.Cast<InterfaceItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("請至少選擇一個介面。", "AutoDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (chkDhcp.Checked)
            {
                await SetDhcpAsync(selected);
                return;
            }

            var profile = CurrentProfile();
            Log($"\r\n=== 套用 DNS 設定：{profile.Name} ===");
            Log($"IPv4: {profile.IPv4Primary}, {profile.IPv4Secondary}");
            Log($"IPv6: {profile.IPv6Primary}, {profile.IPv6Secondary}");

            foreach (var nic in selected)
            {
                Log($"\r\n介面：{nic.Name} (ifIndex={nic.IfIndex})");

                // 確保 IPv6 已啟用
                await EnsureIPv6BindingAsync(nic.Name);

                // 使用 PowerShell 一次性覆蓋整個 DNS 清單（避免殘留）
                var v4Cmd = nic.IfIndex >= 0
                    ? $"Set-DnsClientServerAddress -InterfaceIndex {nic.IfIndex} -ServerAddresses \"{profile.IPv4Primary}\",\"{profile.IPv4Secondary}\""
                    : $"Set-DnsClientServerAddress -InterfaceAlias '{EscapeForPS(nic.Name)}' -ServerAddresses \"{profile.IPv4Primary}\",\"{profile.IPv4Secondary}\"";
                var v6Cmd = nic.IfIndex >= 0
                    ? $"Set-DnsClientServerAddress -InterfaceIndex {nic.IfIndex} -ServerAddresses \"{profile.IPv6Primary}\",\"{profile.IPv6Secondary}\""
                    : $"Set-DnsClientServerAddress -InterfaceAlias '{EscapeForPS(nic.Name)}' -ServerAddresses \"{profile.IPv6Primary}\",\"{profile.IPv6Secondary}\"";

                var ec4 = await RunPowerShellAsync(v4Cmd);
                if (ec4 != 0)
                {
                    // 後備：改用 netsh（IPv4）
                    await RunNetshAsync($"interface ipv4 set dns name=\"{nic.Name}\" static {profile.IPv4Primary} primary");
                    await RunNetshAsync($"interface ipv4 add dns name=\"{nic.Name}\" {profile.IPv4Secondary} index=2 validate=no");
                }

                var ec6 = await RunPowerShellAsync(v6Cmd);
                if (ec6 != 0)
                {
                    // 後備：IPv6 清空 + 加入主/次
                    await RunNetshAsync($"interface ipv6 delete dnsservers name=\"{nic.Name}\" all");
                    await RunNetshAsync($"interface ipv6 add dnsserver name=\"{nic.Name}\" address={profile.IPv6Primary} index=1 validate=no");
                    await RunNetshAsync($"interface ipv6 add dnsserver name=\"{nic.Name}\" address={profile.IPv6Secondary} index=2 validate=no");
                }
            }

            Log("\r\n完成。若應用程式/瀏覽器仍未生效，請嘗試重新連線或清除 DNS 快取：ipconfig /flushdns");

            // 若未開啟紀錄視圖，使用 MessageBox 簡要提示切換到哪個 DNS
            if (!logsEnabled)
            {
                MessageBox.Show($"已套用：{profile.Name} DNS", "AutoDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task SetDhcpAsync(List<InterfaceItem> selected)
        {
            if (selected.Count == 0)
            {
                MessageBox.Show("請至少選擇一個介面。", "AutoDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Log("\r\n=== 恢復自動取得(DHCP) ===");
            foreach (var nic in selected)
            {
                Log($"介面：{nic.Name} (ifIndex={nic.IfIndex})");

                // 先嘗試用 PowerShell 一鍵重置 v4/v6；若失敗再 fallback 到 netsh
                int ec = nic.IfIndex >= 0
                    ? await RunPowerShellAsync($"Set-DnsClientServerAddress -InterfaceIndex {nic.IfIndex} -ResetServerAddresses")
                    : await RunPowerShellAsync($"Set-DnsClientServerAddress -InterfaceAlias '{EscapeForPS(nic.Name)}' -ResetServerAddresses");

                if (ec != 0)
                {
                    await RunNetshAsync($"interface ipv4 set dnsservers name=\"{nic.Name}\" source=dhcp");
                    await EnsureIPv6BindingAsync(nic.Name);
                    await RunNetshAsync($"interface ipv6 set dnsservers name=\"{nic.Name}\" source=dhcp");
                }

                // 依你的要求：用 Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily 做結果列印（驗證/紀錄）
                string baseCmdIdx = nic.IfIndex >= 0 ? $"-InterfaceIndex {nic.IfIndex}" : $"-InterfaceAlias '{EscapeForPS(nic.Name)}'";
                await RunPowerShellAsync($"$a=(Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily IPv4).ServerAddresses; Write-Host '(After DHCP) IPv4: ' + ($a -join ', ')");
                await RunPowerShellAsync($"$b=(Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily IPv6).ServerAddresses; Write-Host '(After DHCP) IPv6: ' + ($b -join ', ')");
            }
            Log("\r\n完成。若應用程式/瀏覽器仍未生效，請嘗試重新連線或清除 DNS 快取：ipconfig /flushdns");

            if (!logsEnabled)
            {
                MessageBox.Show("已切換為自動取得 (DHCP)", "AutoDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async Task ShowDnsAsync()
        {
            var selected = clbIfaces.CheckedItems.Cast<InterfaceItem>().ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("請至少選擇一個介面。", "AutoDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Log("\n=== 目前 DNS 設定 ===");
            foreach (var nic in selected)
            {
                Log($"介面：{nic.Name} (ifIndex={nic.IfIndex})");

                // 以 PowerShell 為主，格式化輸出；若失敗再用 netsh 作為後備
                string baseCmdIdx = nic.IfIndex >= 0 ? $"-InterfaceIndex {nic.IfIndex}" : $"-InterfaceAlias '{EscapeForPS(nic.Name)}'";
                int e1 = await RunPowerShellAsync($"$a=(Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily IPv4).ServerAddresses; Write-Host ('IPv4: ' + ($a -join ', '))");
                int e2 = await RunPowerShellAsync($"$b=(Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily IPv6).ServerAddresses; Write-Host ('IPv6: ' + ($b -join ', '))");
                if (e1 != 0 || e2 != 0)
                {
                    await RunNetshAsync($"interface ipv4 show dnsservers name=\"{nic.Name}\"");
                    await RunNetshAsync($"interface ipv6 show dnsservers name=\"{nic.Name}\"");
                }
            }

            // 若未開啟紀錄視圖，彙整摘要以 MessageBox 顯示（行為與 Flush 類似）
            try
            {
                var summarySb = new StringBuilder();
                foreach (var nic in selected)
                {
                    var net = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == nic.Id);
                    if (net == null) continue;
                    var props = net.GetIPProperties();
                    var v4 = props.DnsAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetwork).Select(ip => ip.ToString()).ToList();
                    var v6 = props.DnsAddresses.Where(ip => ip.AddressFamily == AddressFamily.InterNetworkV6).Select(ip => ip.ToString()).ToList();
                    summarySb.AppendLine($"介面：{nic.Name} (ifIndex={nic.IfIndex})");
                    summarySb.AppendLine("IPv4: " + (v4.Count > 0 ? string.Join(", ", v4) : "(無)"));
                    summarySb.AppendLine("IPv6: " + (v6.Count > 0 ? string.Join(", ", v6) : "(無)"));
                    summarySb.AppendLine();
                }
                if (!logsEnabled)
                {
                    var text = summarySb.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(text))
                        MessageBox.Show("無法取得目前 DNS 設定，請開啟紀錄檢視詳細輸出。", "AutoDNS", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else
                        MessageBox.Show(text, "目前 DNS 設定", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch { }
        }

        private async Task FlushDnsAsync()
        {
            Log("\r\n=== 清除 DNS 快取 ===");
            int ec = await RunProcessAsync(new ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }, prefix: "ipconfig /flushdns");

            if (!logsEnabled)
            {
                MessageBox.Show(ec == 0 ? "DNS 快取已清除" : "DNS 快取清除可能失敗，請查看紀錄。", "AutoDNS", MessageBoxButtons.OK, ec == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
        }

        private async Task<int> RunNetshAsync(string args)
        {
            return await RunProcessAsync(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }, prefix: $"netsh {args}");
        }

        private async Task<int> RunPowerShellAsync(string command)
        {
            // 使用 Out-String 讓輸出更一致；command 會在這層被包住
            string wrapped = "[Console]::OutputEncoding=[System.Text.Encoding]::UTF8; " + command;
            return await RunProcessAsync(new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + wrapped.Replace("\"", "\\\"") + "\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }, prefix: $"powershell -NoProfile -ExecutionPolicy Bypass -Command {command}");
        }

        private async Task<int> RunProcessAsync(ProcessStartInfo psi, string prefix)
        {
            Log(prefix);
            using var p = new Process { StartInfo = psi };
            var sb = new StringBuilder();
            p.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) sb.AppendLine(e.Data); };
            p.Start();
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await Task.Run(() => p.WaitForExit());

            var combined = sb.ToString().Trim();
            if (!string.IsNullOrEmpty(combined)) Log(combined);
            Log(p.ExitCode == 0 ? "[OK] 指令完成" : $"[ERR] ExitCode={p.ExitCode}");
            return p.ExitCode;
        }

        private static string EscapeForPS(string s) => s.Replace("'", "''");

        private async Task EnsureIPv6BindingAsync(string name)
        {
            var n = EscapeForPS(name);
            var cmd = "$b=Get-NetAdapterBinding -Name '" + n + "' -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue; if($b -and -not $b.Enabled) { Enable-NetAdapterBinding -Name '" + n + "' -ComponentID ms_tcpip6 -PassThru | Out-Null; Write-Host 'Enabled IPv6 binding' } else { Write-Host 'IPv6 binding already enabled' }";
            await RunPowerShellAsync(cmd);
        }

        private void Log(string msg)
        {
            if (txtLog.InvokeRequired)
            { txtLog.Invoke(new Action<string>(Log), msg); return; }
            txtLog.AppendText(msg + Environment.NewLine);
        }
    }
}
