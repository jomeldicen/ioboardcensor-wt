using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DemoWorker
{
    public class Worker : BackgroundService
    {
        private LearCebuPAO_API.LearCebuPAO_API OLearCebuPAOapi = new LearCebuPAO_API.LearCebuPAO_API();
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    String JobInfo = OLearCebuPAOapi.GetJobStarted("GAB-20-5482");
                    _logger.LogInformation("Job Information: {x}", JobInfo);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Job Error: {x}", ex.Message);
                }

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
