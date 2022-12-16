using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;

using System.Reflection;
using System.IO;
using System.Net;
using System.Net.Sockets;

using Emgu.CV.Dnn;
using Emgu.CV;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using DarknetYolo;
using DarknetYolo.Models;

namespace DongaDCS
{
    public partial class Form1 : Form
    {
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        private float vertical_servo = 90.0f;   // 수직 서보 각도
        private const float min_v_servo = 30.0f;
        private const float max_v_servo = 180.0f;

        private float horizon_servo = 60.0f;   // 수평 서보 각도
        private const float min_h_servo = 30.0f;
        private const float max_h_servo = 240.0f;

        private int[] dht11 = new int[] { 0, 0 };    // 온도 습도
        private int[] distance = new int[] { 0, 0 };    // 거리
        private bool powerError = false;

        Thread Thread1 = null, Thread2 = null, Thread3 = null;  // 1: TCP송신, 2: Wifi 신호세기, 3: OpenCV
        #region openCV
        string labels = @"Models\coco.names";
        string weights = @"Models\yolov4-tiny.weights";
        string cfg = @"Models\yolov4-tiny.cfg";
        string video = @"Resources\test.mp4";

        Image<Bgr, Byte> imgeOrigenal;
        private void timer1_Tick(object sender, EventArgs e)
        {
            imageBox1.Image = imgeOrigenal;
        }

        private void RefreshCamera()
        {
            Console.WriteLine("카메라 시작");
            VideoCapture cap = new VideoCapture("http://" + Hostname + ":8090/?action=stream");
            DarknetYOLO model = new DarknetYOLO(labels, weights, cfg, PreferredBackend.Cuda, PreferredTarget.Cuda);
            model.NMSThreshold = 0.4f;
            model.ConfidenceThreshold = 0.5f;
            cap.FlipVertical = true;
            cap.FlipHorizontal = true;

            while (true)
            {
                Mat frame = new Mat();
                try
                {
                    cap.Read(frame);
                    //CvInvoke.Resize(frame, frame, new Size(720, 480));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("VideoEnded");
                    frame = null;
                }
                if (frame == null)
                    continue;
                
                CvInvoke.Rotate(frame, frame, Emgu.CV.CvEnum.RotateFlags.Rotate180);
                if (checkbox_openCV.Checked)
                {
                    List<YoloPrediction> results = model.Predict(frame.ToBitmap(), 512, 512);
                    try
                    {
                        foreach (var item in results)
                        {
                            MCvScalar Color;
                            if (item.Label.Contains("person") == true)
                                Color = new MCvScalar(0, 0, 200);
                            else
                                Color = new MCvScalar(100, 200, 0);
                            CvInvoke.Rectangle(frame, new Rectangle(item.Rectangle.X, item.Rectangle.Y - 13, item.Rectangle.Width, 13), Color, -1);
                            CvInvoke.PutText(frame, item.Label, new Point(item.Rectangle.X, item.Rectangle.Y - 5), Emgu.CV.CvEnum.FontFace.HersheySimplex, 0.5, new MCvScalar(255, 255, 255), 2);
                            CvInvoke.Rectangle(frame, item.Rectangle, Color, 1);
                        }
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
                imgeOrigenal = frame.ToImage<Bgr, Byte>();
                
                /*
                CvInvoke.WaitKey(10);
                */
            }
        }
        #endregion

        #region 통신관련
        string Hostname = "";
        /*
         * UDP는 바퀴 제어만 전송하고 따로 수신하지는 않음
         */
        UdpClient UDPClients = new UdpClient();
        const ushort UDP_Port = 50001;

        /*
         * TCP는 서보모터 제어 전송하고
         * 센서류 정보를 받아옴
         */
        Socket TCPClients = null;
        IPEndPoint TCP_IPEndPoint = null;
        const ushort TCP_Port = 50002;
        const ushort MAX_DATA_LENGTH = 1024;
        static byte[] receiveBytes = new byte[MAX_DATA_LENGTH];
        StringBuilder sb = new StringBuilder();

        /*
            TCP 수신된 내용을 처리함
            수신된 데이터의 종류(초음파, 온습도, 전력)를 분류하여 처리함
        */
        public void receiveStr(IAsyncResult ar)
        {
            try
            {
                int bytesRead = TCPClients.EndReceive(ar);
                if (bytesRead > 0)
                {
                    string output = Encoding.Default.GetString(receiveBytes, 0, bytesRead).Replace("\0", string.Empty);
                    sb.Append(output);
                    /*
                    if (waitingBuffer == 0 && output.Contains("$"))
                    {
                        waitingBuffer = Convert.ToInt32(output.Substring(1, output.IndexOf('|') - 1));
                    }  
                    if((waitingBuffer + waitingBuffer.ToString().Length + 2) > sb.ToString().Length && waitingBuffer > 0)
                    {
                        receiveBytes = new byte[MAX_DATA_LENGTH];
                        TCPClients.BeginReceive(receiveBytes, 0, MAX_DATA_LENGTH, SocketFlags.None, new AsyncCallback(receiveStr), TCPClients);
                        return;
                    }
                    output = sb.ToString();
                    */

                    output = sb.ToString();
                    if (output[output.Length - 1] == '\n')  // 전송된 데이터의 끝이라면
                    {
                        Console.WriteLine("결과 " + output);

                        string[] result = output.Split('|');
                        for (int i = 0; i < result.Length; i++)
                        {
                            if (result[i].Contains("POWER"))
                            {
                                string[] data = result[i].Split(':');
                                if (data.Length == 2)
                                {
                                    int tmpPowerValue = System.Convert.ToInt32(data[1]);
                                    if (tmpPowerValue == 255)
                                        powerError = false;
                                    else
                                        powerError = true;
                                    Console.WriteLine("Power:" + data[1]);
                                }
                            }
                            else if (result[i].Contains("TH"))   // 온습도계 일 경우
                            {
                                string[] data = result[i].Split(':');
                                if (data.Length == 2)    // 올바른 값이면 2개
                                {
                                    for (int j = 0; j < 2; j++)
                                    {
                                        if (string.Compare(data[0], ("TH" + j).ToString(), true) == 0) // 거리
                                        {
                                            dht11[j] = System.Convert.ToInt32(data[1]);
                                            Console.WriteLine(data[0] + ":" + data[1]);
                                        }
                                    }
                                }
                            }
                            else if (result[i].Contains("US")) // 초음파 센서
                            {
                                string[] data = result[i].Split(':');
                                if (data.Length == 2)    // 올바른 값이면 2개
                                {
                                    for (int j = 0; j < distance.Length; j++)
                                    {
                                        if (string.Compare(data[0], ("US" + j).ToString(), true) == 0) // 거리
                                        {
                                            distance[j] = System.Convert.ToInt32(data[1]);
                                            Console.WriteLine("UltraSonic" + j + ": " + data[1]);
                                        }
                                    }
                                }
                            }
                        }
                        sb = new StringBuilder();
                        receiveBytes = new byte[MAX_DATA_LENGTH];
                     }
                }
                TCPClients.BeginReceive(receiveBytes, 0, MAX_DATA_LENGTH, SocketFlags.None, new AsyncCallback(receiveStr), TCPClients);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                MessageBox.Show(new Form() { WindowState = FormWindowState.Maximized, TopMost = true }, ex.Message, "경고", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                this.Invoke(new Action(delegate () 
                {
                    GetHostnameForm();
                }));
            }
        }

        /*
            TCP 연결을 성공할 경우 TCP 수신을대기하며 
            추가적으로 파이카메라를 확인할 수 있는 크로미움기반 웹 브라우저를 생성함
         */
        public void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                TCPClients.BeginReceive(receiveBytes, 0, 11, SocketFlags.None, new AsyncCallback(receiveStr), TCPClients);
                TCP_Send("REQUEST|V:" + (int)vertical_servo + "|H:" + (int)horizon_servo + '\n');

                if (Thread3 != null)
                    Thread3.Abort();
                Thread3 = new Thread(new ThreadStart(RefreshCamera));
                Thread3.Start();
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(delegate ()
                {
                    GetHostnameForm();
                }));
                Console.WriteLine(ex.Message);
            }
        }

        /*
            DC모터 제어를 위한 UDP 데이터, 서보모터 제어를 위한 TCP 데이터 전송을 위해 Thread를 생성함
            WASD 키에 따른 제어 조합을 구하고 UDP로 전송 (WASD키를 안 누르면 'S'가 전송됨)
            서보모터 위치가 이전에 전송한 서보모터 위치가 다를 경우 보내줌
         */
        private void TCP_Send(String Data)
        {
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            byte[] szData = Encoding.UTF8.GetBytes(Data);
            args.SetBuffer(szData, 0, szData.Length);
            TCPClients.SendAsync(args);
        }
        private void ThreadMain()
        {
            double prev_vertical_servo = vertical_servo;
            double prev_horizon_servo = horizon_servo;
            string dataMSG = "";
            while (true)
            {
                try
                {
                    if (Hostname.Length > 0)    // 호스트 정보가 있다면
                    {
                        // UDP
                        dataMSG = "S";
                        if (KeyStatus[0] && !KeyStatus[1] && KeyStatus[2] && !KeyStatus[3]) // W A
                            dataMSG = "FL";
                        else if (!KeyStatus[0] && !KeyStatus[1] && KeyStatus[2] && KeyStatus[3]) // W D
                            dataMSG = "FR";
                        else if(KeyStatus[0] && KeyStatus[1] && !KeyStatus[2] && !KeyStatus[3]) // S A
                            dataMSG = "BL";
                        else if(!KeyStatus[0] && KeyStatus[1] && !KeyStatus[2] && KeyStatus[3]) // S D
                            dataMSG = "BR";
                        else if (KeyStatus[0] && !KeyStatus[1] && !KeyStatus[2] && !KeyStatus[3]) // A
                             dataMSG = "L";
                        else if (!KeyStatus[0] && KeyStatus[1] && !KeyStatus[2] && !KeyStatus[3])  // S
                            dataMSG = "B";
                        else if (!KeyStatus[0] && !KeyStatus[1] && KeyStatus[2] && !KeyStatus[3])  // W
                            dataMSG = "F";
                        else if (!KeyStatus[0] && !KeyStatus[1] && !KeyStatus[2] && KeyStatus[3])  // D
                            dataMSG = "R";

                        byte[] datagram = Encoding.UTF8.GetBytes(dataMSG);    // ASCII Key Code를 Byte화함
                        UDPClients.Send(datagram, datagram.Length, Hostname, UDP_Port); // UDP 전송

                        // TCP
                        if(Math.Abs(vertical_servo - prev_vertical_servo) >= 1.0)
                        {
                            prev_vertical_servo = vertical_servo;
                            TCP_Send("|V:" + (int)vertical_servo + '\n');
                        }
                        else if (Math.Abs(horizon_servo - prev_horizon_servo) >= 1.0)
                        {
                            prev_horizon_servo = horizon_servo;
                            TCP_Send("|H:" + (int)horizon_servo + '\n');
                        }
                    }
                }catch(Exception ex) {
                    Console.WriteLine(ex.Message);
                }
                Thread.Sleep(100);  // 전송 주기
            }
        }
        #endregion

        #region 키보드입력함수
        [DllImport("user32")]
        public static extern UInt16 GetAsyncKeyState(int vKey);

        const byte MAX_KEYS = 8;    // 감지할 최대 키 개수

        public bool[] KeyStatus = new bool[MAX_KEYS];      // Key 상태 확인 DOWN 중인지 UP인지
        public int[] KeyList = new int[MAX_KEYS]    // 키 목록
        {
            65, // A
            83, // S
            87, // W
            68, // D
            37, // 방향키 왼쪽
            38, // 방향키 위
            39, // 방향키 오른쪽
            40, // 방향키 아래
        };

        public static bool IsKeyDown(int KeyCode)   // 키 다운 상태인지
        {
            return ((GetAsyncKeyState(KeyCode) & 0x8000) != 0) ? true : false;
        }

        public static bool IsKeyUP(int KeyCode)     // 키 뗀 상태인지
        {
            return ((GetAsyncKeyState(KeyCode) & 0x8000) == 0) ? true : false;
        }

        /*
         FindWindow로 해당 프로그램이 활성화된 경우에
         키보드 입력을 확인하고
         방향키를 누를 경우 라즈베리파이에 전송할 서모모터의 각도를 프로그램에서 계산함
         */
        private void Timer10_Tick(object sender, EventArgs e)
        {
            IntPtr hWnd = FindWindow(null, this.Text);
            IntPtr Foreground = GetForegroundWindow();
            if (!hWnd.Equals(Foreground))  // 이 창이 활성화가 아닌 경우
            {
                for (int i=0; i<MAX_KEYS; i++)   // 모든 키 뗀 걸로 변경
                    KeyStatus[i] = false;
            }
            else
            {
                for (int i = 0; i < MAX_KEYS; i++)
                {
                    if (!KeyStatus[i] && IsKeyDown(KeyList[i])) // 처음으로 KEY DOWN이면 변수 TRUE하여 누른 상태로 감지
                    {
                        KeyStatus[i] = true;
                    }
                    else if (KeyStatus[i] && IsKeyUP(KeyList[i])) // 변수가 TRUE일때 KEY UP이면 막 키를 뗀걸로 감지
                    {
                        KeyStatus[i] = false;
                    }
                }

                if (KeyStatus[4] && !KeyStatus[5] && !KeyStatus[6] && !KeyStatus[7]) // 왼쪽
                    horizon_servo += 1.0f;
                else if (!KeyStatus[4] && !KeyStatus[5] && KeyStatus[6] && !KeyStatus[7]) // 오른쪽
                    horizon_servo -= 1.0f;
                else if (!KeyStatus[4] && KeyStatus[5] && !KeyStatus[6] && !KeyStatus[7]) // 위 
                    vertical_servo += 2.0f;
                else if (!KeyStatus[4] && !KeyStatus[5] && !KeyStatus[6] && KeyStatus[7]) // 아래 
                    vertical_servo -= 2.0f;

                if (horizon_servo < min_h_servo)
                    horizon_servo = min_h_servo;
                else if (horizon_servo > max_h_servo)
                    horizon_servo = max_h_servo;

                if (vertical_servo < min_v_servo)
                    vertical_servo = min_v_servo;
                else if (vertical_servo > max_v_servo)
                    vertical_servo = max_v_servo;
            }
        }
        #endregion

        #region UI관련
        private Panel panel;
        private Label[] panelLabel = new Label[6];

        const int _X = 0, _Y = 0, _SIZE_X = 250, _SIZE_Y = 250,
                  _SERVO_Y = _Y + 75, _PADDING_XY = 4, _RECT_PADDING = 10, _HV_OFFSET = 10, _V_WIDTHDIV = 10;
        private void UI_Initialize()
        {
            panel = new Panel();
            panel.Location = new Point(5, this.Height / 2 + 50);
            panel.Name = "panel";
            panel.Size = new Size(300, 500);
            panel.TabIndex = 6;
            this.Controls.Add(panel);

            for (int i = 0; i < panelLabel.Length; i++)
            {
                panelLabel[i] = new Label();
                panelLabel[i].Font = new Font("굴림", 24.0f);
                panelLabel[i].Location = new Point(723, 160);
                panelLabel[i].Size = new Size(panel.Size.Width, 32);
                panelLabel[i].TabIndex = 8;
                panelLabel[i].Text = "";
                panelLabel[i].TextAlign = ContentAlignment.MiddleCenter;
                panel.Controls.Add(panelLabel[i]);
            }

            /* wifi 신호 표시 */
            panelLabel[0].Location = new Point(_X, _Y);

            /* 초음파 표시 */
            panelLabel[1].Location = new Point(_X, panelLabel[0].Location.Y + 35);
            panelLabel[2].Location = new Point(_X, panelLabel[1].Location.Y + _SIZE_Y + 50);

            /* 온습도계 표시 */
            panelLabel[3].Size = new Size(panel.Size.Width / 2, 30);
            panelLabel[3].Location = new Point(_X, panelLabel[2].Location.Y + 35);
            panelLabel[4].Size = new Size(panel.Size.Width / 2, 30);
            panelLabel[4].Location = new Point(panel.Size.Width / 2, panelLabel[2].Location.Y + 35);

            /* 전원 표시 */
            panelLabel[5].Location = new Point(_X, panelLabel[4].Location.Y + 40);
        }

        private void SetLabelWarn(int index, bool warn)
        {
            if (index < 0 || index >= panelLabel.Length) return;

            if (warn)
            {
                panelLabel[index].ForeColor = Color.Red;
                panelLabel[index].BackColor = Color.Black;
            }
            else
            {
                panelLabel[index].ForeColor = Color.Black;
                panelLabel[index].BackColor = Color.Transparent;
            }
        }

        private void UI_Render()
        {
            Graphics g = panel.CreateGraphics();
            g.Clear(this.BackColor);

            Rectangle rect = new Rectangle(_X, _SERVO_Y, _SIZE_X + _PADDING_XY, _SIZE_Y + _PADDING_XY);
            g.DrawRectangle(Pens.Black, rect);

            rect = new Rectangle(_X+ _RECT_PADDING/2, _SERVO_Y + 30,
                                    _SIZE_X-_RECT_PADDING, _SIZE_Y-_RECT_PADDING);
            g.DrawPie(Pens.Black, rect, 180, 210);

            rect = new Rectangle(_X + _RECT_PADDING / 2, _SERVO_Y + 30,
                                    _SIZE_X - _RECT_PADDING, _SIZE_Y - _RECT_PADDING);
            g.FillPie(Brushes.Green, rect, 0, 5);

            rect = new Rectangle(_X+ _RECT_PADDING/2, _SERVO_Y + 30,
                                    _SIZE_X-_RECT_PADDING, _SIZE_Y-_RECT_PADDING);
            g.FillPie(Brushes.Red, rect, 60 - horizon_servo, 5);

            rect = new Rectangle(_X + _SIZE_X + _HV_OFFSET, _SERVO_Y, 
                                    _SIZE_X / _V_WIDTHDIV, _SIZE_Y + _PADDING_XY);
            g.DrawRectangle(Pens.Black, rect);
            rect = new Rectangle(_X + _SIZE_X + _HV_OFFSET, _SERVO_Y + (int)((_SIZE_Y + _PADDING_XY) * (max_v_servo - vertical_servo) / (max_v_servo-min_v_servo)), 
                                    _SIZE_X / _V_WIDTHDIV, 5);
            g.FillRectangle(Brushes.Red, rect);

            g.Dispose();
        }

        int ui_counter = 100;
        int wifiSignal = 0;

        private void ui_timer10_Tick(object sender, EventArgs e)
        {
            int index = 0;

            SetLabelWarn(index, wifiSignal < 20);
            panelLabel[index++].Text = "신호감도  "+ wifiSignal.ToString() + " %";

            SetLabelWarn(index, distance[0] < 10);
            panelLabel[index++].Text = distance[0].ToString() + " cm";

            SetLabelWarn(index, distance[1] < 10);
            panelLabel[index++].Text = distance[1].ToString() + " cm";

            SetLabelWarn(index, dht11[0] > 40);
            panelLabel[index++].Text = dht11[0].ToString() + " °C";

            SetLabelWarn(index, dht11[1] > 80);
            panelLabel[index++].Text = dht11[1].ToString() + " %";

            SetLabelWarn(index, powerError);
            panelLabel[index].Text = (powerError ? "전원 이상" : "전원 정상");


            ui_counter++;
            if (ui_counter % 5 == 0)  // 50ms마다 작동
            {
                try
                {
                    UI_Render();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                ui_counter = 0;
            }
        }
        #endregion

        /* 현재 연결된 WIFI 정보 가져오기 */
        private void getWifiStrength()
        {
            while (true)
            {
                System.Diagnostics.Process p = new System.Diagnostics.Process();
                p.StartInfo.FileName = "netsh.exe";
                p.StartInfo.Arguments = "wlan show interfaces";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                p.StartInfo.CreateNoWindow = true;
                p.Start();

                string result = p.StandardOutput.ReadToEnd();
                int pos = result.IndexOf("신호");
                if (pos == -1)
                {
                    wifiSignal = -1;
                    continue;
                }

                string signal = result.Substring(pos);
                signal = signal.Substring(signal.IndexOf(":"));
                signal = signal.Substring(1, signal.IndexOf("\n")).Trim();
                int signal_num = System.Convert.ToInt32(signal.Substring(0, signal.Length - 1));
                wifiSignal = signal_num;
                Thread.Sleep(1000);
            }
        }

        /*
         라즈베리파이의 아이피를 입력하는 창을 띄우는 함수
         */
        private void GetHostnameForm()
        {
            Hostname = "";
            Timer10.Enabled = false;
            ui_timer10.Enabled = false;

            this.WindowState = FormWindowState.Minimized; // 폼 최소화
            this.ShowInTaskbar = false; // 작업표시줄 숨기기

            FormConn Connector = new FormConn();    // 서버 정보를 받기 위한 폼을 열음
            Connector.Owner = this;                 // FormConn의 소유주를 이 폼으로
            Connector.DataEvent += new FormConn.FormSendDataHandler(UpdateEventMethod); // delegate 이벤트 전송을 사용하여 자식폼에서 부모폼으로 데이터 전송
            Connector.Show();                       // 자식폼 띄우기
            Connector.TopMost = true;

            if (Thread3 != null)    // 카메라 갱신 정지
                Thread3.Abort();
        }

        /*
         라즈베리파이 아이피 입력창에서 OK라는 수신을 받으면 해당 함수가 호출되는 데 
         수신에 성공한 아이피를 받아와 TCP 연결을 시도함
         */
        private void UpdateEventMethod(string ServerIP)
        {
            Timer10.Enabled = true;
            ui_timer10.Enabled = true;

            this.ShowInTaskbar = true;  // 작업표시줄 보이기

            Hostname = ServerIP;    // 이벤트로 받은 ServerIP 변수에 대입
            Console.WriteLine("호스트 정보 받음 {0}", Hostname);
            this.WindowState = FormWindowState.Maximized;   // 폼 최대화

            TCPClients = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);   // 소켓 생성
            TCP_IPEndPoint = new IPEndPoint(IPAddress.Parse(Hostname), TCP_Port);   // 전달받은 HostIP로 EndPoint 생성

            TCPClients.BeginConnect(TCP_IPEndPoint, new AsyncCallback(ConnectCallback), TCPClients);
            SocketAsyncEventArgs args = new SocketAsyncEventArgs();
            args.RemoteEndPoint = TCP_IPEndPoint;   // 소켓 Args에 Endpoint 대입
            TCPClients.ConnectAsync(args);  // 소켓 비동기 접속 시도
        }

        public Form1()
        {
            InitializeComponent();
            UI_Initialize();
        }

        // 라즈베리파이 아이피 주소를 입력하는 창과 통신 전송을 위한 스레드 생성을 함
        private void Form1_Load(object sender, EventArgs e)
        {
            GetHostnameForm();
            Thread1 = new Thread(new ThreadStart(ThreadMain));  // 통신을 위한 스레드 생성
            Thread1.Start();    // 스레드 시작

            Thread2 = new Thread(new ThreadStart(getWifiStrength));  // WIFI 정보를 얻기 위한 스레드 생성
            Thread2.Start();    // 스레드 시작

            imageBox1.Location = new Point(350, 0);
            imageBox1.Size = new Size(Screen.PrimaryScreen.Bounds.Width - 700, Screen.PrimaryScreen.Bounds.Height);
            imageBox1.SizeMode = PictureBoxSizeMode.Zoom;

            pictureBox_logo.Location = new Point(Screen.PrimaryScreen.Bounds.Width - 150, Screen.PrimaryScreen.Bounds.Height - 150);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Thread1 != null)    // 스레드가 있으면
                Thread1.Abort();    // 스레드 강제 종료

            if (Thread2 != null)
                Thread2.Abort();

            if (Thread3 != null)
                Thread3.Abort();

            if (TCPClients != null && TCPClients.Connected)  // TCP 세션이 열려있고
            {
                TCPClients.Disconnect(false);
                //TCPClients.Close(); // 세션 닫아주기
            }
        }

        private void Form1_Resize(object sender, EventArgs e)   // 폼의 크기가 바뀔 경우
        {
            if(Hostname == string.Empty)    // 호스트 정보가 없을 경우127
            {
                this.WindowState = FormWindowState.Minimized;   // 폼 최소화
            }
        }

        private void Button_Exit_Click(object sender, EventArgs e)
        {        
            this.Close();   // 프로그램 종료 버튼
        }
    }
}
