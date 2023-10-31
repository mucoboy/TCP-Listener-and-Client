using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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

namespace TCP_Listener_and_Client
{
    /// <summary>
    /// MainWindow.xaml etkileşim mantığı
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpListener listener;
        bool windowClosed = false;
        Dictionary<string, NetworkStream> connectedClients; //to select client from combobox
        ObservableCollection<string> clientList;
        object clientLock = new object(); //for thread safety
        NetworkStream clientStream;
        public MainWindow()
        {
            InitializeComponent();

            connectedClients = new Dictionary<string, NetworkStream>();
            clientList = new ObservableCollection<string>();
            serverCombo.ItemsSource = clientList;
        }

        private void listenButton_Click(object sender, RoutedEventArgs e)
        {
            if (ushort.TryParse(serverPortTextBox.Text, out ushort port))
            {
                new Thread(()=> {

                    try
                    {
                        //listener for any adapter on this port
                        listener = new TcpListener(IPAddress.Any, port);
                        listener.Start();
                        enableDisableServer(true);
                        serverRichTextBox.Dispatcher.Invoke(()=>serverRichTextBox.AppendText("listener started on port: " + port.ToString() + "\n"));

                        try
                        {
                            while (!windowClosed)
                            {
                                var client = listener.AcceptTcpClient();
                                var name = client.Client.RemoteEndPoint.ToString();
                                serverRichTextBox.Dispatcher.Invoke(()=>serverRichTextBox.AppendText(name + " connected!\n"));
                                lock (clientLock)
                                {
                                    serverCombo.Dispatcher.Invoke(()=> {
                                        clientList.Add(name);
                                        if (serverCombo.SelectedIndex < 0)
                                            serverCombo.SelectedIndex = 0;
                                    });
                                }

                                //each thread for each client
                                new Thread(()=>startClient(client,name)).Start();
                            }
                        }
                        catch (Exception)
                        {

                            serverRichTextBox.Dispatcher.Invoke(()=>serverRichTextBox.AppendText("listener stopped!\n"));
                        }

                    }
                    catch (Exception ex)
                    {

                        MessageBox.Show("Listener can not started! Ex: " + ex.ToString());
                    }
                }).Start();
            }

            else
                MessageBox.Show("Invalid Port Number!");
        }

        void startClient(TcpClient client, string name)
        {
            try
            {
                var stream = client.GetStream();
                lock (clientLock)
                {
                    connectedClients.Add(name, stream);
                }
                var buffer = new byte[10000];
                int length = 0;

                while ((length = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    serverRichTextBox.Dispatcher.Invoke(() => serverRichTextBox.AppendText("RX: " + Encoding.UTF8.GetString(buffer, 0, length) + "\n"));
                }

                //if we are out of while loop, it means stream closed
                lock (clientLock)
                {
                    if (connectedClients.ContainsKey(name))
                        connectedClients.Remove(name);
                    serverCombo.Dispatcher.Invoke(()=> clientList.Remove(name));
                }

                serverRichTextBox.Dispatcher.Invoke(()=>serverRichTextBox.AppendText(name + " disconnected!\n"));
                
            }
            catch (Exception)
            {

                //if we are out of while loop, it means stream closed
                lock (clientLock)
                {
                    if (connectedClients.ContainsKey(name))
                        connectedClients.Remove(name);
                    serverCombo.Dispatcher.Invoke(() => clientList.Remove(name));
                }

                serverRichTextBox.Dispatcher.Invoke(() => serverRichTextBox.AppendText(name + " disconnected!\n"));
            }
        }

        //when tcp listener is started enable components, otherwise disable
        void enableDisableServer(bool eD)
        {
            Dispatcher.Invoke(()=> {
                listenButton.IsEnabled = !eD;
                stopButton.IsEnabled = eD;
                serverCombo.IsEnabled = eD;
                serverRichTextBox.IsEnabled = eD;
                
            });
        }

        //when client is connected to server enable components, otherwise disable
        void enableDisableClient(bool eD)
        {
            Dispatcher.Invoke(() => {
                connectButton.IsEnabled = !eD;
                disconnectButton.IsEnabled = eD;
                clientTextBox.IsEnabled = eD;
                clientRichTextBox.IsEnabled = eD;

            });
        }

        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (listener != null)
                {
                    listener.Stop();
                }
                enableDisableServer(false);

                lock (clientLock)
                {
                    foreach (var item in clientList)
                    {
                        if (connectedClients.TryGetValue(item, out NetworkStream stream))
                            stream.Close();
                    }
                    clientList.Clear();
                }
            }
            catch (Exception)
            {

                MessageBox.Show("Listener can not stopped!");
            }
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            if (IPAddress.TryParse(clientIpTextBox.Text, out IPAddress ip) && ushort.TryParse(clientPortTextBox.Text, out ushort port))
            {
                //if we don't use thread, window freezes
                new Thread(()=> {

                    try
                    {
                        TcpClient client = new TcpClient();

                        if (client.ConnectAsync(ip, port).Wait(1000))
                        {
                            enableDisableClient(true);
                            clientRichTextBox.Dispatcher.Invoke(()=>clientRichTextBox.AppendText("connected to server!\n"));
                            clientStream = client.GetStream();
                            int length = 0;
                            var buffer = new byte[10000];

                            while ((length = clientStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                clientRichTextBox.Dispatcher.Invoke(() => clientRichTextBox.AppendText("RX: " + Encoding.UTF8.GetString(buffer, 0, length) + "\n"));
                            }

                            clientRichTextBox.Dispatcher.Invoke(() => clientRichTextBox.AppendText("connection closed!\n"));
                            enableDisableClient(false);
                        }

                        else
                            MessageBox.Show("Can not connected!");
                    }
                    catch (Exception)
                    {

                        clientRichTextBox.Dispatcher.Invoke(()=>clientRichTextBox.AppendText("connection closed!\n"));
                    }

                }).Start();
            }

            else
                MessageBox.Show("Invalid IP Address or PORT Number!");
        }

        private void serverCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            serverTextBox.IsEnabled = serverCombo.SelectedIndex >= 0 ? true : false;
        }

        private void disconnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (clientStream != null)
                    clientStream.Close();
                enableDisableClient(false);
            }
            catch (Exception ex)
            {

                clientRichTextBox.AppendText("can not disconnect! ex: " + ex.ToString());
            }
        }

        private void clientTextBox_KeyDown(object sender, KeyEventArgs e)
        {

            try
            {
                //when enter pressed, if textbox is not empty and stream is not null, send message to server
                if (!string.IsNullOrEmpty(clientTextBox.Text) && e.Key == Key.Enter && clientStream != null)
                {
                    var buffer = Encoding.UTF8.GetBytes(clientTextBox.Text);
                    clientStream.Write(buffer, 0, buffer.Length);
                    clientRichTextBox.AppendText("TX: " + clientTextBox.Text + "\n");
                    clientTextBox.Clear();
                }
            }
            catch (Exception ex)
            {
                clientRichTextBox.AppendText("TX error. ex:  " + ex.ToString() +"\n");

            }
        }

        private void serverTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                //we select client via combobox and get it from dictionary. when enter pressed, if textbox is not empty and stream is not null, send message to selected client
                if (!string.IsNullOrEmpty(serverTextBox.Text) && e.Key == Key.Enter && connectedClients.TryGetValue(serverCombo.SelectedItem as string, out NetworkStream stream))
                {
                    var buffer = Encoding.UTF8.GetBytes(serverTextBox.Text);
                    stream.Write(buffer, 0, buffer.Length);
                    serverRichTextBox.AppendText("TX: " + serverTextBox.Text + "\n");
                    serverTextBox.Clear();
                }

            }
            catch (Exception ex)
            {
                serverRichTextBox.AppendText("TX error. ex:  " + ex.ToString() + "\n");

            }
            
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                windowClosed = true;//don't accept client because listener will be stopped
                listener.Stop();//stop listener

                lock (clientLock)
                {
                    //close all streams
                    foreach (var item in clientList)
                    {
                        if (connectedClients.TryGetValue(item, out NetworkStream stream))
                            stream.Close();
                    }
                }
            }
            catch (Exception)
            {
                //if we have a problem, kill software completely
                Process.GetCurrentProcess().Kill();

            }

            
        }
    }
}
