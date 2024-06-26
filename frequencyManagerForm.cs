﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace opentuner
{
    public partial class frequencyManagerForm : Form
    {
        private List<StoredFrequency> stored_frequencies = null;
        public frequencyManagerForm(List<StoredFrequency> _stored_frequencies)
        {
            InitializeComponent();

            stored_frequencies = _stored_frequencies;
            load_frequencies();
        }

        public void load_frequencies()
        {
            listFreq.Items.Clear();

            lblFreq.Text = "";
            lblName.Text = "";
            lblOffset.Text = "";
            lblSymbolRate.Text = "";
            lblRFInput.Text = "";
           

            for (int c = 0; c < stored_frequencies.Count; c++)
            {
                listFreq.Items.Add(stored_frequencies[c].Name);
            }

            if (listFreq.Items.Count > 0)
                listFreq.SelectedIndex = 0;
        }

        public void show_frequency(int index)
        {
            if (index < stored_frequencies.Count)
            {
                lblFreq.Text = stored_frequencies[index].Frequency.ToString();
                lblName.Text = stored_frequencies[index].Name.ToString();
                lblOffset.Text = stored_frequencies[index].Offset.ToString();
                lblSymbolRate.Text = stored_frequencies[index].SymbolRate.ToString();

                if (stored_frequencies[index].RFInput == 1)
                    lblRFInput.Text = "A";
                else
                    lblRFInput.Text = "B";
            }
        }

        private void listFreq_SelectedIndexChanged(object sender, EventArgs e)
        {
            show_frequency(listFreq.SelectedIndex);
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (listFreq.SelectedIndex > -1)
            {
                if (MessageBox.Show("Are you sure you want to delete '" + stored_frequencies[listFreq.SelectedIndex].Name + "'?", "Confirmation", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    stored_frequencies.RemoveAt(listFreq.SelectedIndex);
                    load_frequencies();
                }
            }
        }

        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (listFreq.SelectedIndex > -1)
            {
                int index = listFreq.SelectedIndex;

                editStoredFrequencyForm editForm = new editStoredFrequencyForm();
                editForm.txtName.Text = stored_frequencies[index].Name;
                editForm.txtFreq.Text = stored_frequencies[index].Frequency.ToString();
                editForm.txtOffset.Text = stored_frequencies[index].Offset.ToString();
                editForm.txtSR.Text = stored_frequencies[index].SymbolRate.ToString();
                editForm.comboRFInput.SelectedIndex = stored_frequencies[index].RFInput - 1;
            

                if (editForm.ShowDialog() == DialogResult.OK)
                {
                    stored_frequencies[index].Name = editForm.txtName.Text;
                    stored_frequencies[index].Frequency = Convert.ToUInt32(editForm.txtFreq.Text);
                    stored_frequencies[index].Offset = Convert.ToUInt32(editForm.txtOffset.Text);
                    stored_frequencies[index].SymbolRate = Convert.ToUInt32(editForm.txtSR.Text);
                    stored_frequencies[index].RFInput = Convert.ToByte(editForm.comboRFInput.SelectedIndex + 1);
   
                    load_frequencies();
                }
                
            }
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            editStoredFrequencyForm editForm = new editStoredFrequencyForm();

            if (editForm.ShowDialog() == DialogResult.OK)
            {
                StoredFrequency sf = new StoredFrequency();

                sf.Name = editForm.txtName.Text;
                sf.Frequency = Convert.ToUInt32(editForm.txtFreq.Text);
                sf.Offset = Convert.ToUInt32(editForm.txtOffset.Text);
                sf.SymbolRate = Convert.ToUInt32(editForm.txtSR.Text);
                sf.RFInput = Convert.ToByte(editForm.comboRFInput.SelectedIndex + 1);
                stored_frequencies.Add(sf);

                load_frequencies();
            }

        }

        private void frequencyManagerForm_Load(object sender, EventArgs e)
        {

        }
    }
}
