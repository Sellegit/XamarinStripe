/*
 * Copyright 2011 - 2012 Xamarin, Inc., 2011 - 2012 Joe Dluzen
 *
 * Author(s):
 *  Gonzalo Paniagua Javier (gonzalo@xamarin.com)
 *  Joe Dluzen (jdluzen@gmail.com)
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Xamarin.Payments.Stripe {
    public class StripePayment {
        static readonly string api_endpoint = "https://api.stripe.com/v1";
        static readonly string subscription_path = "{0}/customers/{1}/subscription";
        static readonly string user_agent = "Stripe .NET v1";

        static readonly Encoding encoding = Encoding.UTF8;
        ICredentials credential;
        string apiKey;

        public StripePayment (string api_key)
        {
            credential = new NetworkCredential (api_key, "");
            apiKey = api_key;
            TimeoutSeconds = 80;
        }
        public static string Base64Encode(string plainText) {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        #region Shared
        protected virtual Task<HttpResponseMessage> SetupRequest (HttpMethod method, string url, string body)
        {
            var requestMessage = new HttpRequestMessage (method, url);
            requestMessage.Headers.Add ("User-Agent", user_agent);
            var concat = apiKey + ":";
            Debug.WriteLine (concat);
            Debug.WriteLine (Base64Encode (concat));
            requestMessage.Headers.Add ("Authorization", "Basic " + Base64Encode (concat));
//            if (method == HttpMethod.Post) {
//                requestMessage.Headers.Add ("Content-Type", "application/x-www-form-urlencoded");
//            }
            Debug.WriteLine ("content: " + body);
            if (body != null) {
                requestMessage.Content = new StringContent (body);
                if (method == HttpMethod.Post) {
                    requestMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue ("application/x-www-form-urlencoded");
                }
            }


           
            //var handler = new HttpClientHandler ();
            //handler.Credentials = credential;
            //handler.PreAuthenticate = true;

            var client = new HttpClient ();
            client.Timeout = TimeSpan.FromSeconds (TimeoutSeconds);

            return client.SendAsync (requestMessage);
            //requestMessage.Content = new HttpContent
            //client.SendAsync(
            //return new HttpClient(handler)
            //HttpclientFa
            //httpclientfa

            //return req;
        }

        static string GetResponseAsString (WebResponse response)
        {
            using (StreamReader sr = new StreamReader (response.GetResponseStream (), encoding)) {
                return sr.ReadToEnd ();
            }
        }

        protected virtual async Task<T> DoRequest<T> (string endpoint)
        {
            return await DoRequest<T> (endpoint, HttpMethod.Get, null);
        }

        protected virtual async Task<T> DoRequest<T> (string endpoint, HttpMethod method, string body)
        {
            var json = await DoRequest (endpoint, method, body);
            return JsonConvert.DeserializeObject<T> (json);
        }

        protected virtual async Task<string> DoRequest (string endpoint)
        {
            return await DoRequest (endpoint, HttpMethod.Get, null);
        }

        protected virtual async Task<string> DoRequest (string endpoint, HttpMethod method, string body)
        {


            //string result = null;
            var req = SetupRequest (method, endpoint, body);
            var responseMessage = await req;
            var result = await responseMessage.Content.ReadAsStringAsync ();
            if (responseMessage.IsSuccessStatusCode) {
                return result;
            } else {
                throw StripeException.GetFromJSON(responseMessage.StatusCode, result);
            }
            /*    } catch (HttpRequestException ex) {

            }
            //req.ContinueWith(x => x.Result.
//            WebRequest req = SetupRequest (method, endpoint, body);
//            if (body != null) {
//                byte [] bytes = encoding.GetBytes (body.ToString ());
//                req.ContentLength = bytes.Length;
//                using (Stream st = req.GetRequestStream ()) {
//                    st.Write (bytes, 0, bytes.Length);
//                }
//            }
            var response = await req;
            //response.
            try {
                using (WebResponse resp = (WebResponse) req.EndGetResponse()) {
                    result = GetResponseAsString (resp);
                }
            } catch (WebException wexc) {
                if (wexc.Response != null) {
                    string json_error = GetResponseAsString (wexc.Response);
                    HttpStatusCode status_code = HttpStatusCode.BadRequest;
                    HttpWebResponse resp = wexc.Response as HttpWebResponse;
                    if (resp != null)
                        status_code = resp.StatusCode;

                    if ((int) status_code <= 500)
                        throw StripeException.GetFromJSON (status_code, json_error);
                }
                throw;
            }
            return result;*/
        }

        protected virtual StringBuilder UrlEncode (IUrlEncoderInfo infoInstance)
        {
            StringBuilder str = new StringBuilder ();
            infoInstance.UrlEncode (str);
            if (str.Length > 0)
                str.Length--;
            return str;
        }

        #endregion
        #region Charge
        public async Task<StripeCharge> Charge (int amount_cents, string currency, string customer, string description)
        {
            if (String.IsNullOrEmpty (customer))
                throw new ArgumentNullException ("customer");

            return await Charge (amount_cents, currency, customer, null, description);
        }

        public async Task<StripeCharge> Charge (int amount_cents, string currency, StripeCreditCardInfo card, string description)
        {
            if (card == null)
                throw new ArgumentNullException ("card");

            return await Charge (amount_cents, currency, null, card, description);
        }

        async Task<StripeCharge> Charge (int amount_cents, string currency, string customer, StripeCreditCardInfo card, string description)
        {
            if (amount_cents < 0)
                throw new ArgumentOutOfRangeException ("amount_cents", "Must be greater than or equal 0");
            if (String.IsNullOrEmpty (currency))
                throw new ArgumentNullException ("currency");
            if (currency != "usd")
                throw new ArgumentException ("The only supported currency is 'usd'");

            StringBuilder str = new StringBuilder ();
            str.AppendFormat ("amount={0}&", amount_cents);
            str.AppendFormat ("currency={0}&", currency);
            if (!String.IsNullOrEmpty (description)) {
                str.AppendFormat ("description={0}&", HttpUtility.UrlEncode (description));
            }

            if (card != null) {
                card.UrlEncode (str);
            } else {
                // customer is non-empty
                str.AppendFormat ("customer={0}&", HttpUtility.UrlEncode (customer));
            }
            str.Length--;
            string ep = String.Format ("{0}/charges", api_endpoint);
            return await DoRequest<StripeCharge> (ep, HttpMethod.Post, str.ToString ());
        }

        public async Task<StripeCharge> GetCharge (string charge_id)
        {
            if (String.IsNullOrEmpty (charge_id))
                throw new ArgumentNullException ("charge_id");

            string ep = String.Format ("{0}/charges/{1}", api_endpoint, HttpUtility.UrlEncode (charge_id));
            return await DoRequest<StripeCharge> (ep);
        }

        public async Task<StripeCollection<StripeCharge>> GetCharges (int offset = 0, int count = 10, string customer_id = null, StripeDateTimeInfo created = null)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException ("offset");
            if (count < 1 || count > 100)
                throw new ArgumentOutOfRangeException ("count");

            StringBuilder str = new StringBuilder ();
            str.AppendFormat ("offset={0}&", offset);
            str.AppendFormat ("count={0}&", count);
            if (!String.IsNullOrEmpty (customer_id))
                str.AppendFormat ("customer={0}&", HttpUtility.UrlEncode (customer_id));

            if (created != null) {
                created.Prefix = "created";
                created.UrlEncode (str);
            }

            str.Length--;
            string ep = String.Format ("{0}/charges?{1}", api_endpoint, str);
            return await DoRequest<StripeCollection<StripeCharge>> (ep);
        }

        public async Task<StripeDispute> UpdateDispute (string charge_id, string evidence)
        {
            if (String.IsNullOrEmpty (charge_id))
                throw new ArgumentNullException ("charge_id");

            if (String.IsNullOrEmpty (evidence))
                throw new ArgumentNullException ("evidence");

            string ep = String.Format ("{0}/charges/{1}/dispute", api_endpoint, HttpUtility.UrlEncode (charge_id));
            return await DoRequest<StripeDispute> (ep, HttpMethod.Post, String.Format ("evidence={0}", HttpUtility.UrlEncode (evidence)));
        }

        #endregion
        #region Refund
        public async Task<StripeCharge> Refund (string charge_id)
        {
            if (String.IsNullOrEmpty (charge_id))
                throw new ArgumentNullException ("charge_id");

            string ep = String.Format ("{0}/charges/{1}/refund", api_endpoint, HttpUtility.UrlEncode (charge_id));
            return await DoRequest<StripeCharge> (ep, HttpMethod.Post, null);
        }

        public async Task<StripeCharge> Refund (string charge_id, int amount)
        {
            if (String.IsNullOrEmpty (charge_id))
                throw new ArgumentNullException ("charge_id");
            if (amount <= 0)
                throw new ArgumentException ("Amount must be greater than zero.", "amount");

            string ep = String.Format ("{0}/charges/{1}/refund?amount={2}", api_endpoint, HttpUtility.UrlEncode (charge_id), amount);
            return await DoRequest<StripeCharge> (ep, HttpMethod.Post, null);
        }
        #endregion
        #region Customer
        async Task<StripeCustomer> CreateOrUpdateCustomer (string id, StripeCustomerInfo customer)
        {
            StringBuilder str = UrlEncode (customer);

            string format = "{0}/customers"; // Create
            if (id != null)
                format = "{0}/customers/{1}"; // Update
            string ep = String.Format (format, api_endpoint, HttpUtility.UrlEncode (id));
            return await DoRequest<StripeCustomer> (ep, HttpMethod.Post, str.ToString ());
        }

        public async Task<StripeCustomer> CreateCustomer (StripeCustomerInfo customer)
        {
            if (customer == null)
                throw new ArgumentNullException ("customer");

            return await CreateOrUpdateCustomer (null, customer);
        }

        public async Task<StripeCustomer> UpdateCustomer (string id, StripeCustomerInfo customer)
        {
            if (String.IsNullOrEmpty (id))
                throw new ArgumentNullException ("id");
            if (customer == null)
                throw new ArgumentNullException ("customer");

            return await CreateOrUpdateCustomer (id, customer);
        }

        public async Task<StripeCustomer> GetCustomer (string customer_id)
        {
            if (String.IsNullOrEmpty (customer_id))
                throw new ArgumentNullException ("customer_id");

            string ep = String.Format ("{0}/customers/{1}", api_endpoint, HttpUtility.UrlEncode (customer_id));
            return await DoRequest<StripeCustomer> (ep);
        }

        public async Task<StripeCollection<StripeCustomer>> GetCustomers (int offset = 0, int count = 10)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException ("offset");
            if (count < 1 || count > 100)
                throw new ArgumentOutOfRangeException ("count");

            string str = String.Format ("offset={0}&count={1}", offset, count);
            string ep = String.Format ("{0}/customers?{1}", api_endpoint, str);
            return await DoRequest<StripeCollection<StripeCustomer>> (ep);
        }

        public async Task<StripeCustomer> DeleteCustomer (string customer_id)
        {
            if (String.IsNullOrEmpty (customer_id))
                throw new ArgumentNullException ("customer_id");

            string ep = String.Format ("{0}/customers/{1}", api_endpoint, HttpUtility.UrlEncode (customer_id));
            return await DoRequest<StripeCustomer> (ep, HttpMethod.Delete, null);
        }
        #endregion
        #region Events
        public async Task<StripeEvent> GetEvent (string eventId)
        {
            string ep = string.Format ("{0}/events/{1}", api_endpoint, HttpUtility.UrlEncode (eventId));
            return await DoRequest<StripeEvent> (ep);
        }

        public async Task<StripeCollection<StripeEvent>> GetEvents (int offset = 0, int count = 10, string type = null, StripeDateTimeInfo created = null)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException ("offset");
            if (count < 1 || count > 100)
                throw new ArgumentOutOfRangeException ("count");

           StringBuilder str = new StringBuilder ();
           str.AppendFormat ("offset={0}&count={1}&", offset, count);

            if (!string.IsNullOrEmpty (type))
                str.AppendFormat ("type={0}&", HttpUtility.UrlEncode (type));

            if (created != null) {
                created.Prefix = "created";
                created.UrlEncode (str);
            }

            str.Length--;

            string ep = String.Format ("{0}/events?{1}", api_endpoint, str);
            return await DoRequest<StripeCollection<StripeEvent>> (ep);
        }

        #endregion

        #region Tokens
        public async Task<StripeCreditCardToken> CreateToken (StripeCreditCardInfo card)
        {
            if (card == null)
                throw new ArgumentNullException ("card");
            StringBuilder str = UrlEncode (card);

            string ep = string.Format ("{0}/tokens", api_endpoint);
            return await DoRequest<StripeCreditCardToken> (ep, HttpMethod.Post, str.ToString ());
        }

        public async Task<StripeCreditCardToken> GetToken (string tokenId)
        {
            if (string.IsNullOrEmpty (tokenId))
                throw new ArgumentNullException (tokenId);

            string ep = string.Format ("{0}/tokens/{1}", api_endpoint, HttpUtility.UrlEncode (tokenId));
            return await DoRequest<StripeCreditCardToken> (ep);
        }
        #endregion
        #region Plans
        public async Task<StripePlan> CreatePlan (StripePlanInfo plan)
        {
            if (plan == null)
                throw new ArgumentNullException ("plan");
            StringBuilder str = UrlEncode (plan);

            string ep = string.Format ("{0}/plans", api_endpoint);
            return await DoRequest<StripePlan> (ep, HttpMethod.Post, str.ToString ());
        }

        public async Task<StripePlan> GetPlan (string planId)
        {
            if (string.IsNullOrEmpty (planId))
                throw new ArgumentNullException ("id");

            string ep = string.Format ("{0}/plans/{1}", api_endpoint, HttpUtility.UrlEncode (planId));
            return await DoRequest<StripePlan> (ep);
        }

        public async Task<StripePlan> DeletePlan (string planId)
        {
            if (string.IsNullOrEmpty (planId))
                throw new ArgumentNullException ("id");

            string ep = string.Format ("{0}/plans/{1}", api_endpoint, HttpUtility.UrlEncode (planId));
            return await DoRequest<StripePlan> (ep, HttpMethod.Delete, null);
        }

        public async Task<StripeCollection<StripePlan>> GetPlans (int offset = 0, int count = 10)
        {
            string str = string.Format ("count={0}&offset={1}", count, offset);
            string ep = string.Format ("{0}/plans?{1}", api_endpoint, str);
            return await DoRequest<StripeCollection<StripePlan>> (ep);
        }
        #endregion
        #region Subscriptions
        public async Task<StripeSubscription> Subscribe (string customerId, StripeSubscriptionInfo subscription)
        {
            StringBuilder str = UrlEncode (subscription);
            string ep = string.Format (subscription_path, api_endpoint, HttpUtility.UrlEncode (customerId));
            return await DoRequest<StripeSubscription> (ep, HttpMethod.Post, str.ToString ());
        }

        public async Task<StripeSubscription> GetSubscription (string customerId)
        {
            if (string.IsNullOrEmpty (customerId))
                throw new ArgumentNullException ("customerId");
            string ep = string.Format (subscription_path, api_endpoint, HttpUtility.UrlEncode (customerId));
            return await DoRequest<StripeSubscription>(ep);
        }

        public async Task<StripeSubscription> Unsubscribe (string customerId, bool atPeriodEnd)
        {
            string ep = string.Format (subscription_path + "?at_period_end={2}", api_endpoint, HttpUtility.UrlEncode (customerId), atPeriodEnd.ToString ().ToLowerInvariant ());
            return await DoRequest<StripeSubscription> (ep, HttpMethod.Delete, null);
        }
        #endregion
        #region Invoice items
        public async Task<StripeInvoiceItem> CreateInvoiceItem (StripeInvoiceItemInfo item)
        {
            if (string.IsNullOrEmpty (item.CustomerID))
                throw new ArgumentNullException ("item.CustomerID");
            StringBuilder str = UrlEncode (item);
            string ep = string.Format ("{0}/invoiceitems", api_endpoint);
            return await DoRequest<StripeInvoiceItem> (ep, HttpMethod.Delete, str.ToString ());
        }

        public async Task<StripeInvoiceItem> GetInvoiceItem (string invoiceItemId)
        {
            if (string.IsNullOrEmpty (invoiceItemId))
                throw new ArgumentNullException ("invoiceItemId");
            string ep = string.Format ("{0}/invoiceitems/{1}", api_endpoint, invoiceItemId);
            return await DoRequest<StripeInvoiceItem> (ep);
        }

        public async Task<StripeInvoiceItem> UpdateInvoiceItem (string invoiceItemId, StripeInvoiceItemInfo item)
        {
            StringBuilder str = UrlEncode (item);
            string ep = string.Format ("{0}/invoiceitems/{1}", api_endpoint, invoiceItemId);
            return await DoRequest<StripeInvoiceItem> (ep, HttpMethod.Post, str.ToString ());
        }

        public async Task<StripeInvoiceItem> DeleteInvoiceItem (string invoiceItemId)
        {
            string ep = string.Format ("{0}/invoiceitems/{1}", api_endpoint, invoiceItemId);
            return await DoRequest<StripeInvoiceItem> (ep, HttpMethod.Delete, null);
        }

        public async Task<StripeCollection<StripeInvoiceItem>> GetInvoiceItems (int offset = 0, int count = 10, string customerId = null, StripeDateTimeInfo created = null)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException ("offset");
            if (count < 1 || count > 100)
                throw new ArgumentOutOfRangeException ("count");

            StringBuilder str = new StringBuilder ();
            str.AppendFormat ("offset={0}&", offset);
            str.AppendFormat ("count={0}&", count);
            if (!string.IsNullOrEmpty (customerId))
                str.AppendFormat ("customer={0}&", HttpUtility.UrlEncode (customerId));

            if (created != null) {
                created.Prefix = "created";
                created.UrlEncode (str);
            }
            
            str.Length--;
            string ep = String.Format ("{0}/invoiceitems?{1}", api_endpoint, str);
            return await DoRequest<StripeCollection<StripeInvoiceItem>> (ep);
        }

        #endregion
        #region Invoices
        public async Task<StripeInvoice> GetInvoice (string invoiceId)
        {
            if (string.IsNullOrEmpty (invoiceId))
                throw new ArgumentNullException ("invoiceId");
            string ep = string.Format ("{0}/invoices/{1}", api_endpoint, invoiceId);
            return await DoRequest<StripeInvoice> (ep);
        }
        
        public async Task<StripeCollection<StripeInvoice>> GetInvoices (int offset = 0, int count = 10, string customerId = null)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException ("offset");
            if (count < 1 || count > 100)
                throw new ArgumentOutOfRangeException ("count");

            StringBuilder str = new StringBuilder ();
            str.AppendFormat ("offset={0}&", offset);
            str.AppendFormat ("count={0}&", count);
            if (!string.IsNullOrEmpty (customerId))
                str.AppendFormat ("customer={0}&", HttpUtility.UrlEncode (customerId));

            str.Length--;
            string ep = String.Format ("{0}/invoices?{1}", api_endpoint, str);
            return await DoRequest<StripeCollection<StripeInvoice>>(ep);
        }

        public async Task<StripeInvoice> GetUpcomingInvoice (string customerId)
        {
            if (string.IsNullOrEmpty (customerId))
                throw new ArgumentOutOfRangeException ("customerId");
            string ep = String.Format ("{0}/invoices/upcoming?customer={1}", api_endpoint, customerId);
            return await DoRequest<StripeInvoice> (ep);
        }

        public async Task<StripeCollection<StripeLineItem>> GetInvoiceLines (string invoiceId)
        {
            if (string.IsNullOrEmpty (invoiceId))
                throw new ArgumentNullException ("invoiceId");
            string ep = string.Format ("{0}/invoices/{1}/lines", api_endpoint, invoiceId);
            return await DoRequest<StripeCollection<StripeLineItem>> (ep);
        }
        #endregion
        #region Coupons
        public async Task<StripeCoupon> CreateCoupon (StripeCouponInfo coupon)
        {
            if (coupon == null)
                throw new ArgumentNullException ("coupon");
            if (coupon.PercentOff < 1 || coupon.PercentOff > 100)
                throw new ArgumentOutOfRangeException ("coupon.PercentOff");
            if (coupon.Duration == StripeCouponDuration.Repeating && coupon.MonthsForDuration < 1)
                throw new ArgumentException ("MonthsForDuration must be greater than 1 when Duration = Repeating");
            StringBuilder str = UrlEncode (coupon);
            string ep = string.Format ("{0}/coupons", api_endpoint);
            return await DoRequest<StripeCoupon> (ep, HttpMethod.Post, str.ToString ());
        }

        public async Task<StripeCoupon> GetCoupon (string couponId)
        {
            if (string.IsNullOrEmpty (couponId))
                throw new ArgumentNullException ("couponId");
            string ep = string.Format ("{0}/coupons/{1}", api_endpoint, couponId);
            return await DoRequest<StripeCoupon> (ep);
        }

        public async Task<StripeCoupon> DeleteCoupon (string couponId)
        {
            if (string.IsNullOrEmpty (couponId))
                throw new ArgumentNullException ("couponId");
            string ep = string.Format ("{0}/coupons/{1}", api_endpoint, couponId);
            return await DoRequest<StripeCoupon> (ep, HttpMethod.Delete, null);
        }

        public async Task<StripeCollection<StripeCoupon>> GetCoupons (int offset = 0, int count = 10)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException ("offset");
            if (count > 100)
                throw new ArgumentOutOfRangeException ("count");
            string ep = string.Format ("{0}/coupons?offset={0}&count={1}", api_endpoint, offset, count);
            return await DoRequest<StripeCollection<StripeCoupon>> (ep);
        }
        #endregion
        #region Cards
        public async Task<StripeCard> CreateCard (string customer_id, StripeCreditCardInfo card)
        {
            if (string.IsNullOrWhiteSpace (customer_id)) {
                throw new ArgumentNullException ("customer_id");
            }

            StringBuilder str = UrlEncode (card);
            string ep = String.Format ("{0}/customers/{1}/cards", api_endpoint, HttpUtility.UrlEncode (customer_id));
            return await DoRequest<StripeCard> (ep, HttpMethod.Post, str.ToString ());
        }

        public async Task<StripeCard> DeleteCard (string customer_id, string card_id)
        {
            if (string.IsNullOrWhiteSpace (customer_id)) {
                throw new ArgumentNullException ("customer_id");
            }
            if (string.IsNullOrWhiteSpace (card_id)) {
                throw new ArgumentNullException ("card_id");
            }

            string ep = string.Format ("{0}/customers/{1}/cards/{2}", api_endpoint, HttpUtility.UrlEncode (customer_id),
                                       HttpUtility.UrlEncode (card_id));
            return await DoRequest<StripeCard> (ep, HttpMethod.Delete, null);
        }

        public async Task<StripeCard> UpdateCard (string customer_id, StripeUpdateCreditCardInfo card)
        {
            if (string.IsNullOrWhiteSpace (customer_id))
                throw new ArgumentNullException ("customer_id");
            if (null == card)
                throw new ArgumentNullException ("card");
            if (string.IsNullOrWhiteSpace (card.ID))
                throw new ArgumentNullException ("card.ID");

            StringBuilder str = UrlEncode (card);
            string format = "{0}/customers/{1}/cards/{2}";
            string ep = string.Format (format, api_endpoint, HttpUtility.UrlEncode (customer_id),
                                       HttpUtility.UrlEncode (card.ID));
            return await DoRequest<StripeCard> (ep, HttpMethod.Post, str.ToString ());
        }
        #endregion
        public int TimeoutSeconds { get; set; }
    }
}
