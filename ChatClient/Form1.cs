using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Grpc.Net.Client;

namespace bankClient {
    public partial class Form1 : Form {
        private GrpcChannel channel;
        private ChatServerService.ChatServerServiceClient client;
        private Timer timer1;

        public Form1() {
            InitializeComponent();

            AppContext.SetSwitch(
    "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            channel = GrpcChannel.ForAddress("http://localhost:1001");
            client = new ChatServerService.ChatServerServiceClient(channel);
            
            InitTimer();
        }


        public void InitTimer()
        {
            timer1 = new Timer();
            timer1.Tick += new EventHandler(timer1_Tick);
            timer1.Interval = 2000; // in miliseconds
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var reply2 = client.Update(new ChatUpdateRequest { });
            textBox4.Text = reply2.Messages.Replace("\n", "\r\n");
        }

        private void button1_Click(object sender, EventArgs e) {
            var reply = client.Register(
                         new ChatClientRegisterRequest { Nick = textBox2.Text, Url = "http://localhost:....." });
            textBox1.Text = reply.Ok.ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var reply = client.SendMessage(new ChatMessageRequest { Nick = textBox2.Text, Message = textBox3.Text });
            var reply2 = client.Update(new ChatUpdateRequest { });
            textBox4.Text = reply2.Messages.Replace("\n", "\r\n");

        }
    }
}
