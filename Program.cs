using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DeviceSimulator
{
    class Program
    {
        private static string DPSGlobalDeviceEndpoint = "global.azure-devices-provisioning.net";
        private static string DPSScopeId = "0ne00015C24";

        private static string ADPS_BaseURL = "https://provisionwebapiserviceqat2.azurewebsites.net/";
        private static string ADPS_GetTokenRoute = "api/v1/Token";
        private static string ADPS_GetDPSInfoRoute = "api/v1/DPSInfo";
        private static string ADPS_GetCertificateRoute = "api/v1/CertInfo";



        static void Main(string[] args)
        {
            string askeySN;
            string token;
            string dpsInfoRaw;
            string dpsScopeId;
            string dpsCertRaw;
            string deviceId;
            string certBase64String;
            string privateKeyBase64String;



            Console.Write("Please Enter \"AskeySN\" of activating device:");
            askeySN = Console.ReadLine();
            Console.Write("Please Enter \"RegistrationId\" of activating device:");
            deviceId = Console.ReadLine();


#if DEBUG
            askeySN = "79eaab18-7e02-11ea-bfc0-b06ebfcb46d9";
            deviceId = "20200414-001";
#endif


            if (askeySN == string.Empty)
            {
                Console.WriteLine("Not allow AskeySN is Empty");
                goto Continue;
            }

            if (deviceId == string.Empty)
            {
                Console.WriteLine("Not allow RegistrationId is Empty");
                goto Continue;
            }







            //Get Token First
            Console.WriteLine("Get Token Process...");

            try
            {
                token = GetADPSTokenString(ADPS_BaseURL, ADPS_GetTokenRoute, askeySN).GetAwaiter().GetResult();

                token = token.Substring(1, token.Length - 2);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Get Token Error, " + ex.Message);

                goto Continue;

            }



            Console.WriteLine("Get Token:" + token);
            Console.WriteLine("Expire Time 2 mins");




            Console.WriteLine("");
            Console.WriteLine("Get DPS Info...");
            try
            {
                dpsInfoRaw = GetADPSDPSInfoString(ADPS_BaseURL, ADPS_GetDPSInfoRoute, token).GetAwaiter().GetResult();
                dpsScopeId = dpsInfoRaw.Split('\n')[2].Split(':')[1];
            }
            catch (Exception ex)
            {
                Console.WriteLine("Get DPS Info Error, " + ex.Message);

                goto Continue;

            }
            Console.WriteLine("DPS Scope ID:" + dpsScopeId);


            Console.WriteLine("");
            Console.WriteLine("Get Cert Info...");
            try
            {
                dpsCertRaw = GetADPSCertString(ADPS_BaseURL, ADPS_GetCertificateRoute, token, deviceId).GetAwaiter().GetResult();

                Console.WriteLine("Cert Info:" + dpsCertRaw);


                var CertObject = JObject.Parse(dpsCertRaw);

                certBase64String = CertObject["publickey"].ToString();
                privateKeyBase64String = CertObject["privatekey"].ToString();

            }
            catch (Exception ex)
            {
                Console.WriteLine("Get Cert Error, " + ex.Message);

                goto Continue;

            }

            byte[] pfxByteArray;


            Console.WriteLine("");
            Console.WriteLine("Process Cert Info...");
            try
            {
                pfxByteArray = GetPfxCertificateByteArray(ref certBase64String, ref privateKeyBase64String);
                Console.WriteLine("Process Success");

            }
            catch (Exception ex)
            {
                Console.WriteLine("Process Cert Error, " + ex.Message);
                goto Continue;
            }



            Console.WriteLine("");

            Console.WriteLine("Active Device...");
            try
            {
                X509Certificate2 certificateActive = new X509Certificate2(pfxByteArray);

                using (var transport = new ProvisioningTransportHandlerHttp())
                {
                    var security = new SecurityProviderX509Certificate(certificateActive);
                    ProvisioningDeviceClient client = ProvisioningDeviceClient.Create(DPSGlobalDeviceEndpoint, dpsScopeId, security, transport);

                    var result = client.RegisterAsync().ConfigureAwait(false).GetAwaiter().GetResult();

                    Console.WriteLine("DeviceId:" + result.DeviceId);
                    Console.WriteLine("AssignedHub:" + result.AssignedHub);
                    Console.WriteLine("Status:" + result.Status);
                    Console.WriteLine("Substatus:" + result.Substatus);


                    //client.RegisterAsync().GetAwaiter(false).GetResult();

                    security.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Active Device Error, " + ex.Message);
                goto Continue;
            }



        Continue:
            Console.ReadKey();


        }

        static async Task<string> GetADPSTokenString(string url, string route, string askeySn)
        {
            HttpClient client = new HttpClient();

            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Clear();
            //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            //client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",)

            Dictionary<string, string> formDataDictionary = new Dictionary<string, string>();
            formDataDictionary.Add("askeySN", askeySn);

            var formData = new FormUrlEncodedContent(formDataDictionary);




            HttpResponseMessage response = await client.PostAsync(route, formData);



            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new Exception("Get Error Code" + response.StatusCode + ", Exception:" + ex.Message);

            }


            return await response.Content.ReadAsStringAsync();





            //HttpResponseMessage response = await client.GetAsync(route);

            //if (response.IsSuccessStatusCode)
            //{
            //    return await response.Content.ReadAsStringAsync();

            //}
            //else {
            //    throw new Exception("Get Error Code" + response.StatusCode);
            //}








        }



        static async Task<string> GetADPSDPSInfoString(string url, string route, string token)
        {
            HttpClient client = new HttpClient();

            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Clear();
            //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            //Dictionary<string, string> formDataDictionary = new Dictionary<string, string>();
            //formDataDictionary.Add("askeySN", askeySn);

            //var formData = new FormUrlEncodedContent(formDataDictionary);




            //HttpResponseMessage response = await client.PostAsync(route, formData);



            //try
            //{
            //    response.EnsureSuccessStatusCode();
            //}
            //catch (Exception ex)
            //{
            //    throw new Exception("Get Error Code" + response.StatusCode + ", Exception:" + ex.Message);

            //}


            //return await response.Content.ReadAsStringAsync();





            HttpResponseMessage response = await client.GetAsync(route);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();

            }
            else
            {
                throw new Exception("Get Error Code" + response.StatusCode);
            }









        }




        static async Task<string> GetADPSCertString(string url, string route, string token, string deviceId)
        {
            HttpClient client = new HttpClient();

            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Clear();
            //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            //Dictionary<string, string> formDataDictionary = new Dictionary<string, string>();
            //formDataDictionary.Add("askeySN", askeySn);

            //var formData = new FormUrlEncodedContent(formDataDictionary);
            var builder = new UriBuilder(url + route);
            var query = HttpUtility.ParseQueryString(builder.Query);
            query["DeviceID"] = deviceId;


            builder.Query = query.ToString();




            //HttpResponseMessage response = await client.PostAsync(route, formData);



            //try
            //{
            //    response.EnsureSuccessStatusCode();
            //}
            //catch (Exception ex)
            //{
            //    throw new Exception("Get Error Code" + response.StatusCode + ", Exception:" + ex.Message);

            //}


            //return await response.Content.ReadAsStringAsync();





            HttpResponseMessage response = await client.GetAsync(builder.ToString());

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();

            }
            else
            {
                throw new Exception("Get Error Code" + response.StatusCode);
            }









        }




        static byte[] GetPfxCertificateByteArray(ref string certString, ref string privateKey)
        {


            byte[] certArray = Convert.FromBase64String(certString);
            byte[] privateKeyArray = Convert.FromBase64String(privateKey);


            //Translate to Pkcs#12
            var store = new Pkcs12StoreBuilder().Build();

            Org.BouncyCastle.X509.X509Certificate certTranslate = new X509CertificateParser().ReadCertificate(certArray);

            var certEntry = new X509CertificateEntry(certTranslate);
            var pk = PrivateKeyFactory.CreateKey(privateKeyArray);
            var keyEntry = new AsymmetricKeyEntry(pk);

            store.SetKeyEntry("", keyEntry, new X509CertificateEntry[] { certEntry });


            MemoryStream stream = new MemoryStream();

            store.Save(stream, new char[] { }, new SecureRandom());

            stream.Dispose();
            //FromString
            byte[] pfxByteArray = stream.ToArray();

            return pfxByteArray;

        }





    }
}
