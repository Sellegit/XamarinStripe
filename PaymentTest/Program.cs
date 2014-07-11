/*
 * Copyright 2011 Xamarin, Inc., Joe Dluzen
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
using Xamarin.Payments.Stripe;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace PaymentTest {
    class Program {
        static void Main (string [] args)
        {
            StripePayment payment = new StripePayment ("sk_test_BQokikJOvBiI2HlWgH4olfQ2");
            TestSimpleCharge (payment).Wait();
            TestPartialRefund (payment).Wait();
            TestCustomer (payment).Wait();
            TestCustomerAndCharge (payment).Wait ();
            TestGetCharges (payment).Wait();
            TestGetCustomers (payment).Wait();
            TestCreateGetToken (payment).Wait();
//            TestCreatePlanGetPlan (payment).Wait();
//            TestCreateSubscription (payment).Wait();
//            TestCreateInvoiceItems (payment).Wait();
            //TestInvoices (payment).Wait();
            //TestInvoices2 (payment).Wait();
            TestDeserializePastDue ();
        }

        static StripeCreditCardInfo GetCC ()
        {
            StripeCreditCardInfo cc = new StripeCreditCardInfo ();
            cc.CVC = "1234";
            cc.ExpirationMonth = 6;
            cc.ExpirationYear = 2015;
            cc.Number = "4242424242424242";
            return cc;
        }

        static StripePlanInfo GetPlanInfo ()
        {
            return new StripePlanInfo {
                Amount = 1999,
                ID = "myplan",
                Interval = StripePlanInterval.Month,
                Name = "My standard plan",
                TrialPeriod = 7
            };
        }

        static StripeInvoiceItemInfo GetInvoiceItemInfo ()
        {
            return new StripeInvoiceItemInfo {
                Amount = 1999,
                Description = "Invoice item: " + Guid.NewGuid ().ToString ()
            };
        }

        static async Task TestSimpleCharge (StripePayment payment)
        {
            StripeCreditCardInfo cc = GetCC ();
            StripeCharge charge = await payment.Charge (5001, "usd", cc, "Test charge");
            Console.WriteLine (charge);
            string charge_id = charge.ID;
            StripeCharge charge_info = await payment.GetCharge (charge_id);
            Console.WriteLine (charge_info);

            StripeCharge refund = await payment.Refund (charge_info.ID);
            Console.WriteLine (refund.Created);
        }

        static async Task TestPartialRefund (StripePayment payment)
        {
            StripeCreditCardInfo cc = GetCC ();
            StripeCharge charge = await payment.Charge (5001, "usd", cc, "Test partial refund");
            Console.WriteLine (charge.ID);
            StripeCharge refund = await payment.Refund (charge.ID, 2499);
            Console.WriteLine (refund.Amount);
        }

        static async Task TestCustomer (StripePayment payment)
        {
            StripeCustomerInfo customer = new StripeCustomerInfo ();
            //customer.Card = GetCC ();
            StripeCustomer customer_resp = await payment.CreateCustomer (customer);
            string customer_id = customer_resp.ID;
            StripeCustomer customer_info = await payment.GetCustomer (customer_id);
            Console.WriteLine (customer_info);
            StripeCustomer ci2 = await payment.DeleteCustomer (customer_id);
            if (ci2.Deleted == false)
                throw new Exception ("Failed to delete " + customer_id);
        }

        static async Task TestCustomerAndCharge (StripePayment payment)
        {
            StripeCustomerInfo customer = new StripeCustomerInfo ();
            //customer.Card = GetCC ();
            StripeCustomer response = await payment.CreateCustomer (customer);
            string customer_id = response.ID;
            StripeCustomer customer_info = await payment.GetCustomer (customer_id);
            Console.WriteLine (customer_info);
            StripeCustomerInfo info_update = new StripeCustomerInfo ();
            info_update.Card = GetCC ();
            StripeCustomer update_resp = await payment.UpdateCustomer (customer_id, info_update);
            Console.Write ("Customer updated with CC. Press ENTER to continue...");
            Console.Out.Flush ();
            Console.ReadLine ();
            StripeCustomer ci2 = await payment.DeleteCustomer (customer_id);
            if (ci2.Deleted == false)
                throw new Exception ("Failed to delete " + customer_id);
        }

        static async Task TestGetCharges (StripePayment payment)
        {
            var charges = await payment.GetCharges (0, 10);
            Console.WriteLine (charges.Data.Count);
        }

        static async Task TestGetCustomers (StripePayment payment)
        {
            var customers = await payment.GetCustomers (0, 10);
            Console.WriteLine (customers.Data.Count);
        }

        static async Task TestCreateGetToken (StripePayment payment)
        {
            StripeCreditCardToken tok = await payment.CreateToken (GetCC ());
            StripeCreditCardToken tok2 = await payment.GetToken (tok.ID);
        }

        static async Task TestCreatePlanGetPlan (StripePayment payment)
        {
            StripePlan plan = await CreatePlan (payment);
            var plans = await payment.GetPlans (10, 10);
            Console.WriteLine (plans.Total);
        }

        static async Task<StripePlan> CreatePlan (StripePayment payment)
        {
            StripePlan plan = await payment.CreatePlan (GetPlanInfo ());
            StripePlan plan2 = await payment.GetPlan (plan.ID);
            //DeletePlan (plan2, payment);
            return plan2;
        }

        static async Task<StripePlan> DeletePlan (StripePlan plan, StripePayment payment)
        {
            StripePlan deleted = await payment.DeletePlan (plan.ID);
            return deleted;
        }

        static async Task TestCreateSubscription (StripePayment payment)
        {
            StripeCustomer cust = await payment.CreateCustomer (new StripeCustomerInfo {
                Card = GetCC ()
            });
            //StripePlan temp = new StripePlan { ID = "myplan" };
            //DeletePlan (temp, payment);
            StripePlan plan = await CreatePlan (payment);
            StripeSubscription sub = await payment.Subscribe (cust.ID, new StripeSubscriptionInfo {
                Card = GetCC (),
                Plan = "myplan",
                Prorate = true
            });
            StripeSubscription sub2 = await payment.GetSubscription (sub.CustomerID);
            TestDeleteSubscription (cust, payment);
            DeletePlan (plan, payment);
        }

        static async Task<StripeSubscription> TestDeleteSubscription (StripeCustomer customer, StripePayment payment)
        {
            StripeSubscription sub = await payment.Unsubscribe (customer.ID, true);
            return sub;
        }

        static async Task TestCreateInvoiceItems (StripePayment payment)
        {
            StripeCustomer cust = await payment.CreateCustomer (new StripeCustomerInfo ());
            StripeInvoiceItemInfo info = GetInvoiceItemInfo ();
            info.CustomerID = cust.ID;
            StripeInvoiceItem item = await payment.CreateInvoiceItem (info);
            StripeInvoiceItemInfo newInfo = GetInvoiceItemInfo ();
            newInfo.Description = "Invoice item: " + Guid.NewGuid ().ToString ();
            StripeInvoiceItem item2 = await payment.UpdateInvoiceItem (item.ID, newInfo);
            StripeInvoiceItem item3 = await payment.GetInvoiceItem (item2.ID);
            if (item.Description == item3.Description)
                throw new Exception ("Update failed");
            StripeInvoiceItem deleted = await payment.DeleteInvoiceItem (item2.ID);
            if (!deleted.Deleted.HasValue && deleted.Deleted.Value)
                throw new Exception ("Delete failed");
            var items = await payment.GetInvoiceItems (10, 10, null);
            Console.WriteLine (items.Total);
            payment.DeleteCustomer (cust.ID);
        }

        static async Task TestInvoices (StripePayment payment)
        {
            var invoices = await payment.GetInvoices (10, 10);
            StripeInvoice inv = await payment.GetInvoice (invoices.Data [0].ID);
            StripeCustomer cust = await payment.CreateCustomer (new StripeCustomerInfo ());
            StripeSubscription sub = await payment.Subscribe (cust.ID, new StripeSubscriptionInfo {
                Card = GetCC ()
            });
            StripeInvoice inv2 = await payment.GetUpcomingInvoice (cust.ID);
            payment.Unsubscribe (cust.ID, true);
            payment.DeleteCustomer (cust.ID);
        }

        static async Task TestInvoices2 (StripePayment payment)
        {
            StripeCustomer cust = await payment.GetCustomer ("cus_ulcOcy5Seu2dpq");
            StripePlanInfo planInfo = new StripePlanInfo{
                Amount = 1999,
                ID = "testplan",
                Interval = StripePlanInterval.Month,
                Name = "The Test Plan",
            //TrialPeriod = 7
            };
            //payment.DeletePlan (planInfo.ID);
            StripePlan plan = await payment.CreatePlan (planInfo);
            StripeSubscriptionInfo subInfo = new StripeSubscriptionInfo{
                Card = GetCC (),
                Plan = planInfo.ID,
                Prorate = true
            };
            StripeSubscription sub = await payment.Subscribe (cust.ID, subInfo);
            payment.CreateInvoiceItem (new StripeInvoiceItemInfo {
                CustomerID = cust.ID,
                Amount = 1337,
                Description = "Test single charge"
            });

            var invoices = payment.GetInvoices (0, 10, cust.ID);
            StripeInvoice upcoming = await payment.GetUpcomingInvoice (cust.ID);
            payment.Unsubscribe (cust.ID, true);
            payment.DeletePlan (planInfo.ID);
            foreach (StripeLineItem line in upcoming) {
                Console.WriteLine ("{0} for type {1}", line.Amount, line.GetType ());
            }

        }

        static void TestDeserializePastDue ()
        {
            string json = @"{
  ""status"": ""past_due"",
}";
            StripeSubscription sub = JsonConvert.DeserializeObject<StripeSubscription> (json);
            if (sub.Status != StripeSubscriptionStatus.PastDue)
                throw new Exception ("Failed to deserialize `StripeSubscriptionStatus.PastDue`");
            string json2 = JsonConvert.SerializeObject(sub);
        }
    }
}
