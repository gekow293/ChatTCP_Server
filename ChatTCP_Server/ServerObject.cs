using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;

namespace ChatTCP_Server
{
    public class ServerObject
    {
        public static event EventHandler<string> eventMessage;

        private readonly object syncFileObj = new object();

        TcpListener tcpListener = new TcpListener(IPAddress.Any, 8888); // сервер для прослушивания
        List<ClientObject> clients = new List<ClientObject>(); // все подключения
        protected internal void RemoveConnection(string id)
        {
            // получаем по id закрытое подключение
            ClientObject client = clients.FirstOrDefault(c => c.Id == id);
            // и удаляем его из списка подключений
            if (client != null) clients.Remove(client);
            client?.Close();
        }
        // прослушивание входящих подключений
        protected internal async Task ListenAsync()
        {
            try
            {
                tcpListener.Start();

                eventMessage?.Invoke(null, "Сервер запущен. Ожидание подключений...");

                while (true)
                {
                    TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();

                    ClientObject clientObject = new ClientObject(tcpClient, this);
                    clients.Add(clientObject);

                    await BroadcastListUsersAsync(clientObject.Id);

                    _ = Task.Run(() => clientObject.ProcessAsync());
                }
            }
            catch (Exception ex)
            {
                eventMessage?.Invoke(null, ex.Message);
            }
            finally
            {
                Disconnect();
            }
        }

        // отправка контактов клиенту остальных клиентов в чате
        protected internal async Task BroadcastListUsersAsync(string id)
        {
            string listUsers = "ListUsersAfterIncoming:";

            foreach (var client in clients)
                if(client.Id != id)
                    listUsers += "/" + client.Name;
                
            foreach (var client in clients)   
                if (client.Id == id)
                {            
                    await client.Writer.WriteLineAsync(listUsers); //передача данных
                    await client.Writer.FlushAsync();
                } 
        }

        // трансляция сообщения подключенным клиентам
        protected internal async Task BroadcastMessageAsync(string message, string id, string destUser)
        {
            foreach (var client in clients)   
                if (client.Id != id) // если id клиента не равно id отправителя    
                    if (destUser == "Всем")// общая рассылка
                    {
                        await client.Writer.WriteLineAsync(message); //передача данных
                        await client.Writer.FlushAsync();
                    }
                    else if (client.Name == destUser)// отправка выбранному пользователю
                    {
                        await client.Writer.WriteLineAsync(message); //передача данных
                        await client.Writer.FlushAsync();
                    }
        }

        // трансляция файла подключенным клиентам
        protected internal async Task BroadcastFilesAsync(string fileName, string userName, string id, string destUser)
        {
            try
            {
                foreach (var client in clients)
                    if (client.Id != id) // если id клиента не равно id отправителя
                        if(destUser == "Всем")
                            await SendFileToClientAsync(client, fileName, userName);// общая рассылка
                        else if(client.Name == destUser)// отправка выбранному пользователю
                            await SendFileToClientAsync(client, fileName, userName);     
            }
            catch (Exception e)
            {
                eventMessage?.Invoke(null, e.Message);
            }
        }

        // отправка файла клиенту
        protected internal async Task SendFileToClientAsync(ClientObject client, string fileName, string userName)
        {
            FileInfo fi = new FileInfo(fileName);

            int bufferSize = 1024;
            byte[] buffer = null;
            byte[] header = null;

            FileStream fs = null;
            lock (syncFileObj)
                fs = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            int bufferCount = Convert.ToInt32(Math.Ceiling((double)fs.Length / (double)bufferSize));

            string headerStr = "Content-length:" + fs.Length.ToString() + "$Filename:" + fi.Name + "$UserName:" + userName + "\r\n";
            header = new byte[bufferSize];
            Array.Copy(Encoding.Default.GetBytes(headerStr), header, Encoding.Default.GetBytes(headerStr).Length);

            await client.stream.WriteAsync(header, 0, header.Length);
            await client.stream.FlushAsync();

            for (int i = 0; i < bufferCount; i++)
            {
                buffer = new byte[bufferSize];
                int size = fs.Read(buffer, 0, bufferSize);

                await client.stream.WriteAsync(buffer, 0, buffer.Length);
            }

            await client.stream.FlushAsync();

            fs.Close();
        }

        // отключение всех клиентов
        protected internal void Disconnect()
        {
            foreach (var client in clients)
            {
                client.Close(); //отключение клиента
            }
            tcpListener.Stop(); //остановка сервера
        }
    }
}
