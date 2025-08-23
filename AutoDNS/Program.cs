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
                    Program.ShowDarkInfo(null, "需要系統管理員權限來修改 DNS 設定。\n請以系統管理員身分重新執行", "AutoDNS");
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public static DialogResult ShowDarkInfo(Control owner, string text, string title = "訊息")
        {

            // 跨執行緒安全呼叫
            if (owner != null && owner.InvokeRequired)
            {
                DialogResult r = DialogResult.None;
                owner.Invoke((Action)(() => r = ShowDarkInfo(owner, text, title)));
                return r;
            }

            // 色票
            Color DarkBg = Color.FromArgb(28, 28, 30);
            Color PanelBg = Color.FromArgb(38, 38, 42);
            Color TextFg = Color.White;
            Color BtnBg = Color.FromArgb(56, 56, 64);
            Color BtnHover = Color.FromArgb(72, 72, 80);
            Color BtnDown = Color.FromArgb(88, 88, 96);

            using (var f = new Form())
            {
                f.Text = title;
                f.StartPosition = owner != null ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false; f.MinimizeBox = false;
                f.AutoScaleMode = AutoScaleMode.Dpi;
                f.BackColor = DarkBg; f.ForeColor = TextFg;

                // 佈局常數
                const int minClientW = 100;
                const int minClientH = 100;
                const int iconLeft = 18;
                const int iconTop = 22;
                const int iconSize = 32;
                const int contentLeft = iconLeft + iconSize + 20; // 18+32+20
                const int topPad = 20;
                const int spacing = 20; // 內容與按鈕間距
                const int rightPad = 18;
                const int bottomPad = 18;

                var wa = owner != null ? Screen.FromControl(owner).WorkingArea
                                       : Screen.PrimaryScreen.WorkingArea;
                int maxClientW = Math.Max(minClientW, wa.Width - 120);
                int maxClientH = Math.Max(minClientH, wa.Height - 160);

                f.ClientSize = new Size(minClientW, minClientH);

                var iconBox = new PictureBox
                {
                    Image = SystemIcons.Information.ToBitmap(),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    BackColor = DarkBg,
                    Left = iconLeft,
                    Top = iconTop,
                    Width = iconSize,
                    Height = iconSize
                };

                // 內容 Panel：啟用水平捲軸，不換行
                var content = new Panel
                {
                    Left = contentLeft,
                    Top = topPad,
                    Height = 30, // 先給單行高度，稍後 Reflow 會調
                    BackColor = DarkBg,
                    //AutoScroll = true
                };

                var lbl = new Label
                {
                    AutoSize = true,      // 單行，寬度跟著文字跑
                    BackColor = DarkBg,
                    ForeColor = TextFg,
                    Left = 0,
                    Top = 0,
                    Text = text
                };

                var ok = new Button
                {
                    Text = "確定",
                    Width = 96,
                    Height = 30,
                    DialogResult = DialogResult.OK,
                    Anchor = AnchorStyles.Right | AnchorStyles.Bottom
                };
                ok.FlatStyle = FlatStyle.Flat; ok.UseVisualStyleBackColor = false;
                ok.BackColor = BtnBg; ok.ForeColor = TextFg;
                ok.FlatAppearance.BorderSize = 1;
                ok.FlatAppearance.BorderColor = ControlPaint.Light(BtnBg);
                ok.FlatAppearance.MouseOverBackColor = BtnHover;
                ok.FlatAppearance.MouseDownBackColor = BtnDown;

                content.Controls.Add(lbl);
                f.Controls.Add(iconBox);
                f.Controls.Add(content);
                f.Controls.Add(ok);
                f.AcceptButton = ok;

                void Reflow()
                {
                    // 量測單行寬度（不包內距）
                    int oneLineTextW = TextRenderer.MeasureText(
                        lbl.Text, lbl.Font, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding).Width;

                    // 期望的客戶區寬度（單行塞得下，或最多到螢幕上限）
                    int desiredClientW = Math.Clamp(contentLeft + oneLineTextW + rightPad,
                                                    minClientW, maxClientW);

                    if (f.ClientSize.Width != desiredClientW)
                        f.ClientSize = new Size(desiredClientW+10, f.ClientSize.Height);

                    // Panel 寬度 = 客戶區寬度扣掉左右邊
                    content.Width = Math.Max(120, f.ClientSize.Width - contentLeft - rightPad);
                    content.Height = Math.Max(lbl.Height, 30); // 單行高度即可（不換行）
                                                               // Label 單行：AutoSize=true，不設定 MaximumSize → 絕不換行
                                                               // 若 lbl 寬 > content 寬，Panel 會自動出水平捲軸

                    // 計算需要的高度
                    int requiredH = content.Bottom + spacing + ok.Height + bottomPad;
                    int desiredClientH = Math.Clamp(requiredH, minClientH, maxClientH);
                    if (f.ClientSize.Height != desiredClientH)
                        f.ClientSize = new Size(f.ClientSize.Width, desiredClientH);

                    // 右下角定位 OK
                    ok.Left = f.ClientSize.Width - ok.Width - rightPad;
                    ok.Top = f.ClientSize.Height - ok.Height - bottomPad;
                }

                // 初始佈局
                Reflow();

                return owner != null ? f.ShowDialog(owner) : f.ShowDialog();
            }

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
        public int IfIndex { get; set; }             // 介面索引（用於 PowerShell/Set-DnsClientServerAddress）
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
        private Button btnApply, btnRefresh, btnShow, btnToggleLogs, btnFlush, btnDoneSelect, btnClearLogs;
        private TextBox txtLog;

        // Logs toggle state (default OFF)
        private bool logsEnabled = false;

        private bool isInInterface = false;

        // Profiles
        private readonly DnsProfile AdGuard = new("AdGuard", "94.140.14.14", "94.140.15.15", "2a10:50c0::ad1:ff", "2a10:50c0::ad2:ff");
        private readonly DnsProfile HiNet = new("HiNet", "168.95.1.1", "168.95.192.1", "2001:b000:168::1", "2001:b000:168::2");
        private readonly DnsProfile Cloudflare = new("Cloudflare", "1.1.1.1", "1.0.0.1", "2606:4700:4700::1111", "2606:4700:4700::1001");
        private readonly DnsProfile Google = new("Google", "8.8.8.8", "8.8.4.4", "2001:4860:4860::8888", "2001:4860:4860::8844");

        private Label lblIf = new Label { Left = 450, Top = 15, Width = 540, Text = "選擇要套用的網路介面 (乙太網路 / Wi‑Fi / 進階可選)：" };
        private Label logTitle = new Label { Left = 450, Top = 15, Width = 540, Text = "輸出紀錄：" };


        public MainForm()
        {

            //Dark mode colors

            Color DarkBg = Color.FromArgb(28, 28, 30);
            Color PanelBg = Color.FromArgb(38, 38, 42);
            Color TextFg = Color.White;
            Color TextBg = Color.DimGray;

            Color BtnBg = Color.FromArgb(56, 56, 64);
            Color BtnHover = Color.FromArgb(72, 72, 80);
            Color BtnDown = Color.FromArgb(88, 88, 96);


            Color AccentBg = Color.FromArgb(0, 120, 215);
            Color AccentHover = Color.FromArgb(0, 99, 177);
            Color AccentDown = Color.FromArgb(0, 78, 139);

            void StyleButton(Button b, Color bg, Color fg, Color hover, Color down, Color? border = null)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.UseVisualStyleBackColor = false;
                b.BackColor = bg;
                b.ForeColor = fg;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = border ?? ControlPaint.Light(bg);
                b.FlatAppearance.MouseOverBackColor = hover;
                b.FlatAppearance.MouseDownBackColor = down;
                b.TabStop = false;
            }

            //dns provider dark mode
            void UpdateProviderEnable()
            {
                bool locked = chkAdGuard.Checked || chkDhcp.Checked;

                // 保持 GroupBox 啟用，避免變回系統字色
                grpProvider.Enabled = true;
                grpProvider.ForeColor = locked ? TextBg : TextFg;
                grpProvider.BackColor = DarkBg;

                foreach (var rb in new[] { rbHiNet, rbCloudflare, rbGoogle })
                {
                    rb.AutoCheck = !locked;        // 鎖住選擇確保深色主題字色
                    rb.Cursor = locked ? Cursors.No : Cursors.Default;
                    rb.ForeColor = locked ? TextBg : TextFg;
                    rb.BackColor = DarkBg;
                }
            }



            void ApplyDarkMode()
            {
                // 視窗本體
                BackColor = DarkBg;
                ForeColor = TextFg;

                // 文字/輸入類
                if (txtLog != null)
                {
                    txtLog.BackColor = PanelBg;
                    txtLog.ForeColor = TextFg;
                    txtLog.BorderStyle = BorderStyle.FixedSingle;
                }
                if (clbIfaces != null)
                {
                    clbIfaces.BackColor = PanelBg;
                    clbIfaces.ForeColor = TextFg;
                    clbIfaces.BorderStyle = BorderStyle.FixedSingle;
                }


                // 勾選框：AdGuard / DHCP / 全選 / 包含進階
                foreach (var c in new Control[] { chkAdGuard, chkDhcp, chkSelectAll, chkIncludeAdvanced })
                {
                    if (c != null)
                    {
                        c.ForeColor = TextFg;
                        c.BackColor = DarkBg;
                        if (c is CheckBox cb) cb.UseVisualStyleBackColor = false;
                    }
                }

                // 一般 Label
                foreach (Control c in Controls)
                {
                    if (c is Label) c.ForeColor = TextFg;
                }

                // 按鈕：一般按鈕走暗色，主要動作按鈕走 Accent
                if (btnApply != null) StyleButton(btnApply, AccentBg, Color.White, AccentHover, AccentDown);
                if (btnClearLogs != null) StyleButton(btnClearLogs, AccentBg, Color.White, AccentHover, AccentDown);
                if (btnDoneSelect != null) StyleButton(btnDoneSelect, AccentBg, Color.White, AccentHover, AccentDown);

                foreach (var b in new[] { btnRefresh, btnShow, btnToggleLogs, btnFlush })
                {
                    if (b != null) StyleButton(b, BtnBg, TextFg, BtnHover, BtnDown);
                }

            }


            Text = "AutoDNS";
            Width = 460; Height = 400; StartPosition = FormStartPosition.CenterScreen;

            // 設定介面選擇區
            clbIfaces = new CheckedListBox { Left = 450, Top = 40, Width = 500, Height = 255, CheckOnClick = true };    //Select network interfaces
            chkSelectAll = new CheckBox { Left = 450, Top = 305, Width = 200, Text = "全選目前運作中的介面" };
            chkIncludeAdvanced = new CheckBox { Left = 650, Top = 305, Width = 200, Text = "包含進階/虛擬/撥接介面" };
            btnDoneSelect = new Button { Left = 850, Top = 305, Width = 100, Height = 30, Text = "完成選擇" };
            btnDoneSelect.Click += (s, e) => doneSelect();

            // 設定 DNS 提供者選項
            chkAdGuard = new CheckBox { Left = 15, Top = 40, Width = 400, Text = "使用 AdGuard DNS (94.140.14.14 / 2a10:50c0::ad1:ff)" };
            chkAdGuard.Checked = true;
            chkAdGuard.CheckedChanged += (s, e) => { if (chkAdGuard.Checked) chkDhcp.Checked = false; UpdateProviderEnable(); };

            chkDhcp = new CheckBox { Left = 15, Top = 70, Width = 300, Text = "自動取得 DNS (DHCP)" };
            chkDhcp.CheckedChanged += (s, e) => { if (chkDhcp.Checked) chkAdGuard.Checked = false; UpdateProviderEnable(); };

            grpProvider = new GroupBox { Left = 15, Top = 105, Width = 415, Height = 150, Text = "未勾 AdGuard 與 DHCP 時，改用以下 DNS：" };
            rbHiNet = new RadioButton { Left = 20, Top = 25, Width = 350, Text = "HiNet (168.95.1.1 / 2001:b000:168::1)" };
            rbCloudflare = new RadioButton { Left = 20, Top = 55, Width = 350, Text = "Cloudflare (1.1.1.1 / 2606:4700:4700::1111)" };
            rbGoogle = new RadioButton { Left = 20, Top = 85, Width = 350, Text = "Google (8.8.8.8 / 2001:4860:4860::8888)" };
            rbCloudflare.Checked = true;
            grpProvider.Controls.AddRange(new Control[] { rbHiNet, rbCloudflare, rbGoogle });

            // 設定按鈕
            btnFlush = new Button { Left = 15, Top = 265, Width = 97, Height = 30, Text = "清除 DNS 快取" };
            btnRefresh = new Button { Left = 122, Top = 265, Width = 98, Height = 30, Text = "掃描介面卡" };
            btnShow = new Button { Left = 230, Top = 265, Width = 97, Height = 30, Text = "顯示目前 DNS" };
            btnToggleLogs = new Button { Left = 337, Top = 265, Width = 98, Height = 30, Text = "顯示紀錄：關" };
            btnApply = new Button { Left = 15, Top = 305, Width = 420, Height = 30, Text = "套用設定" };

            btnApply.Click += async (s, e) => await ApplyAsync();
            btnRefresh.Click += (s, e) => InitInterfaces();
            
            btnShow.Click += async (s, e) => await ShowDnsAsync();
            btnToggleLogs.Click += (s, e) => ToggleLogs();
            btnFlush.Click += async (s, e) => await FlushDnsAsync();

            // Bottom label
            var lblInfo = new Label
            {
                Left = 15,
                Top = 340,
                Width = 940,
                Height = 30,
                Text = "提示：需要系統管理員權限。若設定未生效可嘗試重新連線或清除 DNS 快取。"
            };

            //log output box
            txtLog = new TextBox { Left = 450, Top = 40, Width = 500, Height = 255, Multiline = true, ReadOnly = true };
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Font = new Font("Consolas", 10);
            btnClearLogs = new Button { Left = 450, Top = 305, Width = 500, Height = 30, Text = "清除輸出紀錄" };
            btnClearLogs.Click += (s, e) => txtLog.Clear();


            Controls.AddRange(new Control[] { lblIf, clbIfaces, chkSelectAll, chkIncludeAdvanced, chkAdGuard, chkDhcp, grpProvider, btnApply, btnRefresh, btnShow, btnToggleLogs, btnFlush, txtLog, btnDoneSelect, btnClearLogs, logTitle });


            ApplyDarkMode();

            Load += (_, __) => InitInterfaces();
            chkSelectAll.CheckedChanged += (s, e) => SelectAllUpInterfaces(chkSelectAll.Checked);
            chkIncludeAdvanced.CheckedChanged += (s, e) => InitInterfaces();
            controlInterfaceUI(false);
            controlLogUI(false);
            UpdateProviderEnable();
        }

        private void controlInterfaceUI(bool isUIVisible)
        {
            if (isUIVisible)
            {
                lblIf.Visible = true;
                clbIfaces.Visible = true;
                chkSelectAll.Visible = true;
                chkIncludeAdvanced.Visible = true;
                btnDoneSelect.Visible = true;
            }
            else
            {
                lblIf.Visible = false;
                clbIfaces.Visible = false;
                chkSelectAll.Visible = false;
                chkIncludeAdvanced.Visible = false;
                btnDoneSelect.Visible = false;
            }
        }

        private void controlLogUI(bool isLogVisible)
        {
            if (isLogVisible)
            {
                logTitle.Visible = true;
                txtLog.Visible = true;
                btnClearLogs.Visible = true;
            }
            else
            {
                logTitle.Visible = false;
                txtLog.Visible = false;
                btnClearLogs.Visible = false;
            }
            btnToggleLogs.Text = isLogVisible ? "顯示紀錄：開" : "顯示紀錄：關";
        }

        private void expandWindow(bool expand)
        {
            if (expand)
            {
                if (Width != 980)
                {
                    Left = this.Left - 260; //move left to center the expanded window
                }
                Width = 980;
            }
            else
            {
                if (Width != 460)
                {
                    Left = this.Left + 260; //move right to center the collapsed window
                }
                Width = 460;
            }

        }


        private void doneSelect()
        {
            var selected = clbIfaces.CheckedItems.Cast<InterfaceItem>().ToList();
            if (selected.Count == 0)
            {
                Program.ShowDarkInfo(this, "請至少選擇一個介面。", "AutoDNS");
                return;
            }
            isInInterface = false;
            controlInterfaceUI(false);
            if (logsEnabled)
            {
                controlLogUI(true);
            }
            else
            {
                expandWindow(false);
                controlLogUI(false);
            }
        }

        private void ToggleLogs()
        {
            logsEnabled = !logsEnabled;
            controlLogUI(logsEnabled);
            if (logsEnabled)
            {
                expandWindow(true);
                controlInterfaceUI(false);
            }
            else if (isInInterface) 
            {
                controlInterfaceUI(true);
            }
            else
            {
                expandWindow(false);
            }

        }

        //dns provider light mode
        //private void UpdateProviderEnable()
        //{
        //    grpProvider.Enabled = !chkAdGuard.Checked && !chkDhcp.Checked;
        //}

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

            // 若是由 btnRefresh 觸發
            if (ActiveControl == btnRefresh)
            {
                expandWindow(true);
                controlLogUI(false);
                logsEnabled = false;
                controlInterfaceUI(true);
                isInInterface = true;
                Program.ShowDarkInfo(this, "已掃描 " + clbIfaces.Items.Count + " 個介面。\n" +
                    "請選擇要套用的介面，並點擊「套用設定」。\n" +
                    "若需要包含進階/虛擬介面，請勾選「包含進階/虛擬/撥接介面」。", "AutoDNS");
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
                Program.ShowDarkInfo(this, "請至少選擇一個介面。", "AutoDNS");
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
            
            Program.ShowDarkInfo(this, $"已套用：{profile.Name} DNS\n", "AutoDNS");
        }

        private async Task SetDhcpAsync(List<InterfaceItem> selected)
        {
            if (selected.Count == 0)
            {
                Program.ShowDarkInfo(this, "請至少選擇一個介面。", "AutoDNS");
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

                // 用 Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily 做結果列印（驗證/紀錄）
                string baseCmdIdx = nic.IfIndex >= 0 ? $"-InterfaceIndex {nic.IfIndex}" : $"-InterfaceAlias '{EscapeForPS(nic.Name)}'";
                await RunPowerShellAsync($"$a=(Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily IPv4).ServerAddresses; Write-Host '(After DHCP) IPv4: ' + ($a -join ', ')");
                await RunPowerShellAsync($"$b=(Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily IPv6).ServerAddresses; Write-Host '(After DHCP) IPv6: ' + ($b -join ', ')");
            }
            Log("\r\n完成。若應用程式/瀏覽器仍未生效，請嘗試重新連線或清除 DNS 快取：ipconfig /flushdns");

            Program.ShowDarkInfo(this, "已切換為自動取得 (DHCP)\n", "AutoDNS");
        }

        private async Task ShowDnsAsync()
        {
            var selected = clbIfaces.CheckedItems.Cast<InterfaceItem>().ToList();
            if (selected.Count == 0)
            {
                Program.ShowDarkInfo(this, "請至少選擇一個介面。", "AutoDNS");
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
                    summarySb.AppendLine("IPv4: " + (v4.Count > 0 ? string.Join(" / ", v4) : "(無)"));
                    summarySb.AppendLine("IPv6: " + (v6.Count > 0 ? string.Join(" / ", v6) : "(無)"));
                    summarySb.AppendLine();
                }
                
                var text = summarySb.ToString().Trim();
                if (string.IsNullOrWhiteSpace(text))
                    Program.ShowDarkInfo(this, "無法取得目前 DNS 設定，請開啟紀錄檢視詳細輸出。", "AutoDNS");
                
                else Program.ShowDarkInfo(this, text, "目前 DNS 設定");

            }
            catch { }
        }

        private async Task FlushDnsAsync()
        {
            Log("\r\n=== 清除 DNS 快取 ===");
            int ec = await RunProcessAsync(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c chcp 65001 >nul && ipconfig /flushdns",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }, prefix: "ipconfig /flushdns");

            Program.ShowDarkInfo(this, ec == 0 ? "DNS 快取已清除" : "DNS 快取清除可能失敗，請查看紀錄。", "AutoDNS");
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
