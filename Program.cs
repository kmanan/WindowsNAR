using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetworkAdapterRestarter
{
    public class MainForm : Form
    {
        private ListBox adapterListBox = null!;
        private Button refreshButton = null!;
        private Button restartButton = null!;
        private CheckBox autoRestartCheckBox = null!;
        private CheckBox showAdvancedAdaptersCheckBox = null!;
        private Label statusLabel = null!;
        private Label monitorStatusLabel = null!;
        private LinkLabel krytonLink = null!;
        private System.Windows.Forms.Timer monitorTimer = null!;
        private NotifyIcon trayIcon = null!;
        private ContextMenuStrip trayMenu = null!;
        private string? selectedAdapterId;
        private string? monitoredAdapterId;
        private string? monitoredAdapterName;
        private long? lastTrafficBytes;
        private int failedMonitorChecks;
        private bool monitorCheckInProgress;
        private bool exitRequested;

        private const int MonitorIntervalMilliseconds = 30000;
        private const int FailedChecksBeforeRestart = 2;

        public MainForm()
        {
            InitializeUI();
            LoadAdapters();
        }

        private void InitializeUI()
        {
            this.Text = "Network Adapter Restarter";
            this.Size = new System.Drawing.Size(500, 480);
            this.MinimumSize = new System.Drawing.Size(400, 480);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Set the icon
            try
            {
                string iconPath = System.IO.Path.Combine(
                    AppContext.BaseDirectory,
                    "icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    this.Icon = new System.Drawing.Icon(iconPath);
                }
            }
            catch
            {
                // Icon not found, continue without it
            }

            // ListBox for adapters
            adapterListBox = new ListBox
            {
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(440, 220)
            };
            adapterListBox.SelectedIndexChanged += AdapterListBox_SelectedIndexChanged;

            // Restart button
            restartButton = new Button
            {
                Text = "Restart Selected Adapter",
                Location = new System.Drawing.Point(20, 250),
                Size = new System.Drawing.Size(200, 30)
            };
            restartButton.Click += RestartButton_Click;

            // Refresh button
            refreshButton = new Button
            {
                Text = "Refresh List",
                Location = new System.Drawing.Point(230, 250),
                Size = new System.Drawing.Size(100, 30)
            };
            refreshButton.Click += (s, e) => LoadAdapters();

            // Auto restart monitor
            autoRestartCheckBox = new CheckBox
            {
                Text = "Auto-restart selected adapter when traffic stops",
                Location = new System.Drawing.Point(20, 290),
                Size = new System.Drawing.Size(350, 24)
            };
            autoRestartCheckBox.CheckedChanged += AutoRestartCheckBox_CheckedChanged;

            showAdvancedAdaptersCheckBox = new CheckBox
            {
                Text = "Show advanced adapters",
                Location = new System.Drawing.Point(20, 315),
                Size = new System.Drawing.Size(180, 24)
            };
            showAdvancedAdaptersCheckBox.CheckedChanged += (s, e) => LoadAdapters();

            // Status label
            statusLabel = new Label
            {
                Location = new System.Drawing.Point(20, 350),
                Size = new System.Drawing.Size(440, 20),
                Text = "Select an adapter and click Restart"
            };

            // Monitor status label
            monitorStatusLabel = new Label
            {
                Location = new System.Drawing.Point(20, 375),
                Size = new System.Drawing.Size(440, 35),
                Text = "Auto-restart monitor is off"
            };

            // Kryton Labs link
            krytonLink = new LinkLabel
            {
                Location = new System.Drawing.Point(20, 425),
                Size = new System.Drawing.Size(200, 20),
                Text = "Made by Kryton Labs",
                LinkColor = System.Drawing.Color.DodgerBlue
            };
            krytonLink.LinkClicked += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://krytonlabs.com",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            monitorTimer = new System.Windows.Forms.Timer
            {
                Interval = MonitorIntervalMilliseconds
            };
            monitorTimer.Tick += MonitorTimer_Tick;

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open", null, (s, e) => RestoreFromTray());
            trayMenu.Items.Add("Exit", null, (s, e) =>
            {
                exitRequested = true;
                trayIcon.Visible = false;
                Close();
            });

            trayIcon = new NotifyIcon
            {
                Text = "Network Adapter Restarter",
                Icon = this.Icon ?? System.Drawing.SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = false
            };
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();
            this.Resize += MainForm_Resize;
            this.FormClosing += MainForm_FormClosing;

            this.Controls.Add(adapterListBox);
            this.Controls.Add(restartButton);
            this.Controls.Add(refreshButton);
            this.Controls.Add(autoRestartCheckBox);
            this.Controls.Add(showAdvancedAdaptersCheckBox);
            this.Controls.Add(statusLabel);
            this.Controls.Add(monitorStatusLabel);
            this.Controls.Add(krytonLink);
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized)
            {
                return;
            }

            HideToTray();
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (exitRequested)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayMenu.Dispose();
                return;
            }

            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayMenu.Dispose();
        }

        private void HideToTray()
        {
            trayIcon.Visible = true;
            ShowInTaskbar = false;
            Hide();
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            trayIcon.Visible = false;
            Activate();
        }

        private void LoadAdapters()
        {
            string? adapterIdToRestore = selectedAdapterId ?? GetSelectedAdapter()?.Id;
            adapterListBox.Items.Clear();
            statusLabel.Text = "Loading adapters...";

            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(adapter => IsUserSelectableAdapter(adapter, showAdvancedAdaptersCheckBox.Checked))
                    .OrderBy(adapter => adapter.Name))
                {
                    adapterListBox.Items.Add(new AdapterListItem(adapter));
                }

                SelectAdapterById(adapterIdToRestore);
                statusLabel.Text = $"Found {adapterListBox.Items.Count} adapter(s)";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error loading adapters: {ex.Message}";
            }
        }

        private void AdapterListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            AdapterListItem? selectedAdapter = GetSelectedAdapter();
            if (selectedAdapter != null)
            {
                selectedAdapterId = selectedAdapter.Id;
            }

            if (!autoRestartCheckBox.Checked || adapterListBox.SelectedItem == null)
            {
                return;
            }

            if (selectedAdapter == null)
            {
                return;
            }

            selectedAdapterId = selectedAdapter.Id;

            monitoredAdapterId = selectedAdapter.Id;
            monitoredAdapterName = selectedAdapter.Name;
            ResetMonitorState();
            monitorStatusLabel.Text = $"Monitoring {monitoredAdapterName}";
            _ = RunMonitorCheckAsync();
        }

        private void AutoRestartCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (!autoRestartCheckBox.Checked)
            {
                monitorTimer.Stop();
                monitoredAdapterId = null;
                monitoredAdapterName = null;
                ResetMonitorState();
                monitorStatusLabel.Text = "Auto-restart monitor is off";
                return;
            }

            if (adapterListBox.SelectedItem == null)
            {
                MessageBox.Show("Select the adapter you want to monitor first.", "No Adapter Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                autoRestartCheckBox.Checked = false;
                return;
            }

            AdapterListItem? selectedAdapter = GetSelectedAdapter();
            if (selectedAdapter == null)
            {
                MessageBox.Show("The selected adapter is no longer available.", "Adapter Unavailable",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                autoRestartCheckBox.Checked = false;
                return;
            }

            selectedAdapterId = selectedAdapter.Id;
            monitoredAdapterId = selectedAdapter.Id;
            monitoredAdapterName = selectedAdapter.Name;
            ResetMonitorState();
            monitorTimer.Start();
            monitorStatusLabel.Text = $"Monitoring {monitoredAdapterName}: checking now";
            _ = RunMonitorCheckAsync();
        }

        private void RestartButton_Click(object? sender, EventArgs e)
        {
            if (adapterListBox.SelectedItem == null)
            {
                MessageBox.Show("Please select an adapter to restart.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AdapterListItem? selectedAdapter = GetSelectedAdapter();
            if (selectedAdapter == null)
            {
                MessageBox.Show("The selected adapter is no longer available.", "Adapter Unavailable",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string adapterName = selectedAdapter.Name;
            selectedAdapterId = selectedAdapter.Id;
            
            if (MessageBox.Show($"Restart adapter '{adapterName}'?\n\nThis will temporarily disconnect the adapter.", 
                "Confirm Restart", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            statusLabel.Text = $"Restarting {adapterName}...";
            restartButton.Enabled = false;

            try
            {
                // Disable adapter
                RunNetshCommand($"interface set interface \"{adapterName}\" disable");
                System.Threading.Thread.Sleep(1000);

                // Enable adapter
                RunNetshCommand($"interface set interface \"{adapterName}\" enable");

                statusLabel.Text = $"Successfully restarted {adapterName}";
                MessageBox.Show($"Adapter '{adapterName}' has been restarted.", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error restarting adapter: {ex.Message}";
                MessageBox.Show($"Failed to restart adapter:\n{ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                restartButton.Enabled = true;
                LoadAdapters();
            }
        }

        private async void MonitorTimer_Tick(object? sender, EventArgs e)
        {
            await RunMonitorCheckAsync();
        }

        private async Task RunMonitorCheckAsync()
        {
            if (monitorCheckInProgress || string.IsNullOrWhiteSpace(monitoredAdapterId) ||
                string.IsNullOrWhiteSpace(monitoredAdapterName))
            {
                return;
            }

            monitorCheckInProgress = true;

            try
            {
                AdapterHealthResult health = await CheckAdapterHealthAsync(monitoredAdapterId);

                if (health.IsHealthy)
                {
                    failedMonitorChecks = 0;
                    monitorStatusLabel.Text = $"Monitoring {monitoredAdapterName}: {health.Message}";
                    return;
                }

                failedMonitorChecks++;
                monitorStatusLabel.Text =
                    $"Monitoring {monitoredAdapterName}: {health.Message} ({failedMonitorChecks}/{FailedChecksBeforeRestart})";

                if (failedMonitorChecks >= FailedChecksBeforeRestart)
                {
                    await RestartMonitoredAdapterAsync(monitoredAdapterName);
                }
            }
            catch (Exception ex)
            {
                monitorStatusLabel.Text = $"Monitor error: {ex.Message}";
            }
            finally
            {
                monitorCheckInProgress = false;
            }
        }

        private async Task<AdapterHealthResult> CheckAdapterHealthAsync(string adapterId)
        {
            NetworkInterface? adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(networkInterface =>
                    string.Equals(networkInterface.Id, adapterId, StringComparison.OrdinalIgnoreCase));

            if (adapter == null)
            {
                lastTrafficBytes = null;
                return AdapterHealthResult.Unhealthy("selected adapter was not found");
            }

            if (adapter.OperationalStatus != OperationalStatus.Up)
            {
                lastTrafficBytes = null;
                return AdapterHealthResult.Unhealthy($"selected adapter is {adapter.OperationalStatus}");
            }

            IPInterfaceProperties adapterProperties = adapter.GetIPProperties();
            IPv4InterfaceStatistics stats = adapter.GetIPv4Statistics();
            long currentTrafficBytes = stats.BytesReceived + stats.BytesSent;
            bool hasTraffic = lastTrafficBytes.HasValue && currentTrafficBytes > lastTrafficBytes.Value;
            lastTrafficBytes = currentTrafficBytes;

            IPAddress? adapterAddress = GetAdapterIPv4Address(adapterProperties);
            if (adapterAddress == null)
            {
                return AdapterHealthResult.Unhealthy("no IPv4 address on selected adapter");
            }

            if (!HasIPv4Gateway(adapterProperties))
            {
                return AdapterHealthResult.Unhealthy("selected adapter has no IPv4 gateway");
            }

            bool canReachNetwork = await CanReachNetworkEndpointAsync(adapterAddress);
            if (canReachNetwork)
            {
                return AdapterHealthResult.Healthy("selected adapter connectivity confirmed");
            }

            return AdapterHealthResult.Unhealthy(hasTraffic
                ? "selected adapter has traffic but no usable connectivity"
                : "selected adapter has no usable connectivity");
        }

        private static bool IsUserSelectableAdapter(NetworkInterface adapter, bool showAdvancedAdapters)
        {
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
                string.IsNullOrWhiteSpace(adapter.Name))
            {
                return false;
            }

            if (showAdvancedAdapters)
            {
                return true;
            }

            return IsPrimaryUserAdapter(adapter);
        }

        private static bool IsPrimaryUserAdapter(NetworkInterface adapter)
        {
            if (adapter.NetworkInterfaceType != NetworkInterfaceType.Ethernet &&
                adapter.NetworkInterfaceType != NetworkInterfaceType.Wireless80211)
            {
                return false;
            }

            string searchableText = $"{adapter.Name} {adapter.Description}";
            string[] advancedTerms =
            {
                "bluetooth",
                "hyper-v",
                "virtual",
                "vpn",
                "tap",
                "wsl",
                "wi-fi direct",
                "wifi direct",
                "local area connection*"
            };

            return !advancedTerms.Any(term =>
                searchableText.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static IPAddress? GetAdapterIPv4Address(IPInterfaceProperties adapterProperties)
        {
            return adapterProperties.UnicastAddresses
                .FirstOrDefault(address =>
                    address.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address.Address))
                ?.Address;
        }

        private static bool HasIPv4Gateway(IPInterfaceProperties adapterProperties)
        {
            return adapterProperties.GatewayAddresses.Any(gateway =>
                gateway.Address.AddressFamily == AddressFamily.InterNetwork &&
                !gateway.Address.Equals(IPAddress.Any));
        }

        private static async Task<bool> CanReachNetworkEndpointAsync(IPAddress localAddress)
        {
            return await CanConnectAsync("1.1.1.1", 53, localAddress) ||
                await CanConnectAsync("8.8.8.8", 53, localAddress);
        }

        private static async Task<bool> CanConnectAsync(string host, int port, IPAddress localAddress)
        {
            using var client = new TcpClient(localAddress.AddressFamily);

            try
            {
                client.Client.Bind(new IPEndPoint(localAddress, 0));
            }
            catch
            {
                return false;
            }

            Task connectTask = client.ConnectAsync(host, port);
            Task timeoutTask = Task.Delay(2500);

            if (await Task.WhenAny(connectTask, timeoutTask) != connectTask)
            {
                return false;
            }

            try
            {
                await connectTask;
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        private async Task RestartMonitoredAdapterAsync(string adapterName)
        {
            monitorTimer.Stop();
            restartButton.Enabled = false;
            autoRestartCheckBox.Enabled = false;
            monitorStatusLabel.Text = $"Auto-restarting {adapterName}...";

            try
            {
                await Task.Run(() =>
                {
                    RunNetshCommand($"interface set interface \"{adapterName}\" disable");
                    System.Threading.Thread.Sleep(1000);
                    RunNetshCommand($"interface set interface \"{adapterName}\" enable");
                });

                failedMonitorChecks = 0;
                lastTrafficBytes = null;
                statusLabel.Text = $"Successfully restarted {adapterName}";
                monitorStatusLabel.Text = $"Restarted {adapterName}; monitoring resumed";
            }
            catch (Exception ex)
            {
                monitorStatusLabel.Text = $"Auto-restart failed: {ex.Message}";
            }
            finally
            {
                restartButton.Enabled = true;
                autoRestartCheckBox.Enabled = true;
                LoadAdapters();

                if (autoRestartCheckBox.Checked)
                {
                    monitorTimer.Start();
                }
            }
        }

        private void ResetMonitorState()
        {
            failedMonitorChecks = 0;
            lastTrafficBytes = null;
        }

        private AdapterListItem? GetSelectedAdapter()
        {
            return adapterListBox.SelectedItem as AdapterListItem;
        }

        private void SelectAdapterById(string? adapterId)
        {
            if (string.IsNullOrWhiteSpace(adapterId))
            {
                return;
            }

            for (int i = 0; i < adapterListBox.Items.Count; i++)
            {
                if (adapterListBox.Items[i] is AdapterListItem item &&
                    string.Equals(item.Id, adapterId, StringComparison.OrdinalIgnoreCase))
                {
                    adapterListBox.SelectedIndex = i;
                    return;
                }
            }
        }

        private sealed class AdapterHealthResult
        {
            private AdapterHealthResult(bool isHealthy, string message)
            {
                IsHealthy = isHealthy;
                Message = message;
            }

            public bool IsHealthy { get; }
            public string Message { get; }

            public static AdapterHealthResult Healthy(string message) => new AdapterHealthResult(true, message);
            public static AdapterHealthResult Unhealthy(string message) => new AdapterHealthResult(false, message);
        }

        private sealed class AdapterListItem
        {
            public AdapterListItem(NetworkInterface adapter)
            {
                Id = adapter.Id;
                Name = adapter.Name;
            }

            public string Id { get; }
            public string Name { get; }

            public override string ToString()
            {
                return Name;
            }
        }

        private void RunNetshCommand(string arguments)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception(error);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
