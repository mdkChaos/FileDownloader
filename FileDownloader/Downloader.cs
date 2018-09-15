using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
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

        public async Task GetFileAsync(string url, string path, CancellationTokenSource cts)
        {
            await Task.Run(() => DownloadFileAsync(url, path, cts));
        }

        async Task GetPartFileAsync(string url, long start, long end, string pathFileName, CancellationTokenSource cts)
        {
            await Task.Run(() => DownloadPartFileAsync(url, start, end, pathFileName, cts));
        }

        async void DownloadPartFileAsync(string url, long start, long end, string pathFileName, CancellationTokenSource cts)
        {
            int newByte = 131072;
            byte[] inBuf = new byte[newByte];
            int bytesReadTotal = 0;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.AddRange(start, end);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream result = response.GetResponseStream())
                {

                    using (FileStream file = new FileStream(pathFileName, FileMode.Create, FileAccess.Write))
                    {
                        int n;
                        try
                        {
                            while (true)
                            {
                                n = result.Read(inBuf, 0, newByte);
                                if (n <= 0)
                                {
                                    break;
                                }

                                await file.WriteAsync(inBuf, 0, n, cts.Token);

                                bytesReadTotal += n;
                                await MainWindow.progressBar.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate ()
                                {
                                    MainWindow.progressBar.Value = (double)bytesReadTotal * 100 / response.ContentLength;
                                }));
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            await MainWindow.outputText.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate ()
                            {
                                MainWindow.outputText.Text += $"File \"{pathFileName.Split('\\').Last()}\" download canceled.\n";
                            }));
                            await MainWindow.progressBar.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate ()
                            {
                                MainWindow.progressBar.Value = 0;
                            }));
                        }
                        catch (Exception)
                        {
                            await MainWindow.outputText.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate ()
                            {
                                MainWindow.outputText.Text += $"Download failed.\n";
                            }));
                        }
                    }
                    await MainWindow.outputText.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(delegate ()
                    {
                        MainWindow.outputText.Text += $"File \"{pathFileName.Split('\\').Last()}\" downloaded. Total size {bytesReadTotal}\n";
                    }));
                }
            }
        }

        async void DownloadFileAsync(string url, string path, CancellationTokenSource cts)
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
                        MainWindow.outputText.Text += $"Download file \"{fileName}\" started.\n";
                    }));

                    await GetPartFileAsync(url, 0, fileSize, path + "\\" + fileName, cts);
                }
                else
                {
                    Parallel.For(0, nums, async i =>
                    {
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
                        await GetPartFileAsync(url, start, end, partFileName, cts);
                    });
                }
            }
        }
    }
}