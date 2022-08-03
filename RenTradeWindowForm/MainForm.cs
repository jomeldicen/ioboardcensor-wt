using Microsoft.Extensions.Options;
using RenTradeWindowService;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using RenTradeWindowForm.Helper;
using System.Text.RegularExpressions;
using RenTradeWindowForm.Properties;
using System.Linq;
using FoxLearn.License;

namespace RenTradeWindowForm
{
    public partial class MainForm : Form
    {
        private readonly IOptions<ServiceConfiguration> _options;
        private RegistryDriver registry;
        private LearJob learjob;
        private readonly int _threadDelay;

        private readonly string _machineName;
        private readonly string _environmentMode;
        private readonly string _testPcsMsg;
        private readonly string _prodPcsMsg;
        private readonly string _quotaPcsMsg;
        private readonly string _endJobMsg;

        private readonly int _firstPcsInitCount;
        private readonly int _midPcsInitCount;
        private readonly int _lastPcsInitCount;
        private readonly int _quotaInitCount;

        private readonly bool _isLicense = false;

        private string Mode;

        public MainForm(IOptions<ServiceConfiguration> options)
        {
            InitializeComponent();

            _options = options;

            // License Check if valid or not
            this._isLicense = false;
            string prodID = ComputerInfo.GetComputerId();
            KeyManager km = new KeyManager(prodID);
            LicenseInfo lic = new LicenseInfo();
            int value = km.LoadSuretyFile(string.Format(@"{0}\Key.lic", Application.StartupPath), ref lic);
            string productKey = lic.ProductKey;
            if (km.ValidKey(ref productKey))
            {
                this._isLicense = true;
            }

            // Registry init/configuration
            registry = new RegistryDriver(_options);

            // Delay sleep interval
            _threadDelay = _options.Value.ThreadDelay;

            // Confirmation Message
            _testPcsMsg = _options.Value.TestPcsMsg;
            _prodPcsMsg = _options.Value.ProdPcsMsg;
            _quotaPcsMsg = _options.Value.QuotaPcsMsg;
            _endJobMsg = _options.Value.EndJobMsg;
            _machineName = String.IsNullOrEmpty(_options.Value.MachineName) ? Environment.MachineName : _options.Value.MachineName;
            _environmentMode = _options.Value.EnvironmentMode;

            // Count for Pull Test and Caliper
            _firstPcsInitCount = _options.Value.FirstPcsInitCount;
            _midPcsInitCount = _options.Value.MidPcsInitCount;
            _lastPcsInitCount = _options.Value.LastPcsInitCount;
            _quotaInitCount = _options.Value.QuotaInitCount;

            // Windows Form Start Position
            this.StartPosition = FormStartPosition.Manual;
            foreach (var scrn in Screen.AllScreens)
            {
                if (scrn.Bounds.Contains(this.Location))
                {
                    this.Location = new Point(scrn.Bounds.Right - this.Width, scrn.Bounds.Top);
                    return;
                }
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            backgroundWorker.RunWorkerAsync();
        }

        private bool GetLearInformation()
        {
            learjob = new LearJob(_options);

            lblLeadSet.Text = learjob.LeadSet;
            lblOrderNos.Text = learjob.OrderNumber;
            lblDailyTarget.Text = learjob.JobQty.ToString();
            lblBatchTarget.Text = learjob.BundleSize.ToString();
            lblMachineName.Text = _machineName;

            // Clear and Disable controls if no active job loaded
            if(String.IsNullOrEmpty(learjob.OrderNumber))
            {
                lblLeadSet.Text = "n/a";
                lblOrderNos.Text = "n/a";

                btnStart.Enabled = false;
                btnTerminate.Enabled = false;

                stStripMenuItem.Enabled = false;
                tmStripMenuItem.Enabled = false;
                ptStripMenuItem.Enabled = false;
                cmStripMenuItem.Enabled = false;

                lblStatus.Text = "No Active JO";
                lblRemarks.Text = "Ongoing checking...";

                return false;
            }

            return true;
        }

        private void backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            // get Job Information
            if (!GetLearInformation())
                goto proceed;

            Mode = "X0";

            // X0 - means device is working as expected
            if (registry.IOBoardStatus == Mode)
            {
                // means license is valid
                if(this._isLicense)
                {
                    // reel section
                    if (!registry.ReelStatus)
                    {
                        menuStrip.Enabled = false;

                        btnStart.Enabled = false;
                        btnTerminate.Enabled = false;

                        lblStatus.Text = (registry.IsProd) ? "Production" : "Test Mode";
                        lblRemarks.Text = "Reel has been deactivated! For BOM scanning.";

                    InputQty:
                        string input = "0";
                        DialogBox.ShowInputDialogBox(ref input, "Please scan BOM.", "Message Confirmation", 300, 110);

                        if (!String.IsNullOrEmpty(input) && learjob.IsBOMExist(input))
                        {
                            registry.WriteRegistry("reelStatus", "True");
                            registry.WriteRegistry("pedalStatus", "True");
                        }
                        else
                        {
                            MessageBox.Show(this, "Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            goto InputQty;
                        }

                        goto proceed;
                    }

                    // Test Mode
                    if (!registry.IsProd)
                    {
                        menuStrip.Enabled = true;
                        lblStatus.Text = "Test Mode";

                        // Intial First Step
                        if (registry.ProcessStage == "A1")
                        {
                            lblRemarks.Text = (registry.PedalStatus) ? "For item pcs execution: " + registry.ProcessCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + "." : "Click 'Start' button to proceed";
                        }

                        // validation if achieve test qty
                        if (registry.ProcessStage == "A2" && !registry.PedalStatus)
                        {
                            lblRemarks.Text = "For quantity validation";

                            DialogResult result = MessageBox.Show(this, _testPcsMsg, "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                            {
                                registry.WriteRegistry("processStage", "A4A");
                            }
                            else
                            {
                            InputQty:
                                string input = "0";
                                DialogBox.ShowInputDialogBox(ref input, "Please input additional quantity.", "Message Confirmation", 300, 110);

                                if (int.TryParse(input, out int value) && !String.IsNullOrEmpty(input))
                                {
                                    // not greater than 9
                                    if (input.Length > 1)
                                    {
                                        MessageBox.Show(this, "Maximum quantity is not greater than 9.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }

                                    // not less than or equal to 0
                                    if (input.Length <= 0)
                                    {
                                        MessageBox.Show(this, "Quantity should not be less than or equal to 0.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }

                                    if (value <= registry.ProcessCounter)
                                    {
                                        lblRemarks.Text = "Pedal is in action";

                                        var counter1 = registry.ProcessCounter - value;
                                        registry.WriteRegistry("processCounter", counter1.ToString());

                                        // activate pedal to continue
                                        registry.WriteRegistry("pedalStatus", "True");
                                        registry.WriteRegistry("processStage", "A1");
                                    }
                                    else
                                    {
                                        MessageBox.Show(this, "Quantity should not be equal to 0 or greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(this, "Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }
                            }
                        }

                        // Additional Pcs for Test
                        if (registry.ProcessStage == "A3")
                        {
                            lblRemarks.Text = "For item pcs execution: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                        }


                        // Caliper Test message confirmation
                        if (registry.ProcessStage == "A4A")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, "Please perform (" + _firstPcsInitCount.ToString() + ") CALIPER/PULL TEST input.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                            if (result == DialogResult.OK)
                            {
                                registry.WriteRegistry("processStage", "A4B");
                            }
                        }

                        // Caliper Test execution
                        if (registry.ProcessStage == "A4B")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                        }

                        // Caliper Test validation if no data within specific timeframe
                        if (registry.ProcessStage == "A4C")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, "Do you want to continue CALIPER execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                            {
                                registry.WriteRegistry("processStage", "A4B");
                            }
                            else
                            {
                                MessageBox.Show(this, "Please continue CALIPER execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }

                        }

                        // Pull Test message confirmation
                        if (registry.ProcessStage == "A5A")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                            registry.WriteRegistry("processStage", "A5B");
                        }

                        // Pull Test execution
                        if (registry.ProcessStage == "A5B")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                        }

                        // Pull Test validation if no data within specific timeframe
                        if (registry.ProcessStage == "A5C")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, "Do you want to continue PULL TEST execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                            {
                                registry.WriteRegistry("processStage", "A5B");
                            }
                            else
                            {
                                MessageBox.Show(this, "Please continue PULL TEST execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }

                        // Message Confirmation to back to execution
                        if (registry.ProcessStage == "A8")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                            MessageBox.Show(this, "Production execution activated.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            registry.WriteRegistry("isProd", "True");
                            registry.WriteRegistry("processStage", "B1");
                            registry.WriteRegistry("testCounter", "0");
                            registry.WriteRegistry("processCounter", "0");
                            registry.WriteRegistry("pedalStatus", "True");
                        }
                    }
                    else // Production
                    {
                        menuStrip.Enabled = true;
                        lblStatus.Text = "Production";
                        lblRemarks.Text = "Pedal is in action";

                        //*********************** Start Daily Quota Section ***********************//
                        // validation if achieve test qty
                        if (registry.ProcessStage == "C2" && !registry.PedalStatus)
                        {
                            lblRemarks.Text = "For daily quota validation";
                            DialogResult result = MessageBox.Show(this, _quotaPcsMsg, "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                            if (result == DialogResult.OK)
                            {
                                lblRemarks.Text = "Execute additional item(s).";
                                DialogResult result2 = MessageBox.Show(this, "Please excute additional (" + _midPcsInitCount.ToString() + ") item/s.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                                if (result2 == DialogResult.OK)
                                {
                                    // activate pedal to continue
                                    registry.WriteRegistry("pedalStatus", "True");
                                    registry.WriteRegistry("processStage", "C3");
                                }
                            }
                        }

                        // Additional Pcs for Test
                        if (registry.ProcessStage == "C3")
                        {
                            lblRemarks.Text = "For additional pcs execution: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                        }

                        // validation if achieve test qty
                        if (registry.ProcessStage == "C3B")
                        {
                            lblRemarks.Text = "For additional pcs execution: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, _testPcsMsg, "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                            {
                                registry.WriteRegistry("pedalStatus", "False");
                                registry.WriteRegistry("processStage", "C4A");
                                registry.WriteRegistry("testCounter", "0");
                            }
                            else
                            {
                            InputQty:
                                string input = "0";
                                DialogBox.ShowInputDialogBox(ref input, "Please input additional quantity.", "Message Confirmation", 300, 110);

                                if (int.TryParse(input, out int value) && !String.IsNullOrEmpty(input))
                                {
                                    // not greater than 9
                                    if (input.Length > 1)
                                    {
                                        MessageBox.Show(this, "Maximum quantity is not greater than 9.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }

                                    // not less than or equal to 0
                                    if (input.Length <= 0)
                                    {
                                        MessageBox.Show(this, "Quantity should not be less than or equal to 0.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }

                                    if (value <= registry.TestCounter && value != 0)
                                    {
                                        lblRemarks.Text = "Pedal is in action";

                                        var counter1 = registry.TestCounter - value;
                                        registry.WriteRegistry("testCounter", counter1.ToString());

                                        // activate pedal to continue
                                        registry.WriteRegistry("pedalStatus", "True");
                                        registry.WriteRegistry("processStage", "C3");
                                    }
                                    else
                                    {
                                        MessageBox.Show(this, "Quantity should not be equal to 0 or greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(this, "Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }
                            }
                        }

                        // Caliper Test message confirmation
                        if (registry.ProcessStage == "C4A")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, "Please perform (" + _midPcsInitCount.ToString() + ") CALIPER/PULL TEST input.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                            if (result == DialogResult.OK)
                            {
                                registry.WriteRegistry("processStage", "C4B");
                            }
                        }


                        // Caliper Test execution
                        if (registry.ProcessStage == "C4B")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                        }

                        // Caliper Test validation if no data within specific timeframe
                        if (registry.ProcessStage == "C4C")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";

                            DialogResult result = MessageBox.Show(this, "Do you want to continue CALIPER execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                                registry.WriteRegistry("processStage", "C4B");
                            else
                                MessageBox.Show(this, "Please continue CALIPER execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        // Pull Test message confirmation
                        if (registry.ProcessStage == "C5A")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                            registry.WriteRegistry("processStage", "C5B");
                        }

                        // Pull Test execution
                        if (registry.ProcessStage == "C5B")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                        }

                        // Pull Test validation if no data within specific timeframe
                        if (registry.ProcessStage == "C5C")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, "Do you want to continue PULL TEST execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                                registry.WriteRegistry("processStage", "C5B");
                            else
                                MessageBox.Show(this, "Please continue PULL TEST execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        // Message Confirmation to back to execution
                        if (registry.ProcessStage == "C8")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, "Please continue production execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                            if (result == DialogResult.OK)
                            {
                                registry.WriteRegistry("processStage", "B1");
                                registry.WriteRegistry("quotaCounter", "0");
                                registry.WriteRegistry("testCounter", "0");
                            }
                        }
                        //*********************** End Daily Quota Section ***********************//

                        //*********************** Start Production Section ***********************//

                        // Start Production
                        if (registry.ProcessStage == "B1" && !registry.PedalStatus)
                        {
                            // activate pedal to continue
                            registry.WriteRegistry("pedalStatus", "True");
                        }

                        // validation if achieve prod qty
                        if (registry.ProcessStage == "B2" && !registry.PedalStatus)
                        {
                            lblRemarks.Text = "For quantity validation";
                            DialogResult result = MessageBox.Show(this, _prodPcsMsg, "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                            {
                                lblRemarks.Text = "Execute additional item(s).";
                                DialogResult result2 = MessageBox.Show(this, "Please excute additional (" + _lastPcsInitCount.ToString() + ") item/s.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                                if (result2 == DialogResult.OK)
                                {
                                    // activate pedal to continue
                                    registry.WriteRegistry("pedalStatus", "True");
                                    registry.WriteRegistry("processStage", "B3");
                                }
                            }
                            else
                            {
                            InputQty:
                                string input = "0";
                                DialogBox.ShowInputDialogBox(ref input, "Please input additional quantity.", "Message Confirmation", 300, 110);

                                if (int.TryParse(input, out int value) && !String.IsNullOrEmpty(input))
                                {
                                    // not greater than 9
                                    if (input.Length > 1)
                                    {
                                        MessageBox.Show(this, "Maximum quantity is not greater than 9.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }

                                    // not less than or equal to 0
                                    if (input.Length <= 0)
                                    {
                                        MessageBox.Show(this, "Quantity should not be less than or equal to 0.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }

                                    if (value <= registry.ProcessCounter && value != 0)
                                    {
                                        lblRemarks.Text = "Pedal is in action";

                                        var counter1 = registry.ProcessCounter - value;
                                        registry.WriteRegistry("processCounter", counter1.ToString());

                                        if (registry.QuotaCounter >= value)
                                        {
                                            counter1 = registry.QuotaCounter - value;
                                            registry.WriteRegistry("quotaCounter", counter1.ToString());
                                        }

                                        // activate pedal to continue
                                        registry.WriteRegistry("pedalStatus", "True");
                                        registry.WriteRegistry("processStage", "B1");

                                    }
                                    else
                                    {
                                        MessageBox.Show(this, "Quantity should not be equal to 0 or greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(this, "Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }
                            }
                        }

                        // Additional Pcs for Test
                        if (registry.ProcessStage == "B3")
                        {
                            lblRemarks.Text = "For additional pcs execution: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                        }

                        // validation if achieve test qty
                        if (registry.ProcessStage == "B3B")
                        {
                            lblRemarks.Text = "For additional pcs execution: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, _testPcsMsg, "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                            {
                                registry.WriteRegistry("pedalStatus", "False");
                                registry.WriteRegistry("processStage", "B4A");
                                registry.WriteRegistry("testCounter", "0");
                            }
                            else
                            {
                            InputQty:
                                string input = "0";
                                DialogBox.ShowInputDialogBox(ref input, "Please input additional quantity.", "Message Confirmation", 300, 110);

                                if (int.TryParse(input, out int value) && !String.IsNullOrEmpty(input))
                                {
                                    // not greater than 9
                                    if (input.Length > 1)
                                    {
                                        MessageBox.Show(this, "Maximum quantity is not greater than 9.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }

                                    // not less than or equal to 0
                                    if (input.Length <= 0)
                                    {
                                        MessageBox.Show(this, "Quantity should not be less than or equal to 0.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }

                                    if (value <= registry.TestCounter && value != 0)
                                    {
                                        lblRemarks.Text = "Pedal is in action";

                                        var counter1 = registry.TestCounter - value;
                                        registry.WriteRegistry("testCounter", counter1.ToString());

                                        // activate pedal to continue
                                        registry.WriteRegistry("pedalStatus", "True");
                                        registry.WriteRegistry("processStage", "B3");

                                    }
                                    else
                                    {
                                        MessageBox.Show(this, "Quantity should not be equal to 0 or greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                        goto InputQty;
                                    }
                                }
                                else
                                {
                                    MessageBox.Show(this, "Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }
                            }
                        }

                        // Caliper Test message confirmation
                        if (registry.ProcessStage == "B4A")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                            DialogResult result = MessageBox.Show(this, "Please perform (" + _lastPcsInitCount.ToString() + ") CALIPER/PULL TEST input.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                            if (result == DialogResult.OK)
                            {
                                registry.WriteRegistry("processStage", "B4B");
                            }
                        }


                        // Caliper Test execution
                        if (registry.ProcessStage == "B4B")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                        }


                        // Caliper Test validation if no data within specific timeframe
                        if (registry.ProcessStage == "B4C")
                        {
                            lblRemarks.Text = "For CALIPER input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";

                            DialogResult result = MessageBox.Show(this, "Do you want to continue CALIPER execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                                registry.WriteRegistry("processStage", "B4B");
                            else
                                MessageBox.Show(this, "Please continue CALIPER execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        // Pull Test message confirmation
                        if (registry.ProcessStage == "B5A")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                            registry.WriteRegistry("processStage", "B5B");
                        }

                        // Pull Test execution
                        if (registry.ProcessStage == "B5B")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                        }

                        // Pull Test validation if no data within specific timeframe
                        if (registry.ProcessStage == "B5C")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";

                            DialogResult result = MessageBox.Show(this, "Do you want to continue PULL TEST execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                                registry.WriteRegistry("processStage", "B5B");
                            else
                                MessageBox.Show(this, "Please continue PULL TEST execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        // Finished Job Message
                        if (registry.ProcessStage == "B8")
                        {
                            lblRemarks.Text = "For PULL TEST input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";

                            btnStart.Enabled = false;
                            btnTerminate.Enabled = false;

                        InputEndJob:
                            DialogResult result = MessageBox.Show(this, _endJobMsg, "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                            if (result == DialogResult.Yes)
                            {
                                registry.WriteRegistry("processStage", "F1");

                                menuStrip.Enabled = true;

                                btnStart.Enabled = true;
                                btnTerminate.Enabled = false;

                                stStripMenuItem.Enabled = true;
                                tmStripMenuItem.Enabled = false;
                                ptStripMenuItem.Enabled = false;
                                cmStripMenuItem.Enabled = false;
                            }
                            else
                                goto InputEndJob;
                        }
                        //*********************** End Production Section ***********************//
                    }
                } 
                else
                {
                    lblRemarks.Text = "A valid software license could not be found.";

                    btnStart.Enabled = false;
                    btnTerminate.Enabled = false;
                    menuStrip.Enabled = true;

                    stStripMenuItem.Enabled = false;
                    tmStripMenuItem.Enabled = false;
                    ptStripMenuItem.Enabled = false;
                    cmStripMenuItem.Enabled = false;
                    lcStripMenuItem.Enabled = true;
                    abStripMenuItem.Enabled = true;
                    rsStripMenuItem.Enabled = false;    
                }
            } 
            else
            {
                switch (registry.IOBoardStatus)
                {
                    case "X1":
                        MessageBox.Show(this, "IO Device is not properly set.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case "X2":
                        MessageBox.Show(this, "Com Port is not properly set.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case "X3":
                        MessageBox.Show(this, "IO Device error upon Initialize.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case "X4":
                        MessageBox.Show(this, "IO Device port is closed", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case "X5":
                        MessageBox.Show(this, "IO Device error upon Closing.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    default:
                        break;
                }

                btnStart.Enabled = false;
                btnTerminate.Enabled = false;
                menuStrip.Enabled = false;
            }

        proceed:
            Thread.Sleep(_threadDelay);
            backgroundWorker.ReportProgress(0);
        }

        private void backgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            registry.ReadRegistry();

            // X0 - meand device is working as expected
            if (registry.IOBoardStatus == Mode)
            {
                // valid license
                if(this._isLicense)
                {
                    stStripMenuItem.Enabled = false;
                    tmStripMenuItem.Enabled = true;
                    ptStripMenuItem.Enabled = false;
                    cmStripMenuItem.Enabled = false;

                    if (!registry.PedalStatus && !registry.IsProd && registry.ProcessStage == "A1")
                    {
                        btnStart.Enabled = true;
                        btnTerminate.Enabled = false;

                        //stStripMenuItem.Enabled = true;
                        //tmStripMenuItem.Enabled = false;
                        //ptStripMenuItem.Enabled = false;
                        //cmStripMenuItem.Enabled = false;
                    }
                    else
                    {
                        btnStart.Enabled = false;
                        btnTerminate.Enabled = true;
                    }

                    if (registry.ProcessStage == "A1")
                    {
                        stStripMenuItem.Enabled = btnStart.Enabled;
                        tmStripMenuItem.Enabled = !btnStart.Enabled;
                        ptStripMenuItem.Enabled = false;
                        cmStripMenuItem.Enabled = false;
                    }

                    // 2 - validation if achieve test qty, 3 - Pull Test message confirmation, 5 - Pull Test message confirmation
                    string[] stageArray1 = { "A2", "A3", "A5A", "A8",
                                             "B1", "B2", "B3", "B5A", "B8",
                                             "C2", "C3", "C5A", "C8" };
                    if (stageArray1.Contains(registry.ProcessStage))
                    {
                        stStripMenuItem.Enabled = false;
                        tmStripMenuItem.Enabled = true;
                        ptStripMenuItem.Enabled = false;
                        cmStripMenuItem.Enabled = false;
                    }

                    // 4 - Caliper Test
                    string[] stageArray2 = { "A4A", "B4A", "C4A" };
                    if (stageArray2.Contains(registry.ProcessStage))
                    {
                        stStripMenuItem.Enabled = false;
                        tmStripMenuItem.Enabled = true;
                        ptStripMenuItem.Enabled = true;
                        cmStripMenuItem.Enabled = true;
                    }

                    // 4 - Caliper Test Execution
                    string[] stageArray4 = { "A4B", "B4B", "C4B", "A4C", "B4C", "C4C" };
                    if (stageArray4.Contains(registry.ProcessStage))
                    {
                        stStripMenuItem.Enabled = false;
                        tmStripMenuItem.Enabled = true;
                        ptStripMenuItem.Enabled = true;
                        cmStripMenuItem.Enabled = false;
                    }

                    // 6 - Pull Test execution, 7 - Pull Test validation if no data within specific timeframe, Message Confirmation to back to execution
                    string[] stageArray3 = { "A5B", "A5C",
                                             "B5B", "B5C",
                                             "C5B", "C5C" };
                    if (stageArray3.Contains(registry.ProcessStage))
                    {
                        stStripMenuItem.Enabled = false;
                        tmStripMenuItem.Enabled = true;
                        ptStripMenuItem.Enabled = true;
                        cmStripMenuItem.Enabled = false;
                    }

                    tsStatusLabel.Text = (registry.PedalStatus) ? "Pedal is active" : "Pedal is disabled";
                    tsStatusLabel.ToolTipText = "Last Activity";
                    tsStatusLabel.Image = (Image)(Resources.ResourceManager.GetObject((registry.PedalStatus) ? "green" : "red"));
                } 
                else
                {
                    tsStatusLabel.Text = "Unregistered Software";
                    tsStatusLabel.ToolTipText = "Invalid License";
                    tsStatusLabel.Image = (Image)(Resources.ResourceManager.GetObject("red"));
                }
            } 
            else
            {
                tsStatusLabel.Text = "IO Device error";
                tsStatusLabel.ToolTipText = "Last Activity";
                tsStatusLabel.Image = (Image)(Resources.ResourceManager.GetObject("red"));
            }

            lblCounter.Text = (registry.IsProd) ? registry.ProcessCounter.ToString() : "0";
            lblQuota.Text = registry.QuotaCounter.ToString();

            tsDateTimeLabel.Text = System.DateTime.Today.ToLongDateString();
            tsDateTimeLabel.ToolTipText = "Last Activity";
        }

        private void backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            backgroundWorker.RunWorkerAsync();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            btnTerminate.Enabled = true;

            stStripMenuItem.Enabled = false;
            tmStripMenuItem.Enabled = true;
            ptStripMenuItem.Enabled = false;
            cmStripMenuItem.Enabled = false;
            registry.WriteRegistry("pedalStatus", "True");
        }

        private void btnTerminate_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Do you want to terminate this job?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (result == DialogResult.Yes)
            {
                stStripMenuItem.Enabled = true;
                tmStripMenuItem.Enabled = false;
                ptStripMenuItem.Enabled = false;
                cmStripMenuItem.Enabled = false;

                btnStart.Enabled = true;
                btnTerminate.Enabled = false;

                registry.WriteRegistry("processStage", "F2");
            } 
        }

        private void stStripMenuItem_Click(object sender, EventArgs e)
        {
            btnStart_Click(sender, e);
        }

        private void tmStripMenuItem_Click(object sender, EventArgs e)
        {
            btnTerminate_Click(sender, e);
        }

        private void cmStripMenuItem_Click(object sender, EventArgs e)
        {
            string input = "";
            DialogBox.ShowInputDialogBox(ref input, "Please input caliper results.", "Message Confirmation", 300, 110);

            Regex regex = new(@"[^0-9^.^\;^\s*]");
            MatchCollection matches = regex.Matches(input);

            if (!String.IsNullOrEmpty(input) && matches.Count > 0)
            {
                switch (registry.ProcessStage)
                {
                    case "A4A":
                        registry.WriteRegistry("processStage", "A5A");
                        break;
                    case "B4A":
                        registry.WriteRegistry("processStage", "B5A");
                        break;
                    case "C4A":
                        registry.WriteRegistry("processStage", "C5A");
                        break;
                    default:
                        break;
                }

                // log results
                registry.TextLogger(learjob.OrderNumber, DateTimeOffset.Now + " - [" + learjob.OrderNumber + ":" + learjob.LeadSet + "] - Caliper Dimension: " + input);
            }
            else
            {
                MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ptStripMenuItem_Click(object sender, EventArgs e)
        {
            menuStrip.Enabled = false;

            DialogResult result = MessageBox.Show("Add additional test pcs?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (result == DialogResult.Yes)
            {
                string input = "0";
                DialogBox.ShowInputDialogBox(ref input, "Please input additional quantity.", "Message Confirmation", 300, 110);

                if (int.TryParse(input, out int value) && !String.IsNullOrEmpty(input))
                {
                    // not greater than 9
                    if (input.Length > 1)
                    {
                        MessageBox.Show("Maximum quantity is not greater than 9.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // not less than or equal to 0
                    if (input.Length <= 0)
                    {
                        MessageBox.Show("Quantity should not be less than or equal to 0.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if(value == 0)
                    {
                        MessageBox.Show("Quantity should not be equal to 0.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // Test Mode
                    if (!registry.IsProd)
                    {
                        if (value <= registry.ProcessCounter)
                        {
                            lblRemarks.Text = "Pedal is in action";
                            menuStrip.Enabled = true;

                            var counter1 = registry.ProcessCounter - value;
                            registry.WriteRegistry("processCounter", counter1.ToString());

                            if (value >= registry.TestCounter)
                                registry.WriteRegistry("testCounter", "0");
                            else
                            {
                                counter1 = registry.TestCounter - value;
                                registry.WriteRegistry("testCounter", counter1.ToString());
                            }

                            // activate pedal to continue
                            registry.WriteRegistry("pedalStatus", "True");

                            string[] stageArray1 = { "A4A", "A4B", "A4C", "A5A", "A5B", "A5C", "A8" };
                            if (stageArray1.Contains(registry.ProcessStage))
                            {
                                registry.WriteRegistry("processStage", "A1");
                            }

                            string[] stageArray2 = { "B4A", "B4B", "B4C", "B5A", "B5B", "B5C", "B8" };
                            if (stageArray2.Contains(registry.ProcessStage))
                            {
                                registry.WriteRegistry("processStage", "B1");
                            }

                            string[] stageArray3 = { "C4A", "C4B", "C4C", "C5A", "C5B", "C5C", "C8" };
                            if (stageArray3.Contains(registry.ProcessStage))
                            {
                                registry.WriteRegistry("processStage", "C2");
                            }
                        }
                        else
                        {
                            MessageBox.Show("Quantity should not greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    else
                    {
                        string[] stageArray0 = { "B4B", "B5B", "C4B", "C5B" };
                        string[] stageArray1 = { "B4B", "B5B" };
                        string[] stageArray2 = { "C4B", "C5B" };

                        if (stageArray0.Contains(registry.ProcessStage))
                        {
                            int initCount = 0;

                            if (stageArray1.Contains(registry.ProcessStage))
                                initCount = _lastPcsInitCount;


                            if (stageArray2.Contains(registry.ProcessStage))
                                initCount = _midPcsInitCount;

                            if (value <= initCount)
                            {
                                lblRemarks.Text = "Pedal is in action";
                                menuStrip.Enabled = true;

                                var counter1 = initCount - value;
                                registry.WriteRegistry("testCounter", counter1.ToString());

                                // activate pedal to continue
                                registry.WriteRegistry("pedalStatus", "True");

                                if (stageArray1.Contains(registry.ProcessStage))
                                    registry.WriteRegistry("processStage", "B3");

                                if (stageArray2.Contains(registry.ProcessStage))
                                    registry.WriteRegistry("processStage", "C3");
                            }
                            else
                            {
                                MessageBox.Show("Quantity should not greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                return;
                            }
                        } 
                    }
                }
                else
                {
                    MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void rsStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Restarting PAO Interface I/O Board.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            registry.WriteRegistry("ioBoardStatus", "R0");
        }

        private void abStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm aboutForm = new AboutForm();
            aboutForm.ShowDialog();
        }

        private void lcStripMenuItem_Click(object sender, EventArgs e)
        {
            LicenseRegForm licRegForm = new LicenseRegForm();
            licRegForm.ShowDialog();
        }
    }
}
