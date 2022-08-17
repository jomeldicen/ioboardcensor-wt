using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Experimental.System.Messaging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Serilog;
using Serilog.Events;

namespace RenTradeWindowService
{
    public class MainWorker : BackgroundService
    {
        private LearCebuPAO_API.LearCebuPAO_API OLearCebuPAOapi = new LearCebuPAO_API.LearCebuPAO_API();
        private readonly ILogger<MainWorker> _logger;
        private readonly IOptions<ServiceConfiguration> _options;
        private ioBoardLib.ioBoardLib oIOBoard = new ioBoardLib.ioBoardLib();
        private RegistryDriver registry;
        private LearJob learjob;

        private MessageQueue queue;

        private readonly string _machineName;
        private readonly string _environmentMode;
        private readonly string _defaultMsgQueName;
        private readonly string _logPath;
        private readonly string _dashboardFormPath;
        private readonly int _threadDelay;
        private readonly int _mgQueueWaitingTime;

        private readonly int _firstPcsInitCount;        // test pcs
        private readonly int _midPcsInitCount;          // quota pcs
        private readonly int _lastPcsInitCount;         // prod pcs
        private readonly int _quotaInitCount;

        private readonly string _comPort;

        private string Mode;

        public MainWorker(ILogger<MainWorker> logger, IOptions<ServiceConfiguration> options)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(@_logPath + "LogFile.txt")
                .CreateLogger();

            _logger = logger;

            _options = options;

            // Registry init/configuration
            registry = new RegistryDriver(options);

            _environmentMode = _options.Value.EnvironmentMode;
            _machineName = String.IsNullOrEmpty(_options.Value.MachineName)? Environment.MachineName : _options.Value.MachineName;
            _defaultMsgQueName = _options.Value.MessageQueueName;
            _logPath = _options.Value.LogPath;
            _dashboardFormPath = _options.Value.DashboardFormPath;
            _threadDelay = _options.Value.ThreadDelay;
            _mgQueueWaitingTime = _options.Value.MsgQueueWaitingTime;

            _firstPcsInitCount = _options.Value.FirstPcsInitCount;
            _midPcsInitCount = _options.Value.MidPcsInitCount;
            _lastPcsInitCount = _options.Value.LastPcsInitCount;
            _quotaInitCount = _options.Value.QuotaInitCount;

            _comPort = options.Value.ComPort;

            // create queue name if not existed
            if (!MessageQueue.Exists(this._defaultMsgQueName)) MessageQueue.Create(this._defaultMsgQueName);

            this.queue = new MessageQueue(this._defaultMsgQueName);
        }

        private void InitializePort()
        {
            if (oIOBoard == null)
            {
                registry.WriteRegistry("ioBoardStatus", "X1");
            }

            if (String.IsNullOrEmpty(_comPort))
            {
                registry.WriteRegistry("ioBoardStatus", "X2");
            }
        }

        private bool ReadIOStatus(string type, int index)
        {
            if (index > 7)
                return false;

            string sReturnPattern = "";

        reconnect:
            if (type == "I")
                oIOBoard.ioBoard_ReadInputs(ref sReturnPattern);
            else
                oIOBoard.ioBoard_ReadOutputs(ref sReturnPattern);

            if(sReturnPattern == "SerialPortIsClosed" || sReturnPattern == "")
            {
                registry.WriteRegistry("ioBoardStatus", "X4");
                if (oIOBoard.ioBoard_Initialize(_comPort))
                {
                    registry.WriteRegistry("ioBoardStatus", "X0");
                    goto reconnect;
                }
                goto reconnect;
            } 

            return (sReturnPattern[(sReturnPattern.Length - 1) - index] == '1') ? true : false;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {   
            // Initial IO Board Port
            if (oIOBoard.ioBoard_Initialize(_comPort))
                registry.WriteRegistry("ioBoardStatus", "X0");
            else
                registry.WriteRegistry("ioBoardStatus", "X3");           

            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (Process clsProcess in Process.GetProcesses())
            {
                //now we're going to see if any of the running processes
                //match the currently running processes. Be sure to not add the .exe to the name you provide, 
                //Remember, if you have the process running more than once,  say IE open 4 times the loop thr way it is now will close all 4,
                //if you want it to just close the first one it finds then add a return; after the Kill
                if (clsProcess.ProcessName.Contains("RenTradeWindowForm"))
                    clsProcess.Kill();
            }

            // Close IO Board Port
            if (oIOBoard.ioBoard_Close(_comPort))
                registry.WriteRegistry("ioBoardStatus", "X4");
            else
                registry.WriteRegistry("ioBoardStatus", "X3");


            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                String JobInfo = OLearCebuPAOapi.GetJobStarted();
                _logger.LogInformation(JobInfo.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                // Read Current Registry Config
                registry.ReadRegistry();

                // Make it sure that Dashboard form is always open
                this.OpenForm();

                // Device Execution
                if (!this.DeviceExecution()) goto proceed;

                Mode = "X0";
                // X0 Determine that Device is working
                if (registry.IOBoardStatus == Mode)
                {
                    // Get Lear Job Information
                    learjob = new LearJob(_options);

                    if (!string.IsNullOrEmpty(learjob.OrderNumber))
                    {
                        // Force Terminate Job 
                        if (registry.ProcessStage == "F2")
                            learjob.JobFinished();

                        // Purge All Message queue prior to caliper and pull test
                        string[] stageArray = { "A4B", "A5B", "B4B", "B5B", "C4B", "C5B" };
                        if (!stageArray.Contains(registry.ProcessStage))
                        {
                            MessageQueue queue = new MessageQueue(this._defaultMsgQueName);
                            queue.Purge(); //deletes the entire queue
                            queue.Dispose();
                            queue.Close();
                            queue.Refresh();
                        }

                        // in Test Mode
                        if (!registry.IsProd)
                        {
                            // check if pedal hit counter exceeds the default initial value
                            if (registry.ProcessCounter >= this._firstPcsInitCount)
                            {
                                registry.WriteRegistry("pedalStatus", "False");

                                // confirmation message if 1st pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter >= this._firstPcsInitCount && registry.ProcessStage == "A1")
                                {
                                    registry.WriteRegistry("processStage", "A2");
                                    registry.WriteRegistry("processCounter", this._firstPcsInitCount.ToString());

                                    goto proceed;
                                }

                                // confirmation message if first pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter == this._firstPcsInitCount && (registry.ProcessStage == "A4B" || registry.ProcessStage == "A5B"))
                                {
                                    // check if pull test counter is equal to first pcs count
                                firstpcsLabelCounter:
                                    if (registry.TestCounter <= _firstPcsInitCount)
                                    {
                                        // for caliper input
                                        if (registry.TestCounter == _firstPcsInitCount && registry.ProcessStage == "A4B")
                                        {
                                            registry.WriteRegistry("testCounter", "0");
                                            registry.WriteRegistry("processStage", "A5A");

                                            goto proceed;
                                        }

                                        // for pull test input
                                        if (registry.TestCounter == _firstPcsInitCount && registry.ProcessStage == "A5B")
                                        {
                                            registry.WriteRegistry("processStage", "A8");

                                            goto proceed;
                                        }

                                        // read message queue
                                        this.MessageQueuing();
                                    }
                                    else
                                    {
                                        registry.WriteRegistry("testCounter", _firstPcsInitCount.ToString());
                                        registry.ReadRegistry();
                                        goto firstpcsLabelCounter;
                                    }

                                    goto proceed;
                                }

                                goto proceed;
                            }
                        }
                        else // in Production Mode
                        {
                            // Finished Job Production
                            if(registry.ProcessStage == "F1")
                                learjob.JobFinished();

                            //*********************** Start Daily Quota Section ***********************//
                            // confirmation message if mid pcs counter is greater to the nos of initial config.
                            if (registry.QuotaCounter > _quotaInitCount && (registry.ProcessStage == "B1" || registry.ProcessStage == "C2"))
                            {
                                registry.WriteRegistry("quotaCounter", _quotaInitCount.ToString());
                                if (registry.ProcessStage == "B1")
                                {
                                    registry.WriteRegistry("processStage", "C2");
                                    registry.WriteRegistry("pedalStatus", "False");
                                    goto proceed;
                                }

                                registry.ReadRegistry();
                            }

                            // confirmation message if mid pcs counter is equal to the nos of initial config.
                            if (registry.QuotaCounter == _quotaInitCount && registry.ProcessStage == "B1")
                            {
                                registry.WriteRegistry("processStage", "C2");
                                registry.WriteRegistry("pedalStatus", "False");

                                goto proceed;
                            }

                            // execute additional pcs
                            if (registry.TestCounter >= _midPcsInitCount && registry.ProcessStage == "C3")
                            {
                                registry.WriteRegistry("processStage", "C3B");
                                registry.WriteRegistry("pedalStatus", "False");

                                goto proceed;
                            }

                            // confirmation message if last pcs counter is equal to the nos of initial config.
                            if (registry.QuotaCounter == _quotaInitCount && (registry.ProcessStage == "C4B" || registry.ProcessStage == "C5B"))
                            {
                            // check if pull test counter is equal to mid pcs count
                            midpcsLabelCounter:
                                if (registry.TestCounter <= _midPcsInitCount)
                                {
                                    // for caliper input
                                    if (registry.TestCounter == _midPcsInitCount && registry.ProcessStage == "C4B")
                                    {
                                        registry.WriteRegistry("testCounter", "0");
                                        registry.WriteRegistry("processStage", "C5A");

                                        goto proceed;
                                    }

                                    // for pull test input
                                    if (registry.TestCounter == _midPcsInitCount && registry.ProcessStage == "C5B")
                                    {
                                        registry.WriteRegistry("processStage", "C8");

                                        goto proceed;
                                    }

                                    // read message queue
                                    this.MessageQueuing();
                                }
                                else
                                {
                                    registry.WriteRegistry("testCounter", _midPcsInitCount.ToString());
                                    registry.ReadRegistry();
                                    goto midpcsLabelCounter;
                                }

                                goto proceed;
                            }
                            //*********************** End Daily Quota Section ***********************//

                            // check if pedal hit counter exceeds the production quantity
                            if (registry.ProcessCounter >= learjob.BundleSize)
                            {                               

                                //*********************** Start Production Section ***********************//
                                // confirmation message if last pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter >= learjob.BundleSize && registry.ProcessStage == "B1")
                                {
                                    registry.WriteRegistry("processStage", "B2");
                                    registry.WriteRegistry("pedalStatus", "False");
                                    registry.WriteRegistry("processCounter", learjob.BundleSize.ToString());

                                    goto proceed;
                                }

                                // execute additional pcs
                                if (registry.TestCounter >= _lastPcsInitCount && registry.ProcessStage == "B3")
                                {
                                    registry.WriteRegistry("processStage", "B3B");
                                    registry.WriteRegistry("pedalStatus", "False");

                                    goto proceed;
                                }

                                // confirmation message if last pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter == learjob.BundleSize && (registry.ProcessStage == "B4B" || registry.ProcessStage == "B5B"))
                                {
                                    // check if pull test counter is equal to last pcs count
                                    lastpcsLabelCounter:
                                    if (registry.TestCounter <= _lastPcsInitCount)
                                    {
                                        // for caliper input
                                        if (registry.TestCounter == _lastPcsInitCount && registry.ProcessStage == "B4B")
                                        {
                                            registry.WriteRegistry("testCounter", "0");
                                            registry.WriteRegistry("processStage", "B5A");

                                            goto proceed;
                                        }

                                        // for pull test input
                                        if (registry.TestCounter == _lastPcsInitCount && registry.ProcessStage == "B5B")
                                        {
                                            registry.WriteRegistry("processStage", "B8");

                                            goto proceed;
                                        }

                                        // read message queue
                                        this.MessageQueuing();
                                    }
                                    else
                                    {
                                        registry.WriteRegistry("testCounter", _lastPcsInitCount.ToString());
                                        registry.ReadRegistry();
                                        goto lastpcsLabelCounter;
                                    }

                                    goto proceed;
                                }
                                //*********************** End Production Section ***********************//
                            }
                        }
                    }
                } 
                else
                {
                    switch (registry.IOBoardStatus)
                    {
                        case "X1":
                            _logger.LogError("Device is not properly set");
                            break;
                        case "X2":
                            _logger.LogError("Com Port is not properly set");
                            break;
                        case "X3":
                            _logger.LogError("IO Device error upon Initialize");
                            break;
                        case "X4":
                            _logger.LogError("IO Device is currently closed");
                            break;
                        case "X5":
                            _logger.LogError("IO Device error upon Closing");
                            break;
                        default:
                            break;
                    }
                }

            proceed:
                await Task.Delay(_threadDelay, stoppingToken);
            }
        }

        #region Message Queue Function
        private void MessageQueuing()
        {
            try
            {
                //create queue name if not existed
                if (!MessageQueue.Exists(this._defaultMsgQueName)) MessageQueue.Create(this._defaultMsgQueName);

                using (this.queue = new MessageQueue(this._defaultMsgQueName))
                {
                    this.queue.Formatter = new XmlMessageFormatter(new Type[] { typeof(String) });

                    // Add an event handler for the ReceiveCompleted event.
                    this.queue.ReceiveCompleted += new ReceiveCompletedEventHandler(this.MyReceiveCompleted);

                    // Define wait handles for multiple operations.
                    WaitHandle[] waitHandleArray = new WaitHandle[1];

                    // Begin asynchronous operations.
                    waitHandleArray[0] = queue.BeginReceive(TimeSpan.FromSeconds(10.0)).AsyncWaitHandle;

                    var index = WaitHandle.WaitAny(waitHandleArray, _mgQueueWaitingTime);
                    if (index == 258) // Timeout
                    {
                        if(!registry.IsProd)
                            registry.WriteRegistry("processStage", "A5C");
                        else
                        {
                            if(registry.ProcessStage == "B5B")
                                registry.WriteRegistry("processStage", "B5C");
                            else if (registry.ProcessStage == "C5B")
                                registry.WriteRegistry("processStage", "C5C");
                        }
                    }

                    // Specify to wait for all operations to return.
                    WaitHandle.WaitAll(waitHandleArray);

                    queue.Dispose();
                    queue.Close();
                    queue.Refresh();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.StackTrace);
                _logger.LogError("Message Queue Exception 1: " + ex.Message);
            }
        }

        private void MyReceiveCompleted(Object source, ReceiveCompletedEventArgs asyncResult)
        {
            // Connect to the queue.
            MessageQueue mq = (MessageQueue)source;
            try
            {
                // End the asynchronous Receive operation.
                using (Message m = mq.EndReceive(asyncResult.AsyncResult))
                {
                    // split string code by delimeter
                    string label = (string)m.Label;

                    string[] stringSeparators = new string[] { ":::" };
                    string[] tokens = label.Split(stringSeparators, StringSplitOptions.None);

                    bool isLeadSetExist = true;
                    // Production Mode
                    if (this._environmentMode == "PRD")
                    {
                        isLeadSetExist = false;

                        // expect caliper input
                        string[] stageArray1 = { "A4B", "B4B", "C4B" };
                        if (stageArray1.Contains(registry.ProcessStage))
                        {
                            if (tokens[0].ToString().Contains("H=") && tokens[1].ToString().Contains("W="))
                                isLeadSetExist = (learjob.LeadSet == tokens[2].ToString() && learjob.OrderNumber == tokens[5].ToString()) ? true : false;
                        }

                        // expect pull test input
                        string[] stageArray2 = { "A5B", "B5B", "C5B" };
                        if (stageArray1.Contains(registry.ProcessStage))
                        {
                            if (!tokens[0].ToString().Contains("H=") && !tokens[1].ToString().Contains("W="))
                                isLeadSetExist = (learjob.LeadSet == tokens[2].ToString() && learjob.OrderNumber == tokens[5].ToString()) ? true : false;
                        }
                    }

                    if (isLeadSetExist)
                    {
                        // log message queue results
                        registry.TextLogger(learjob.OrderNumber, DateTimeOffset.Now + " - [" + label + "]");

                        registry.TestCounter++;
                        registry.WriteRegistry("testCounter", registry.TestCounter.ToString());
                    }
                }
            }
            catch (Exception ex)
            {

            }
            finally
            {
                // Dispose the asynchronous Receive operation.
                mq.Dispose();
            }

            return;
        }
        #endregion

        private void OpenForm()
        {
            bool isProcessExist = false;
            //here we're going to get a list of all running processes on
            //the computer
            foreach (Process clsProcess in Process.GetProcesses())
            {
                //now we're going to see if any of the running processes
                //match the currently running processes. Be sure to not add the .exe to the name you provide, 
                //Remember, if you have the process running more than once,  say IE open 4 times the loop thr way it is now will close all 4,
                //if you want it to just close the first one it finds then add a return; after the Kill
                if (clsProcess.ProcessName.Contains("RenTradeWindowForm"))
                    isProcessExist = true;
            }

            if (!isProcessExist)
            {
                //ProcessStartInfo info = new ProcessStartInfo(_dashboardFormPath);
                //info.CreateNoWindow = true;
                //info.UseShellExecute = false;
                //Process.Start(info);
                ProcessAsUser.Launch(_dashboardFormPath);
            }
        }


        private bool DeviceExecution()
        {
            if(registry.IOBoardStatus == "R0")
            {
                if (oIOBoard.ioBoard_Close(_comPort))
                    registry.WriteRegistry("ioBoardStatus", "X4");
                else
                    registry.WriteRegistry("ioBoardStatus", "X3");

                if (oIOBoard.ioBoard_Initialize(_comPort))
                    registry.WriteRegistry("ioBoardStatus", "X0");
                else
                    registry.WriteRegistry("ioBoardStatus", "X3");
            }

            //// Input 0 - Pedal Indicator
            //// Input 1 - Reel Indicator

            if ( !registry.ReelStatus)
                oIOBoard.ioBoard_SetOutput(0, "ON");

            // reel section
            if (!ReadIOStatus("I", 1))
            {
                registry.WriteRegistry("reelStatus", "False");
                registry.WriteRegistry("pedalStatus", "False");
            }

            // Check Pedal Status set to true
            if (registry.PedalStatus)
            {
                oIOBoard.ioBoard_SetOutput(0, "ON");

                // Read Input 0 set flag to 1
                if (ReadIOStatus("I", 0))
                {
                    registry.WriteRegistry("pedalFlag", "1");
                    registry.ReadRegistry();
                }

                // 1 means hit pedal; not in pull test yet; pedal status is activated
                if (registry.PedalFlag == 1)
                {
                    // Get Lear Job Information
                    learjob = new LearJob(_options);

                    // Read Input 0 set flag to 1
                    if (!ReadIOStatus("I", 0))
                    {
                        string[] process = { "B3", "C3" };
                        if (process.Contains(registry.ProcessStage))
                        {
                            // validate if test counter is equal to last pcs then exit counter
                            if (registry.ProcessCounter == learjob.BundleSize && registry.TestCounter == _lastPcsInitCount && registry.ProcessStage == "B3") return false;

                            // validate if test counter is equal to last pcs then exit counter
                            if (registry.ProcessCounter == learjob.BundleSize && registry.TestCounter == _midPcsInitCount && registry.ProcessStage == "C3") return false;

                            registry.TestCounter++;
                            registry.WriteRegistry("testCounter", registry.TestCounter.ToString());
                        } else
                        {
                            if (registry.IsProd)
                            {
                                // confirmation message if last pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter == learjob.BundleSize && (registry.ProcessStage == "B1" || registry.ProcessStage == "B2")) return false;
                                else
                                {
                                    registry.ProcessCounter++;
                                    registry.WriteRegistry("processCounter", registry.ProcessCounter.ToString());

                                    registry.QuotaCounter++;
                                    registry.WriteRegistry("quotaCounter", registry.QuotaCounter.ToString());
                                }
                            }
                            else
                            {
                                // confirmation message if 1st pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter == this._firstPcsInitCount && registry.ProcessStage == "A1") return false;
                                else
                                {
                                    registry.ProcessCounter++;
                                    registry.WriteRegistry("processCounter", registry.ProcessCounter.ToString());
                                }
                            }
                        }    

                        registry.WriteRegistry("pedalFlag", "0");
                        registry.ReadRegistry();
                    }
                }
            }
            else
                oIOBoard.ioBoard_SetOutput(0, "OFF");

            return true;
        }
    }



}
