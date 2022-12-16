using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using System.Net;
using System.Net.Sockets;
//UI 예외처리
namespace DongaDCS
{
    public partial class FormConn : Form
    {
        public delegate void FormSendDataHandler(string ServerIP);
        public event FormSendDataHandler DataEvent;

        private UdpClient UDPClients = new UdpClient(); // UDP Client
        string Hostname = "";
        const ushort ServerPort = 50001;         // RemoteServer Port
        bool Success = false;                    // 호스트 확인이 성공적이면

        public FormConn()
        {
            InitializeComponent();
        }

        private void FormConn_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason != CloseReason.FormOwnerClosing && !Success)  // 폼 사용자가 닫을 경우
                this.Owner.Close(); // 폼 소유주까지 닫기
        }

        /*
         아이피를 입력하고 연결 버튼을 클릭하면 수행되는 작동
         해당 아이피 주소로 HEY라는 UDP를 보내주고 수신을 받을 준비함
        */
        private void button1_Click(object sender, EventArgs e)
        {
            Hostname = ipText.Text.Trim(); // TextBox에서 빈칸 제거

            if (Hostname.Length > 0)   // 서버 정보가 있다면
            {
                string data = "HEY";    // 보낼 메세지
                byte[] datagram = Encoding.UTF8.GetBytes(data); // 메세지를 바이트화해서 보냄

                this.Text = Hostname + "의 상태 확인중...";
                try
                {
                    UDPClients.Send(datagram, datagram.Length, Hostname, ServerPort);   // HEY라는 단어를 서버로 UDP 전송
                    UDPClients.BeginReceive(new AsyncCallback(recv), null); // 전송 후 응답 확인을 비동기적으로 확인
                }
                catch (Exception ex)
                {
                    this.TopMost = false;   // 최상위 폼에서 잠시 내림
                    MessageBox.Show(new Form() { WindowState = FormWindowState.Maximized, TopMost = true }, ex.Message, "경고", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Console.WriteLine(ex.ToString());
                    this.TopMost = true;   // 메세지 박스를 클릭 후 원복
                }
            }
        }

        /*
         UDP 수신에서 OK라는 답변을 받으면 해당 창을 닫고 제어 프로그램에게 아이피 주소를 전송하고 프로그램을 보여준다
        */
        private void recv(IAsyncResult res)
        {
            IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Parse(Hostname), ServerPort);
            byte[] received = UDPClients.EndReceive(res, ref RemoteIpEndPoint);

            string receiveMSG = Encoding.UTF8.GetString(received);  // 수신받은 바이트를 UTF8로 인코딩 
            Console.WriteLine("UDP Recv: {0}", receiveMSG);
            if(receiveMSG.Equals("OK")) // OK면
            {
                this.Invoke(new Action(delegate ()  // 스레드 내에서는 폼 컨트롤을 변경 불가능해서 Delegate를 사용함
                {
                    Success = true;
                    DataEvent(Hostname);    // 부모창으로 이벤트 전송
                    this.Close();
                }));
            }
            else
                UDPClients.BeginReceive(new AsyncCallback(recv), null); // OK가 아니면 다시 수신 대기
        }
    }
}
