using System.Text.Json;

namespace GGG_WinFormer
{
    public partial class GGG_Winformer : Form
    {
        public GGG_Winformer()
        {
            InitializeComponent();
            // The constructor stays light and synchronous. 
            // Logic starts in the Load event.
            this.Load += GGG_Winformer_Load!;
            _obj_uiList = new obj_uiList
            {
                _tb_output = this.tb_output
            };
        }

        private async void GGG_Winformer_Load(object sender, EventArgs e)
        {
            var result = await GGG_Main.Instance.GetServerTimeWithLatency();
            long ms = result.Latency;

            // Update your label with the time and latency
            lblLatency.Text = $"{ms}ms";

            // Visual Quality Indicator
            if (ms < 100)
            {
                lblLatency.ForeColor = Color.Green; // Excellent connection
            }
            else if (ms < 300)
            {
                lblLatency.ForeColor = Color.Orange; // Average/Lagging
            }
            else
            {
                lblLatency.ForeColor = Color.Red; // Critical Lag - Caution trading!
            }

            int maxRetries = 3;
            int delayBase = 2000; // 2 seconds

            for (int i = 0; i <= maxRetries; i++)
            {
                _obj_uiList._tb_output!.Text = $"Connecting (Attempt {i + 1})...";

                try
                {
                    _obj_uiList._tb_output!.Text = "Connecting to MEXC.../n" + Environment.NewLine;

                    // Now you can safely await your singleton's methods
                    string jsonResponse = await GGG_Main.Instance.GetServerTime();

                    // 1. Parse the JSON string
                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        // 2. Access the 'data' property which contains the timestamp
                        long unixTimeMs = doc.RootElement.GetProperty("data").GetInt64();

                        // 3. Convert Unix milliseconds to C# DateTimeOffset
                        DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(unixTimeMs);

                        // 4. Format to HH:mm:ss (local time)
                        string formattedTime = dateTimeOffset.LocalDateTime.ToString("HH:mm:ss");

                        _obj_uiList._tb_output!.Text = "Connected: " + formattedTime;
                    }

                    return; // Success! Exit the loop
                }
                catch (Exception ex)
                {
                    if (i == maxRetries)
                    {
                        _obj_uiList._tb_output!.Text = "Failed after 3 attempts.";
                        MessageBox.Show($"Final Connection Error: {ex.Message}");
                    }
                    else
                    {
                        // Exponential Backoff: Wait 2s, then 4s, then 8s...
                        int waitTime = delayBase * (int)Math.Pow(2, i);
                        _obj_uiList._tb_output!.Text = $"Retrying in {waitTime / 1000}s...";
                        await Task.Delay(waitTime); // Non-blocking wait
                    }
                }
            }
        }

        public obj_uiList _obj_uiList { get; set; }

    }

    public class obj_uiList
    {
        public obj_uiList()
        {
            _tb_output = _tb_output;
        }
        public TextBox? _tb_output { get; set; }
    }
}
