
using System;
using System.Collections;
using System.Net;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Diagnostics.Metrics;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Dynamic;
using System.Net.Cache;

Console.OutputEncoding = System.Text.Encoding.UTF8;

ReadNetworkConfig config = new ReadNetworkConfig();
config.ReadNetworkConfigAsync(new CancellationToken(false));
config.show();
Console.Title = "Сетевое окружение";

BlackBoard board = new BlackBoard(config);



/// <summary>
/// Доска статусов обновляемое каждую минуту
/// </summary>
public class BlackBoard
{
    public List<InfoTable> list = new List<InfoTable>();
    public ReadNetworkConfig reader = null;
    public int scanners = 0;
    public bool exit = false;
    public double inet_speed = 0.0f;

    public double GetInternetSpeed()
    {
        HttpClient client = new HttpClient();
        int sum = 0;
        Stopwatch watch = Stopwatch.StartNew();
        for (int i = 0; i < 20; i++)
        {
            string s = "";
            if (i % 3 == 0)
            { try { s = client.GetStringAsync("https://google.com/").Result; } catch (Exception ex) { Debug.WriteLine(ex.Message); } }

            if (i % 3 == 1)
            {
                try { s = client.GetStringAsync("https://yandex.ru/").Result; } catch (Exception ex) { Debug.WriteLine(ex.Message); }
            }

            if (i % 3 == 2)
            {
                try { s = client.GetStringAsync("https://microsoft.com/").Result; } catch (Exception ex) { Debug.WriteLine(ex.Message); }
            }
            sum += s.Length;
        }
        watch.Stop();
        double inet_speed = sum / (watch.ElapsedMilliseconds);
        return inet_speed;

    }


    /// <summary>
    /// Показывает результаты скана
    /// </summary>
    public void ShowBlackBoard()
    {
        //Ожидалка

        Console.SetCursorPosition(0, 0);

        Console.WriteLine($"Задачи запущены {scanners} | Целей {list.Count} ");
        if (list.Count > 0)
            do
            {

                Console.SetCursorPosition(0, 0);
                ConsoleColor c = Console.BackgroundColor;
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.WriteLine($"Задачи запущены {scanners} │ Целей {list.Count} │ Интернет {this.inet_speed} Кб/с │ ESC = выход  │ F1 = очистка окна                             \r");
                Console.BackgroundColor = c;
                Console.WriteLine($"{"  Хост",-20} {" ",-18} -{"Скорость Мин. Текущее",15} ~ {" Макс ",5} ~ {"Отклик, мс",13}  {" ",5} ║ ");

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
                                show_host = it.Host.Substring(0, it.Host.Length > 25 ? 25 : it.Host.Length);
                            } //host !=null
                            string status = it.status == IPStatus.Success ? "OK" : "!!";
                            string speed = it.Speed.ToString();
                            if (speed == "-1") { speed = "∞"; }

                            string Http = "";
                            if (it.flag_http) { Http += "HTTP"; }
                            if (it.flag_https)
                            {
                                if (Http != "") { Http += "/"; }
                                Http += "HTTPS";

                            }
                            if (Http == "")
                            { Http = "UnCHK"; }

                            if ((it.status == IPStatus.Success) || (it.Host != null))
                            {
                                try { Console.Write($"{show_host,-25} {Http,-13} -{it.min_Speed,7:G} ~ {speed,7:G} ~ {it.max_Speed,7:G} Кб/c - {it.ping_time,3} мс - {status,-10}║ "); } catch { }
                                i++;
                                if (i % (Console.WindowWidth / 93) == 0) { Console.WriteLine(); }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message + " >> " + ex.Source + " >> " + ex.Data);
                        }
                    }
                    ); //list each

                if (scanners > 0) { Thread.Sleep(100); }

                if (Console.KeyAvailable)
                {
                    if (Console.ReadKey(!false).Key == ConsoleKey.Escape)
                    {
                        exit = true;
                        break;
                    }
                    if (Console.ReadKey(!false).Key == ConsoleKey.F1)
                    {
                        Console.Clear();

                        break;
                    }
                }

                Thread.Sleep(100);
            } while ((scanners > 0) && (list.Count > 0));

        GC.Collect();

    } //Показ сканеров



    public async void Refresh()
    {
        // Cycle scan
        scanners = 0;
        lock (list)
        {
            list.ForEach(itask =>
            {
                if (itask.active) scanners++;
            });//list
        }//lock

    }

    public async void Scan()
    {
        //загрузка данных
        Console.WriteLine("Сканирование ...");

        foreach (var ip in reader.working_machines)
        {
            try
            {
                Task task = new Task(
                       () =>
                       {
                           InfoTable ifx = new InfoTable(ip);
                           //ifx.CheckWeb();
                           Thread.Sleep(2400);

                           lock (list)
                           {

                               list.Add(ifx);
                               Task.Delay(19);
                           }
                       });
                task.Start();

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                // Thread.Sleep(2);
            }
        }//foreach

    } //scan


    public BlackBoard(ReadNetworkConfig? r)
    {

        if (r == null)
        { reader = new ReadNetworkConfig(); }
        else
        { reader = r; }


        inet_speed = GetInternetSpeed();


        //Поиск сетей
        Scan();
        exit = false;
        int tx = 0;
        do
        {
            tx = (tx + 1) % 50 + 1;
            if (tx == 2)
            {
                ThreadStart ts = new ThreadStart(delegate () { inet_speed = GetInternetSpeed(); });
                Thread t = new Thread(ts);
                t.Start();

                Console.Clear();
            }

            ShowBlackBoard();
            Thread.Sleep(1000);
        } while (!exit);





    }//DeskTOp

}//class

/// <summary>
/// Сканер хоста
/// </summary>
public class InfoTable
{
    public string Host;
    public IPAddress Address;
    public double Speed = 0;
    public double inet_Speed = 0;

    public int ping_time = 0;

    public double max_Speed = 0;
    public double min_Speed = 0;

    public bool flag_http = false;
    public bool flag_https = false;

    public IPStatus status = IPStatus.BadDestination;
    private bool locked = false;
    public bool active = false;

    private bool _once_url_check = false;


    public void Stop()
    {
        locked = true;
        active = false;
    }


    public async Task<bool> CheckWeb()
    {

        //if (this.status != IPStatus.Success) { return; }
        flag_http = !true;
        int read = 0;
        Task.Delay(10);

        string prefix = "http://";


        for (int i = 1; i < 3; i++)
        {
            if (i == 2) { prefix = "https://"; }

            Stopwatch stopwatch = new Stopwatch();
            Uri url = new Uri(prefix + this.Address.ToString() + "/");

            using (HttpClient request = new HttpClient())
            {
                bool flag = false;
                try
                {
                    request.Timeout = TimeSpan.FromSeconds(5);
                    string data = await request.GetStringAsync(url);
                    read = data.Length;
                    flag = true;

                }
                catch (Exception ex)
                {
                    flag = !true;
                }
                finally
                {
                    stopwatch.Stop();
                    request.Dispose();

                    if ((stopwatch.ElapsedMilliseconds > 0) && (flag_http))
                    {
                        Speed = (Speed + read / stopwatch.ElapsedMilliseconds) / 2;
                    }

                    if (i == 1) flag_http = flag;
                    if (i == 2) flag_https = flag;
                }

            }//using
        }


        return flag_http;

    }

    public InfoTable(string host)
    {
        Host = host;
        Address = System.Net.Dns.Resolve(Host).AddressList.First();
        CoreTasker();

    }

    public InfoTable(IPAddress address)
    {
        Address = address;
        try { Host = System.Net.Dns.GetHostByAddress(Address).HostName; }
        catch
        {
            Address = System.Net.Dns.Resolve(address.ToString()).AddressList.First();

        }
        CoreTasker();

    }

    /// <summary>
    /// Главный датчик
    /// </summary>
    public void CoreTasker()
    {
        CheckWeb();
        // double x2= await MeasureSpeed(new CancellationToken(false));
    }


    /// <summary>
    /// Измерение скорости по Ping
    /// </summary>
    public async Task<double> MeasureSpeed(CancellationToken c)
    {
        //защита от перегруза

        active = true;
        if (locked) { return (Speed); } else { locked = true; }

        int i = 1;
        Ping p = new Ping();
        try
        {
            //постоянный скан
            do
            {
                byte[] buffer = new byte[i * 100];
                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                PingReply r = await p.SendPingAsync(Address, 1000, buffer);
                stopwatch.Stop();

                ping_time = ((int)r.RoundtripTime + ping_time) / 2;

                status = r.Status;
                double _speed = 0.0f;
                if (r.RoundtripTime == 0)
                {
                    i = (i % 32) + 1;//от 100...500 байт
                    //_speed = (Speed + buffer.Length / (stopwatch.ElapsedMilliseconds + 1)) / 2;
                }
                else
                {
                    _speed = (Speed + buffer.Length / (r.RoundtripTime)) / 2; // DIV 0!!!
                }


                Speed = Math.Round(_speed, 2);

                if (max_Speed < Speed) { max_Speed = Speed; }
                if ((min_Speed > Speed) || (min_Speed == 0)) { min_Speed = Speed; }

                Thread.Sleep(600);

            } while ((!c.IsCancellationRequested) || (locked));
        }
        catch
        {
            Speed = -1; //error
            ping_time = -1;
        }
        finally
        {
            Speed = Math.Round(Speed, 2);
            p.Dispose();
            locked = false;
        }
        return (Speed);
    }
}//class

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

        List<IPAddress> data_base = new List<IPAddress>();
        data_base.AddRange(gateways.ToArray());
        data_base.AddRange(dns_servers.ToArray());
        data_base.AddRange(dhcp_servers.ToArray());
        data_base = data_base.Distinct().ToList();

        foreach (var i in data_base)
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
                //Console.Write($"Проверка {ip} ... \r");
                working_machines.Add(ip);

            } // for

        }

        //убираем задвоенные
        working_machines = working_machines.Distinct().ToList();
        working_machines.Insert(0, IPAddress.Parse("1.1.1.1"));
        working_machines.Insert(0, IPAddress.Parse("8.8.8.8"));

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
