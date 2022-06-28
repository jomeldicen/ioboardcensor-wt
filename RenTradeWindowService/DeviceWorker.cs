using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace RenTradeWindowService
{
    public class DeviceWorker : BackgroundService
    {
        private readonly ILogger<DeviceWorker> _logger;
        private RegistryDriver registry;
        private ioBoardLib.ioBoardLib oIOBoard;

        private readonly int _threadDelay;
        private readonly string _environmentMode;

        private readonly string _comPort;
        private readonly int _baudRate;
        private readonly string _parity;
        private readonly int _dataBits;
        private readonly string _stopBits;

        public DeviceWorker(ILogger<DeviceWorker> logger, IOptions<ServiceConfiguration> options)
        {
            _logger = logger;
            _threadDelay = options.Value.ThreadDelay;
            _environmentMode = options.Value.EnvironmentMode;

            // Registry init/configuration
            registry = new RegistryDriver(options);

            _comPort = options.Value.ComPort;
            _baudRate = options.Value.BaudRate;
            _parity = options.Value.Parity;
            _dataBits = options.Value.DataBits;
            _stopBits = options.Value.StopBits;
        }

        private Boolean IsDeviceAvailable()
        {
            if (oIOBoard == null)
            {
                registry.WriteRegistry("ioBoardStatus", "X1");
                return false;
            }

            if (String.IsNullOrEmpty(_comPort))
            {
                registry.WriteRegistry("ioBoardStatus", "X2");
                return false;
            }

            return true;
        }

        private Boolean InitializePort()
        {
            if (oIOBoard.ioBoard_Initialize(_comPort))
            {
                registry.WriteRegistry("ioBoardStatus", "X0");
                return true;
            }
            else
            {
                // X2 - means error detected on IO Device
                registry.WriteRegistry("ioBoardStatus", "X3");
                return false;
            }
        }

        private Boolean ClosePort()
        {
            if (oIOBoard.ioBoard_Close(_comPort))
            {
                registry.WriteRegistry("ioBoardStatus", "X0");
                return true;
            }
            else
            {
                // X2 - means error detected on IO Device
                registry.WriteRegistry("ioBoardStatus", "X4");
                return false;
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return base.StopAsync(cancellationToken);
        }

        private bool ReadInputStatus(string type, int index)
        {
            if (index > 7)
                return false;

            string sReturnPattern = "";

            if (type == "I")
                oIOBoard.ioBoard_ReadInputs(ref sReturnPattern);
            else
                oIOBoard.ioBoard_ReadOutputs(ref sReturnPattern);

            return (sReturnPattern[index] == '1')? true : false;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                registry.ReadRegistry();

                // Check Port if available
                if (_environmentMode == "PRD" && !IsDeviceAvailable())
                    goto proceed;

                // Initialize and Open the Port
                if(_environmentMode == "PRD" && !InitializePort())
                    goto proceed;

                // Input 0 - Pedal
                // Input 1 - Reel

                if (_environmentMode == "PRD" && String.IsNullOrEmpty(registry.IOBoardStatus))
                    ClosePort();

                // reel section
                if (_environmentMode == "PRD" && ReadInputStatus("I", 1))
                {
                    registry.WriteRegistry("reelStatus", "False");
                    registry.WriteRegistry("pedalStatus", "False");
                } 

                // Check Pedal Status set to true
                if (registry.PedalStatus)
                {
                    if(_environmentMode == "PRD")
                        oIOBoard.ioBoard_SetOutput(0, "ON");

                    // Read Input 0 set flag to 1
                    if (_environmentMode == "PRD" && ReadInputStatus("I", 0))
                        registry.WriteRegistry("pedalFlag", "1");

                    // 1 means hit pedal; not in pull test yet; pedal status is activated
                    if (registry.PedalFlag == 1)
                    {
                        // Read Input 0 set flag to 1
                        //if (!ReadInputStatus("I", 0))
                        //{
                            string[] process = { "B3", "C3" };
                            if (process.Contains(registry.ProcessStage))
                            {
                                registry.TestCounter++;
                                registry.WriteRegistry("testCounter", registry.TestCounter.ToString());
                            }
                            else
                            {
                                registry.ProcessCounter++;
                                registry.WriteRegistry("processCounter", registry.ProcessCounter.ToString());

                                if (registry.IsProd)
                                {
                                    registry.QuotaCounter++;
                                    registry.WriteRegistry("quotaCounter", registry.QuotaCounter.ToString());
                                }
                            }

                            registry.WriteRegistry("pedalFlag", "0");
                        //}
                    }
                }
                else
                    if (_environmentMode == "PRD")
                        oIOBoard.ioBoard_SetOutput(0, "OFF");

            proceed:
                await Task.Delay(_threadDelay, stoppingToken);
            }
        }
    }
}
