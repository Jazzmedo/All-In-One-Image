using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using openCV;

namespace AIOI
{
    public partial class FormHistogram : Form
    {
        private Chart chart;

        public FormHistogram(IplImage image)
        {
            // Initialize the chart directly in the constructor to ensure it's never null
            chart = new Chart
            {
                Dock = DockStyle.Fill
            };
            this.Controls.Add(chart);

            InitializeComponent();

            // Call SetupChart after InitializeComponent
            SetupChart();

            // Generate the histogram
            GenerateHistogram(image);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormHistogram));
            this.SuspendLayout();
            // 
            // FormHistogram
            // 
            this.ClientSize = new System.Drawing.Size(784, 561);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "FormHistogram";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Image Histogram";
            this.ResumeLayout(false);

        }

        private void SetupChart()
        {
            if (chart == null)
            {
                // Fallback initialization if chart is somehow null
                chart = new Chart
                {
                    Dock = DockStyle.Fill
                };
                this.Controls.Add(chart);
            }

            try
            {
                // Clear any existing chart areas and series
                chart.ChartAreas.Clear();
                chart.Series.Clear();

                // Create a chart area
                ChartArea chartArea = new ChartArea
                {
                    Name = "HistogramArea"
                };
                chart.ChartAreas.Add(chartArea);

                // Create series for R, G, B channels
                Series seriesR = new Series
                {
                    Name = "Red",
                    Color = Color.Red,
                    ChartType = SeriesChartType.Column,
                    BorderWidth = 1
                };
                Series seriesG = new Series
                {
                    Name = "Green",
                    Color = Color.Green,
                    ChartType = SeriesChartType.Column,
                    BorderWidth = 1
                };
                Series seriesB = new Series
                {
                    Name = "Blue",
                    Color = Color.Blue,
                    ChartType = SeriesChartType.Column,
                    BorderWidth = 1
                };

                chart.Series.Add(seriesR);
                chart.Series.Add(seriesG);
                chart.Series.Add(seriesB);

                // Customize chart appearance
                chartArea.AxisX.Title = "Pixel Value";
                chartArea.AxisY.Title = "Frequency";
                chartArea.AxisX.Minimum = 0;
                chartArea.AxisX.Maximum = 255;
                chartArea.AxisX.Interval = 10;
            }
            catch (Exception ex)
            {
                if (!this.DesignMode)
                {
                    MessageBox.Show($"Error setting up chart: {ex.Message}");
                }
            }
        }

        private void GenerateHistogram(IplImage image)
        {
            if (image.width <= 0 || image.height <= 0)
            {
                MessageBox.Show("No valid image provided for histogram generation.");
                return;
            }

            try
            {
                // Initialize histogram arrays
                int[] histR = new int[256];
                int[] histG = new int[256];
                int[] histB = new int[256];

                // Access image data
                IntPtr srcData = image.imageData;
                int step = image.widthStep;
                int nChannels = image.nChannels;

                unsafe
                {
                    byte* srcPtr = (byte*)srcData.ToPointer();

                    for (int r = 0; r < image.height; r++)
                    {
                        for (int c = 0; c < image.width; c++)
                        {
                            int index = (r * step) + (c * nChannels);
                            histB[srcPtr[index + 0]]++; // Blue
                            histG[srcPtr[index + 1]]++; // Green
                            histR[srcPtr[index + 2]]++; // Red
                        }
                    }
                }

                // Ensure chart and series are available
                if (chart == null || chart.Series.Count < 3)
                {
                    MessageBox.Show("Chart is not properly initialized.");
                    return;
                }

                // Add histogram data to series
                for (int i = 0; i < 256; i++)
                {
                    chart.Series["Red"].Points.AddXY(i, histR[i]);
                    chart.Series["Green"].Points.AddXY(i, histG[i]);
                    chart.Series["Blue"].Points.AddXY(i, histB[i]);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating histogram: {ex.Message}");
            }
        }
    }
}