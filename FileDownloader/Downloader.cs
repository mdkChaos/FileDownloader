using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FileDownloader
{
    class Downloader
    {
        public Downloader(MainWindow mainWindow)
        {
            MainWindow = mainWindow;
        }

        public MainWindow MainWindow { get; set; }

        public async Task GetFile(string url, string path)
        {
            await Task.Run(() => DownloadFile(url, path));
        }

        async void DownloadFile(string url, string path)
        {
            int nums = 4; //количество потоков
            long start, end;
            int part;
            string fileName;
            int newByte = 131072;
            byte[] inBuf = new byte[newByte];
            long fileSize;
            bool seek;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                fileName = response.ResponseUri.AbsoluteUri.Split('/').Last();
                fileSize = response.ContentLength;
                part = (int)(fileSize / nums);
                seek = response.GetResponseStream().CanSeek;
                await MainWindow.outputText.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate ()
                {
                    MainWindow.outputText.Text += $"File name: {fileName}\n";
                    MainWindow.outputText.Text += $"File size: {fileSize}\n";
                    MainWindow.outputText.Text += $"Seek: {seek}\n";
                }));
                if (!seek)
                {
                    await MainWindow.outputText.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate ()
                    {
                        MainWindow.outputText.Text += "Start download file.\n";
                    }));
                    using (Stream result = response.GetResponseStream())
                    {
                        int bytesReadTotal = 0;

                        using (FileStream file = new FileStream(path + "\\" + fileName, FileMode.Create, FileAccess.Write))
                        {
                            int n;
                            while (true)
                            {
                                n = result.Read(inBuf, 0, newByte);
                                if (n <= 0)
                                {
                                    break;
                                }

                                file.Write(inBuf, 0, n);

                                bytesReadTotal += n;
                                await MainWindow.progressBar.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate ()
                                {
                                    MainWindow.progressBar.Value = (double)bytesReadTotal * 100 / fileSize;
                                }));
                            }
                        }
                        await MainWindow.outputText.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate ()
                        {
                            MainWindow.outputText.Text += $"File \"{fileName}\" downloaded. Total size {bytesReadTotal / 1024} KB\n";
                        }));
                    }
                }
                else
                {
                    //List<Task> tasks = new List<Task>();
                    Parallel.For(0, nums, i =>
                    {
                        //Console.WriteLine(new string('-', 20));
                        //Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} Started");

                        start = i * part + i;
                        if (i == nums - 1)
                        {
                            end = fileSize;
                        }
                        else
                        {
                            end = start + part;
                        }

                        string partFileName = path + "\\" + fileName + "." + i + ".tmp";
                        request = (HttpWebRequest)WebRequest.Create(MainWindow.url.Text);
                        request.AddRange(start, end);
                        HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse();
                        Stream str = webResponse.GetResponseStream();
                        int bytesReadTotal = 0;

                        FileStream fstr = new FileStream(partFileName, FileMode.Create, FileAccess.Write);

                        while (true)
                        {
                            int n = str.Read(inBuf, 0, newByte);
                            if (n <= 0)
                            {
                                //Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} Finished");
                                break;
                            }

                            fstr.WriteAsync(inBuf, 0, n);

                            bytesReadTotal += n;
                        }

                        //Console.WriteLine($"Thread: {Thread.CurrentThread.ManagedThreadId} download: {bytesReadTotal} byte");
                        //Console.WriteLine(new string('-', 20));
                        //var t = Task.Run(() => ThreadDownloadFileAsync(vars));
                        //tasks.Add(t);
                    });
                    //    var t = Task.Run(() => ThreadDownloadFileAsync(vars));
                    //    tasks.Add(t);
                    //}
                    //Task.WaitAll(tasks.ToArray());
                }
            }
        }
    }
}
