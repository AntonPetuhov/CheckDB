using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net;
using System.Configuration;
using System.Data.SqlClient;

namespace DBcheck_console
{
    class Program
    {
        static object ServiceLogLocker = new object();     // локер для логов драйвера
        static object CheckLogLocker = new object();     // локер для логов драйвера
        public static string IPadress = "10.128.131.111";  // cgm-app11, сервер БД
        public static bool ServiceIsActive;                // флаг для запуска и остановки потока

        public static string user = "PSMExchangeUser";     // логин для базы обмена файлами и для базы CGM Analytix
        public static string password = "PSM_123456";      // пароль для базы обмена файлами и для базы CGM Analytix  

        #region logs
        static void ServiceLog(string Message)
        {
            lock (ServiceLogLocker)
            {
                try
                {
                    string path = AppDomain.CurrentDomain.BaseDirectory + "\\Log\\Service";
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    string filename = path + "\\ServiceThread_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
                    if (!System.IO.File.Exists(filename))
                    {
                        using (StreamWriter sw = System.IO.File.CreateText(filename))
                        {
                            sw.WriteLine(DateTime.Now + ": " + Message);
                        }
                    }
                    else
                    {
                        using (StreamWriter sw = System.IO.File.AppendText(filename))
                        {
                            sw.WriteLine(DateTime.Now + ": " + Message);
                        }
                    }
                }
                catch
                {

                }
            }
        }

        static void CheckLog(string Message)
        {
            lock (CheckLogLocker)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory + "\\Log\\CheckDB";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                string filename = path + "\\CheckDBThread_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
                if (!System.IO.File.Exists(filename))
                {
                    using (StreamWriter sw = System.IO.File.CreateText(filename))
                    {
                        sw.WriteLine(DateTime.Now + ": " + Message);
                    }
                }
                else
                {
                    using (StreamWriter sw = System.IO.File.AppendText(filename))
                    {
                        sw.WriteLine(DateTime.Now + ": " + Message);
                    }
                }

            }
        }
        #endregion

        public static void CheckDB()
        {
            string CGMConnectionString = ConfigurationManager.ConnectionStrings["CGMConnection"].ConnectionString;
            CGMConnectionString = String.Concat(CGMConnectionString, $"User Id = {user}; Password = {password}");

            while (ServiceIsActive)
            {
                string RID = "";
                DateTime RegistrationDateDate = DateTime.Now;

                try
                {
                    using (SqlConnection CGMconnection = new SqlConnection(CGMConnectionString))
                    {
                        CheckLog("Open DB connection");
                        CGMconnection.Open();

                        SqlCommand CheckSelectCommand = new SqlCommand(
                          "SELECT TOP 1 " +
                          //" * " +
                          "r.rem_ank_dttm AS RegistrationDate, " +
                          "r.rem_rid as rid " +
                          //"p.pop_pid AS PID, p.pop_enamn AS PatientSurname, p.pop_fnamn AS PatientName, p.pop_fdatum AS PatientBirthday, " +
                          //"CASE WHEN p.pop_kon = 'K' THEN 'F' ELSE 'M' END AS PatientSex, " +
                          //"r.rem_ank_dttm AS RegistrationDate " +
                          "FROM dbo.remiss (NOLOCK) r", CGMconnection);
                        SqlDataReader Reader = CheckSelectCommand.ExecuteReader();

                        // если такой ШК есть
                        if (Reader.HasRows)
                        {
                            CheckLog(CheckSelectCommand.CommandText);
                            while (Reader.Read())
                            {
                                if (!Reader.IsDBNull(0)) { RegistrationDateDate = Reader.GetDateTime(0); };
                                if (!Reader.IsDBNull(1)) { RID = Reader.GetString(1); };

                                CheckLog($"RID: {RID} , RegDate: {RegistrationDateDate}");
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    CheckLog(ex.ToString());
                }

                Thread.Sleep(10000);
            }

            
        }

        public static void PingSRV()
        {
            System.Net.IPAddress ip = IPAddress.Parse(IPadress);
            Ping ping = new Ping();
            PingOptions options = new PingOptions();

            // Use the default Ttl value which is 128,
            // but change the fragmentation behavior.
            options.DontFragment = true;

            while (ServiceIsActive)
            {
                try
                {
                    PingReply reply = ping.Send(ip);

                    if (reply.Status == IPStatus.Success)
                    {
                        //Console.WriteLine("Status: " + reply.Status);
                        //Console.WriteLine("Address: {0}", reply.Address.ToString());
                        //Console.WriteLine("RoundTrip time: {0}", reply.RoundtripTime);
                        //Console.WriteLine("Time to live: {0}", reply.Options.Ttl);
                        //Console.WriteLine("Don't fragment: {0}", reply.Options.DontFragment);
                        //Console.WriteLine("Buffer size: {0}", reply.Buffer.Length);

                        ServiceLog("Status: " + reply.Status + "; " +
                                   "Address:" + reply.Address.ToString() + "; " +
                                   "RoundTrip time: " + reply.RoundtripTime + " мс" + "; " +
                                   "Time to live: " + reply.Options.Ttl + "; " +
                                   "Don't fragment: " + reply.Options.DontFragment + "; " +
                                   "Buffer size: " + reply.Buffer.Length);
                    }
                    else
                    {
                        //Console.WriteLine("Статус: " + reply.Status);
                        //Console.WriteLine(reply.RoundtripTime + " мс");
                        ServiceLog("Status: " + reply.Status);
                    }
                }
                catch(Exception ex)
                {
                    ServiceLog(ex.ToString());
                }

                Thread.Sleep(5000);
            }
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Database checking");
            Console.WriteLine($"{IPadress}");

            ServiceIsActive = true;

            Thread PingSRVThread = new Thread(new ThreadStart(PingSRV));
            PingSRVThread.Name = "PingServer";
            PingSRVThread.Start();

            Thread CheckDBThread = new Thread(new ThreadStart(CheckDB));
            CheckDBThread.Name = "CheckDB";
            CheckDBThread.Start();

            Console.ReadLine();

        }
    }
}
