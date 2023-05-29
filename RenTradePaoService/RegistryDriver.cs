﻿using Microsoft.Extensions.Options;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RenTradeWindowService
{
    public class RegistryDriver
    {
        private RegistryKey key;
        private string[] logs;

        public int PedalFlag { get; private set; }
        public bool PedalStatus { get; private set; }
        public bool PedalIsActivated { get; private set; }
        public int ProcessCounter { get; set; }
        public bool IsProd { get; private set; }
        public bool IsPullTest { get; private set; }
        public int TestCounter { get; set; }
        public int QuotaCounter { get; set; }
        public int CycleCounter { get; set; }
        public string ProcessStage { get; private set; }
        public bool ReelStatus { get; private set; }
        public bool EndJob { get; private set; }

        public string IOBoardStatus { get; private set; }
        public string OldOrderNos { get; private set; }
        public string JobInfo { get; private set; }
        public string RefValue { get; private set; }
        public string RefStatus { get; private set; }
        public int RefInitialCount { get; set; }
        public int RefFinalCount { get; set; }
        public int RefCounter { get; set; }

        private readonly string _logPath;

        public RegistryDriver(IOptions<ServiceConfiguration> options)
        {
            //InitializeRegistry();

            _logPath = options.Value.LogPath;
        }

        private void InitializeRegistry()
        {
            ////opening the subkey  
            RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\RenTradeSettings");

            //if it does exist, set key values
            if (key == null)
            {
                //accessing the LocalMachine root element  
                //and adding "OurSettings" subkey to the "SOFTWARE" subkey  
                this.key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\RenTradeSettings");

                //storing the values  
                key.SetValue("pedalFlag", "0");                 // 1 means hit, 0 means loose
                key.SetValue("pedalStatus", "False");           // true means pedal is activated by default
                key.SetValue("processCounter", "0");            // number of process production item per batches
                key.SetValue("isProd", "False");                // true means reel material is detected

                key.SetValue("testCounter", "0");               // number of pull test item on first Pcs
                key.SetValue("quotaCounter", "0");              // number of cummulative production count
                key.SetValue("cycleCounter", "0");              // number of cycle count

                key.SetValue("processStage", "A1");             // A1-validation if achieve test qty, A2-1st pull test, A3-1st calipher test, 
                                                                // B1-validation if achieve prod qty, B2-2nd pull test, B3-2nd calipher test, B4-
                key.SetValue("reelStatus", "True");             // true means reel material is detected

                key.SetValue("ioBoardStatus", "");              // IO Board Status
                key.SetValue("refValue", "");                   // 
                key.SetValue("refStatus", "");                   // 
                key.SetValue("refCounter", "0");                // 
                key.SetValue("refInitialCount", "0");           // 
                key.SetValue("refFinalCount", "0");             // 
                key.SetValue("endJob", "False");                // End of Job

                key.Close();
            }
        }

        public void ReadRegistry()
        {
            try
            {
                //opening the subkey  
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\RenTradeSettings");

                //if it does exist, retrieve the stored values  
                if (key != null)
                {
                    this.PedalFlag = Convert.ToInt16(key.GetValue("pedalFlag"));                            // 1 means hit, 0 means loose
                    this.PedalStatus = Convert.ToBoolean(key.GetValue("pedalStatus"));                      // true means pedal is activated by default
                    this.ProcessCounter = Convert.ToInt16(key.GetValue("processCounter"));                  // number of process production item per batches
                    this.IsProd = Convert.ToBoolean(key.GetValue("isProd"));                                // number of process production item per batches
                    this.IsPullTest = Convert.ToBoolean(key.GetValue("isPullTest"));                        // true means reel material is detected

                    this.TestCounter = Convert.ToInt16(key.GetValue("testCounter"));                        // number of pull test item on first Pcs
                    this.QuotaCounter = Convert.ToInt16(key.GetValue("quotaCounter"));                      // number of cummulative production count    
                    this.CycleCounter = Convert.ToInt16(key.GetValue("cycleCounter"));                      // number of cycle wt count                    
                    this.ProcessStage = key.GetValue("processStage").ToString();                            // get the current stage

                    this.ReelStatus = Convert.ToBoolean(key.GetValue("reelStatus"));                        // Check Reel Status

                    this.IOBoardStatus = key.GetValue("ioBoardStatus").ToString();
                    this.OldOrderNos = key.GetValue("oldOrderNos").ToString();
                    this.JobInfo = key.GetValue("jobInfo").ToString();
                    this.RefValue = key.GetValue("refValue").ToString();
                    this.RefStatus = key.GetValue("refStatus").ToString();
                    this.RefInitialCount = Convert.ToInt16(key.GetValue("refInitialCount"));
                    this.RefFinalCount = Convert.ToInt16(key.GetValue("refFinalCount"));
                    this.RefCounter = Convert.ToInt16(key.GetValue("refCounter"));

                    this.EndJob = Convert.ToBoolean(key.GetValue("endJob"));

                    key.Close();
                }
            }
            catch (Exception ex)
            {
                
            }
        }

        public void WriteRegistry(string settingName, string settingVal)
        {
            try
            {
                //accessing the LocalMachine root element and adding "RenTradeSettings" subkey to the "SOFTWARE" subkey  
                this.key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\RenTradeSettings");

                //storing the values  
                key.SetValue(settingName, settingVal);
                key.Close();
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex.Message);
            }
        }

        public void ResetRegistry()
        {
            this.WriteRegistry("jobInfo", "");
            this.WriteRegistry("refValue", "");
            this.WriteRegistry("refStatus", "");
            this.WriteRegistry("refInitialCount", "0");
            this.WriteRegistry("refFinalCount", "0");
            this.WriteRegistry("refCounter", "0");
            this.WriteRegistry("pedalFlag", "0");
            this.WriteRegistry("isProd", "False");
            this.WriteRegistry("pedalStatus", "False");
            this.WriteRegistry("processCounter", "0");
            this.WriteRegistry("testCounter", "0");
            this.WriteRegistry("cycleCounter", "0");
            this.WriteRegistry("processStage", "A1");

            this.WriteRegistry("reelStatus", "True");
            this.WriteRegistry("ioBoardStatus", "X0");
            this.WriteRegistry("endJob", "False");
        }

        public void ErrorLogger(string message)
        {
            string errorFolder = "error";
            string subFolderPath = this.@_logPath + errorFolder + "\\";

            if (!Directory.Exists(subFolderPath))
                Directory.CreateDirectory(subFolderPath);

            this.logs = new string[] { DateTime.Now.ToString("MM/dd/yyyy hh:mm:ss tt") + " - ERROR: " + message };
            File.AppendAllLines(subFolderPath + "ErrorLogs.txt", this.logs);
        }

        public void TextLogger(string filename, string folder, string message)
        {
            string dateFolder = DateTime.Now.ToString("yyyyMMdd");
            string subFolderPath = this.@_logPath + folder + "\\" + dateFolder + "\\";

            if (!Directory.Exists(subFolderPath))
                Directory.CreateDirectory(subFolderPath);

            string[] lines = new string[] { message.ToString() };
            File.AppendAllLines(subFolderPath + "datalogs-" + filename + ".txt", lines);
        }

        public int XmlSerialFinder(string serial)
        {
            string subFolderPath = this.@_logPath + "serial\\";

            if (!Directory.Exists(subFolderPath))
                Directory.CreateDirectory(subFolderPath);

            string filePath = subFolderPath + serial + ".xml";
            int serialCount = 0;
            //return filePath + " " + File.Exists(filePath).ToString();
            if (File.Exists(filePath))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);
                serialCount = Convert.ToInt16(xmlDoc.SelectSingleNode("/Serial/Count").InnerText);
            }
            else
            {
                serialCount = -1;
            }

            return serialCount;
        }

        public void XmlSerialLogger(string serial, string type, string value)
        {
            string subFolderPath = this.@_logPath + "serial\\";

            if (!Directory.Exists(subFolderPath))
                Directory.CreateDirectory(subFolderPath);

            string filePath = subFolderPath + serial + ".xml";
            if (File.Exists(filePath))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);
                xmlDoc.SelectSingleNode("/Serial/" + type).InnerText = value;
                xmlDoc.Save(filePath);
            }
        }

        public void XmlSerialLogger(string serial, string initialCount, string finalCount, int count)
        {
            string subFolderPath = this.@_logPath + "serial\\";

            if (!Directory.Exists(subFolderPath))
                Directory.CreateDirectory(subFolderPath);

            string filePath = subFolderPath + serial + ".xml";
            if (File.Exists(filePath))
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);
                xmlDoc.SelectSingleNode("/Serial/Count").InnerText = (count).ToString();
                xmlDoc.Save(filePath);
            } 
            else
            {
                XmlTextWriter writer = new XmlTextWriter(filePath, Encoding.UTF8);
                writer.WriteStartDocument(true);
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 2;
                writer.WriteStartElement("Serial");
                writer.WriteStartElement("RefNos");
                writer.WriteString(serial);
                writer.WriteEndElement();
                writer.WriteStartElement("InitialLimit");
                writer.WriteString(initialCount);
                writer.WriteEndElement();
                writer.WriteStartElement("FinalLimit");
                writer.WriteString(finalCount);
                writer.WriteEndElement();
                writer.WriteStartElement("Count");
                writer.WriteString(count.ToString());
                writer.WriteEndElement();
                writer.WriteStartElement("Status");
                writer.WriteString("-");
                writer.WriteEndElement();
                writer.WriteStartElement("DateLog");
                writer.WriteString(DateTime.Now.ToString("MM/dd/yyyy H:mm:ss"));
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.Close();
            }
        }
    }
}
