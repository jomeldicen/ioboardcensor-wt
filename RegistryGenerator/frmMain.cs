using Experimental.System.Messaging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RegistryGenerator
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }

        private int InitializeRegistry()
        {
            ////opening the subkey  
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\RenTradeSettings");

            //if it does exist, set key values
            if (key == null)
            {
                //accessing the LocalMachine root element  
                //and adding "OurSettings" subkey to the "SOFTWARE" subkey  
                key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\RenTradeSettings");

                //storing the values  
                key.SetValue("ioBoardStatus", "X0");            // IO Board Status
                key.SetValue("isProd", "False");                // true means reel material is detected
                key.SetValue("jobInfo", "");                    // Job Info
                key.SetValue("oldOrderNos", "");                // Old Order Nos
                key.SetValue("pedalFlag", "0");                 // 1 means hit, 0 means loose
                key.SetValue("pedalStatus", "False");           // true means pedal is activated by default
                key.SetValue("processCounter", "0");            // number of process production item per batches
                key.SetValue("processStage", "A1");             // Stages
                key.SetValue("quotaCounter", "0");              // number of cummulative production count
                key.SetValue("reelStatus", "True");             // true means reel material is detected
                key.SetValue("testCounter", "0");               // number of pull test item on first Pcs

                key.Close();

                return 0;
            } else
            {
                return 1;
            }
        }

        private void btnGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                int regStatus = this.InitializeRegistry();

                if (regStatus == 0)
                {
                    MessageBox.Show(this, "Registry has been successfully created! Please assign permission on RenTradeSettings key.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this, "Registry already exist! Make it sure that you applied permission.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnMsmq_Click(object sender, EventArgs e)
        {
            try
            {
                // create queue name if not existed
                if (!MessageQueue.Exists(".\\private$\\test"))
                {
                    MessageQueue.Create(".\\private$\\test");
                    MessageBox.Show(this, "MSMQ has been successfully created! Please assign permission on 'test' Private Queue.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                    MessageBox.Show(this, "MSMQ already exist! Make it sure that you applied permission.", "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Message Confirmation", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
        }
    }
}
