using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ChatTCP_Server
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ServerObject.eventMessage += ServerObject_eventMessage;

            ClientObject.eventMessage += ClientObject_eventMessage;

            Task.Run(() => ServerStart());

            //ServerStart();
        }

        private void ClientObject_eventMessage(object sender, string e)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                Messager.Items.Add(e.ToString());
            }));
        }

        private void ServerObject_eventMessage(object sender, string e)
        {
            this.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() =>
            {
                Messager.Items.Add(e.ToString());
            }));  
        }

        async void ServerStart()
        {
            ServerObject server = new ServerObject();// создаем сервер
            await server.ListenAsync(); // запускаем сервер
        }
    }
}
