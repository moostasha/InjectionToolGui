﻿using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace InjectionToolGui
{
    internal static class InjectionController
    {
        public static bool IsInitialized { get; private set; }
        public static bool IsInjected { get; private set; }
        public static bool IsReported { get; private set; }
        public static bool HasBin { get; private set; }

        public static void Initialization()
        {
            CheckForInjection();
            ParseXml();
            CheckForBin();
            CheckForReport();

            IsInitialized = true;
        }

        public static string? CheckForInjection()
        {
            ManagementObjectSearcher key = new(
                "root\\CIMV2", "SELECT * FROM SoftwareLicensingService");
            foreach (ManagementObject obj in key.Get())
            {
                if ((string)obj["OA3xOriginalProductKey"] != "")
                {
                    IsInjected = true;
                    return (string)obj["OA3xOriginalProductKey"];
                }
            }

            IsInjected = false;
            return null;
        }

        public static string? ParseXml()
        {
            try
            {
                // Parses oa3.xml for the Windows Product Key ID
                if (File.Exists((@"C:\Temp\Data\oa3.xml")))
                {
                    XmlDocument oa3Xml = new();
                    oa3Xml.Load(@"C:\Temp\Data\oa3.xml");

                    XmlNodeList nodeList = oa3Xml.GetElementsByTagName("ProductKeyID");
                    foreach (XmlNode node in nodeList)
                    {
                        if (node.InnerText != "")
                        {
                            return node.InnerText;
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException(@"C:\Temp\Data\oa3.xml");
                }

            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine(ex.Message);
                return String.Empty;
            }

            return null;
        }

        public static void CheckForBin()
        {
            if (File.Exists(@"C:\Temp\Data\oa3.bin"))
            {
                HasBin = true;
            }
        }

        public static void CheckForReport()
        {
            if (File.Exists(@"C:\Temp\Data\Report.xml"))
            {
                IsReported = true;
            }
        }

        public static async Task InjectNewKey(bool debug, SystemInfo systemInfo)
        {
            await WriteLogAsync("InjectNew", systemInfo);
            systemInfo.DebugList.Insert(0, $"{DateTime.Now} Injecting New Key");

            try
            {
                string toolNum = systemInfo.Baseboard.Manufacturer switch
                {
                    Baseboard.MANUFACTURER.ASROCK => "1",
                    Baseboard.MANUFACTURER.ASUS => "2",
                    Baseboard.MANUFACTURER.ASUSZ690 => "22",
                    Baseboard.MANUFACTURER.GIGABYTE => "3",
                    Baseboard.MANUFACTURER.MSI => "4",
                    Baseboard.MANUFACTURER.SAGER => "5",
                    Baseboard.MANUFACTURER.SAGERH2O => "5h",
                    Baseboard.MANUFACTURER.TONGFANG => "9",
                    _ => "4",
                };
                string editionNum = systemInfo.OS.Edition switch
                {
                    OS.Editions.Home => "1",
                    OS.Editions.Pro => "2",
                    OS.Editions.HomeA => "3",
                    OS.Editions.ProA => "4",
                    _=> throw new Exception("Could not resolve windows edition."),
                };

                systemInfo.DebugList.Insert(0,
                    $"{DateTime.Now} {systemInfo.Baseboard.Name} {systemInfo.OS.Version}  " +
                    $"{editionNum}({systemInfo.OS.Edition}) {systemInfo.OrderId} {toolNum}({systemInfo.Baseboard.Manufacturer})");

                await Task.Run(() =>
                {
                    string cmdSwitch = systemInfo.Interactive ? "/K" : "/C" ;

                    var InjectProcess = new Process();
                    InjectProcess.StartInfo.FileName = "cmd.exe";
                    InjectProcess.StartInfo.Arguments = systemInfo.UseTestKey ? 
                        @$"{cmdSwitch} {systemInfo.Baseboard.Manufacturer.ToString().ToLower()} inject .\OA3.bin" :
                        @$"{cmdSwitch} .\OA30\pcloa3assemble11.cmd {systemInfo.Baseboard.Name} {systemInfo.OS.Version} {editionNum} {systemInfo.OrderId} {toolNum}";
                    InjectProcess.StartInfo.CreateNoWindow = !debug;
                    InjectProcess.StartInfo.RedirectStandardError = true;

                    InjectProcess.Start();
                    InjectProcess.WaitForExit();

                    var exitCode = InjectProcess.ExitCode;

                    CheckForInjection();
                    CheckForReport();
                    CheckForBin();

                    try
                    {
                        if (!HasBin) { throw new Exception("Server did not provide a key. Check stock levels and contact a manager if the error continues"); }
                        if (!IsInjected) { throw new Exception("Key was pulled, but was not injected. Try rebooting the system and reporting."); }
                        if (!IsReported) { throw new Exception("Failed to report to Microsoft."); }
                    }
                    catch (ApplicationException ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        MessageBox.Show(e.Message, "Error While Injecting", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });

                if (systemInfo.RebootSystem) { await Reboot(false); }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error While Injecting", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine(e.Message);
            }
        }

        public static async Task InjectOldKey(bool debug, SystemInfo systemInfo)
        {
            if (HasBin)
            {
                await WriteLogAsync("InjectOld", systemInfo);
                systemInfo.DebugList.Insert(0, $"{DateTime.Now} Injecting pulled Key");

                try
                {
                    string tool = systemInfo.Baseboard.Manufacturer switch
                    {
                        Baseboard.MANUFACTURER.ASROCK => "asrock",
                        Baseboard.MANUFACTURER.ASUS => "asus",
                        Baseboard.MANUFACTURER.ASUSZ690 => "asusz690",
                        Baseboard.MANUFACTURER.GIGABYTE => "gigabyte",
                        Baseboard.MANUFACTURER.MSI => "msi",
                        Baseboard.MANUFACTURER.SAGER => "sager",
                        Baseboard.MANUFACTURER.SAGERH2O => "sagerh2o",
                        Baseboard.MANUFACTURER.TONGFANG => "tongfang",
                        _ => "msi",
                    };

                    systemInfo.DebugList.Insert(0, $"{DateTime.Now} {tool} inject");
                    await Task.Run(() =>
                    {
                        string cmdSwitch = systemInfo.Interactive ? "/K" : "/C";

                        var InjectProcess = new Process();
                        InjectProcess.StartInfo.FileName = "cmd.exe";
                        InjectProcess.StartInfo.Arguments =
                            @$"{cmdSwitch} .\OA30\{tool} inject";
                        InjectProcess.StartInfo.CreateNoWindow = !debug;
                        InjectProcess.StartInfo.RedirectStandardError = true;

                        InjectProcess.Start();
                        InjectProcess.WaitForExit();


                        CheckForInjection();

                        try
                        {      
                            if (!IsInjected) { throw new Exception("Injection finished, but key is not detecting. Try rebooting the system and reporting."); }
                        }
                        catch (ApplicationException ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(e.Message, "Error While Injecting", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    });

                    if (systemInfo.RebootSystem) { await Reboot(false); }
                }
                catch (Exception e)
                {
                    systemInfo.DebugList.Insert(0, $"{DateTime.Now} {e}");
                    MessageBox.Show(e.Message, "Error While Injecting", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static async Task ClearKey(SystemInfo systemInfo)
        {
            if (IsInjected)
            {
                await WriteLogAsync("Clear", systemInfo);
                systemInfo.DebugList.Insert(0, $"{DateTime.Now} Clearing Key");

                try
                {
                    string tool = systemInfo.Baseboard.Manufacturer switch
                    {
                        Baseboard.MANUFACTURER.ASROCK => "asrock",
                        Baseboard.MANUFACTURER.ASUS => "asus",
                        Baseboard.MANUFACTURER.ASUSZ690 => "asusz690",
                        Baseboard.MANUFACTURER.GIGABYTE => "gigabyte",
                        Baseboard.MANUFACTURER.MSI => "msi",
                        Baseboard.MANUFACTURER.SAGER => "sager",
                        Baseboard.MANUFACTURER.SAGERH2O => "sagerh2o",
                        Baseboard.MANUFACTURER.TONGFANG => "tongfang",
                        _ => "msi",
                    };

                    systemInfo.DebugList.Insert(0, $"{DateTime.Now} {tool} clear");
                    await Task.Run(() =>
                    {
                        string cmdSwitch = systemInfo.Interactive ? "/K" : "/C";

                        var InjectProcess = new Process();
                        InjectProcess.StartInfo.FileName = "cmd.exe";
                        InjectProcess.StartInfo.Arguments =
                            @$"{cmdSwitch} .\OA30\{tool} clear";
                        InjectProcess.StartInfo.CreateNoWindow = false;
                        InjectProcess.StartInfo.RedirectStandardError = true;

                        InjectProcess.Start();
                        InjectProcess.WaitForExit();
                    });

                    await Reboot(false);
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Error While Clearing Key", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBoxResult userResponse = 
                    MessageBox.Show("System doesn't have a key injected. Are you sure you want to clear?",
                    "No Key Present",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (userResponse == MessageBoxResult.Yes)
                {
                    IsInjected = true;
                    await ClearKey(systemInfo);
                }
            }
        }

        public static async Task ReportKey(bool debug, string orderId)
        {
            if (!IsReported && HasBin)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var Report = new Process();
                        Report.StartInfo.FileName = "cmd.exe";
                        Report.StartInfo.Arguments = $@"/C .\OA30\pcloa3report.cmd {orderId} wrapper";
                        Report.StartInfo.CreateNoWindow = !debug;
                        Report.StartInfo.RedirectStandardError = true;

                        Report.Start();
                        Report.WaitForExit();

                        CheckForReport();
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error While Reporting", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static async Task ReturnKey(bool debug, string orderId)
        {
            if (!IsReported)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var ReturnKey = new Process();
                        ReturnKey.StartInfo.FileName = "cmd.exe";
                        ReturnKey.StartInfo.Arguments = $@"/C .\OA30\pcloa3return.cmd {orderId} wrapper";
                        ReturnKey.StartInfo.CreateNoWindow = !debug;
                        ReturnKey.StartInfo.RedirectStandardError = true;

                        ReturnKey.Start();
                        ReturnKey.WaitForExit();
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error While Returning", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static async Task UploadLogs(bool debug, string orderId)
        {
            if (IsReported)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var Upload = new Process();
                        Upload.StartInfo.FileName = "cmd.exe";
                        Upload.StartInfo.Arguments = @$"/C .\OA30\uploadAssemble.cmd {orderId} & "
                            + @$".\OA30\uploadReport.cmd C:\Temp\Data\Report.xml {orderId}";
                        Upload.StartInfo.CreateNoWindow = !debug;
                        Upload.StartInfo.RedirectStandardError = true;

                        Upload.Start();
                        Upload.WaitForExit();
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error Uploading", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public static async Task WriteLogAsync(string callerName, SystemInfo systemInfo)
        {
            // Logs information to a text file
            DateTime dateTime = DateTime.Now;

            if (!File.Exists(@".\InjectionLogs.csv"))
            {
                string header = "DATETIME,ACTION,ORDERID,MBMFR,MODEL,TOOL,OS,EDITION\n";
                using StreamWriter stream = new(@".\InjectionLogs.csv", append: true);
                await stream.WriteAsync(header);
            }

            StringBuilder line = new();

            line.Append(dateTime + ",");
            line.Append(callerName + ",");
            line.Append(systemInfo.OrderId + ",");
            line.Append(systemInfo.Baseboard.Manufacturer + ",");

            string scriptMFR = systemInfo.Baseboard.Manufacturer switch
            {
                Baseboard.MANUFACTURER.ASROCK => "1",
                Baseboard.MANUFACTURER.ASUS => "2",
                Baseboard.MANUFACTURER.ASUSZ690 => "22",
                Baseboard.MANUFACTURER.GIGABYTE => "3",
                Baseboard.MANUFACTURER.MSI => "4",
                Baseboard.MANUFACTURER.SAGER => "5",
                Baseboard.MANUFACTURER.SAGERH2O => "5h",
                Baseboard.MANUFACTURER.TONGFANG => "9",
                _ => "4",
            };

            line.Append(scriptMFR + ",");
            line.Append(systemInfo.OS.Version + ",");
            line.Append(systemInfo.OS.Edition + ",");

            using StreamWriter file = new(@".\InjectionLogs.csv", append: true);
            await file.WriteAsync(line);
        }

        public static async Task Reboot(bool prompt)
        {
            await Task.Run(() =>
            {
                if (prompt)
                {
                    MessageBoxResult result = MessageBox.Show(
                        "System restart is REQUIRED to finish the clear process. Would you like to reboot now?",
                        "System Restart Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation);

                    if (result == MessageBoxResult.Yes) { Process.Start("cmd.exe", "shutdown -r -t 0"); }
                }
                else
                {
                    Process.Start("cmd.exe", "shutdown -r -t 0");
                }
            });
        }
    }
}