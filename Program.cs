using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace NetworkAdapterRestarter
{
    public class MainForm : Form
    {
        private ListBox adapterListBox;
        private Button refreshButton;
        private Button restartButton;
        private Label statusLabel;
        private LinkLabel krytonLink;

        public MainForm()
        {
            InitializeUI();
            LoadAdapters();
        }

        private void InitializeUI()
        {
            this.Text = "Network Adapter Restarter";
            this.Size = new System.Drawing.Size(500, 420);
            this.MinimumSize = new System.Drawing.Size(400, 420);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Set the icon
            try
            {
                string iconPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
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
                Size = new System.Drawing.Size(440, 250)
            };

            // Restart button
            restartButton = new Button
            {
                Text = "Restart Selected Adapter",
                Location = new System.Drawing.Point(20, 280),
                Size = new System.Drawing.Size(200, 30)
            };
            restartButton.Click += RestartButton_Click;

            // Refresh button
            refreshButton = new Button
            {
                Text = "Refresh List",
                Location = new System.Drawing.Point(230, 280),
                Size = new System.Drawing.Size(100, 30)
            };
            refreshButton.Click += (s, e) => LoadAdapters();

            // Status label
            statusLabel = new Label
            {
                Location = new System.Drawing.Point(20, 320),
                Size = new System.Drawing.Size(440, 20),
                Text = "Select an adapter and click Restart"
            };

            // Kryton Labs link
            krytonLink = new LinkLabel
            {
                Location = new System.Drawing.Point(20, 345),
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

            this.Controls.Add(adapterListBox);
            this.Controls.Add(restartButton);
            this.Controls.Add(refreshButton);
            this.Controls.Add(statusLabel);
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

        private void RestartButton_Click(object sender, EventArgs e)
        {
            if (adapterListBox.SelectedItem == null)
            {
                MessageBox.Show("Please select an adapter to restart.", "No Selection", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string adapterName = adapterListBox.SelectedItem.ToString();
            
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
