using System;
using System.Windows.Forms;
using MetadataExtractor;
using System.Collections.Generic;
using System.Linq;

namespace AIOI
{
    public partial class Form2 : Form
    {
        private ListView listViewMetadata;

        public Form2()
        {
            InitializeComponent();
            InitializeListView();
        }

        private void InitializeListView()
        {
            listViewMetadata = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            listViewMetadata.Columns.Add("Tag", 200);
            listViewMetadata.Columns.Add("Value", 400);

            this.Controls.Add(listViewMetadata);
        }

        public void DisplayMetadata(string imagePath)
        {
            try
            {
                listViewMetadata.Items.Clear();
                var directories = ImageMetadataReader.ReadMetadata(imagePath);

                foreach (var directory in directories)
                {
                    foreach (var tag in directory.Tags)
                    {
                        var item = new ListViewItem(new[] { tag.Name, tag.Description });
                        listViewMetadata.Items.Add(item);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading metadata: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
