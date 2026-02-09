using Microsoft.Data.SqlClient;
using System.Data;

namespace StripeDetachWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfiguration _config;
        private readonly int _intervalMinutes;
        private readonly StripeService _stripeService;

        public Worker(ILogger<Worker> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            _intervalMinutes = _config.GetValue<int>("Scheduler:IntervalMinutes");
            _stripeService = new StripeService(_config["Stripe:ApiKey"], Convert.ToInt32(_config["DetachPaymentMethodThreshold"]));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Run();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in detach job");
                }

                _logger.LogInformation("Next run in {minutes} minutes...", _intervalMinutes);
                await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
            }
        }

        public void Run()
        {
            var customerIds = GetAllCustomerIdsFromDb();

            foreach (var customerId in customerIds)
            {
                var request = new DetachPaymentMethodRequest
                {
                    CustomerId = customerId
                };

                var response = _stripeService.DetachPaymentMethodForCustomer(request);
                LogResult(customerId, response);
            }
        }

        private List<string> GetAllCustomerIdsFromDb()
        {
            var customerIds = new List<string>();
            var connString = _config.GetConnectionString("DefaultConnection");

            using (SqlConnection conn = new SqlConnection(connString))
            using (SqlCommand cmd = new SqlCommand("SK_POS_Get_All_Guest_CustomerId", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                conn.Open();

                using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                {
                    DataSet ds = new DataSet();
                    da.Fill(ds);

                    if (ds != null && ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                    {
                        foreach (DataRow row in ds.Tables[0].Rows)
                        {
                            var customerId = Convert.ToString(row["CustomerId"]);
                            if (!string.IsNullOrWhiteSpace(customerId))
                            {
                                customerIds.Add(customerId);
                            }
                        }
                    }
                }
            }
            return customerIds;
        }

        private void LogResult(string customerId, ServiceResponse response)
        {
            _logger.LogInformation("[{time}] Customer {customerId} => {msg}",
                DateTime.Now, customerId, response.ReturnMessage);
        }
    }
}
