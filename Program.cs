
using System;
using System.Collections;
using System.Net;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Diagnostics.Metrics;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;

ReadNetworkConfig config = new ReadNetworkConfig();
config.ReadNetworkConfigAsync(new CancellationToken(false));
config.show();

BlackBoard board = new BlackBoard(config);

board.Scan();





/// <summary>
/// Доска статусов обновляемое каждую минуту
/// </summary>
public class BlackBoard
{
    public List<InfoTable> list = new List<InfoTable>();
    public ReadNetworkConfig reader = null;
    public int scanners = 0;
    /// <summary>
    /// Показывает результаты скана
    /// </summary>
    public void ShowBlackBoard()
    {
        //Ожидалка
        Console.WriteLine($"Задачи запущены {scanners} | Целей {list.Count} ");
        do
        {

            Console.SetCursorPosition(0, 0);
            ConsoleColor c = Console.BackgroundColor;
            Console.BackgroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine($"Задачи запущены {scanners} | Целей {list.Count} | ESC = выход");
            Console.BackgroundColor = c;

            /// Console.SetCursorPosition(x, y);

            int i = 0;
            lock (list)
                list.ForEach(it =>
                {
                    try
                    {
                        //Выводим сообщения
                        string show_host = it.Address.ToString();

                        if (it.Host != null)
                        {
                            show_host = it.Host.Substring(0, it.Host.Length > 34 ? 35 : it.Host.Length);
                        } //host !=null
                        string status = it.status == IPStatus.Success ? "OK" : "Проблемы";
                        string speed = it.Speed.ToString();
                        if (speed == "-1") { speed = "∞"; }

                        try { Console.Write($"{show_host,-35} - {speed,5:G} Кб/c - {status,-10}| "); } catch { }
                        i++;
                        if (i % 3 == 0) { Console.WriteLine(); }


                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message + " >> " + ex.Source + " >> " + ex.Data);
                    }
                }
                );

            if (scanners > 0) { Thread.Sleep(10); }

            if (Console.KeyAvailable)
            {
                if (Console.ReadKey(false).Key == ConsoleKey.Escape)
                { break; }
            }

        } while ((scanners > 0) && (list.Count > 0));

        GC.Collect();
    } //Показ сканеров


    public async void Scan()
    {
        //загрузка данных
        Console.WriteLine("Сканирование ...");

        
        foreach (var ip in reader.working_machines)
        {
            try {
                Task task = new Task(
                       () =>
                       {
                          
                           InfoTable ifx = new InfoTable(ip);
                           lock (list) {
                                           ifx.MeasureSpeed();
                                           list.Add(ifx);
                                        
                                        }
                       });
                    task.Start();

                   }
            catch { }
            finally
            {
                Thread.Sleep(2);
            }
        }

      
        // Cycle scan
        scanners = 0;
        lock (list)
        {
            list.ForEach(itask =>
            {
                scanners++;
                Task task = new Task(
                    () =>
                    {
                        scanners++;
                        try
                        {
                            lock (list)
                            {
                                itask.MeasureSpeed();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                        finally
                        {
                            scanners--;
                        }
                    }); //task
                task.Start();
                Thread.Sleep(1);

            });//foraech
            }//lock

    } //scan


    public BlackBoard( ReadNetworkConfig r)
    {
        reader = r;
        Scan();
        ShowBlackBoard();

    }

    public BlackBoard()
    {
        reader = new ReadNetworkConfig();
        Scan(); 
        ShowBlackBoard() ;  

    } //class
}

/// <summary>
/// Сканер хоста
/// </summary>
public class InfoTable
{
    public string Host;
    public IPAddress Address;
    public  double Speed;
    public IPStatus status=IPStatus.BadDestination;

    public InfoTable  (string host )
    { 
        Host = host;
        Address = System.Net.Dns.Resolve(Host).AddressList.First();
    }

    public InfoTable(IPAddress address)
    {
        Address = address;
        try { Host = System.Net.Dns.GetHostByAddress(Address).HostName; }
        catch 
        {
            Address = System.Net.Dns.Resolve(address.ToString()).AddressList.First();
        }
    }
    /// <summary>
    /// Измерение скорости
    /// </summary>
    public async Task<double> MeasureSpeed()
    {
        byte[] buffer = new byte[10000];
        Ping p = new Ping();
        try
        {
            PingReply r =  await p.SendPingAsync(Address,100, buffer);
            status = r.Status;
            if (r.RoundtripTime == 0) { Speed = -1; }
            else
            {
                Speed = buffer.Length / (r.RoundtripTime); // DIV 0!!!
            }
        }
        catch {
            Speed = -1; //error
                }
        finally
        {
            p.Dispose();
        }

        return (Speed);
    }
}

public class ReadNetworkConfig
{

    public List<IPAddress> gateways = new List<IPAddress>();
    public List<IPAddress> dns_servers = new List<IPAddress>();
    public List<IPAddress> dhcp_servers = new List<IPAddress>();
    public List<IPAddress> working_machines = new List<IPAddress>();

    public bool active_scan = false;
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
    async public void ReadNetworkConfigAsync(CancellationToken token)
    {
        active_scan = true;
        foreach (var i in gateways)
        {
            byte[] tmp = i.GetAddressBytes();
            if (i.IsIPv6SiteLocal) continue;
            if (i.IsIPv6Multicast) continue;
            if (i.IsIPv6LinkLocal) continue;
            if (i.IsIPv6UniqueLocal) continue;
            if (i.ToString() == "::") continue;
            if (i.ToString() == ":") continue;


            for (int j = 1; j < 255; j++)
            {
                tmp[3] = (byte)j;
                IPAddress ip = new IPAddress(tmp);
                Console.Write($"Проверка {ip} ... \r");
                working_machines.Add(ip);

            } // for

        }
        
        //убираем задвоенные
        working_machines = working_machines.Distinct().ToList();    
    }

    public void show()
    {
        gateways.ForEach(x =>
        {
            try
            {
                Console.WriteLine($" Шлюз: {x.ToString()}");
            }
            catch { }
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

} //class


