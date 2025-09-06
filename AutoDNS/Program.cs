using System.ComponentModel;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

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

            // cross-thread
            if (owner != null && owner.InvokeRequired)
            {
                DialogResult r = DialogResult.None;
                owner.Invoke((Action)(() => r = ShowDarkInfo(owner, text, title)));
                return r;
            }

            // colors
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

                // layout constants
                const int minClientW = 100;
                const int minClientH = 100;
                const int iconLeft = 18;
                const int iconTop = 22;
                const int iconSize = 40;
                const int contentLeft = iconLeft + iconSize + 20; // 18+40+20
                const int topPad = 20;
                const int spacing = 20;
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

                
                var content = new Panel
                {
                    Left = contentLeft,
                    Top = topPad,
                    Height = 30, 
                    BackColor = DarkBg,
                    //AutoScroll = true
                };

                var lbl = new Label
                {
                    AutoSize = true,      // forced one line
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
                    // scale line width
                    int oneLineTextW = TextRenderer.MeasureText(
                        lbl.Text, lbl.Font, new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding).Width;

                    // expected ClientWidth
                    int desiredClientW = Math.Clamp(contentLeft + oneLineTextW + rightPad,
                                                    minClientW, maxClientW);

                    if (f.ClientSize.Width != desiredClientW)
                        f.ClientSize = new Size(desiredClientW+10, f.ClientSize.Height);

                    // Panel width
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

    // encapsulation interface item for CheckedListBox
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
        private CheckBox chkIncludeAdvanced;
        private CheckBox chkAdGuard;
        private CheckBox chkDhcp;
        private CheckBox chkAutoSwitch;
        private GroupBox grpProvider;
        private RadioButton rbHiNet, rbCloudflare, rbGoogle;
        //also need to add in ApplyDarkMode.StyleButton and Controls.AddRange
        private Button btnApplyDns, btnRefreshInterface, btnShowCurrentDns, btnToggleLogs, btnFlushDnsCache, btnDoneSelect, btnClearLogs, btnQueryResponse, btnExePathCustomize ; 
        private TextBox txtLog;

        // Logs toggle state (default OFF)
        private bool logsEnabled = false;

        private bool isInInterface = false;

        private bool isPerforming = false;

        private bool isAutoSwitchEnabled = false;

        // Profiles
        private readonly DnsProfile AdGuard = new("AdGuard", "94.140.14.14", "94.140.15.15", "2a10:50c0::ad1:ff", "2a10:50c0::ad2:ff");
        private readonly DnsProfile HiNet = new("HiNet", "168.95.1.1", "168.95.192.1", "2001:b000:168::1", "2001:b000:168::2");
        private readonly DnsProfile Cloudflare = new("Cloudflare", "1.1.1.1", "1.0.0.1", "2606:4700:4700::1111", "2606:4700:4700::1001");
        private readonly DnsProfile Google = new("Google", "8.8.8.8", "8.8.4.4", "2001:4860:4860::8888", "2001:4860:4860::8844");
        private readonly DnsProfile DhcpPlaceHolder = new("Dhcp", "", "", "", "");

        private Label lbSelectInterface = new Label { Left = rightPanelStartX, Top = 15, Width = 540, Text = "選擇要套用的網路介面 (乙太網路 / Wi‑Fi / 進階可選)：" };
        private Label logTitle = new Label { Left = rightPanelStartX, Top = 15, Width = 540, Text = "輸出紀錄：" };
        private Label lbIntervalAdjust = new Label { Left = 187, Top = 13, Width = 140, Text = "掃描間隔秒數：" };

        //Panel edges
        private static int leftPanelEndX = 466;
        private static int rightPanelStartX = 450;
        private static int rightPanelEndX = 881;
        private static int expandShiftX = (rightPanelEndX - leftPanelEndX)/2;

        // internal variables
        private static Dictionary<string, string> exePathListKVP = new Dictionary<string, string>();
        private static string prevDnsProvider = ""; //only saves manually selected DNS, leave as empty for initial run no prompt
        private static string realConnectedDns = "";    //saves the actually connected DNS, used for switching back when no exe is running
        private static string lastApplySignature = "";  


        //for custom time interval
        private static double timerInterval = 5;
        private NumericUpDown intervalAdjust;
        // interval control
        private CancellationTokenSource? autoSwitchCts;
        private Task? autoSwitchTask;
        private TimeSpan autoPeriod = TimeSpan.FromSeconds(timerInterval);
        // prevent multiple autoSwitch at the same time
        private readonly System.Threading.SemaphoreSlim autoSwitchGate = new(1, 1);
        



        public MainForm()
        {
            // make sure user cant resize the window
            this.FormBorderStyle = FormBorderStyle.FixedSingle; // disable resizing
            this.MaximizeBox = false;                           // disable maximize button
            this.SizeGripStyle = SizeGripStyle.Hide;            // disable resize grip

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

                grpProvider.ForeColor = locked ? TextBg : TextFg;
                grpProvider.BackColor = DarkBg;

                foreach (var rb in new[] { rbHiNet, rbCloudflare, rbGoogle })
                {
                    rb.AutoCheck = !locked;        // lock to ensure wont be changed, didnt use Enabled to avoid greyed out
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
                //add colored button here
                if (btnApplyDns != null) StyleButton(btnApplyDns, AccentBg, Color.White, AccentHover, AccentDown);
                if (btnClearLogs != null) StyleButton(btnClearLogs, AccentBg, Color.White, AccentHover, AccentDown);
                if (btnDoneSelect != null) StyleButton(btnDoneSelect, AccentBg, Color.White, AccentHover, AccentDown);
                if (btnExePathCustomize != null) StyleButton(btnExePathCustomize, AccentBg, Color.White, AccentHover, AccentDown);


                //add normal button here
                foreach (var b in new[] { btnRefreshInterface, btnShowCurrentDns, btnToggleLogs, btnFlushDnsCache, btnQueryResponse })
                {
                    if (b != null) StyleButton(b, BtnBg, TextFg, BtnHover, BtnDown);
                }

                //timerInterval dark mode
                if (intervalAdjust != null)
                {
                    intervalAdjust.BackColor = PanelBg;
                    intervalAdjust.ForeColor = TextFg;
                    intervalAdjust.BorderStyle = BorderStyle.FixedSingle;
                }

            }


            Text = "AutoDNS";
            Width = leftPanelEndX; Height = 400; StartPosition = FormStartPosition.CenterScreen;

            // 設定 DNS 提供者選項
            chkAdGuard = new CheckBox { Left = 15, Top = 40, Width = 400, Text = "使用 AdGuard DNS (94.140.14.14 / 2a10:50c0::ad1:ff)" };
            chkAdGuard.Checked = true;
            chkAdGuard.CheckedChanged += (s, e) => { if (chkAdGuard.Checked) chkDhcp.Checked = false; UpdateProviderEnable(); };

            chkDhcp = new CheckBox { Left = 15, Top = 70, Width = 300, Text = "自動取得 DNS (DHCP)" };
            chkDhcp.CheckedChanged += (s, e) => { if (chkDhcp.Checked) chkAdGuard.Checked = false; UpdateProviderEnable(); };

            grpProvider = new GroupBox { Left = 15, Top = 105, Width = 420, Height = 150, Text = "未勾 AdGuard 與 DHCP 時，改用以下 DNS：" };
            rbHiNet = new RadioButton { Left = 20, Top = 25, Width = 350, Text = "HiNet (168.95.1.1 / 2001:b000:168::1)" };
            rbCloudflare = new RadioButton { Left = 20, Top = 55, Width = 350, Text = "Cloudflare (1.1.1.1 / 2606:4700:4700::1111)" };
            rbGoogle = new RadioButton { Left = 20, Top = 85, Width = 350, Text = "Google (8.8.8.8 / 2001:4860:4860::8888)" };
            rbCloudflare.Checked = true;
            grpProvider.Controls.AddRange(new Control[] { rbHiNet, rbCloudflare, rbGoogle });
            
            // 設定按鈕
            btnFlushDnsCache = new Button { Left = 15, Top = 265, Width = 97, Height = 30, Text = "清除 DNS 快取" };
            btnRefreshInterface = new Button { Left = 122, Top = 265, Width = 98, Height = 30, Text = "掃描介面卡" };
            btnShowCurrentDns = new Button { Left = 230, Top = 265, Width = 97, Height = 30, Text = "顯示目前 DNS" };
            btnToggleLogs = new Button { Left = 337, Top = 265, Width = 98, Height = 30, Text = "顯示紀錄：關" };
            btnApplyDns = new Button { Left = 15, Top = 305, Width = 312, Height = 30, Text = "套用設定" };
            btnQueryResponse = new Button { Left = 337, Top = 305, Width = 98, Height = 30, Text = "測試回應時間" };


            btnApplyDns.Click += async (s, e) => await ApplyAsync();
            btnRefreshInterface.Click += (s, e) => InitInterfaces();
            
            btnShowCurrentDns.Click += async (s, e) => await ShowDnsAsync();
            btnToggleLogs.Click += (s, e) => ToggleLogs();
            btnFlushDnsCache.Click += async (s, e) => await FlushDnsAsync();
            btnQueryResponse.Click += async (s, e) => await queryResponseTime();

            // 設定介面選擇區
            clbIfaces = new CheckedListBox { Left = rightPanelStartX, Top = 40, Width = 400, Height = 255, CheckOnClick = true };    //Select network interfaces
            chkSelectAll = new CheckBox { Left = rightPanelStartX, Top = 310, Width = 150, Text = "全選目前運作中的介面" };
            chkIncludeAdvanced = new CheckBox { Left = rightPanelStartX+150, Top = 310, Width = 160, Text = "包含進階/虛擬/撥接介面" };
            btnDoneSelect = new Button { Left = rightPanelStartX + 310, Top = 305, Width = 90, Height = 30, Text = "完成選擇" };
            btnDoneSelect.Click += (s, e) => doneSelect();

            // Bottom label
            var lbBottomBarInfo = new Label
            {
                Left = 15,
                Top = 340,
                Width = 865,
                Height = 30,
                Text = "提示：需要系統管理員權限。若設定未生效可嘗試重新連線或清除 DNS 快取。"
            };
            lbBottomBarInfo.Visible = false;


            //log output box
            txtLog = new TextBox { Left = rightPanelStartX, Top = 40, Width = 400, Height = 255, Multiline = true, ReadOnly = true };
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.Font = new Font("Consolas", 10);
            btnClearLogs = new Button { Left = rightPanelStartX, Top = 305, Width = 400, Height = 30, Text = "清除輸出紀錄" };
            btnClearLogs.Click += (s, e) => txtLog.Clear();


            //enable/disable auto switch
            chkAutoSwitch = new CheckBox { Left = 15, Top = 10, Width = 170, Text = "啟用/停用自動切換DNS" };
            chkAutoSwitch.Checked = false;

            chkAutoSwitch.CheckedChanged += (s, e) =>
            {
                canCheckAutoSwitch();
                if (chkAutoSwitch.Checked)
                {
                    if (!isAutoSwitchEnabled) enableAutoSwitch();
                }
                else
                {
                    if (isAutoSwitchEnabled) disableAutoSwitch();
                }
            };

            intervalAdjust = new NumericUpDown
            {
                Left = 277,
                Top = 10,
                Width = 50,
                Minimum = 3,
                Maximum = 9999,
                Increment = 1,
                DecimalPlaces = 0,
                ThousandsSeparator = false,
                TextAlign = HorizontalAlignment.Right,
                ReadOnly = false,
                InterceptArrowKeys = true
            };
            intervalAdjust.Value = (decimal)timerInterval;
            intervalAdjust.ValueChanged += (s, e) =>
            {
                timerInterval = (double)intervalAdjust.Value;
                autoPeriod = TimeSpan.FromSeconds(timerInterval);
            };

            btnExePathCustomize = new Button { Left = 337, Top = 10, Width = 98, Height = 23, Text = "自訂路徑" };

            btnExePathCustomize.Click += (s, e) => ExePathEditor.ShowExePathEditor(this);




            //add all controls to form
            Controls.AddRange(new Control[] { lbSelectInterface, lbBottomBarInfo, clbIfaces, chkSelectAll
                                            , chkIncludeAdvanced, chkAdGuard, chkDhcp, grpProvider, btnApplyDns
                                            , btnRefreshInterface, btnShowCurrentDns, btnToggleLogs, btnFlushDnsCache
                                            , txtLog, btnDoneSelect, btnClearLogs, logTitle, intervalAdjust
                                            , chkAutoSwitch, btnQueryResponse, lbIntervalAdjust, btnExePathCustomize });


            // load saved timerInterval
            timerInterval = Properties.Settings.Default.TimerInterval;
            if (timerInterval < 3) timerInterval = 3;
            if (timerInterval > 9999) timerInterval = 9999;
            intervalAdjust.Value = (decimal)timerInterval;
            autoPeriod = TimeSpan.FromSeconds(timerInterval);


            ApplyDarkMode();

            Load += (_, __) =>
            {
                InitInterfaces();
                ApplyAsync();
                exePathListKVP = ProviderLookup.OutputKeyValuePair();
            };
            chkSelectAll.CheckedChanged += (s, e) => SelectAllUpInterfaces(chkSelectAll.Checked);
            chkIncludeAdvanced.CheckedChanged += (s, e) => InitInterfaces();
            controlInterfaceUI(false);
            controlLogUI(false);
            UpdateProviderEnable();
            
            
        }


        // Save settings on close
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Properties.Settings.Default.TimerInterval = timerInterval;
            Properties.Settings.Default.Save();

            base.OnFormClosing(e);
        }


        private void controlInterfaceUI(bool isInterfaceVisible)
        {
            if (isInterfaceVisible)
            {
                lbSelectInterface.Visible = true;
                clbIfaces.Visible = true;
                chkSelectAll.Visible = true;
                chkIncludeAdvanced.Visible = true;
                btnDoneSelect.Visible = true;
            }
            else
            {
                lbSelectInterface.Visible = false;
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
                if (Width != rightPanelEndX)
                {
                    Left = this.Left - expandShiftX; //move left to center the expanded window
                }
                Width = rightPanelEndX;
            }
            else
            {
                if (Width != leftPanelEndX)
                {
                    Left = this.Left + expandShiftX; //move right to center the collapsed window
                }
                Width = leftPanelEndX;
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


        //current issue:
        // 1. unchecks all dns causing when no exe match will clear groupbox dns selection
        // step: uncheck adguard or dhcp then hit isExeRunning then 
        // nvm leave ts rn prob wont fix cuz too lazy
        // ::prob could fix with realConnectedDns var
        // well, already fixed ts by UIEnable function, unexpectedly
        //
        // 2. is it rly nessary to call UIEnable form another function? y not js call it directly
        // ::leave it for now, might need to do more things in the future
        // rly need to call UIEnable from other func for stopping autoSwitch
        // huh? seems like everything is fine now bruh
        //
        // 3. autoSwitch has not been fully tested yet
        // :: too lazy to do it rn, will do it later idk
        //
        // 4. didn't block click enable/disable autoSwitch when applying dns, although will block at autoSwitch func but the click state will be wrong
        // ::fixed by canCheckAutoSwitch function
        //
        // 5. queryResponseTime function does not show dns name
        // ::fixed by adding dns name array
        // hardcoded dns profiles for now, will make it dynamic later


        //wish list:
        // 1. add custom dns profile
        //
        // 2. add custom exe path with simple UI
        //
        // 3. add custom domain to do queryResponseTime
        //
        // 4. monitor outgoing dns traffic to select dns profile
        //
        // 5. use parallel dns for fastest response
        //
        // 6. add tray icon support and auto start with windows option


        public static class ExePathEditor
        {
            public static void ShowExePathEditor(IWin32Window owner = null)
            {
                using (var f = new ExePathEditorForm()) { f.ShowDialog(owner); }
            }
        }

        internal sealed class ExePathEditorForm : Form
        {
            // 資料模型（用 BindingList 保持順序並便於重排）
            private sealed class ExePathItem
            {
                public string Directory { get; set; } = "";
                public string Value { get; set; } = "";
            }

            private readonly DataGridView grid = new DataGridView();
            private readonly TextBox txtDir = new TextBox();
            private readonly ComboBox cmbValue = new ComboBox();
            private readonly Button btnApply = new Button();
            private readonly Button btnSave = new Button();
            private readonly Button btnClose = new Button();
            private readonly Button btnEdit = new Button();
            private readonly Button btnDelete = new Button();
            private readonly Label lblDir = new Label();
            private readonly Label lblVal = new Label();

            private readonly BindingList<ExePathItem> items = new BindingList<ExePathItem>();
            private string jsonPath = "";

            // 拖曳重排狀態
            private Rectangle dragBoxFromMouseDown = Rectangle.Empty;
            private int rowIndexFromMouseDown = -1;
            private bool modified = false;

            public ExePathEditorForm()
            {
                Text = "exePath.json 編輯器";
                StartPosition = FormStartPosition.CenterParent;
                MinimizeBox = false; MaximizeBox = false;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                Width = 900; Height = 600;

                // Grid
                grid.ReadOnly = true;
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                grid.MultiSelect = false;
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
                grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                grid.Top = 10; grid.Left = 10; grid.Width = 860; grid.Height = 340;
                grid.DataSource = items;
                grid.AllowDrop = true;
                grid.CellDoubleClick += (s, e) => LoadSelectedRowToInputs();
                grid.MouseDown += Grid_MouseDown;
                grid.MouseMove += Grid_MouseMove;
                grid.DragOver += Grid_DragOver;
                grid.DragDrop += Grid_DragDrop;

                // Inputs
                lblDir.Text = "Directory(.exe 路徑)：";
                lblDir.Top = 360; lblDir.Left = 10; lblDir.Width = 180;

                txtDir.Top = 382; txtDir.Left = 10; txtDir.Width = 670; txtDir.Anchor = AnchorStyles.Left | AnchorStyles.Right;

                lblVal.Text = "Value：";
                lblVal.Top = 360; lblVal.Left = 690; lblVal.Width = 60;

                cmbValue.Top = 382; cmbValue.Left = 690; cmbValue.Width = 180;
                cmbValue.DropDownStyle = ComboBoxStyle.DropDownList;
                cmbValue.Items.AddRange(new object[] { "Cloudflare", "Google", "AdGuard", "HiNet" });

                // Row ops
                btnEdit.Text = "編輯(載入下方)";
                btnEdit.Top = 420; btnEdit.Left = 10; btnEdit.Width = 170;
                btnEdit.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
                btnEdit.Click += OnEdit;

                btnDelete.Text = "刪除選取列";
                btnDelete.Top = 420; btnDelete.Left = 190; btnDelete.Width = 140;
                btnDelete.Anchor = AnchorStyles.Left | AnchorStyles.Bottom;
                btnDelete.Click += OnDelete;

                // Bottom buttons
                btnApply.Text = "Apply";
                btnApply.Top = 500; btnApply.Left = 470; btnApply.Width = 120;
                btnApply.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                btnApply.Click += OnApply;

                btnSave.Text = "Save";
                btnSave.Top = 500; btnSave.Left = 600; btnSave.Width = 120;
                btnSave.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                btnSave.Click += OnSave;

                btnClose.Text = "Close";
                btnClose.Top = 500; btnClose.Left = 730; btnClose.Width = 140;
                btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
                btnClose.DialogResult = DialogResult.Cancel;
                btnClose.Click += OnClose;

                Controls.Add(grid);
                Controls.Add(lblDir);
                Controls.Add(txtDir);
                Controls.Add(lblVal);
                Controls.Add(cmbValue);
                Controls.Add(btnEdit);
                Controls.Add(btnDelete);
                Controls.Add(btnApply);
                Controls.Add(btnSave);
                Controls.Add(btnClose);

                Load += OnLoad;
            }

            private void OnLoad(object sender, EventArgs e)
            {
                try
                {
                    var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                    var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                    jsonPath = Path.Combine(exeDir, "exePath.json");

                    items.Clear();

                    if (File.Exists(jsonPath))
                    {
                        // 用 JsonDocument 依「檔案中屬性出現順序」讀入
                        using var doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in doc.RootElement.EnumerateObject())
                            {
                                items.Add(new ExePathItem
                                {
                                    Directory = prop.Name,
                                    Value = prop.Value.ValueKind == JsonValueKind.String ? (prop.Value.GetString() ?? "") : prop.Value.ToString()
                                });
                            }
                        }
                    }
                    // 若不存在檔案 -> 空列表，等待新增
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"載入 exePath.json 失敗：{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            // ===== Drag & Drop 重排 =====
            private void Grid_MouseDown(object? sender, MouseEventArgs e)
            {
                rowIndexFromMouseDown = grid.HitTest(e.X, e.Y).RowIndex;
                if (rowIndexFromMouseDown != -1)
                {
                    var dragSize = SystemInformation.DragSize;
                    dragBoxFromMouseDown = new Rectangle(
                        new Point(e.X - dragSize.Width / 2, e.Y - dragSize.Height / 2),
                        dragSize
                    );
                }
                else dragBoxFromMouseDown = Rectangle.Empty;
            }

            private void Grid_MouseMove(object? sender, MouseEventArgs e)
            {
                if ((e.Button & MouseButtons.Left) == MouseButtons.Left)
                {
                    if (dragBoxFromMouseDown != Rectangle.Empty && !dragBoxFromMouseDown.Contains(e.X, e.Y))
                    {
                        grid.DoDragDrop(grid.Rows[rowIndexFromMouseDown], DragDropEffects.Move);
                    }
                }
            }

            private void Grid_DragOver(object? sender, DragEventArgs e)
            {
                e.Effect = DragDropEffects.Move;
            }

            private void Grid_DragDrop(object? sender, DragEventArgs e)
            {
                var clientPoint = grid.PointToClient(new Point(e.X, e.Y));
                int dropIndex = grid.HitTest(clientPoint.X, clientPoint.Y).RowIndex;
                if (dropIndex < 0) dropIndex = items.Count - 1;

                if (rowIndexFromMouseDown < 0 || dropIndex < 0 || rowIndexFromMouseDown == dropIndex) return;

                // 取出被拖曳的項目
                var moved = items[rowIndexFromMouseDown];
                items.RemoveAt(rowIndexFromMouseDown);

                // 移除後，若往下拖，需要把目標索引 -1
                if (dropIndex > rowIndexFromMouseDown) dropIndex--;

                if (dropIndex < 0) dropIndex = 0;
                if (dropIndex > items.Count) dropIndex = items.Count;

                items.Insert(dropIndex, moved);

                grid.ClearSelection();
                grid.Rows[dropIndex].Selected = true;
                grid.CurrentCell = grid.Rows[dropIndex].Cells[0];

                // 清除拖曳狀態
                rowIndexFromMouseDown = -1;
                dragBoxFromMouseDown = Rectangle.Empty;
                modified = true;
            }

            // ===== Row 操作 =====
            private void OnEdit(object? sender, EventArgs e) => LoadSelectedRowToInputs();

            private void LoadSelectedRowToInputs()
            {
                if (grid.CurrentRow == null)
                {
                    MessageBox.Show(this, "先選取一列再按「編輯」", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var dir = grid.CurrentRow.Cells["Directory"].Value?.ToString() ?? "";
                var val = grid.CurrentRow.Cells["Value"].Value?.ToString() ?? "";
                txtDir.Text = dir;
                cmbValue.Text = val;
                txtDir.Focus();
                txtDir.SelectionStart = txtDir.TextLength;
            }

            private void OnDelete(object? sender, EventArgs e)
            {
                if (grid.CurrentRow == null)
                {
                    MessageBox.Show(this, "先選取一列再按「刪除」", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                var dir = grid.CurrentRow.Cells["Directory"].Value?.ToString();
                if (string.IsNullOrEmpty(dir)) return;

                var confirm = MessageBox.Show(this, $"確定刪除？\n{dir}", "確認刪除",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                for (int i = 0; i < items.Count; i++)
                {
                    if (string.Equals(items[i].Directory, dir, StringComparison.OrdinalIgnoreCase))
                    {
                        items.RemoveAt(i);
                        break;
                    }
                }
                txtDir.Clear();
                cmbValue.SelectedIndex = -1;
                modified = true;
            }

            // ===== 底部三鍵 =====
            private void OnApply(object? sender, EventArgs e)
            {
                if (!UpsertFromInputs()) return;
                try { SaveJson(); }
                catch (Exception ex)
                { MessageBox.Show(this, $"寫入 exePath.json 失敗：{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                // don't close window

                exePathListKVP = ProviderLookup.OutputKeyValuePair();
            }

            private void OnSave(object? sender, EventArgs e)
            {
                if (!UpsertFromInputs()) { /* don't close window */ return; }
                try { SaveJson(); }
                catch (Exception ex)
                { MessageBox.Show(this, $"寫入 exePath.json 失敗：{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                DialogResult = DialogResult.OK;
                Close();

                exePathListKVP = ProviderLookup.OutputKeyValuePair();
            }

            private void OnClose(object? sender, EventArgs e)
            {
                DialogResult = DialogResult.Cancel;
                Close();

                exePathListKVP = ProviderLookup.OutputKeyValuePair();
            }

            // 新增或更新（不改變順序；不存在則附加在最後）
            private bool UpsertFromInputs()
            {
                var dir = txtDir.Text.Trim();
                var val = cmbValue.Text.Trim();

                if (string.IsNullOrWhiteSpace(dir) && modified == false)
                {
                    MessageBox.Show(this, "請輸入 directory(完整 .exe 路徑)", "Oops",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                if (string.IsNullOrWhiteSpace(val) && modified == false)
                {
                    MessageBox.Show(this, "請選擇/輸入 value", "Oops",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                for (int i = 0; i < items.Count; i++)
                {
                    if (string.Equals(items[i].Directory, dir, StringComparison.OrdinalIgnoreCase))
                    {
                        items[i].Value = val; // 更新原位，不動順序
                        grid.Refresh(); // 確保畫面更新
                        grid.ClearSelection();
                        grid.Rows[i].Selected = true;
                        grid.CurrentCell = grid.Rows[i].Cells[0];
                        return true;
                    }
                }
                // 沒找到 -> 追加在最後
                items.Add(new ExePathItem { Directory = dir, Value = val });
                grid.ClearSelection();
                var idx = items.Count - 1;
                grid.Rows[idx].Selected = true;
                grid.CurrentCell = grid.Rows[idx].Cells[0];
                return true;
            }

            // 以「目前列表順序」輸出為 JSON 物件（保持屬性順序）
            private void SaveJson()
            {
                Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);

                using var fs = new FileStream(jsonPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true, SkipValidation = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                writer.WriteStartObject();
                foreach (var it in items)
                {
                    writer.WriteString(it.Directory, it.Value);
                }
                writer.WriteEndObject();
                writer.Flush();

            }
        }

        private async Task queryResponseTime()
        {
            if (isAutoSwitchEnabled)
            {
                Program.ShowDarkInfo(this, "請先停用自動切換 DNS 後再進行測試。", "AutoDNS");
                return;
            }

            if (DeadLockCheck()) return;
            isPerforming = true;

            canCheckAutoSwitch();

            controlLogUI(true);
            expandWindow(true);
            controlInterfaceUI(false);

            DnsProfile queryProfile1 = AdGuard;
            DnsProfile queryProfile2 = HiNet;
            DnsProfile queryProfile3 = Cloudflare;
            DnsProfile queryProfile4 = Google;

            var queryTimes = 10;

            var script = $$"""
                    
                    $servers=@('{{queryProfile1.IPv4Primary}}','{{queryProfile2.IPv4Primary}}','{{queryProfile3.IPv4Primary}}','{{queryProfile4.IPv4Primary}}')
                    $names=@('{{queryProfile1.Name}}','{{queryProfile2.Name}}','{{queryProfile3.Name}}','{{queryProfile4.Name}}')
                    $domains=@(
                      'youtube.com',                # Google CDN
                      'store.steampowered.com',     # Valve
                      'netflix.com',                # Multiple CDN
                      'assets1.xboxlive.com',       # Microsoft CDN
                      'cdn.cloudflare.steamstatic.com' # Cloudflare
                    )

                    $result=@()
                    foreach($s in $servers){
                        $idx=[Array]::IndexOf($servers,$s)
                        foreach($d in $domains){
                            $times=@()
                            1..{{queryTimes}} | % {
                                $t=(Measure-Command {
                                    Resolve-DnsName -Server $s $d -Type A -NoHostsFile -ErrorAction SilentlyContinue
                                }).TotalMilliseconds
                                $times+=$t
                            }
                            $stat=$times | Measure-Object -Average -Minimum -Maximum
                            $result += [pscustomobject]@{
                                Domain=$d
                                Name=$names[$idx]
                                Avg=[math]::Round($stat.Average,3)
                            }
                        }
                    }

                    $sorted=$result | Sort-Object Domain,Avg

                    $mins=@{}
                    $sorted | Group-Object Domain | ForEach-Object {
                        $mins[$_.Name]=($_.Group | Measure-Object Avg -Minimum).Minimum
                    }

                    # add * to the minimum avg
                    $fmt='{0,-30} {1,-12} {2,8}'
                    Write-Host ($fmt -f 'Domain','Name','Avg')
                    Write-Host ($fmt -f '------','----','---')

                    $sorted | Group-Object Domain | ForEach-Object {
                        $_.Group | ForEach-Object {
                            $min=$mins[$_.Domain]
                            $avgTxt = if($_.Avg -eq $min){ '{0}*' -f $_.Avg } else { '{0}' -f $_.Avg }
                            Write-Host ($fmt -f $_.Domain, $_.Name, $avgTxt)
                        }
                        Write-Host "---"
                    }






                #  ================================================
                #  ========== Querying DNS Response Time ==========
                #  ====== May take some time, please wait... ======
                #  ================================================
                




                """;


            await RunPowerShellAsync(script);

            isPerforming = false;
            canCheckAutoSwitch();
        }

        private void canCheckAutoSwitch()
        {
            Color TextFg = Color.White;
            Color TextBg = Color.DimGray;

            if (isAutoSwitchEnabled) return; //prevent changing state when autoSwitch is already enabled

            if (!isPerforming)
            {
                chkAutoSwitch.AutoCheck = true;
                chkAutoSwitch.Cursor = Cursors.Default;
                chkAutoSwitch.ForeColor = TextFg;
                lbIntervalAdjust.ForeColor  = TextFg;
            }
            else
            {
                chkAutoSwitch.Checked = false;
                chkAutoSwitch.AutoCheck = false;
                chkAutoSwitch.Cursor = Cursors.No;
                chkAutoSwitch.ForeColor = TextBg;
                lbIntervalAdjust.ForeColor = TextBg;
            }
        }

        private void enableAutoSwitch()
        {
            if (DeadLockCheck()) return;
            if (isAutoSwitchEnabled) return; //prevent multiple enable
            isAutoSwitchEnabled = true;
            UIDisable();

            // 先清掉任何殘留
            autoSwitchCts?.Cancel();
            autoSwitchCts?.Dispose();

            // 開新一輪背景輪詢
            autoSwitchCts = new CancellationTokenSource();
            autoSwitchTask = runAutoSwitchLoopAsync(autoSwitchCts.Token); // 不阻塞 UI
        }

        private async Task autoDnsSwitch()
        {

            //prob will be an issue after implementing auto do ts for interval
            // ::seems fine now, might need more testing later
            if (DeadLockCheck()) return;
            isPerforming = true;
            

            bool matchAnyExe = false;
            var matchedExe = new KeyValuePair<string, string>();
            foreach (var kvp in exePathListKVP)
            {
                if (ProgramIsRunning(kvp.Key))
                {
                    matchAnyExe = true; //upper exe path got higher priority
                    matchedExe = kvp;
                    break;
                }
            }
            if (matchAnyExe)
            {
                Log($"Key: {matchedExe.Key}, Value: {matchedExe.Value}");

                if (!checkIfSameDns(prevDnsProvider, matchedExe.Value))
                {
                    await DnsSwitcher(matchedExe.Value);
                }
            }
            else
            {
                Log("No matched exe running, revert to previous DNS");
                await DnsSwitcher(prevDnsProvider);
            }

            async Task DnsSwitcher(string targetDns)
            {
                
                if (!checkIfSameDns(CurrentProfile().Name, targetDns))
                {
                    isPerforming = false;   //release lock to avoid deadlock in ApplyAsync
                    switchCheckProfile(targetDns);
                    await ApplyAsync();
                    Log($"Switched to {targetDns} DNS provider.");
                    isPerforming = true;   //reacquire lock
                }

            }

            isPerforming = false;


            static bool ProgramIsRunning(string fullPath)
            {
                // 標準化並去掉尾端斜線
                string target = Path.GetFullPath(fullPath)
                                  .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                string exeName = Path.GetFileNameWithoutExtension(target);

                foreach (var p in Process.GetProcessesByName(exeName))
                {
                    try
                    {
                        string procPath = Path.GetFullPath(p.MainModule!.FileName)
                                               .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        if (string.Equals(procPath, target, StringComparison.OrdinalIgnoreCase))
                            return true; // 同檔名、同完整路徑 → 命中
                    }
                    catch (Win32Exception)
                    {
                        // 權限不足或跨位元數，略過即可
                    }
                    catch (InvalidOperationException)
                    {
                        // 行程可能在讀取前就結束了，略過
                    }
                    finally
                    {
                        p.Dispose(); // 釋放資源
                    }
                }
                return false;
            }
        
        }

        private void disableAutoSwitch()
        {
            isAutoSwitchEnabled = false;
            UIEnable();

            // 停止背景輪詢
            autoSwitchCts?.Cancel();
            autoSwitchCts?.Dispose();
            autoSwitchCts = null;
            autoSwitchTask = null;

        }

        private async Task runAutoSwitchLoopAsync(CancellationToken token)
        {

            // 先跑一次（立即生效）
            await safeAutoSwitchOnceAsync(token);

            // 固定週期
            using var timer = new System.Threading.PeriodicTimer(autoPeriod);
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    await safeAutoSwitchOnceAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常結束
            }
        }

        private async Task safeAutoSwitchOnceAsync(CancellationToken token)
        {
            // 防重入：上一輪還在跑就直接跳過這一輪
            if (!await autoSwitchGate.WaitAsync(0, token)) return;

            try
            {
                await autoDnsSwitch();
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                Log($"AutoSwitch error: {ex.Message}");
            }
            finally
            {
                autoSwitchGate.Release();
            }
        }

        private void UIDisable()
        {
            Color DarkBg = Color.FromArgb(28, 28, 30);
            Color TextBg = Color.DimGray;

            clearSelectedDns();

            grpProvider.ForeColor = TextBg;
            grpProvider.BackColor = DarkBg;

            foreach (var rb in new RadioButton[] { rbHiNet, rbCloudflare, rbGoogle })
            {
                rb.AutoCheck = false;
                rb.Cursor = Cursors.No;
                rb.ForeColor = TextBg;
                rb.BackColor = DarkBg;
            }

            foreach (var cb in new CheckBox[] { chkAdGuard, chkDhcp })
            {
                cb.AutoCheck = false;
                cb.Cursor = Cursors.No;
                cb.ForeColor = TextBg;
                cb.BackColor = DarkBg;
            }

            intervalAdjust.Enabled = false;
            lbIntervalAdjust.ForeColor = TextBg;

            btnExePathCustomize.Enabled = false;
            

        }

        private void UIEnable()
        {
            Color DarkBg = Color.FromArgb(28, 28, 30);
            Color PanelBg = Color.FromArgb(38, 38, 42);
            Color TextFg = Color.White;
            Color TextBg = Color.DimGray;

            bool locked = chkAdGuard.Checked || chkDhcp.Checked;

            grpProvider.ForeColor = locked ? TextBg : TextFg;
            grpProvider.BackColor = DarkBg;

            foreach (var rb in new[] { rbHiNet, rbCloudflare, rbGoogle })
            {
                rb.AutoCheck = !locked;        // 鎖住選擇確保深色主題字色
                rb.Cursor = locked ? Cursors.No : Cursors.Default;
                rb.ForeColor = locked ? TextBg : TextFg;
                rb.BackColor = DarkBg;
            }

            foreach (var cb in new CheckBox[] { chkAdGuard, chkDhcp })
            {
                cb.AutoCheck = true;
                cb.Cursor = Cursors.Default;
                cb.ForeColor = TextFg;
                cb.BackColor = DarkBg;
            }

            switchCheckProfile(realConnectedDns);

            intervalAdjust.Enabled = true;
            lbIntervalAdjust.ForeColor = TextFg;

            btnExePathCustomize.Enabled = true;
            

        }

        private void switchCheckProfile(string profileName)
        {
            clearSelectedDns();
            switch (profileName)
            {
                case "AdGuard":
                    chkAdGuard.Checked = true;
                    break;
                case "Dhcp":
                    chkDhcp.Checked = true;
                    break;
                case "HiNet":
                    rbHiNet.Checked = true;
                    break;
                case "Cloudflare":
                    rbCloudflare.Checked = true;
                    break;
                case "Google":
                    rbGoogle.Checked = true;
                    break;
                default:
                    chkAdGuard.Checked = true; //fallback to adguard
                    break;
            }
        }

        public void clearSelectedDns()
        {
            rbHiNet.Checked = false;
            rbCloudflare.Checked = false;
            rbGoogle.Checked = false;
            chkAdGuard.Checked = false;
            chkDhcp.Checked = false;
        }

        private bool DeadLockCheck()
        {
            if (isPerforming)
            {
                Program.ShowDarkInfo(this, "目前有其他操作正在進行中，請稍後再試。", "AutoDNS");
                return true;
            }
            return false;
        }

        public static class ProviderLookup
        {
            public static Dictionary<string, string> OutputKeyValuePair(string jsonFileName = "exePath.json")
            {

                // 找同目錄的 json
                string baseDir = AppDomain.CurrentDomain.BaseDirectory; // 兼容 WinForms/.NET Framework
                string jsonPath = Path.Combine(baseDir, jsonFileName);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


                try
                {
                    using var fs = File.OpenRead(jsonPath);
                    using var doc = JsonDocument.Parse(fs);

                    // expect：{ "C:\\exampleDir\\example.exe": "Cloudflare", ... }
                    // also normalize path to full path without trailing slash
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        string k = NormalizePath(prop.Name);
                        string? v = prop.Value.GetString();

                        if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(v))
                            map[k] = v!;

                    }
                }
                catch
                { 
                
                }
                return map;
            }

            private static string NormalizePath(string path)
            {
                try
                {
                    // 轉成完整路徑 & 去掉尾端斜線，Windows 用 OrdinalIgnoreCase 比較
                    return Path.GetFullPath(path)
                               .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                catch
                {
                    // 無效路徑就原樣修剪
                    return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
            }
        }

        private bool checkIfSameDns(string OGDns, string NBDns)
        {
            if(OGDns == NBDns)
            {
                Log("Same DNS provider as before, no need to re-apply.");
                switchCheckProfile(OGDns);
                return true;
            }
            else
            {
                return false;
            }
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

            if (DeadLockCheck()) return;

            isPerforming = true;

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

            // 若是由 btnRefreshInterface 觸發
            if (ActiveControl == btnRefreshInterface)
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

            isPerforming = false;

        }

        private void SelectAllUpInterfaces(bool check)
        {
            for (int i = 0; i < clbIfaces.Items.Count; i++) clbIfaces.SetItemChecked(i, check);
        }
        
        public DnsProfile CurrentProfile()
        {
            if (chkAdGuard.Checked) return AdGuard; //AdGuard and Dhcp can override other selections
            if (chkDhcp.Checked) return DhcpPlaceHolder;
            if (rbHiNet.Checked) return HiNet;
            if (rbGoogle.Checked) return Google;
            if (rbCloudflare.Checked) return Cloudflare;
            return AdGuard;
        }

        private async Task ApplyAsync([CallerMemberName] string? caller = null)
        {

            if (isAutoSwitchEnabled && caller != nameof(autoDnsSwitch))
            {
                Program.ShowDarkInfo(this, "自動切換功能已啟用，請先停用後再手動套用。", "AutoDNS");
                return;
            }

            if (DeadLockCheck()) return;

            var selected = clbIfaces.CheckedItems.Cast<InterfaceItem>().ToList();
            if (selected.Count == 0)
            {
                Program.ShowDarkInfo(this, "請至少選擇一個介面。", "AutoDNS");
                return;
            }

            var profile = CurrentProfile();
            string sig = profile.Name + "|" + string.Join(",", selected.Select(i => i.Id).OrderBy(x => x));

            if (sig == lastApplySignature && caller != nameof(autoDnsSwitch))
            {
                Log("Same DNS and interface as before, no need to re-apply.");
                Program.ShowDarkInfo(this, "目前的 DNS 及介面卡與上次相同，無需重新套用。", "AutoDNS");
                return;
            }

            isPerforming = true;
            canCheckAutoSwitch();

            realConnectedDns = CurrentProfile().Name;
            lastApplySignature = sig;

            if (chkDhcp.Checked)
            {
                Log($"\nCALLER: {caller}");
                if (caller != nameof(autoDnsSwitch))
                {
                    prevDnsProvider = profile.Name;
                    Log($"SAVED PREVDNS: {prevDnsProvider}");
                }
                isPerforming = false;
                await SetDhcpAsync(selected, caller);
                return;
            }

            Log($"\r\n=== 套用 DNS 設定：{profile.Name} ===");
            Log($"IPv4: {profile.IPv4Primary}, {profile.IPv4Secondary}");
            Log($"IPv6: {profile.IPv6Primary}, {profile.IPv6Secondary}");

            foreach (var nic in selected)
            {
                Log($"\r\n介面：{nic.Name} (ifIndex={nic.IfIndex})");

                // 確保 IPv6 已啟用
                await EnsureIPv6BindingAsync(nic.Name);

                // 使用 PowerShell 一次性覆蓋整個 DNS 清單
                var v4Cmd = nic.IfIndex >= 0
                    ? $"Set-DnsClientServerAddress -InterfaceIndex {nic.IfIndex} -ServerAddresses \"{profile.IPv4Primary}\",\"{profile.IPv4Secondary}\""
                    : $"Set-DnsClientServerAddress -InterfaceAlias '{EscapeForPS(nic.Name)}' -ServerAddresses \"{profile.IPv4Primary}\",\"{profile.IPv4Secondary}\"";
                var v6Cmd = nic.IfIndex >= 0
                    ? $"Set-DnsClientServerAddress -InterfaceIndex {nic.IfIndex} -ServerAddresses \"{profile.IPv6Primary}\",\"{profile.IPv6Secondary}\""
                    : $"Set-DnsClientServerAddress -InterfaceAlias '{EscapeForPS(nic.Name)}' -ServerAddresses \"{profile.IPv6Primary}\",\"{profile.IPv6Secondary}\"";

                var ec4 = await RunPowerShellAsync(v4Cmd);
                if (ec4 != 0)
                {
                    // fallback use netsh IPv4
                    await RunNetshAsync($"interface ipv4 set dns name=\"{nic.Name}\" static {profile.IPv4Primary} primary");
                    await RunNetshAsync($"interface ipv4 add dns name=\"{nic.Name}\" {profile.IPv4Secondary} index=2 validate=no");
                }

                var ec6 = await RunPowerShellAsync(v6Cmd);
                if (ec6 != 0)
                {
                    // fallback use netsh IPv6
                    await RunNetshAsync($"interface ipv6 delete dnsservers name=\"{nic.Name}\" all");
                    await RunNetshAsync($"interface ipv6 add dnsserver name=\"{nic.Name}\" address={profile.IPv6Primary} index=1 validate=no");
                    await RunNetshAsync($"interface ipv6 add dnsserver name=\"{nic.Name}\" address={profile.IPv6Secondary} index=2 validate=no");
                }
            }

            Log("\r\n完成。若應用程式/瀏覽器仍未生效，請嘗試重新連線或清除 DNS 快取：ipconfig /flushdns");

            if (prevDnsProvider != "" && caller != nameof(autoDnsSwitch))
            {
                Program.ShowDarkInfo(this, $"已套用：{profile.Name} DNS\n", "AutoDNS");
            }


            Log($"\nCALLER: {caller}");

            if (caller != nameof(autoDnsSwitch))
            {
                prevDnsProvider = profile.Name;
                Log($"SAVED PREVDNS: {prevDnsProvider}");

            }

            isPerforming = false;
            canCheckAutoSwitch();

        }

        private async Task SetDhcpAsync(List<InterfaceItem> selected, [CallerMemberName] string? caller = null)
        {

            if (DeadLockCheck()) return;

            if (selected.Count == 0)
            {
                Program.ShowDarkInfo(this, "請至少選擇一個介面。", "AutoDNS");
                return;
            }

            isPerforming = true;
            canCheckAutoSwitch();

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

            if (caller != nameof(autoDnsSwitch))
            {
                Program.ShowDarkInfo(this, "已切換為自動取得 (DHCP)\n", "AutoDNS");
            }

            isPerforming = false;
            canCheckAutoSwitch();
        }

        private async Task ShowDnsAsync()
        {

            if (DeadLockCheck()) return;

            var selected = clbIfaces.CheckedItems.Cast<InterfaceItem>().ToList();
            if (selected.Count == 0)
            {
                Program.ShowDarkInfo(this, "請至少選擇一個介面。", "AutoDNS");
                return;
            }

            isPerforming = true;

            Log("\n=== 目前 DNS 設定 ===");
            foreach (var nic in selected)
            {
                Log($"介面：{nic.Name} (ifIndex={nic.IfIndex})");

                // use powershell first, if fails fallback to netsh
                string baseCmdIdx = nic.IfIndex >= 0 ? $"-InterfaceIndex {nic.IfIndex}" : $"-InterfaceAlias '{EscapeForPS(nic.Name)}'";
                int e1 = await RunPowerShellAsync($"$a=(Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily IPv4).ServerAddresses; Write-Host ('IPv4: ' + ($a -join ', '))");
                int e2 = await RunPowerShellAsync($"$b=(Get-DnsClientServerAddress {baseCmdIdx} -AddressFamily IPv6).ServerAddresses; Write-Host ('IPv6: ' + ($b -join ', '))");
                if (e1 != 0 || e2 != 0)
                {
                    await RunNetshAsync($"interface ipv4 show dnsservers name=\"{nic.Name}\"");
                    await RunNetshAsync($"interface ipv6 show dnsservers name=\"{nic.Name}\"");
                }
            }

            // 彙整摘要以 MessageBox 顯示
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

            isPerforming = false;

        }

        private async Task FlushDnsAsync()
        {

            if (DeadLockCheck()) return;

            isPerforming = true;

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
        
            isPerforming = false;

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
            if (txtLog.InvokeRequired) { 
                txtLog.Invoke(new Action<string>(Log), msg);
                return; 
            }
            txtLog.AppendText(msg + Environment.NewLine);
            const int maxLines = 1000;
            if (txtLog.Lines.Length > maxLines)
            {
                txtLog.Lines = txtLog.Lines.Skip(maxLines/2).ToArray();
                txtLog.SelectionStart = txtLog.TextLength;
                txtLog.ScrollToCaret();
            }
        }
    
    }
}
