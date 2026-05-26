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
        private Label statusLabel = null!;
        private Label monitorStatusLabel = null!;
        private LinkLabel krytonLink = null!;
        private System.Windows.Forms.Timer monitorTimer = null!;
        private string? monitoredAdapterName;
        private long? lastTrafficBytes;
        private int failedMonitorChecks;
        private bool monitorCheckInProgress;

        private const int MonitorIntervalMilliseconds = 120000;
        private const int FailedChecksBeforeRestart = 3;

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

            // Status label
            statusLabel = new Label
            {
                Location = new System.Drawing.Point(20, 325),
                Size = new System.Drawing.Size(440, 20),
                Text = "Select an adapter and click Restart"
            };

            // Monitor status label
            monitorStatusLabel = new Label
            {
                Location = new System.Drawing.Point(20, 350),
                Size = new System.Drawing.Size(440, 35),
                Text = "Auto-restart monitor is off"
            };

            // Kryton Labs link
            krytonLink = new LinkLabel
            {
                Location = new System.Drawing.Point(20, 395),
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

            this.Controls.Add(adapterListBox);
            this.Controls.Add(restartButton);
            this.Controls.Add(refreshButton);
            this.Controls.Add(autoRestartCheckBox);
            this.Controls.Add(statusLabel);
            this.Controls.Add(monitorStatusLabel);
            this.Controls.Add(krytonLink);
        }

        private void LoadAdapters()
        {
            adapterListBox.Items.Clear();
            statusLabel.Text = "Loading adapters...";

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "interface show interface",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Skip header lines
                for (int i = 3; i < lines.Length; i++)
                {
                    var parts = lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        // Join the remaining parts as the adapter name
                        string adapterName = string.Join(" ", parts, 3, parts.Length - 3);
                        adapterListBox.Items.Add(adapterName);
                    }
                }

                statusLabel.Text = $"Found {adapterListBox.Items.Count} adapter(s)";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error loading adapters: {ex.Message}";
            }
        }

        private void AdapterListBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (!autoRestartCheckBox.Checked || adapterListBox.SelectedItem == null)
            {
                return;
            }

            monitoredAdapterName = adapterListBox.SelectedItem.ToString();
            ResetMonitorState();
            monitorStatusLabel.Text = $"Monitoring {monitoredAdapterName}";
        }

        private void AutoRestartCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (!autoRestartCheckBox.Checked)
            {
                monitorTimer.Stop();
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

            monitoredAdapterName = adapterListBox.SelectedItem.ToString();
            ResetMonitorState();
            monitorTimer.Start();
            monitorStatusLabel.Text = $"Monitoring {monitoredAdapterName}";
        }

        private void RestartButton_Click(object? sender, EventArgs e)
        {
            if (adapterListBox.SelectedItem == null)
            {
                MessageBox.Show("Please select an adapter to restart.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string adapterName = adapterListBox.SelectedItem.ToString() ?? "";
            
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
            if (monitorCheckInProgress || string.IsNullOrWhiteSpace(monitoredAdapterName))
            {
                return;
            }

            monitorCheckInProgress = true;

            try
            {
                bool isHealthy = await IsAdapterHealthyAsync(monitoredAdapterName);

                if (isHealthy)
                {
                    failedMonitorChecks = 0;
                    monitorStatusLabel.Text = $"Monitoring {monitoredAdapterName}: Windows reports activity";
                    return;
                }

                failedMonitorChecks++;
                monitorStatusLabel.Text =
                    $"Monitoring {monitoredAdapterName}: no activity or response ({failedMonitorChecks}/{FailedChecksBeforeRestart})";

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

        private async Task<bool> IsAdapterHealthyAsync(string adapterName)
        {
            NetworkInterface? adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(networkInterface =>
                    string.Equals(networkInterface.Name, adapterName, StringComparison.OrdinalIgnoreCase));

            if (adapter == null)
            {
                throw new InvalidOperationException($"Adapter '{adapterName}' was not found.");
            }

            if (adapter.OperationalStatus != OperationalStatus.Up)
            {
                lastTrafficBytes = null;
                return false;
            }

            IPv4InterfaceStatistics stats = adapter.GetIPv4Statistics();
            long currentTrafficBytes = stats.BytesReceived + stats.BytesSent;
            bool hasTraffic = lastTrafficBytes.HasValue && currentTrafficBytes > lastTrafficBytes.Value;
            lastTrafficBytes = currentTrafficBytes;

            if (hasTraffic)
            {
                return true;
            }

            IPAddress? adapterAddress = GetAdapterIPv4Address(adapter);
            return adapterAddress != null && await CanReachNetworkEndpointAsync(adapterAddress);
        }

        private static IPAddress? GetAdapterIPv4Address(NetworkInterface adapter)
        {
            return adapter.GetIPProperties()
                .UnicastAddresses
                .FirstOrDefault(address =>
                    address.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address.Address))
                ?.Address;
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
