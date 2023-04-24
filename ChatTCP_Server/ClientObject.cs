using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatTCP_Server
{
    public class ClientObject
    {
        public static event EventHandler<string> eventMessage;
        protected internal string Id { get; } = Guid.NewGuid().ToString();
        protected internal StreamWriter Writer { get; }
        protected internal StreamReader Reader { get; }
        public string DestUser { get; set; }
        public string Name { get; set; }

        protected internal NetworkStream stream { get; }

        private readonly object syncFileObj = new object();

        public TcpClient client;
        ServerObject server; // объект сервера

        public ClientObject(TcpClient tcpClient, ServerObject serverObject)
        {
            client = tcpClient;
            server = serverObject;
            // получаем NetworkStream
            stream = client.GetStream();
            // создаем StreamReader для чтения данных
            Reader = new StreamReader(stream, Encoding.Default);
            // создаем StreamWriter для отправки данных
            Writer = new StreamWriter(stream, Encoding.Default);
        }

        // процесс работы сервера
        public async Task ProcessAsync()
        {
            try
            {
                // получаем имя пользователя
                string userName = await Reader.ReadLineAsync();
                Name = userName;
                string message = $"{userName} вошел в чат";
                // посылаем сообщение о входе в чат всем подключенным пользователям
                await server.BroadcastMessageAsync(message, Id, "Всем");

                // вывод сообщения о  вхождении нового клиента в чат
                eventMessage?.Invoke(null, message);

                // в бесконечном цикле получаем сообщения от клиента
                while (true)
                {
                    try
                    {
                        message = await Reader.ReadLineAsync();
                        if (message == null) continue;

                        // если получен файл
                        if(message.Contains("Content-length:") && message.Contains("Filename:"))
                        {
                            var filePath = await ReceiveFileAsync(stream, message);
                            // расслылаем всем или единственному клиенту
                            await server.BroadcastFilesAsync(filePath, userName, Id, DestUser);
                        }
                        else
                        {
                            string[] splitted = message.Split(new string[] { "&&&" }, StringSplitOptions.None);
                            DestUser = splitted[0];
                            message = splitted[1];

                            message = $"{userName}: {message}";
                            eventMessage?.Invoke(null, message);

                            await server.BroadcastMessageAsync(message, Id, DestUser);
                        }
                    }
                    catch (Exception e)
                    {
                        message = $"{userName} покинул чат";
                        eventMessage?.Invoke(null, message);

                        await server.BroadcastMessageAsync(message, Id, DestUser);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                // сообщение о сбое
                eventMessage?.Invoke(null, e.Message);
            }
            finally
            {
                // в случае выхода из цикла закрываем ресурсы
                server.RemoveConnection(Id);
            }
        }
        // закрытие подключения
        protected internal void Close()
        {
            Writer.Close();
            Reader.Close();
            client.Close();
        }


        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            throw new NotImplementedException();
        }


        private async Task<string> ReceiveFileAsync(NetworkStream stream, string headMessage)
        {
            try
            {
                int bufferSize = 1024;
                byte[] buffer = null;
                string filename = "";
                string username = "";
                //string destname = "";
                int filesize = 0;

                string[] splitted = headMessage.Split(new string[] { "$" }, StringSplitOptions.None);
                Dictionary<string, string> headers = new Dictionary<string, string>();
                foreach (string s in splitted)
                {
                    if (s.Contains(":"))
                    {

                        if (s.Contains("Content-length:"))
                        {
                            var f = s.Substring(s.IndexOf("C"));

                            headers.Add(f.Substring(0, f.IndexOf(":")), f.Substring(f.IndexOf(":") + 1));
                        }
                        else headers.Add(s.Substring(0, s.IndexOf(":")), s.Substring(s.IndexOf(":") + 1));
                    }
                }

                filesize = Convert.ToInt32(headers["Content-length"]);
             
                filename = headers["Filename"];

                username = headers["UserName"];

                DestUser = headers["DestUser"];

                int bufferCount = Convert.ToInt32(Math.Ceiling((double)filesize / (double)bufferSize));

                var pathFile = Environment.CurrentDirectory + "\\files\\";

                FileStream fs = null;
                lock (syncFileObj)
                    fs = new FileStream(pathFile + filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                while (filesize > 0)
                {
                    buffer = new byte[bufferSize];

                    int size = await stream.ReadAsync(buffer, 0, buffer.Length);

                    fs.Write(buffer, 0, size);

                    filesize -= size;
                }

                await stream.FlushAsync();

                eventMessage?.Invoke(null, username + ": Получен файл: " + filename + "\r\n     В папке: " + pathFile);

                fs.Close();

                return pathFile + filename;
            }
            catch (Exception e)
            {
                eventMessage?.Invoke(null, e.Message);
            }
            return null;
        }
    }
}
