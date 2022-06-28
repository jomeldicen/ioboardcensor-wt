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

        private string Mode;

        public MainForm(IOptions<ServiceConfiguration> options)
        {
            InitializeComponent();

            _options = options;

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

            Mode = "";
            if (_environmentMode == "PRD")
                Mode = "X0";

            // X0 - meand device is working as expected
            if (registry.IOBoardStatus == Mode)
            {
                // reel section
                if (!registry.ReelStatus)
                {
                    menuStrip.Enabled = false;

                    btnStart.Enabled = false;
                    btnTerminate.Enabled = false;

                    MessageBox.Show("Reel has been deactivated! Waiting to scan BOM.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);

                InputQty:
                    string input = "0";
                    DialogBox.ShowInputDialogBox(ref input, "Please input additional quantity.", "Message Confirmation", 300, 110);

                    if (!String.IsNullOrEmpty(input) && learjob.IsBOMExist(input))
                    {
                        registry.WriteRegistry("reelStatus", "True");
                        registry.WriteRegistry("pedalStatus", "True");
                    }
                    else
                    {
                        MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        goto InputQty;
                    }
                }

                // Test Mode
                if (!registry.IsProd)
                {
                    menuStrip.Enabled = true;
                    lblStatus.Text = "Test Mode";
                    lblRemarks.Text = (registry.PedalStatus) ? "For item pcs execution: " + registry.ProcessCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + "." : "Click 'Start' button to proceed";

                    // validation if achieve test qty
                    if (registry.ProcessStage == "A2" && !registry.PedalStatus)
                    {
                        menuStrip.Enabled = false;
                        lblRemarks.Text = "For quantity validation";

                        DialogResult result = MessageBox.Show(_testPcsMsg, "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        if (result == DialogResult.Yes)
                        {
                            registry.WriteRegistry("processStage", "A4");
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
                                    MessageBox.Show("Maximum quantity is not greater than 9.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }

                                // not less than or equal to 0
                                if (input.Length <= 0)
                                {
                                    MessageBox.Show("Quantity should not be less than or equal to 0.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }

                                if (value <= registry.ProcessCounter)
                                {
                                    lblRemarks.Text = "Pedal is in action";
                                    menuStrip.Enabled = true;

                                    var counter1 = registry.ProcessCounter - value;
                                    registry.WriteRegistry("processCounter", counter1.ToString());

                                    // activate pedal to continue
                                    registry.WriteRegistry("pedalStatus", "True");
                                    registry.WriteRegistry("processStage", "A1");

                                }
                                else
                                {
                                    MessageBox.Show("Quantity should not be equal to 0 or greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }
                            }
                            else
                            {
                                MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                goto InputQty;
                            }
                        }
                    }

                    // Additional Pcs for Test
                    if (registry.ProcessStage == "A3")
                    {
                        lblRemarks.Text = "For item pcs execution: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                    }

                    // Caliper Test
                    if (registry.ProcessStage == "A4")
                    {
                        lblRemarks.Text = "For caliper input";

                    InputCaliper:
                        string input = "";
                        DialogBox.ShowInputDialogBox(ref input, "Please input caliper results.", "Message Confirmation", 300, 110);

                        Regex regex = new(@"[^0-9^.^\;^\s*]");
                        MatchCollection matches = regex.Matches(input);

                        if (!String.IsNullOrEmpty(input) && matches.Count > 0)
                        {
                            menuStrip.Enabled = false;

                            // set process stage to Pull test
                            registry.WriteRegistry("processStage", "A5");
                            // log results
                            registry.TextLogger(DateTimeOffset.Now + " - [" + learjob.OrderNumber + ":" + learjob.LeadSet + "] - Caliper Dimension: " + input);
                        }
                        else
                        {
                            MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            goto InputCaliper;
                        }
                    }

                    // Pull Test message confirmation
                    if (registry.ProcessStage == "A5")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                        MessageBox.Show("Please perform (" + _firstPcsInitCount.ToString() + ") pull test.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        registry.WriteRegistry("processStage", "A6");
                    }

                    // Pull Test execution
                    if (registry.ProcessStage == "A6")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";
                    }

                    // Pull Test validation if no data within specific timeframe
                    if (registry.ProcessStage == "A7")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";

                        DialogResult result = MessageBox.Show("Do you want to continue pull test execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        if (result == DialogResult.Yes)
                            registry.WriteRegistry("processStage", "A6");
                        else
                            MessageBox.Show("Please continue pull test execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    // Message Confirmation to back to production
                    if (registry.ProcessStage == "A8")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _firstPcsInitCount.ToString() + ".";

                        MessageBox.Show("Production execution activated.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                        menuStrip.Enabled = false;
                        lblRemarks.Text = "For daily quota validation";
                        MessageBox.Show(_quotaPcsMsg, "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        lblRemarks.Text = "Execute additional item(s).";
                        MessageBox.Show("Please excute additional (" + _midPcsInitCount.ToString() + ") item/s.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        // activate pedal to continue
                        registry.WriteRegistry("pedalStatus", "True");
                        registry.WriteRegistry("processStage", "C3");
                    }

                    // Additional Pcs for Test
                    if (registry.ProcessStage == "C3")
                    {
                        lblRemarks.Text = "For additional pcs execution: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                    }

                    // Caliper Test
                    if (registry.ProcessStage == "C4")
                    {
                        lblRemarks.Text = "For caliper input";

                    InputCaliper:
                        string input = "";
                        DialogBox.ShowInputDialogBox(ref input, "Please input caliper results.", "Message Confirmation", 300, 110);

                        Regex regex = new(@"[^0-9^.^\;^\s*]");
                        MatchCollection matches = regex.Matches(input);

                        if (!String.IsNullOrEmpty(input) && matches.Count > 0)
                        {
                            menuStrip.Enabled = false;

                            // set process stage to Pull test
                            registry.WriteRegistry("processStage", "C5");
                            // log results
                            registry.TextLogger(DateTimeOffset.Now + " - [" + learjob.OrderNumber + ":" + learjob.LeadSet + "] - Caliper Dimension: " + input);
                        }
                        else
                        {
                            MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            goto InputCaliper;
                        }
                    }

                    // Pull Test message confirmation
                    if (registry.ProcessStage == "C5")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                        MessageBox.Show("Please perform (" + _midPcsInitCount.ToString() + ") pull test.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        registry.WriteRegistry("processStage", "C6");
                    }

                    // Pull Test execution
                    if (registry.ProcessStage == "C6")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";
                    }

                    // Pull Test validation if no data within specific timeframe
                    if (registry.ProcessStage == "C7")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";

                        DialogResult result = MessageBox.Show("Do you want to continue pull test execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        if (result == DialogResult.Yes)
                            registry.WriteRegistry("processStage", "C6");
                        else
                            MessageBox.Show("Please continue pull test execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    // Message Confirmation to back to production
                    if (registry.ProcessStage == "C8")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _midPcsInitCount.ToString() + ".";

                        MessageBox.Show("Please continue production execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        registry.WriteRegistry("processStage", "B1");
                        registry.WriteRegistry("quotaCounter", "0");
                    }
                    //*********************** End Daily Quota Section ***********************//

                    //*********************** Start Production Section ***********************//
                    // validation if achieve test qty
                    if (registry.ProcessStage == "B2" && !registry.PedalStatus)
                    {
                        menuStrip.Enabled = false;
                        lblRemarks.Text = "For quantity validation";

                        DialogResult result = MessageBox.Show(_prodPcsMsg, "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        if (result == DialogResult.Yes)
                        {
                            lblRemarks.Text = "Execute additional item(s).";
                            MessageBox.Show("Please excute additional (" + _lastPcsInitCount.ToString() + ") item/s.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // activate pedal to continue
                            registry.WriteRegistry("pedalStatus", "True");
                            registry.WriteRegistry("processStage", "B3");
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
                                    MessageBox.Show("Maximum quantity is not greater than 9.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }

                                // not less than or equal to 0
                                if (input.Length <= 0)
                                {
                                    MessageBox.Show("Quantity should not be less than or equal to 0.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }

                                if (value <= registry.ProcessCounter && value != 0)
                                {
                                    lblRemarks.Text = "Pedal is in action";
                                    menuStrip.Enabled = true;

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
                                    MessageBox.Show("Quantity should not be equal to 0 or greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                    goto InputQty;
                                }
                            }
                            else
                            {
                                MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                goto InputQty;
                            }
                        }
                    }

                    // Additional Pcs for Test
                    if (registry.ProcessStage == "B3")
                    {
                        lblRemarks.Text = "For additional pcs execution: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                    }

                    // Caliper Test
                    if (registry.ProcessStage == "B4")
                    {
                        lblRemarks.Text = "For caliper input";

                    InputCaliper:
                        string input = "";
                        DialogBox.ShowInputDialogBox(ref input, "Please input caliper results.", "Message Confirmation", 300, 110);

                        Regex regex = new(@"[^0-9^.^\;^\s*]");
                        MatchCollection matches = regex.Matches(input);

                        if (!String.IsNullOrEmpty(input) && matches.Count > 0)
                        {
                            menuStrip.Enabled = false;

                            // set process stage to Pull test
                            registry.WriteRegistry("processStage", "B5");
                            // log results
                            registry.TextLogger(DateTimeOffset.Now + " - [" + learjob.OrderNumber + ":" + learjob.LeadSet + "] - Caliper Dimension: " + input);
                        }
                        else
                        {
                            MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            goto InputCaliper;
                        }
                    }

                    // Pull Test message confirmation
                    if (registry.ProcessStage == "B5")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                        MessageBox.Show("Please perform (" + _lastPcsInitCount.ToString() + ") pull test.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        registry.WriteRegistry("processStage", "B6");
                    }

                    // Pull Test execution
                    if (registry.ProcessStage == "B6")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";
                    }

                    // Pull Test validation if no data within specific timeframe
                    if (registry.ProcessStage == "B7")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";

                        DialogResult result = MessageBox.Show("Do you want to continue pull test execution?", "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                        if (result == DialogResult.Yes)
                            registry.WriteRegistry("processStage", "B6");
                        else
                            MessageBox.Show("Please continue pull test execution.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    // Finished Job Message
                    if (registry.ProcessStage == "B8")
                    {
                        lblRemarks.Text = "For pull test input: " + registry.TestCounter.ToString() + " out of " + _lastPcsInitCount.ToString() + ".";

                        btnStart.Enabled = false;
                        btnTerminate.Enabled = false;
                        menuStrip.Enabled = false;

                    InputEndJob:
                        DialogResult result = MessageBox.Show(_endJobMsg, "Message Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
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
                switch (registry.IOBoardStatus)
                {
                    case "X1":
                        MessageBox.Show("IO Device is not properly set.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case "X2":
                        MessageBox.Show("Com Port is not properly set.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case "X3":
                        MessageBox.Show("IO Device error upon Initialize.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        break;
                    case "X4":
                        MessageBox.Show("IO Device error upon Closing.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                if (!registry.PedalStatus && !registry.IsProd && registry.ProcessStage == "A1")
                {
                    btnStart.Enabled = true;
                    btnTerminate.Enabled = false;

                    stStripMenuItem.Enabled = true;
                    tmStripMenuItem.Enabled = false;
                    ptStripMenuItem.Enabled = false;
                    cmStripMenuItem.Enabled = false;
                }
                else
                {
                    btnStart.Enabled = false;
                    btnTerminate.Enabled = true;

                    stStripMenuItem.Enabled = false;
                    tmStripMenuItem.Enabled = true;
                    ptStripMenuItem.Enabled = true;
                    cmStripMenuItem.Enabled = true;
                }

                tsStatusLabel.Text = (registry.PedalStatus) ? "Pedal is active" : "Pedal is disabled";
                tsStatusLabel.ToolTipText = "Last Activity";
                tsStatusLabel.Image = (Image)(Resources.ResourceManager.GetObject((registry.PedalStatus) ? "green" : "red"));
            } 
            else
            {
                tsStatusLabel.Text = "IO Device error";
                tsStatusLabel.ToolTipText = "Last Activity";
                tsStatusLabel.Image = (Image)(Resources.ResourceManager.GetObject("red"));

                //registry.WriteRegistry("pedalStatus", "False");
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
            ptStripMenuItem.Enabled = true;
            cmStripMenuItem.Enabled = true;
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

        }

        private void ptStripMenuItem_Click(object sender, EventArgs e)
        {
            menuStrip.Enabled = false;

            // Test Mode
            if (!registry.IsProd)
            {
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
                            registry.WriteRegistry("processStage", "A1");
                        }
                        else
                        {
                            MessageBox.Show("Quantity should not greater than the accumulated count", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please input a valid value", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            else
            {

            }
        }
    }
}
