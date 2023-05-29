using Microsoft.Extensions.Options;
using RenTradeWindowService;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RenTradeWindowForm
{
    public partial class SerialQueryForm : Form
    {
        private readonly IOptions<ServiceConfiguration> _options;
        private RegistryDriver registry;

        public SerialQueryForm(IOptions<ServiceConfiguration> options)
        {
            InitializeComponent();

            _options = options;

            // Registry init/configuration
            registry = new RegistryDriver(_options);
        }

        private void btnRefOk_Click(object sender, EventArgs e)
        {
            int result = registry.XmlSerialFinder(txtRef.Text);
            lblSerialResult.Text = "0";
            lblCountResult.Text = "0";

            if (result < 0)
            {
                lblRefRemarks.Text = "Record not found!";
            }
            else
            {
                lblRefRemarks.Text = "Record found!";
                lblSerialResult.Text = txtRef.Text;
                lblCountResult.Text = result.ToString();
            }
        }
    }
}
