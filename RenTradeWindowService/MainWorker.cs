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

namespace RenTradeWindowService
{
    public class MainWorker : BackgroundService
    {
        private readonly ILogger<MainWorker> _logger;
        private readonly IOptions<ServiceConfiguration> _options;
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

        private string Mode;

        public MainWorker(ILogger<MainWorker> logger, IOptions<ServiceConfiguration> options)
        {
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

            // create queue name if not existed
            if (!MessageQueue.Exists(this._defaultMsgQueName)) MessageQueue.Create(this._defaultMsgQueName);

            this.queue = new MessageQueue(this._defaultMsgQueName);
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Read Current Registry Config
                registry.ReadRegistry();

                // Make it sure that Dashboard form is always open
                OpenForm();

                Mode = "";
                if (_environmentMode == "PRD")
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

                        // in Test Mode
                        if (!registry.IsProd)
                        {
                            // check if pedal hit counter exceeds the default initial value
                            if (registry.ProcessCounter >= this._firstPcsInitCount)
                            {
                                registry.WriteRegistry("pedalStatus", "False");

                                // confirmation message if 1st pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter == this._firstPcsInitCount && registry.ProcessStage == "A1")
                                {
                                    registry.WriteRegistry("processStage", "A2");

                                    goto proceed;
                                }

                                // confirmation message if first pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter == this._firstPcsInitCount && registry.ProcessStage == "A6")
                                {
                                    // check if pull test counter is equal to first pcs count
                                    if (registry.TestCounter <= _firstPcsInitCount)
                                    {
                                        if(registry.TestCounter == _firstPcsInitCount)
                                        {
                                            //registry.WriteRegistry("testCounter", "0");
                                            //registry.WriteRegistry("processCounter", "0");
                                            //registry.WriteRegistry("isProd", "True");
                                            //registry.WriteRegistry("pedalStatus", "True");
                                            //registry.WriteRegistry("processStage", "B1");
                                            registry.WriteRegistry("processStage", "A8");

                                            goto proceed;
                                        }

                                        // read message queue
                                        this.MessageQueuing();
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

                            // check if pedal hit counter exceeds the production quantity
                            if (registry.ProcessCounter >= learjob.BundleSize)
                            {
                                //*********************** Start Daily Quota Section ***********************//
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
                                    registry.WriteRegistry("testCounter", "0");
                                    registry.WriteRegistry("processStage", "C4");
                                    registry.WriteRegistry("pedalStatus", "False");

                                    goto proceed;
                                }

                                // confirmation message if last pcs counter is equal to the nos of initial config.
                                if (registry.QuotaCounter == _quotaInitCount && registry.ProcessStage == "C6")
                                {
                                    // check if pull test counter is equal to last pcs count
                                    if (registry.TestCounter <= _midPcsInitCount)
                                    {
                                        if (registry.TestCounter == _midPcsInitCount)
                                        {
                                            registry.WriteRegistry("processStage", "C8");

                                            goto proceed;
                                        }

                                        // read message queue
                                        this.MessageQueuing();
                                    }

                                    goto proceed;
                                }
                                //*********************** End Daily Quota Section ***********************//

                                //*********************** Start Production Section ***********************//
                                // confirmation message if last pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter == learjob.BundleSize && registry.ProcessStage == "B1")
                                {
                                    registry.WriteRegistry("processStage", "B2");
                                    registry.WriteRegistry("pedalStatus", "False");

                                    goto proceed;
                                }

                                // execute additional pcs
                                if (registry.TestCounter >= _lastPcsInitCount && registry.ProcessStage == "B3")
                                {
                                    registry.WriteRegistry("testCounter", "0");
                                    registry.WriteRegistry("processStage", "B4");
                                    registry.WriteRegistry("pedalStatus", "False");

                                    goto proceed;
                                }

                                // confirmation message if last pcs counter is equal to the nos of initial config.
                                if (registry.ProcessCounter == learjob.BundleSize && registry.ProcessStage == "B6")
                                {
                                    // check if pull test counter is equal to last pcs count
                                    if (registry.TestCounter <= _lastPcsInitCount)
                                    {
                                        if (registry.TestCounter == _lastPcsInitCount)
                                        {
                                            registry.WriteRegistry("processStage", "B8");

                                            goto proceed;
                                        }

                                        // read message queue
                                        this.MessageQueuing();
                                    }

                                    goto proceed;
                                }
                                //*********************** End Production Section ***********************//
                            }
                        }
                    }

                    _logger.LogInformation("Worker start running at Job Order: {0}", learjob.OrderNumber);
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
                            registry.WriteRegistry("processStage", "A7");
                        else
                        {
                            if(registry.ProcessStage == "B6")
                                registry.WriteRegistry("processStage", "B7");
                            else if (registry.ProcessStage == "C6")
                                registry.WriteRegistry("processStage", "C7");
                        }

                    }

                    // Specify to wait for all operations to return.
                    WaitHandle.WaitAll(waitHandleArray);
                }
            }
            catch (Exception ex)
            {
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
                        isLeadSetExist = (learjob.LeadSet == tokens[2].ToString() && learjob.OrderNumber == tokens[5].ToString()) ? true : false;

                    if (isLeadSetExist)
                    {
                        // log message queue results
                        registry.TextLogger(DateTimeOffset.Now + " - [" + label + "]");

                        registry.TestCounter++;
                        registry.WriteRegistry("testCounter", registry.TestCounter.ToString());
                    }
                }
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
                if (clsProcess.ProcessName.Contains("RenTradeUserInterface"))
                    isProcessExist = true;
            }

            if (!isProcessExist)
                Process.Start(_dashboardFormPath);
        }
    }
}
