
using System;
using System.Collections;
using System.Net;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;

ReadNetworkConfig config = new ReadNetworkConfig();
config.ReadNetworkConfigAsync(new CancellationToken(false));
while (config.active_scan) { Thread.Sleep(10);  }

config.show();


class ReadNetworkConfig
{

    public List<IPAddress> gateways = new List<IPAddress>();
    public List<IPAddress> dns_servers = new List<IPAddress>();
    public List<IPAddress> dhcp_servers = new List<IPAddress>();
    public List<IPAddress> working_machines = new List <IPAddress>();

    public bool  active_scan =false;
    public int scanners = 0; 

    /// <summary>
    /// Конструктор
    /// </summary>
    public ReadNetworkConfig()
    {
        gateways.Clear();
        dns_servers.Clear();
        dhcp_servers.Clear();
        working_machines.Clear();

        active_scan = false;
        scanners = 0;

        foreach (System.Net.NetworkInformation.NetworkInterface iface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            Console.WriteLine($" Сеть {iface.Description}: ");
            dhcp_servers.AddRange(iface.GetIPProperties().DhcpServerAddresses.ToArray<IPAddress>());
            dns_servers.AddRange(iface.GetIPProperties().DnsAddresses.ToArray<IPAddress>());

            foreach (var p in iface.GetIPProperties().GatewayAddresses.ToArray())
            { if (!p.Address.IsIPv6SiteLocal) gateways.Add(p.Address); }
        }

    }
    async  public void  ReadNetworkConfigAsync(CancellationToken token) 
    {
            active_scan = true;
            
            foreach (var i in gateways)
            {
                byte[] tmp = i.GetAddressBytes();
                if (i.IsIPv6SiteLocal) continue;
                if (i.IsIPv6Multicast) continue;
                if (i.IsIPv6LinkLocal) continue;
                if (i.IsIPv6UniqueLocal) continue;
                if (i.ToString()=="::") continue;
                if (i.ToString() == ":") continue;


            for (int j = 1; j<255;j++)
                {
                    tmp[3] =(byte) j;
                    IPAddress ip = new IPAddress(tmp);
                    Console.Write($"Проверка {ip} ... \r");
                    working_machines.Add(ip);

                } // for
                
            }

        scanners = 0;
        foreach (var ip in working_machines)
        {
            scanners++;
            Task DOX = Task.Factory.StartNew(() =>
            {

                string host = "";
                try
                {
                    
                    Console.Write($"Проверка: {scanners:2} | {ip:50} ...  | \r ");
                    Ping ping = new Ping();

                    PingReply ping_res = ping.Send(ip, 120, new byte[2024]);

                    if (ping_res.Status == IPStatus.Success)
                    {
                      //  Console.WriteLine($" +PING | ");
                        try
                        {
                            IPHostEntry entry = System.Net.Dns.GetHostEntry(ip.ToString());
                            host = entry.HostName;
                            double speed = Math.Round((double)ping_res.Buffer.Length / (double)(ping_res.RoundtripTime + 1), 2);
                            Console.Write($"скорость  = {speed:4} кБайт/с ... {ip} {host}  ");
                        }
                        catch

                        {
                        }

                    }
                    else
                    {
                       // Console.WriteLine($" -PING | ");
                        host = null;
                        if ((ping_res.Status == IPStatus.TimedOut) || (ping_res.Status == IPStatus.BadRoute))
                        { host = null; }
                    }

                }
                catch
                { host = null; }
                finally
                {
                    scanners--;
                }

                //если нет имени - просто ip
                if (host != null)
                {
                    Console.WriteLine($" >> {host:50}");
                }


                Thread.Sleep(1);
                if (scanners > 0)
                { active_scan = true; }
                else
                { active_scan = false; }


                //Console.WriteLine($" >> scan = {scanners:2}");
                
            }); //DOX

            
           

        } // foreach
        //---скан подсетки
        
    }

    public void show()
    {
        gateways.ForEach(x => 
            { 
                    try { 
                          Console.WriteLine($" Шлюз: {x.ToString()}"); 
                        } catch { }
            });

        dhcp_servers.ForEach(x =>
        {
            try
            {
                Console.WriteLine($" DHCP: {x.ToString()}");
            }
            catch { }
        });

        dns_servers.ForEach(x =>
        {
            try
            {
                Console.WriteLine($" DNS: {x.ToString()}");
            }
            catch { }
        });


    }

}




