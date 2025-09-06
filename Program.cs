using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WizMoodLight
{
    public partial class MainForm : Form
    {
        private System.Windows.Forms.Timer screenCaptureTimer;
        private UdpClient udpClient;
        private string wizBulbIP = ""; // Set this to your Wiz bulb IP
        private int wizBulbPort = 38899;
        private bool isRunning = false;
        private int updateInterval = 1500; // milliseconds

        // UI Controls
        private TextBox ipTextBox;
        private NumericUpDown intervalNumeric;
        private NumericUpDown dimUpDown;
        private NumericUpDown sampleSizeUpDown;
        private Button startButton;
        private Button stopButton;
        private Panel colorPreview;
        private Label statusLabel;
        private CheckBox smoothTransitionCheckBox;

        public MainForm()
        {
            InitializeComponent();
            screenCaptureTimer = new System.Windows.Forms.Timer();
            screenCaptureTimer.Tick += ScreenCaptureTimer_Tick;
        }

        private void InitializeComponent()
        {
            this.Text = "Wiz Mood Light Controller (UDP)";
            this.Size = new Size(400, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // IP Address input
            var ipLabel = new Label
            {
                Text = "Wiz Bulb IP Address:",
                Location = new Point(20, 20),
                Size = new Size(150, 20)
            };
            this.Controls.Add(ipLabel);

            ipTextBox = new TextBox
            {
                Location = new Point(20, 45),
                Size = new Size(150, 25),
                Text = "192.168.0.107" // Default IP
            };
            this.Controls.Add(ipTextBox);

            // Update interval
            var intervalLabel = new Label
            {
                Text = "Update Interval (ms):",
                Location = new Point(20, 80),
                Size = new Size(120, 20)
            };
            this.Controls.Add(intervalLabel);

            intervalNumeric = new NumericUpDown
            {
                Location = new Point(20, 105),
                Size = new Size(100, 25),
                Minimum = 100,
                Maximum = 5000,
                Value = 1500,
                Increment = 100
            };
            this.Controls.Add(intervalNumeric);

            // Smooth transition option
            smoothTransitionCheckBox = new CheckBox
            {
                Text = "Smooth transitions",
                Location = new Point(20, 140),
                Size = new Size(150, 25),
                Checked = true
            };
            this.Controls.Add(smoothTransitionCheckBox);

            // Control buttons
            startButton = new Button
            {
                Text = "Start Mood Lighting",
                Location = new Point(200, 45),
                Size = new Size(150, 35),
                BackColor = Color.LightGreen
            };
            startButton.Click += StartButton_Click;
            this.Controls.Add(startButton);

            stopButton = new Button
            {
                Text = "Stop Mood Lighting",
                Location = new Point(200, 90),
                Size = new Size(150, 35),
                BackColor = Color.LightCoral,
                Enabled = false
            };
            stopButton.Click += StopButton_Click;
            this.Controls.Add(stopButton);

            // Color preview
            var previewLabel = new Label
            {
                Text = "Current Color:",
                Location = new Point(200, 140),
                Size = new Size(80, 20)
            };
            this.Controls.Add(previewLabel);

            colorPreview = new Panel
            {
                Location = new Point(290, 135),
                Size = new Size(60, 30),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black
            };
            this.Controls.Add(colorPreview);

            // Status label
            statusLabel = new Label
            {
                Text = "Ready",
                Location = new Point(20, 200),
                Size = new Size(350, 40),
                ForeColor = Color.Blue
            };
            this.Controls.Add(statusLabel);

            var dimLabel = new Label
            {
                Text = "Dim",
                Location = new Point(20, 240),
                Size = new Size(120, 20)
            };
            this.Controls.Add(dimLabel);

            dimUpDown = new NumericUpDown
            {
                Location = new Point(20, 260),
                Size = new Size(100, 25),
                Minimum = 10,
                Maximum = 100,
                Value = 100,
                Increment = 1
            };
            this.Controls.Add(dimUpDown);

            var sampleLabel = new Label
            {
                Text = "Sample size",
                Location = new Point(20, 300),
                Size = new Size(120, 20)
            };
            this.Controls.Add(sampleLabel);

            sampleSizeUpDown = new NumericUpDown
            {
                Location = new Point(20, 320),
                Size = new Size(100, 25),
                Minimum = 5,
                Maximum = 50,
                Value = 5,
                Increment = 1
            };
            this.Controls.Add(sampleSizeUpDown);
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            wizBulbIP = ipTextBox.Text.Trim();
            if (string.IsNullOrEmpty(wizBulbIP))
            {
                MessageBox.Show("Please enter the Wiz bulb IP address.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            updateInterval = (int)intervalNumeric.Value;

            statusLabel.Text = "Testing connection to Wiz bulb...";

            udpClient = new UdpClient();
            udpClient.Client.ReceiveTimeout = 1000;

            if (await TestBulbConnection())
            {
                isRunning = true;
                screenCaptureTimer.Interval = updateInterval;
                screenCaptureTimer.Start();

                startButton.Enabled = false;
                stopButton.Enabled = true;
                ipTextBox.Enabled = false;
                intervalNumeric.Enabled = false;

                statusLabel.Text = "Mood lighting active - analyzing screen colors...";
            }
            else
            {
                MessageBox.Show($"Could not connect to Wiz bulb at {wizBulbIP}. Please check the IP address and ensure the bulb is on the same network.",
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                statusLabel.Text = "Connection failed";
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            isRunning = false;
            screenCaptureTimer.Stop();

            startButton.Enabled = true;
            stopButton.Enabled = false;
            ipTextBox.Enabled = true;
            intervalNumeric.Enabled = true;

            statusLabel.Text = "Mood lighting stopped";
            colorPreview.BackColor = Color.Black;
        }

        int tick = 0;
        private async void ScreenCaptureTimer_Tick(object sender, EventArgs e)
        {
            if (!isRunning) return;

            try
            {
                var dominantColor = GetDominantScreenColor();
                colorPreview.BackColor = dominantColor;

                await SetBulbColor(dominantColor);
                statusLabel.Text = $"{tick++} Color updated: R={dominantColor.R}, G={dominantColor.G}, B={dominantColor.B}";
            }
            catch (Exception ex)
            {
                statusLabel.Text = $"Error: {ex.Message}";
            }
        }
        

        private Color GetDominantScreenColor()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
                }

                var colorCounts = new Dictionary<Color, int>();
                int sampleSize = (int)sampleSizeUpDown.Value;

                for (int x = 0; x < bounds.Width; x += sampleSize)
                {
                    for (int y = 0; y < bounds.Height; y += sampleSize)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        var quantized = Color.FromArgb(
                            (pixel.R / 32) * 32,
                            (pixel.G / 32) * 32,
                            (pixel.B / 32) * 32
                        );

                        if (quantized.GetBrightness() <= 0.1) continue;

                        if (colorCounts.ContainsKey(quantized))
                            colorCounts[quantized]++;
                        else
                            colorCounts[quantized] = 1;
                    }
                }

                var dominantColor = colorCounts
                    .OrderByDescending(kvp => kvp.Value)
                    .FirstOrDefault().Key;

                return dominantColor == Color.Empty ? Color.White : dominantColor;
            }
        }

        private async Task<bool> TestBulbConnection()
        {
            try
            {
                var testCommand = new
                {
                    id = 1,
                    method = "getPilot",
                    @params = new { }
                };

                string json = JsonSerializer.Serialize(testCommand);
                Console.WriteLine(json);
                byte[] data = Encoding.UTF8.GetBytes(json);

                Console.WriteLine("Sending");
                await udpClient.SendAsync(data, data.Length, wizBulbIP, wizBulbPort);
                Console.WriteLine("Sent");
                var result = await udpClient.ReceiveAsync();
                string response = Encoding.UTF8.GetString(result.Buffer);

                return response.Contains("result");
            }
            catch
            {
                return false;
            }
        }

        private async Task SetBulbColor(Color color)
        {
            try
            {
                var command = new
                {
                    id = 1,
                    method = "setPilot",
                    @params = new
                    {
                        r = color.R,
                        g = color.G,
                        b = color.B,
                        dimming = dimUpDown.Value
                    }
                };

                string json = JsonSerializer.Serialize(command);
                Console.WriteLine(json);
                byte[] data = Encoding.UTF8.GetBytes(json);

                await udpClient.SendAsync(data, data.Length, wizBulbIP, wizBulbPort);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to set bulb color: {ex.Message}");
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            isRunning = false;
            screenCaptureTimer?.Stop();
            screenCaptureTimer?.Dispose();
            udpClient?.Close();
            udpClient?.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [STAThread]
        static void Main()
        {
            AllocConsole(); // attach a console
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
