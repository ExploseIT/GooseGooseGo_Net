using System.Text.Json;
using static GGG_WinFormer.GGG_Main;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace GGG_WinFormer
{
    public partial class GGG_Winformer : Form
    {
        // Global cache for contract sizes
        private MexcContractResponse? contractInfo = new MexcContractResponse();

        public GGG_Winformer()
        {
            InitializeComponent();

            // Initialize the timer
            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 5000; // Update every 5 seconds (5000ms)
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();

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
                    
                    contractInfo = await GGG_Main.Instance.LoadContractDetails();

                    // string respPositions = await GGG_Main.Instance.GetOpenPositions();

                    // 1. Get the raw string as we discussed
                    string jsonResult = await GGG_Main.Instance.GetOpenPositions();

                    // 2. Deserialize into our C# class
                    var positionData = System.Text.Json.JsonSerializer.Deserialize<MexcPositionResponse>(jsonResult);

                    if (positionData != null && positionData.success)
                    {
                        // Bind to DataGridView
                        dgvPositions.DataSource = positionData.data;

                        // Hide the integer column by its property name
                        if (dgvPositions.Columns["positionType"] != null)
                        {
                            dgvPositions.Columns["positionType"].Visible = false;
                        }

                        // Optional: Make the String version look professional
                        dgvPositions.Columns["positionTypeStr"].HeaderText = "Side";
                    }
                    else
                    {
                        _obj_uiList._tb_output!.Text = $"Error: {positionData?.code}";
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
        public decimal GetAccuratePnL(MexcPosition pos, decimal currentFairPrice)
        {
            // Use LINQ to find the first contract matching the symbol
            var contract = contractInfo.data.FirstOrDefault(c => c.symbol == pos.symbol);

            // If not found, default to 1.0 (or 0.0001 for BTC)
            decimal multiplier = (contract != null) ? contract.contractSize : 1.0m;

            decimal priceDiff = (pos.positionType == 2)
                ? (pos.holdAvgPrice - currentFairPrice)  // Short
                : (currentFairPrice - pos.holdAvgPrice); // Long

            return priceDiff * pos.holdVol * multiplier;
        }

        private async void UpdateTimer_Tick(object sender, EventArgs e)
        {
            updateTimer.Stop();
            try
            {
                string json = await GGG_Main.Instance.GetOpenPositions();
                var positionData = JsonSerializer.Deserialize<MexcPositionResponse>(json);

                if (positionData != null && positionData.success)
                {
                    var tickers = await GGG_Main.Instance.GetAllTickers();
                    var priceMap = tickers.data.ToDictionary(t => t.symbol, t => t.fairPrice);

                    foreach (var pos in positionData.data)
                    {
                        if (priceMap.TryGetValue(pos.symbol, out decimal currentFairPrice))
                        {
                            // 3. Use your cached contractSize + live Fair Price
                            pos.accuratePnL = GetAccuratePnL(pos, currentFairPrice);

                            // 4. THE DEGEN CHECK
                            if (pos.accuratePnL < -20.0m)
                            {
                                //await ExecutePartialClose(pos);
                            }
                        }
                    }

                    // Option A: The "Nuke and Rebind" (Reliable but resets scroll)
                    dgvPositions.DataSource = null;
                    dgvPositions.DataSource = positionData.data;

                    // Option B: The "Smooth Update" (Requires setting up a BindingSource)
                    // myBindingSource.DataSource = positionData.data;
                    // myBindingSource.ResetBindings(false); 

                    tb_output.Text = $"Last Update: {DateTime.Now:HH:mm:ss} - Rows: {positionData.data.Count}";
                    // Hide the integer column by its property name
                    if (dgvPositions.Columns["positionType"] != null)
                    {
                        dgvPositions.Columns["positionType"].Visible = false;
                    }

                    // Optional: Make the String version look professional
                    dgvPositions.Columns["positionTypeStr"].HeaderText = "Side";
                }
            }
            catch (Exception ex) { tb_output.Text = "Update Error: " + ex.Message; }
            finally { updateTimer.Start(); }
        }

        private async void UpdateTimer_Tick_2(object sender, EventArgs e)
        {
            // Temporarily stop to prevent overlapping calls if the network is slow
            updateTimer.Stop();

            try
            {
                string json = await GGG_Main.Instance.GetOpenPositions();
                var positionData = JsonSerializer.Deserialize<MexcPositionResponse>(json);

                if (positionData != null && positionData.success)
                {
                    // Hide the integer column by its property name
                    if (dgvPositions.Columns["positionType"] != null)
                    {
                        dgvPositions.Columns["positionType"].Visible = false;
                    }

                    // Optional: Make the String version look professional
                    dgvPositions.Columns["positionTypeStr"].HeaderText = "Side";

                    // Force the grid to see a new data source
                    dgvPositions.DataSource = null;
                    dgvPositions.DataSource = positionData.data;

                    tb_output.Text = $"Last Update: {DateTime.Now:HH:mm:ss} - Rows: {positionData.data.Count}";
                }
            }
            catch { /* Log errors or update a status label */ }
            finally
            {
                updateTimer.Start(); // Resume the timer
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
