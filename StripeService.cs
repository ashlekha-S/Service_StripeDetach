using Stripe;

namespace StripeDetachWorker
{
    public class StripeService
    {
        private readonly int _threshold;
        public StripeService(string apiKey, int threshold)
        {
            StripeConfiguration.ApiKey = apiKey;
            _threshold = threshold;
        }

        public ServiceResponse DetachPaymentMethodForCustomer(DetachPaymentMethodRequest request)
        {
             try
            {
                string stripeCustomerId = request.CustomerId;
                if (string.IsNullOrEmpty(stripeCustomerId))
                {
                    return new ServiceResponse
                    {
                        IsSuccess = false,
                        ReturnMessage = "Stripe CustomerId not found in DB."
                    };
                }

                var paymentMethodService = new PaymentMethodService();
                var listOptions = new PaymentMethodListOptions
                {
                    Customer = stripeCustomerId, 
                    Type = "card",     
                    Limit = 100
                };
                StripeList<Stripe.PaymentMethod> paymentMethods = paymentMethodService.List(listOptions);

                if (paymentMethods.Data.Count > _threshold)
                {
                    foreach (var pm in paymentMethods.Data)
                    {
                        paymentMethodService.Detach(pm.Id);
                    }
                }

                return new ServiceResponse
                {
                    IsSuccess = true,
                    ReturnMessage = "Detach all"
                };
            }
            catch (Exception ex)
            {
                return new ServiceResponse
                {
                    IsSuccess = false,
                    ReturnMessage = ex.Message
                };
            }
        }
    }

    public class DetachPaymentMethodRequest
    {
        public string CustomerId { get; set; }
    }

    public class ServiceResponse
    {
        public bool IsSuccess { get; set; }
        public string ReturnMessage { get; set; }
    }
}
