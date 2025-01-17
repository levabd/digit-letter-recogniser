﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using ContourAnalysisNS;
using TemplatesViewer.Properties;

namespace TemplatesViewer
{
    public partial class MainForm : Form
    {
        private ImageProcessor _processor;
        private Dictionary<string, int> _templates;

        private string _currentFilename =
            Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName) +
            Path.DirectorySeparatorChar + Settings.Default.TemplatesFile;

        public MainForm()
        {
            InitializeComponent();

            _processor = new ImageProcessor();

            LoadTemplateFile(_currentFilename);
            
        }

        private void LoadTemplateFile(string filename)
        {
            try
            {
                LoadTemplates(filename, _processor);
            }
            catch (FileNotFoundException)
            {
                return;
            }

            UpdateInterface();
            Bitmap ACF = new Bitmap(Settings.Default.TemplatesWidth, Settings.Default.TemplatesHeight);
            using (Graphics acfGraphics = Graphics.FromImage(ACF))
            {
                _processor.templates[0].Draw(acfGraphics, new Rectangle(0, 0, Settings.Default.TemplatesWidth, Settings.Default.TemplatesHeight));
            }
            pictureBoxACF.Image = ACF;

        }

        private void CalculateTemplates()
        {
            _templates = new Dictionary<string, int>(1);

            _templates.Add("0", 0);
            _templates.Add("1", 0);
            _templates.Add("2", 0);
            _templates.Add("3", 0);
            _templates.Add("4", 0);
            _templates.Add("5", 0);
            _templates.Add("6", 0);
            _templates.Add("7", 0);
            _templates.Add("8", 0);
            _templates.Add("9", 0);
            _templates.Add("6890", 0);

            foreach (var row in dgvTemplates.Rows)
            {
                _templates[(row as DataGridViewRow).Cells[1].Value.ToString()] += 1;
            }
        }

        void UpdateInterface()
        {
            dgvTemplates.RowCount = _processor.templates.Count;
            CalculateTemplates();
            templatesCounter.Rows.Clear();
            foreach (var template in _templates)
            {
                templatesCounter.Rows.Add(new object[] { template.Key, template.Value.ToString() });
            }
        }

        private void dgvTemplates_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _processor.templates.Count)
            {   
                switch(e.ColumnIndex)
                {
                    case 0:
                        e.Value = e.RowIndex;
                        break;
                    case 1:
                        e.Value = _processor.templates[e.RowIndex].name;
                        break;
                    case 2:
                        e.Value = _processor.templates[e.RowIndex].preferredAngleNoMore90;
                        break;
                }
            }   
        }

        private void dgvTemplates_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _processor.templates.Count && e.ColumnIndex == 1)
                _processor.templates[e.RowIndex].name = e.Value.ToString();
        }

        private void dgvTemplates_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvTemplates.SelectedCells.Count > 0)
            {
                int iRow = dgvTemplates.SelectedCells[0].RowIndex;
                if (iRow >= 0 && iRow < _processor.templates.Count)
                {
                    Refresh();
                    Bitmap ACF = new Bitmap(Settings.Default.TemplatesWidth, Settings.Default.TemplatesHeight);
                    using (Graphics acfGraphics = Graphics.FromImage(ACF))
                    {
                        _processor.templates[iRow].Draw(acfGraphics, new Rectangle(0, 0, Settings.Default.TemplatesWidth, Settings.Default.TemplatesHeight));
                    }
                    pictureBoxACF.Image = ACF;
                }
            }
        }

        private void btDelete_Click(object sender, EventArgs e)
        {
            if (dgvTemplates.SelectedCells.Count > 0)
            {
                int iRow = dgvTemplates.SelectedCells[0].RowIndex;
                if (iRow >= 0 && iRow < _processor.templates.Count)
                {
                    _processor.templates.RemoveAt(iRow);
                    UpdateInterface();
                    Refresh();
                }
            }
        }

        private void buttonOpen_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = Resources.TemplatesFilePattern;
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                _currentFilename = ofd.FileName;
                LoadTemplateFile(_currentFilename);
            }
            
        }

        private void SaveTemplates(string fileName, ImageProcessor proc)
        {
            try
            {
                using (FileStream fs = new FileStream(fileName, FileMode.Create))
                    new BinaryFormatter().Serialize(fs, proc.templates);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadTemplates(string fileName, ImageProcessor proc)
        {
            using (FileStream fs = new FileStream(fileName, FileMode.Open))
                proc.templates = (Templates) new BinaryFormatter().Deserialize(fs);
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = Resources.TemplatesFilePattern;
            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                _currentFilename = sfd.FileName;
                SaveTemplates(sfd.FileName, _processor);
            }
        }

        private void buttonReload_Click(object sender, EventArgs e)
        {
            LoadTemplateFile(_currentFilename);
        }

        private void dgvTemplates_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            CalculateTemplates();
            templatesCounter.Rows.Clear();
            foreach (var template in _templates)
            {
                templatesCounter.Rows.Add(new object[] { template.Key, template.Value.ToString() });
            }
        }
    }
}
